using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamGradingWorkerWhisperAndR2Tests
{
    private static IConfiguration CreateR2Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:AccessKey"] = "test-access-key",
                ["Storage:SecretKey"] = "test-secret-key",
                ["Storage:ServiceUrl"] = "https://r2.test.invalid",
                ["Storage:BucketName"] = "oral-exam-test"
            })
            .Build();

    [Fact]
    public async Task CloudflareR2StorageService_VerifySha256_UsesHeadObjectChecksumFastPath_WithoutCallingGetObject()
    {
        var payload = Encoding.UTF8.GetBytes("official-exam-audio-webm-bytes");
        var hashBytes = SHA256.HashData(payload);
        var expectedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var wrongHex = new string('0', 64);
        var checksumBase64 = Convert.ToBase64String(hashBytes);

        var s3Mock = new Mock<IAmazonS3>();
        s3Mock.Setup(c => c.GetObjectMetadataAsync(
                It.Is<GetObjectMetadataRequest>(r => r.Key == "official-exams/s1/t1/01_SE170001_Q01.webm" && r.ChecksumMode == ChecksumMode.ENABLED),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                ChecksumSHA256 = checksumBase64,
                ETag = "\"etag-1\""
            });

        using var service = new CloudflareR2StorageService(CreateR2Config(), s3Client: s3Mock.Object);

        var matched = await service.VerifySha256Async("official-exams/s1/t1/01_SE170001_Q01.webm", expectedHex);
        var mismatched = await service.VerifySha256Async("official-exams/s1/t1/01_SE170001_Q01.webm", wrongHex);

        matched.Should().BeTrue();
        mismatched.Should().BeFalse();
        s3Mock.Verify(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never,
            "HeadObject ChecksumSHA256 fast-path must avoid re-downloading audio bytes");
    }

    [Fact]
    public async Task CloudflareR2StorageService_StreamingFallback_OpenReadStream_AndPresignedChecksumHeader()
    {
        var payload = Encoding.UTF8.GetBytes("fallback-audio-stream-payload");
        var hashBytes = SHA256.HashData(payload);
        var expectedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var expectedBase64 = Convert.ToBase64String(hashBytes);

        var s3Mock = new Mock<IAmazonS3>();
        s3Mock.Setup(c => c.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse { ChecksumSHA256 = null });
        s3Mock.Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new GetObjectResponse { ResponseStream = new MemoryStream(payload) });
        s3Mock.Setup(c => c.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync("https://r2.test.invalid/presigned-put");

        using var service = new CloudflareR2StorageService(CreateR2Config(), s3Client: s3Mock.Object);

        // 1. Streaming SHA-256 fallback when HeadObject has no ChecksumSHA256
        var verified = await service.VerifySha256Async("official-exams/s1/t1/01_SE170001_Q01.webm", expectedHex);
        verified.Should().BeTrue();
        s3Mock.Verify(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        // 2. OpenReadStreamAsync returns full stream content
        await using (var stream = await service.OpenReadStreamAsync("official-exams/s1/t1/01_SE170001_Q01.webm"))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            var text = await reader.ReadToEndAsync();
            text.Should().Be("fallback-audio-stream-payload");
        }

        // 3. CreatePresignedUploadUrlAsync signs x-amz-checksum-sha256
        var presigned = await service.CreatePresignedUploadUrlAsync(
            "official-exams/s1/t1/01_SE170001_Q01.webm",
            "audio/webm",
            TimeSpan.FromMinutes(10),
            expectedBase64);
        presigned.RequiredHeaders.Should().ContainKey("x-amz-checksum-sha256").WhoseValue.Should().Be(expectedBase64);
        presigned.RequiredHeaders.Should().ContainKey("If-None-Match").WhoseValue.Should().Be("*");

        // Invalid Base64 throws ArgumentException
        var badAct = () => service.CreatePresignedUploadUrlAsync(
            "official-exams/s1/t1/01_SE170001_Q01.webm", "audio/webm", TimeSpan.FromMinutes(10), "not-valid-base64!!!");
        await badAct.Should().ThrowAsync<ArgumentException>();
    }

    private static (ServiceProvider Provider, OralExamDbContext Db, OfficialExamGradingTask Task, ExamQuestionSubmission Submission) CreateWorkerHarness(
        string examInputMode,
        string? clientTranscript,
        string? initialVoiceTranscript,
        string? audioObjectKey,
        string? audioHash,
        bool isFollowUp,
        IStorageService? storage,
        ISpeechToTextService? stt,
        IAiGradingService grading)
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddSingleton(grading);
        if (storage is not null) services.AddSingleton(storage);
        if (stt is not null) services.AddSingleton(stt);

        var provider = services.BuildServiceProvider();
        var seedContext = provider.GetRequiredService<OralExamDbContext>();

        var student = new User { Id = Guid.NewGuid(), Email = "sv.worker@fpt.edu.vn", StudentCode = "SE174001", FullName = "SV Worker", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", Credits = 3, IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "FE SWD392",
            ExamDate = new DateOnly(2026, 11, 20),
            ExamInputMode = examInputMode,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ExamShiftWindow.InProgress
        };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Accuracy", MaxScore = 10m, Weight = 1m, OrderIndex = 1 };
        var question = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Explain SOLID", SampleAnswer = new string('a', 60), ApprovalStatus = "APPROVED", IsActive = true };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, SeatNumber = 1, Status = ExamTicketStatus.Submitted };

        seedContext.Users.Add(student);
        seedContext.Courses.Add(course);
        seedContext.OfficialExamSessions.Add(session);
        seedContext.RealExamSessionShifts.Add(shift);
        seedContext.Rubrics.Add(rubric);
        seedContext.RubricCriteria.Add(criterion);
        seedContext.ExamQuestions.Add(question);
        seedContext.StudentExamTickets.Add(ticket);

        Guid? parentId = null;
        if (isFollowUp)
        {
            var mainSub = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                ExamQuestionId = question.Id,
                IsFollowUp = false,
                FollowUpIndex = 0,
                TranscriptWhisper = "Main SOLID explanation",
                GeneratedFollowUpPrompt = "Give an LSP violation example?",
                GradingStatus = "ai_graded",
                AiScore = 7.0m
            };
            seedContext.ExamQuestionSubmissions.Add(mainSub);
            parentId = mainSub.Id;
        }

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = question.Id,
            IsFollowUp = isFollowUp,
            ParentSubmissionId = parentId,
            FollowUpIndex = isFollowUp ? 1 : 0,
            FollowUpPrompt = isFollowUp ? "Give an LSP violation example?" : null,
            InitialVoiceTranscript = initialVoiceTranscript,
            TranscriptWhisper = clientTranscript,
            AudioObjectKey = audioObjectKey,
            AudioR2Url = audioObjectKey,
            AudioHashSha256 = audioHash,
            GradingStatus = "pending"
        };
        var job = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Status = "pending",
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        seedContext.ExamQuestionSubmissions.Add(submission);
        seedContext.GradingJobs.Add(job);
        seedContext.SaveChanges();

        var task = new OfficialExamGradingTask(job.Id, submission.Id);
        return (provider, seedContext, task, submission);
    }

    [Fact]
    public async Task Worker_VoiceOnlyEmptyTranscript_RunsWhisperFromR2_AndPassesFollowUpContextToAiGrader()
    {
        var validHash = new string('c', 64);
        var audioKey = "official-exams/s1/t1/01_SE174001_Q01_FU1.webm";

        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.VerifySha256Async(audioKey, validHash, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        storageMock.Setup(s => s.OpenReadStreamAsync(audioKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("audio-bytes")));

        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), "01_SE174001_Q01_FU1.webm", "SWD392", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult(
                Text: "Square inheriting Rectangle violates LSP when SetWidth changes height.",
                RawText: "Square inheriting Rectangle violates LSP when SetWidth changes height.",
                Provider: "CloudflareWhisper",
                UsedFallback: false,
                TimestampsJson: "[{\"word\":\"Square\",\"start\":0.1}]",
                ConfidenceScore: 0.94));

        AiGradingRequest? capturedRequest = null;
        var gradingMock = new Mock<IAiGradingService>();
        gradingMock.Setup(g => g.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiGradingRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new AiGradingResult { Score = 8.5m, ConfidenceScore = 0.93m, Feedback = "Good LSP example", CotTrace = "Checked LSP" });

        var (provider, _, task, submission) = CreateWorkerHarness(
            ExamInputMode.VoiceOnly,
            clientTranscript: "",
            initialVoiceTranscript: null,
            audioObjectKey: audioKey,
            audioHash: validHash,
            isFollowUp: true,
            storageMock.Object,
            sttMock.Object,
            gradingMock.Object);

        using (provider)
        {
            var worker = new OfficialExamGradingWorker(
                Mock.Of<IOfficialExamGradingQueue>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OfficialExamGradingWorker>.Instance);

            await worker.ProcessAsync(task, CancellationToken.None);

            using var verifyScope = provider.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var saved = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
            saved.GradingStatus.Should().Be("ai_graded");
            saved.TranscriptWhisper.Should().Contain("Square inheriting Rectangle");
            saved.InitialVoiceTranscript.Should().Contain("Square inheriting Rectangle");
            saved.TimestampsWhisper.Should().Contain("Square");
            saved.AiScore.Should().Be(8.5m);
            saved.AudioVerifiedAt.Should().NotBeNull();

            var ticket = await db.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == submission.TicketId);
            ticket.Status.Should().Be(ExamTicketStatus.AiGraded);

            capturedRequest.Should().NotBeNull();
            capturedRequest!.IsFollowUp.Should().BeTrue();
            capturedRequest.FollowUpIndex.Should().Be(1);
            capturedRequest.FollowUpPrompt.Should().Be("Give an LSP violation example?");
            capturedRequest.OriginalAnswerTranscript.Should().Be("Main SOLID explanation");
        }
    }

    [Fact]
    public async Task Worker_Sha256Mismatch_FlagsSuspiciousAndThrowsForDlq_WithoutCallingSttOrAi()
    {
        var hash = new string('d', 64);
        var audioKey = "official-exams/s1/t1/01_SE174001_Q01.webm";

        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.VerifySha256Async(audioKey, hash, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var sttMock = new Mock<ISpeechToTextService>();
        var gradingMock = new Mock<IAiGradingService>();

        var (provider, _, task, submission) = CreateWorkerHarness(
            ExamInputMode.VoiceOnly, "", null, audioKey, hash, false,
            storageMock.Object, sttMock.Object, gradingMock.Object);

        using (provider)
        {
            var worker = new OfficialExamGradingWorker(
                Mock.Of<IOfficialExamGradingQueue>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OfficialExamGradingWorker>.Instance);

            var act = () => worker.ProcessAsync(task, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AUDIO_INTEGRITY_MISMATCH*");

            sttMock.Verify(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            gradingMock.Verify(g => g.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Never);

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var saved = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
            saved.IsSuspicious.Should().BeTrue();
            saved.SuspiciousReason.Should().Contain("SHA-256");
        }
    }

    [Fact]
    public async Task Worker_WhisperDivergesOver35PercentFromEditedTranscript_FlagsSuspicious_AndSilentWhisperGradesZero()
    {
        var hash = new string('e', 64);
        var audioKey = "official-exams/s1/t1/01_SE174001_Q01.webm";

        // Case 1: Tamper suspicion when Whisper R2 transcript differs > 35% from client-submitted transcript
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.VerifySha256Async(audioKey, hash, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        storageMock.Setup(s => s.OpenReadStreamAsync(audioKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("audio-tamper")));

        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult("Em không nhớ định nghĩa này lắm ạ", "Em không nhớ định nghĩa này lắm ạ", "Whisper", false, null, 0.90));

        var gradingMock = new Mock<IAiGradingService>();
        gradingMock.Setup(g => g.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult { Score = 9.0m, ConfidenceScore = 0.96m, Feedback = "High score", CotTrace = "CoT" });

        var (provider1, _, task1, sub1) = CreateWorkerHarness(
            ExamInputMode.VoiceWithTranscriptEdit,
            clientTranscript: "Single Responsibility, Open Closed, Liskov Substitution, Interface Segregation, Dependency Inversion.",
            initialVoiceTranscript: null,
            audioObjectKey: audioKey,
            audioHash: hash,
            isFollowUp: false,
            storageMock.Object,
            sttMock.Object,
            gradingMock.Object);

        using (provider1)
        {
            var worker = new OfficialExamGradingWorker(
                Mock.Of<IOfficialExamGradingQueue>(),
                provider1.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OfficialExamGradingWorker>.Instance);

            await worker.ProcessAsync(task1, CancellationToken.None);

            using var scope = provider1.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var saved = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub1.Id);
            saved.IsSuspicious.Should().BeTrue();
            saved.TranscriptEditRatio.Should().BeGreaterThan(0.35m);
            saved.SuspiciousReason.Should().Contain("Whisper");
        }

        // Case 2: Silent audio (Whisper returns empty) -> NON_ANSWER_GUARD 0.0đ without calling LLM
        var silentStt = new Mock<ISpeechToTextService>();
        silentStt.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult("   ", "   ", "Whisper", false, null, 0.99));
        var unusedGrader = new Mock<IAiGradingService>();

        var (provider2, _, task2, sub2) = CreateWorkerHarness(
            ExamInputMode.VoiceOnly, "", null, audioKey, hash, false,
            storageMock.Object, silentStt.Object, unusedGrader.Object);

        using (provider2)
        {
            var worker = new OfficialExamGradingWorker(
                Mock.Of<IOfficialExamGradingQueue>(),
                provider2.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OfficialExamGradingWorker>.Instance);

            await worker.ProcessAsync(task2, CancellationToken.None);

            unusedGrader.Verify(g => g.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Never);

            using var scope = provider2.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var saved = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub2.Id);
            saved.GradingStatus.Should().Be("ai_graded");
            saved.AiScore.Should().Be(0m);
            saved.IsSuspicious.Should().BeTrue();
        }
    }
}
