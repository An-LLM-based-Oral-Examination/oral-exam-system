using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OfficeOpenXml;
using OralExamination.API.Hubs;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.Admin.Queries.GetAbuseMetrics;
using OralExamination.Application.Features.Admin.Queries.GetRuntimeDiagnostics;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using OralExamination.Infrastructure.Reporting;
using OralExamination.Infrastructure.Resilience;
using OralExamination.Infrastructure.Services;
using Xunit;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;

namespace OralExamination.UnitTests.Features.Resilience;

public sealed class Pr3ResiliencePkiDlqAndInfraTests
{
    private static (IServiceScopeFactory ScopeFactory, OralExamDbContext Context, ServiceProvider Provider) CreateDbScope()
    {
        var dbName = $"Pr3Tests_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<OralExamDbContext>(), provider);
    }

    [Fact(DisplayName = "PR3-A1: PadesPdfDigitalSigner khởi tạo lazy — Production thiếu .pfx vẫn ExportExcel và sanitize công thức, chỉ Fail-Closed khi ExportPdf")]
    public void PadesSigner_AndExporter_ProductionMissingCert_AllowsExcelWithSanitization_AndFailsClosedOnPdf()
    {
        var prodMissingCertConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "true",
                ["PdfSigning:CertificatePath"] = "",
                ["PdfSigning:CertificateBase64"] = "",
                ["PdfSigning:CertificatePassword"] = ""
            })
            .Build();

        using var signer = new PadesPdfDigitalSigner(prodMissingCertConfig);
        var exporter = new ExamGradeReportExporter(signer, prodMissingCertConfig);

        var report = new ExamShiftGradeReport(
            ShiftId: Guid.NewGuid(),
            SemesterCode: "=FA26",
            CourseCode: "PRN231",
            CourseName: "+Web API",
            SessionTitle: "@Final Exam",
            ExamDate: new DateOnly(2026, 11, 24),
            ShiftName: "-Ca 1",
            RoomLab: "\tLab 301",
            ProctorName: "\rProctor A",
            ShiftStatus: "completed",
            CertificateHash: new string('a', 64),
            IssuedAtUtc: DateTime.UtcNow,
            Rows:
            [
                new ShiftGradeRowDto
                {
                    Index = 1,
                    SeatNumber = 1,
                    StudentCode = "=CMD|' /C calc'!A0",
                    FullName = "+SUM(A1:A2)",
                    Email = "-2+3@fpt.edu.vn",
                    AiScore = 8.0m,
                    FinalScore = 8.5m,
                    TicketStatus = "@PUBLISHED",
                    IsLocked = true,
                    OverrideReason = "\t=1+1",
                    AudioHashSha256 = "\rdeadbeef"
                },
                new ShiftGradeRowDto
                {
                    Index = 2,
                    SeatNumber = 2,
                    StudentCode = "\nSE170002",
                    FullName = "Nguyễn Quang Thành",
                    Email = "se170002@fpt.edu.vn",
                    AiScore = 9.0m,
                    FinalScore = 9.0m,
                    TicketStatus = "PUBLISHED",
                    IsLocked = true,
                    OverrideReason = "Giải thích đúng kiến trúc",
                    AudioHashSha256 = "abcdef123456"
                }
            ]);

        var excelBytes = exporter.ExportExcel(report);
        excelBytes.Should().NotBeEmpty();

        using (var package = new ExcelPackage(new MemoryStream(excelBytes)))
        {
            var sheet = package.Workbook.Worksheets["Bảng điểm khảo thí"];
            sheet.Should().NotBeNull();
            sheet!.Cells["B4"].Text.Should().Be("'=FA26");
            sheet.Cells["B5"].Text.Should().Be("'@Final Exam");
            sheet.Cells["B6"].Text.Should().Be("'-Ca 1");
            sheet.Cells["E6"].Text.Should().Be("'Lab 301");
            sheet.Cells["H6"].Text.Should().Be("'Proctor A");

            sheet.Cells[9, 3].Text.Should().Be("'=CMD|' /C calc'!A0");
            sheet.Cells[9, 4].Text.Should().Be("'+SUM(A1:A2)");
            sheet.Cells[9, 5].Text.Should().Be("'-2+3@fpt.edu.vn");
            sheet.Cells[9, 8].Text.Should().Be("'@PUBLISHED");
            sheet.Cells[9, 10].Text.Should().Be("'=1+1");
            sheet.Cells[9, 11].Text.Should().Be("'deadbeef");

            sheet.Cells[10, 3].Text.Should().Be("'SE170002");
            sheet.Cells[10, 4].Text.Should().Be("Nguyễn Quang Thành");
        }

        Action exportPdfAct = () => exporter.ExportPdf(report);
        exportPdfAct.Should().Throw<InvalidOperationException>().WithMessage("*FAIL CLOSED*");
    }

    [Fact(DisplayName = "PR3-A2: GetRuntimeDiagnosticsQueryHandler đếm đủ OFFICIAL_EXAM trong TotalPending và ByFlowType.OfficialExam")]
    public async Task GetRuntimeDiagnosticsQueryHandler_CountsOfficialExamInTotalPendingAndBreakdown()
    {
        var (_, context, provider) = CreateDbScope();
        await using var ownedProvider = provider;

        context.DeadLetterQueues.AddRange(
            new DlqEntity { TaskType = "PracticeGrading", FlowType = "PRACTICE", Status = "pending", ErrorMessage = "P1" },
            new DlqEntity { TaskType = "MockExamGrading", FlowType = "MOCK_EXAM", Status = "pending", ErrorMessage = "M1" },
            new DlqEntity { TaskType = "InClassGrading", FlowType = "IN_CLASS_EXAM", Status = "pending", ErrorMessage = "IC1" },
            new DlqEntity { TaskType = "OfficialExamGrading", FlowType = "OFFICIAL_EXAM", Status = "pending", ErrorMessage = "OE1" },
            new DlqEntity { TaskType = "OfficialExamGrading", FlowType = "OFFICIAL_EXAM", Status = "pending", ErrorMessage = "OE2" },
            new DlqEntity { TaskType = "OfficialExamGrading", FlowType = "OFFICIAL_EXAM", Status = "resolved", ErrorMessage = "OE-resolved" });
        await context.SaveChangesAsync();

        var gradingQueueMock = new Mock<IGradingQueueChannel>();
        gradingQueueMock.Setup(q => q.Count).Returns(3);
        gradingQueueMock.Setup(q => q.Capacity).Returns(1000);

        var abuseReaderMock = new Mock<IAbuseMetricsReader>();
        abuseReaderMock.Setup(r => r.Snapshot(0)).Returns(new AbuseMetricsSnapshot(20, 1, 0, "api-process"));

        var loginGuardMock = new Mock<ILoginAttemptGuard>();
        loginGuardMock.Setup(g => g.Lockouts).Returns(0);

        var metricsTrackerMock = new Mock<IRuntimeMetricsTracker>();
        metricsTrackerMock.Setup(m => m.GetSnapshot()).Returns(new RuntimeMetricsSnapshot(
            TotalRequests: 50,
            ServerErrors5xx: 0,
            ServerErrorRatePercent: 0,
            AverageLatencyMs: 12.0,
            P95LatencyMs: 25.0,
            Uptime: TimeSpan.FromMinutes(30),
            WorkingSetMb: 80.0,
            GcTotalMemoryMb: 32.0,
            ThreadCount: 8));

        var handler = new GetRuntimeDiagnosticsQueryHandler(
            context,
            gradingQueueMock.Object,
            abuseReaderMock.Object,
            loginGuardMock.Object,
            metricsTrackerMock.Object);

        var result = await handler.Handle(new GetRuntimeDiagnosticsQuery(), CancellationToken.None);

        result.DeadLetterQueue.TotalPending.Should().Be(5);
        result.DeadLetterQueue.ByFlowType.Practice.Should().Be(1);
        result.DeadLetterQueue.ByFlowType.MockExam.Should().Be(1);
        result.DeadLetterQueue.ByFlowType.InClassExam.Should().Be(1);
        result.DeadLetterQueue.ByFlowType.OfficialExam.Should().Be(2);
    }

    [Fact(DisplayName = "PR3-A2: PracticeHub chặn sinh viên IsActive = false bằng HubException FORBIDDEN và cho phép sinh viên active chính chủ")]
    public async Task PracticeHub_BlocksInactiveStudent_AndAllowsActiveOwner()
    {
        var (_, context, provider) = CreateDbScope();
        await using var ownedProvider = provider;

        var inactiveStudentId = Guid.NewGuid();
        var activeStudentId = Guid.NewGuid();
        context.Users.AddRange(
            new User
            {
                Id = inactiveStudentId,
                Email = "locked.student@fpt.edu.vn",
                FullName = "Locked Student",
                Role = UserRole.Student,
                IsActive = false
            },
            new User
            {
                Id = activeStudentId,
                Email = "active.student@fpt.edu.vn",
                FullName = "Active Student",
                Role = UserRole.Student,
                IsActive = true
            });

        var inactiveSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = inactiveStudentId,
            CourseId = Guid.NewGuid(),
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };
        var activeSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = activeStudentId,
            CourseId = Guid.NewGuid(),
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };
        context.PracticeSessions.AddRange(inactiveSession, activeSession);
        await context.SaveChangesAsync();

        var groupsMock = new Mock<IGroupManager>();
        groupsMock
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        static HubCallerContext BuildCaller(string connId, Guid userId)
        {
            var callerMock = new Mock<HubCallerContext>();
            callerMock.Setup(c => c.ConnectionId).Returns(connId);
            callerMock.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);
            callerMock.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, UserRole.Student)
            ], "Test")));
            return callerMock.Object;
        }

        var hub = new PracticeHub(context) { Groups = groupsMock.Object };

        hub.Context = BuildCaller("conn-inactive", inactiveStudentId);
        var inactiveAct = async () => await hub.JoinSession(inactiveSession.Id.ToString());
        await inactiveAct.Should().ThrowAsync<HubException>().WithMessage("FORBIDDEN:*");
        groupsMock.Verify(
            g => g.AddToGroupAsync("conn-inactive", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        hub.Context = BuildCaller("conn-active", activeStudentId);
        await hub.JoinSession(activeSession.Id.ToString());
        groupsMock.Verify(
            g => g.AddToGroupAsync("conn-active", $"session_{activeSession.Id:D}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "PR3-A2: OfficialExamDlqReplayWorker giữ pending + dời NextRetryAt khi enqueue vào RAM channel và chỉ resolved khi đã chấm/khóa ca")]
    public async Task OfficialExamDlqReplayWorker_KeepsPendingOnRamEnqueue_ResolvesOnlyWhenGradedOrLocked()
    {
        var (scopeFactory, context, provider) = CreateDbScope();
        await using var ownedProvider = provider;

        var openShiftId = Guid.NewGuid();
        var lockedShiftId = Guid.NewGuid();
        var openInClassSubId = Guid.NewGuid();
        var lockedInClassSubId = Guid.NewGuid();
        var practiceAnswerId = Guid.NewGuid();
        var practiceSessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        context.InClassExamShifts.AddRange(
            new InClassExamShift
            {
                Id = openShiftId,
                ClassId = Guid.NewGuid(),
                LecturerId = Guid.NewGuid(),
                ShiftName = "Ca mở",
                TotpSecretKey = "OPEN",
                Status = "IN_PROGRESS",
                IsLocked = false
            },
            new InClassExamShift
            {
                Id = lockedShiftId,
                ClassId = Guid.NewGuid(),
                LecturerId = Guid.NewGuid(),
                ShiftName = "Ca khóa",
                TotpSecretKey = "LOCKED",
                Status = "FINALIZED",
                IsLocked = true
            });

        context.InClassExamSubmissions.AddRange(
            new InClassExamSubmission
            {
                Id = openInClassSubId,
                ShiftId = openShiftId,
                StudentId = studentId,
                QuestionId = questionId,
                Transcript = "Bài làm MF-05 chờ chấm lại",
                Status = "SUBMITTED",
                SubmittedAt = DateTime.UtcNow.AddMinutes(-20)
            },
            new InClassExamSubmission
            {
                Id = lockedInClassSubId,
                ShiftId = lockedShiftId,
                StudentId = studentId,
                QuestionId = questionId,
                Transcript = "Bài làm MF-05 ca đã chốt sổ",
                Status = "SUBMITTED",
                SubmittedAt = DateTime.UtcNow.AddMinutes(-20)
            });

        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = practiceAnswerId,
            SessionId = practiceSessionId,
            QuestionId = questionId,
            StudentId = studentId,
            AnswerText = "Bài luyện tập chờ chấm bù",
            Status = "failed",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-20)
        });

        var inClassDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "admin",
            CorrelationId = openInClassSubId,
            PayloadJson = JsonSerializer.Serialize(new GradingTask
            {
                AnswerId = openInClassSubId,
                InClassSubmissionId = openInClassSubId,
                SessionId = openShiftId,
                QuestionId = questionId,
                StudentId = studentId,
                AnswerText = "Bài làm MF-05 chờ chấm lại"
            }),
            ErrorMessage = "Gemini 503",
            RetryCount = 3,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var lockedInClassDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "admin",
            CorrelationId = lockedInClassSubId,
            PayloadJson = JsonSerializer.Serialize(new GradingTask
            {
                AnswerId = lockedInClassSubId,
                InClassSubmissionId = lockedInClassSubId,
                SessionId = lockedShiftId,
                QuestionId = questionId,
                StudentId = studentId,
                AnswerText = "Bài làm MF-05 ca đã chốt sổ"
            }),
            ErrorMessage = "Gemini 503",
            RetryCount = 3,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var practiceDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            CorrelationId = practiceAnswerId,
            PayloadJson = JsonSerializer.Serialize(new GradingTask
            {
                AnswerId = practiceAnswerId,
                SessionId = practiceSessionId,
                QuestionId = questionId,
                StudentId = studentId,
                AnswerText = "Bài luyện tập chờ chấm bù"
            }),
            ErrorMessage = "Gemini 429",
            RetryCount = 3,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
        };

        context.DeadLetterQueues.AddRange(inClassDlq, lockedInClassDlq, practiceDlq);
        await context.SaveChangesAsync();

        var enqueued = new List<GradingTask>();
        var channelMock = new Mock<IGradingQueueChannel>();
        channelMock
            .Setup(c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Callback<GradingTask, CancellationToken>((task, _) => enqueued.Add(task))
            .Returns(ValueTask.CompletedTask);

        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            channelMock.Object);

        worker.PollInterval.Should().Be(TimeSpan.FromMinutes(5));

        var beforeReplay = DateTime.UtcNow;
        await worker.ReplayDueAsync(CancellationToken.None);

        enqueued.Should().HaveCount(2);
        enqueued.Should().ContainSingle(t => t.InClassSubmissionId == openInClassSubId);
        enqueued.Should().ContainSingle(t => t.AnswerId == practiceAnswerId && t.InClassSubmissionId == null);

        var refreshedInClass = await context.DeadLetterQueues.AsNoTracking().SingleAsync(d => d.Id == inClassDlq.Id);
        refreshedInClass.Status.Should().Be("pending");
        refreshedInClass.ResolvedAt.Should().BeNull();
        refreshedInClass.NextRetryAt.Should().BeAfter(beforeReplay.AddMinutes(10));
        refreshedInClass.LastAttemptAt.Should().NotBeNull();

        var refreshedPractice = await context.DeadLetterQueues.AsNoTracking().SingleAsync(d => d.Id == practiceDlq.Id);
        refreshedPractice.Status.Should().Be("pending");
        refreshedPractice.ResolvedAt.Should().BeNull();
        refreshedPractice.NextRetryAt.Should().BeAfter(beforeReplay.AddMinutes(10));
        refreshedPractice.LastAttemptAt.Should().NotBeNull();

        var refreshedAnswer = await context.PracticeAnswers.AsNoTracking().SingleAsync(a => a.Id == practiceAnswerId);
        refreshedAnswer.Status.Should().Be("pending");

        var refreshedLocked = await context.DeadLetterQueues.AsNoTracking().SingleAsync(d => d.Id == lockedInClassDlq.Id);
        refreshedLocked.Status.Should().Be("resolved");
        refreshedLocked.ResolvedAt.Should().NotBeNull();
        refreshedLocked.NextRetryAt.Should().BeNull();
    }

    [Fact(DisplayName = "PR3-A3: GeminiAiGradingService lọc key placeholder/ngắn và xoay vòng bỏ qua key 401/403 tới key hợp lệ")]
    public async Task GeminiAiGradingService_FiltersPlaceholders_AndRotatesPast401And403ToValidKey()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "YOUR_GEMINI_API_KEY",
                ["Gemini:ApiKeys"] = "placeholder-key;changeme-12345;replace_me_123;<secret-key>;short;bad-auth-key-401;bad-forbid-key-403",
                ["Gemini:FallbackApiKey"] = "valid-real-key-999",
                ["Gemini:Model"] = "gemini-2.5-flash",
                ["Cloudflare:AccountId"] = "",
                ["Cloudflare:ApiToken"] = ""
            })
            .Build();

        var seenKeys = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            var apiKey = request.Headers.GetValues("x-goog-api-key").Single();
            seenKeys.Add(apiKey);

            if (apiKey == "bad-auth-key-401")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            if (apiKey == "bad-forbid-key-403")
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            if (apiKey == "valid-real-key-999")
            {
                const string responseJson = """
                    {
                      "candidates": [
                        {
                          "content": {
                            "parts": [
                              {
                                "text": "{\"Score\":8.5,\"ConfidenceScore\":0.94,\"IsSuspicious\":false,\"Feedback\":\"Trả lời rõ nguyên lý Dependency Inversion.\",\"CotTrace\":\"Đủ ý chính.\",\"Details\":[{\"CriterionId\":\"crit-1\",\"Score\":8.5,\"Comment\":\"Đạt yêu cầu\"}]}"
                              }
                            ]
                          }
                        }
                      ]
                    }
                    """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GeminiAiGradingService(httpClient, config, NullLogger<GeminiAiGradingService>.Instance);

        var result = await service.GradeAnswerAsync(new AiGradingRequest
        {
            QuestionContent = "Explain Dependency Inversion Principle in Clean Architecture.",
            SampleAnswer = "High-level modules should depend on abstractions rather than concrete infrastructure.",
            Transcript = "Dependency Inversion decouples high-level application use cases from database and HTTP infrastructure via interfaces.",
            Criteria = [new AiGradingCriterion { Id = "crit-1", Name = "DIP", MaxScore = 10m, Weight = 1m }],
            ThrowOnTransientFailure = true
        });

        result.Score.Should().Be(8.5m);
        seenKeys.Should().HaveCount(3);
        seenKeys.Should().Contain("bad-auth-key-401");
        seenKeys.Should().Contain("bad-forbid-key-403");
        seenKeys.Last().Should().Be("valid-real-key-999");
        seenKeys.Should().NotContain(k =>
            k.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("changeme", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("replace_me", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith('<') ||
            k == "short");
    }

    [Fact(DisplayName = "PR3-A3: CloudflareR2StorageService.DeleteObjectAsync gọi S3 DeleteObject và trả false khi NoSuchKey")]
    public async Task CloudflareR2StorageService_DeleteObjectAsync_DeletesAndHandlesNotFound()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:ServiceUrl"] = "https://r2.example.test",
                ["Storage:AccessKey"] = "test-access-key",
                ["Storage:SecretKey"] = "test-secret-key",
                ["Storage:BucketName"] = "oral-exam-bucket"
            })
            .Build();

        var s3Mock = new Mock<IAmazonS3>();
        s3Mock.Setup(s => s.DeleteObjectAsync(
                It.Is<DeleteObjectRequest>(r => r.BucketName == "oral-exam-bucket" && r.Key == "questions/202611/img.png"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });
        s3Mock.Setup(s => s.DeleteObjectAsync(
                It.Is<DeleteObjectRequest>(r => r.Key == "questions/202611/missing.png"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" });

        using var storage = new CloudflareR2StorageService(config, resilience: null, s3Client: s3Mock.Object);

        (await storage.DeleteObjectAsync("questions/202611/img.png")).Should().BeTrue();
        (await storage.DeleteObjectAsync("questions/202611/missing.png")).Should().BeFalse();
    }

    [Fact(DisplayName = "PR3-A3: appsettings.Production.json nạp Polly RetryCount=3 & RetryDelayMilliseconds=2000 (2s-4s-8s) hợp lệ")]
    public void ProductionSettings_ResilienceRetryCount3And2000msDelay_LoadsWithoutError()
    {
        var productionSettingsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "API", "appsettings.Production.json"));
        File.Exists(productionSettingsPath).Should().BeTrue();

        var config = new ConfigurationBuilder()
            .AddJsonFile(productionSettingsPath, optional: false)
            .Build();

        foreach (var dep in new[] { "Gemini", "Whisper", "CloudflareAi", "R2" })
        {
            config[$"Resilience:{dep}:RetryCount"].Should().Be("3");
            config[$"Resilience:{dep}:RetryDelayMilliseconds"].Should().Be("2000");
        }

        using var resilience = new ExternalDependencyResilience(config, NullLogger<ExternalDependencyResilience>.Instance);
        resilience.Should().NotBeNull();
    }

    [Fact(DisplayName = "PR3-A1: DependencyInjection nối ITextToSpeechService qua ExternalDependencyHttpHandler")]
    public async Task AddInfrastructure_WiresTextToSpeechHttpClient_ThroughExternalDependencyHttpHandler()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=isolated;Username=isolated;Password=not-used",
                ["Resilience:Defaults:RetryCount"] = "1",
                ["Resilience:Defaults:RetryDelayMilliseconds"] = "0",
                ["Resilience:Defaults:MinimumThroughput"] = "2",
                ["Resilience:Defaults:FailureRatio"] = "1"
            })
            .Build();

        var calls = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddHttpClient(nameof(ITextToSpeechService))
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(_ =>
            {
                calls++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }));

        await using var provider = services.BuildServiceProvider();
        var clientFactory = provider.GetRequiredService<IHttpClientFactory>();
        using var ttsClient = clientFactory.CreateClient(nameof(ITextToSpeechService));

        var act = async () => await ttsClient.GetAsync("https://generativelanguage.googleapis.com/tts-test");
        await act.Should().ThrowAsync<HttpRequestException>();
        calls.Should().Be(2);
    }

    [Fact(DisplayName = "PR3-A4: ReplayDlqTaskCommandHandler reset RetryCount từ abandoned và chặn replay lặp trong 2 phút bằng CONFLICT")]
    public async Task ReplayDlqTaskCommandHandler_EnforcesInFlightConflictGuard_AndResetsRetryCountFromAbandoned()
    {
        var (_, context, provider) = CreateDbScope();
        await using var ownedProvider = provider;

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin.inflight@fpt.edu.vn",
            FullName = "Admin DLQ",
            Role = UserRole.Admin,
            IsActive = true
        };
        var answer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            AnswerText = "Giải thích nguyên lý Clean Architecture.",
            Status = "failed",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-20)
        };
        var abandonedDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            CorrelationId = answer.Id,
            PayloadJson = JsonSerializer.Serialize(new GradingTask
            {
                AnswerId = answer.Id,
                SessionId = answer.SessionId,
                QuestionId = answer.QuestionId,
                StudentId = answer.StudentId,
                AnswerText = answer.AnswerText
            }),
            ErrorMessage = "Max retries exceeded",
            RetryCount = 9,
            Status = "abandoned",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            LastAttemptAt = DateTime.UtcNow.AddMinutes(-10)
        };

        context.Users.Add(admin);
        context.PracticeAnswers.Add(answer);
        context.DeadLetterQueues.Add(abandonedDlq);
        await context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        queueMock
            .Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var handler = new ReplayDlqTaskCommandHandler(context, queueMock.Object);

        // Lần 1: Replay từ abandoned -> reset RetryCount = 0, chuyển sang pending + dời NextRetryAt
        var firstReplay = await handler.Handle(
            new ReplayDlqTaskCommand(abandonedDlq.Id, admin.Id, UserRole.Admin),
            CancellationToken.None);

        firstReplay.IsSuccess.Should().BeTrue();
        var afterFirst = await context.DeadLetterQueues.AsNoTracking().SingleAsync(d => d.Id == abandonedDlq.Id);
        afterFirst.Status.Should().Be("pending");
        afterFirst.RetryCount.Should().Be(0);
        afterFirst.ResolvedByUserId.Should().Be(admin.Id);
        afterFirst.NextRetryAt.Should().NotBeNull();
        afterFirst.LastAttemptAt.Should().NotBeNull();

        // Lần 2 (ngay trong cửa sổ 2 phút): In-flight guard chặn bằng CONFLICT
        var secondReplay = await handler.Handle(
            new ReplayDlqTaskCommand(abandonedDlq.Id, admin.Id, UserRole.Admin),
            CancellationToken.None);

        secondReplay.IsSuccess.Should().BeFalse();
        secondReplay.Error.Should().StartWith("CONFLICT:");
        queueMock.Verify(
            q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "PR3-A4: OfficialExamDlqReplayWorker bỏ qua vé thi IsLocked/PUBLISHED không ném OneWayLock và abandoned khi RetryCount >= 9")]
    public async Task OfficialExamDlqReplayWorker_SkipsLockedPublishedTicketWithoutOneWayLockError_AndAbandonsRetryCount9()
    {
        var dbName = $"Pr3LockedOfficialDlq_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(options =>
            options.UseInMemoryDatabase(dbName).AddInterceptors(new OneWayLockInterceptor()));
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var context = provider.GetRequiredService<OralExamDbContext>();

        var lockedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            SeatNumber = 1,
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };
        var lockedSub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = lockedTicket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Câu trả lời thi thật đã công bố điểm.",
            GradingStatus = "failed"
        };
        var lockedJob = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = lockedSub.Id,
            Status = "failed",
            AttemptCount = 3
        };
        var lockedDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            CorrelationId = lockedJob.Id,
            PayloadJson = JsonSerializer.Serialize(new OfficialExamGradingTask(lockedJob.Id, lockedSub.Id)),
            ErrorMessage = "Gemini 503",
            RetryCount = 3,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var maxRetryTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            SeatNumber = 2,
            Status = ExamTicketStatus.Submitted,
            IsLocked = false
        };
        var maxRetrySub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = maxRetryTicket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Câu trả lời vượt trần retry.",
            GradingStatus = "failed"
        };
        var maxRetryJob = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = maxRetrySub.Id,
            Status = "failed",
            AttemptCount = 9
        };
        var maxRetryDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            CorrelationId = maxRetryJob.Id,
            PayloadJson = JsonSerializer.Serialize(new OfficialExamGradingTask(maxRetryJob.Id, maxRetrySub.Id)),
            ErrorMessage = "Permanent error",
            RetryCount = 9,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
        };

        context.StudentExamTickets.AddRange(lockedTicket, maxRetryTicket);
        context.ExamQuestionSubmissions.AddRange(lockedSub, maxRetrySub);
        context.GradingJobs.AddRange(lockedJob, maxRetryJob);
        context.DeadLetterQueues.AddRange(lockedDlq, maxRetryDlq);
        await context.SaveChangesAsync();

        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            new Mock<IGradingQueueChannel>().Object);

        await worker.ReplayDueAsync(CancellationToken.None);

        var refreshedLockedDlq = await context.DeadLetterQueues.AsNoTracking().SingleAsync(d => d.Id == lockedDlq.Id);
        refreshedLockedDlq.Status.Should().Be("resolved");
        refreshedLockedDlq.ResolvedAt.Should().NotBeNull();
        refreshedLockedDlq.NextRetryAt.Should().BeNull();
        refreshedLockedDlq.ResolutionNotes.Should().Contain("khóa một chiều");

        var refreshedLockedSub = await context.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == lockedSub.Id);
        refreshedLockedSub.GradingStatus.Should().Be("failed");

        var refreshedLockedJob = await context.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == lockedJob.Id);
        refreshedLockedJob.Status.Should().Be("failed");

        var refreshedMaxRetryDlq = await context.DeadLetterQueues.AsNoTracking().SingleAsync(d => d.Id == maxRetryDlq.Id);
        refreshedMaxRetryDlq.Status.Should().Be("abandoned");
        refreshedMaxRetryDlq.NextRetryAt.Should().BeNull();

        var refreshedMaxRetryJob = await context.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == maxRetryJob.Id);
        refreshedMaxRetryJob.Status.Should().Be("failed");
    }

    [Fact(DisplayName = "PR3-A4: ExternalDependencyHttpHandler cô lập translate.googleapis.com sang GoogleTts độc lập với Gemini")]
    public async Task ExternalDependencyHttpHandler_RoutesGoogleTranslateTtsIndependentlyFromGeminiCircuit()
    {
        ExternalDependencyHttpHandler.ResolveDependency(
                ExternalDependency.Gemini,
                new Uri("https://translate.googleapis.com/translate_tts?q=test"))
            .Should().Be(ExternalDependency.GoogleTts);
        ExternalDependencyHttpHandler.ResolveDependency(
                ExternalDependency.GoogleTts,
                new Uri("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent"))
            .Should().Be(ExternalDependency.Gemini);
        ExternalDependencyHttpHandler.ResolveDependency(
                ExternalDependency.Gemini,
                new Uri("https://api.cloudflare.com/client/v4/accounts/test/ai/run/llama"))
            .Should().Be(ExternalDependency.CloudflareAi);
        ExternalDependencyHttpHandler.ResolveDependency(
                ExternalDependency.Whisper,
                new Uri("https://api.cloudflare.com/client/v4/accounts/test/ai/run/whisper"))
            .Should().Be(ExternalDependency.Whisper);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resilience:Defaults:RetryCount"] = "0",
                ["Resilience:Defaults:RetryDelayMilliseconds"] = "0",
                ["Resilience:Defaults:MinimumThroughput"] = "2",
                ["Resilience:Defaults:FailureRatio"] = "1",
                ["Resilience:Defaults:BreakDurationSeconds"] = "30"
            })
            .Build();

        using var resilience = new ExternalDependencyResilience(config, NullLogger<ExternalDependencyResilience>.Instance);
        var translateCalls = 0;
        var geminiCalls = 0;

        HttpResponseMessage HandleRequest(HttpRequestMessage req)
        {
            if (string.Equals(req.RequestUri?.Host, "translate.googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                translateCalls++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            geminiCalls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        using var ttsClient = new HttpClient(
            new ExternalDependencyHttpHandler(resilience, ExternalDependency.GoogleTts)
            {
                InnerHandler = new StubHttpMessageHandler(HandleRequest)
            });
        using var geminiClient = new HttpClient(
            new ExternalDependencyHttpHandler(resilience, ExternalDependency.Gemini)
            {
                InnerHandler = new StubHttpMessageHandler(HandleRequest)
            });

        // 2 lỗi 503 từ Google Translate TTS sẽ ngắt cầu dao GoogleTts
        for (var i = 0; i < 2; i++)
        {
            var failAct = async () => await ttsClient.GetAsync("https://translate.googleapis.com/translate_tts?q=hello");
            await failAct.Should().ThrowAsync<HttpRequestException>();
        }

        // Lần gọi thứ 3 tới Google Translate TTS bị chặn bởi mạch mở GoogleTts (không gọi xuống mạng)
        var openCircuitAct = async () => await ttsClient.GetAsync("https://translate.googleapis.com/translate_tts?q=hello");
        await openCircuitAct.Should().ThrowAsync<HttpRequestException>().WithMessage("*GoogleTts*");
        translateCalls.Should().Be(2);

        // Cầu dao Gemini vẫn hoàn toàn khỏe mạnh trên cả hai client
        using var ttsGeminiResp = await ttsClient.GetAsync("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent");
        using var gradingGeminiResp = await geminiClient.GetAsync("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent");
        ttsGeminiResp.StatusCode.Should().Be(HttpStatusCode.OK);
        gradingGeminiResp.StatusCode.Should().Be(HttpStatusCode.OK);
        geminiCalls.Should().Be(2);

        // Xác nhận appsettings.Production.json có cấu hình độc lập cho GoogleTts
        var productionSettingsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "API", "appsettings.Production.json"));
        var prodConfig = new ConfigurationBuilder()
            .AddJsonFile(productionSettingsPath, optional: false)
            .Build();
        prodConfig["Resilience:GoogleTts:RetryCount"].Should().Be("2");
        prodConfig["Resilience:GoogleTts:RetryDelayMilliseconds"].Should().Be("1000");
        prodConfig["Resilience:GoogleTts:AttemptTimeoutSeconds"].Should().Be("10");
        prodConfig["Resilience:GoogleTts:TotalTimeoutSeconds"].Should().Be("30");
        prodConfig["Resilience:GoogleTts:BreakDurationSeconds"].Should().Be("15");
        prodConfig["Resilience:GoogleTts:MinimumThroughput"].Should().Be("20");
    }

    [Fact(DisplayName = "PR3-A4: InClassShiftPassword.GraceUntil cho vào khi còn hạn, chặn khi quá hạn và hỗ trợ fallback ValidTo + 45s cho bản ghi legacy")]
    public async Task EnterInClassShift_EnforcesGraceUntilColumn_RejectsExpiredGrace_AndSupportsLegacyFallback()
    {
        static string PickNonLiveOtp(string secretKey, DateTime utcNow, int seed)
        {
            for (var offset = 0; offset < 50; offset++)
            {
                var candidate = ((seed + offset) % 900000 + 100000).ToString();
                if (!TotpPasswordHelper.ValidateCode(secretKey, candidate, utcNow, gracePeriodSeconds: 60))
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException("Unable to pick a non-live OTP code for GraceUntil test.");
        }

        var (_, context, provider) = CreateDbScope();
        await using var ownedProvider = provider;

        var now = DateTime.UtcNow;
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv.grace@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var studentActiveGrace = new User { Id = Guid.NewGuid(), Email = "sv1.grace@fpt.edu.vn", FullName = "SV1", StudentCode = "SE190001", Role = UserRole.Student, IsActive = true };
        var studentExpiredGrace = new User { Id = Guid.NewGuid(), Email = "sv2.grace@fpt.edu.vn", FullName = "SV2", StudentCode = "SE190002", Role = UserRole.Student, IsActive = true };
        var studentLegacyFallback = new User { Id = Guid.NewGuid(), Email = "sv3.grace@fpt.edu.vn", FullName = "SV3", StudentCode = "SE190003", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", Credits = 3, IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1901", Name = "PRN231 - SE1901", CourseId = course.Id, Course = course, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Giải thích Dependency Injection.",
            SampleAnswer = "Dependency Injection đảo ngược sự phụ thuộc để dễ bảo trì và kiểm thử độc lập.",
            Difficulty = "medium",
            BloomLevel = "Understand",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true,
            IsDeleted = false
        };
        const string secretKey = "GRACE_UNTIL_TEST_SECRET_2026";
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            LecturerId = lecturer.Id,
            ShiftName = "Ca kiểm tra GraceUntil",
            TotpSecretKey = secretKey,
            Status = "IN_PROGRESS",
            IsLocked = false,
            QuestionPoolMode = "ALL_COURSE_BANK",
            SelectedDifficultiesJson = "[\"easy\",\"medium\",\"hard\"]",
            TotalQuestionsPerStudent = 1,
            ExamDurationMinutes = 20,
            StartTime = now.AddMinutes(-10),
            EndTime = now.AddMinutes(30)
        };

        var activeGraceCode = PickNonLiveOtp(secretKey, now, 111222);
        var expiredGraceCode = PickNonLiveOtp(secretKey, now, 333444);
        var legacyCode = PickNonLiveOtp(secretKey, now, 555666);

        context.Users.AddRange(lecturer, studentActiveGrace, studentExpiredGrace, studentLegacyFallback);
        context.Courses.Add(course);
        context.Classes.Add(cls);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        context.InClassExamShifts.Add(shift);
        context.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = studentActiveGrace.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = studentExpiredGrace.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = studentLegacyFallback.Id, Status = "enrolled" });
        context.InClassShiftPasswords.AddRange(
            new InClassShiftPassword
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                PlainCode = activeGraceCode,
                PasswordHash = "hash-active",
                ValidFrom = now.AddMinutes(-6),
                ValidTo = now.AddMinutes(-1),
                GraceUntil = now.AddSeconds(30)
            },
            new InClassShiftPassword
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                PlainCode = expiredGraceCode,
                PasswordHash = "hash-expired",
                ValidFrom = now.AddMinutes(-6),
                ValidTo = now.AddSeconds(-20),
                GraceUntil = now.AddSeconds(-5)
            },
            new InClassShiftPassword
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                PlainCode = legacyCode,
                PasswordHash = "hash-legacy",
                ValidFrom = now.AddMinutes(-6),
                ValidTo = now.AddSeconds(-20),
                GraceUntil = null
            });
        await context.SaveChangesAsync();

        var handler = new EnterInClassShiftCommandHandler(
            context,
            new EnterInClassShiftCommandValidator(),
            new Mock<IGradingQueueChannel>().Object);

        // 1. GraceUntil >= utcNow -> được phép vào thi
        var activeGraceResult = await handler.Handle(
            new EnterInClassShiftCommand(studentActiveGrace.Id, shift.Id, activeGraceCode),
            CancellationToken.None);
        activeGraceResult.IsSuccess.Should().BeTrue();

        // 2. GraceUntil < utcNow -> bị chặn InvalidOtp (kể cả khi ValidTo còn trong 45s)
        var expiredGraceResult = await handler.Handle(
            new EnterInClassShiftCommand(studentExpiredGrace.Id, shift.Id, expiredGraceCode),
            CancellationToken.None);
        expiredGraceResult.IsSuccess.Should().BeFalse();
        expiredGraceResult.Error.Should().Be(InClassShiftErrors.InvalidOtp);

        // 3. Legacy GraceUntil == null fallback ValidTo + 45s -> được phép vào thi
        var legacyFallbackResult = await handler.Handle(
            new EnterInClassShiftCommand(studentLegacyFallback.Id, shift.Id, legacyCode),
            CancellationToken.None);
        legacyFallbackResult.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "PR3-A4: ExamGradeReportExporter và PadesPdfDigitalSigner fallback đúng khi config rỗng \"\" và từ chối chứng thư hết hạn / thiếu khóa bí mật")]
    public void ExamGradeReportExporter_AndPadesSigner_HandleEmptyStringConfigFallback_AndRejectInvalidCertificates()
    {
        var emptyUrlConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfSigning:VerificationBaseUrl"] = ""
            })
            .Build();

        using var devSigner = new PadesPdfDigitalSigner();
        var defaultExporter = new ExamGradeReportExporter(devSigner, emptyUrlConfig, _ => null);
        defaultExporter.VerificationBaseUrl.Should().Be("https://oralexam.io.vn");

        var envFallbackExporter = new ExamGradeReportExporter(
            devSigner,
            emptyUrlConfig,
            name => name == "PDF_SIGNING_VERIFICATION_BASE_URL" ? "https://verify.fpt.edu.vn/" : null);
        envFallbackExporter.VerificationBaseUrl.Should().Be("https://verify.fpt.edu.vn");

        // Tạo PFX hợp lệ và xác nhận PadesPdfDigitalSigner fallback sang ENV khi config chứa ""
        using var rsa = RSA.Create(2048);
        var validReq = new CertificateRequest(
            "CN=FPTU Env Fallback Signer, O=FPT University, C=VN",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var validCert = validReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        var expectedThumbprint = Convert.ToHexString(validCert.GetCertHash(HashAlgorithmName.SHA256)).ToLowerInvariant();
        const string pfxPassword = "Env-Fallback-Password-2026!";
        var validPfxBase64 = Convert.ToBase64String(validCert.Export(X509ContentType.Pfx, pfxPassword));

        var emptyProdConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "",
                ["PdfSigning:CertificateBase64"] = "",
                ["PdfSigning:CertificatePath"] = "",
                ["PdfSigning:CertificatePassword"] = ""
            })
            .Build();

        using var envSigner = new PadesPdfDigitalSigner(
            emptyProdConfig,
            name => name switch
            {
                "PDF_SIGNING_CERTIFICATE_BASE64" => validPfxBase64,
                "PDF_SIGNING_CERTIFICATE_PASSWORD" => pfxPassword,
                _ => null
            });
        envSigner.GetActiveCertificateThumbprint().Should().Be(expectedThumbprint);

        // Chứng thư hết hạn (NotAfter < UtcNow) phải bị chặn FAIL CLOSED
        using var expiredCert = validReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1));
        var expiredPfxBase64 = Convert.ToBase64String(expiredCert.Export(X509ContentType.Pfx, pfxPassword));
        using var expiredSigner = new PadesPdfDigitalSigner(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Production",
                    ["PdfSigning:CertificateBase64"] = expiredPfxBase64,
                    ["PdfSigning:CertificatePassword"] = pfxPassword
                })
                .Build(),
            _ => null);
        Action expiredAct = () => _ = expiredSigner.GetActiveCertificateThumbprint();
        expiredAct.Should().Throw<InvalidOperationException>().WithMessage("*hết hạn*");

        // Chứng thư chỉ có Public Key (không có Private Key) phải bị chặn FAIL CLOSED
        var publicOnlyBase64 = Convert.ToBase64String(validCert.Export(X509ContentType.Cert));
        using var publicOnlySigner = new PadesPdfDigitalSigner(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Production",
                    ["PdfSigning:CertificateBase64"] = publicOnlyBase64
                })
                .Build(),
            _ => null);
        Action publicOnlyAct = () => _ = publicOnlySigner.GetActiveCertificateThumbprint();
        publicOnlyAct.Should().Throw<InvalidOperationException>().WithMessage("*khóa bí mật*");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
