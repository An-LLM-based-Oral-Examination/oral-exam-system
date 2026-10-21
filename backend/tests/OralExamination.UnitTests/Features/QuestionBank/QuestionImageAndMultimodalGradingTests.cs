using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.Commands.UploadQuestionImage;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;
using OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank;

public sealed class QuestionImageAndMultimodalGradingTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"QuestionImageTests_{Guid.NewGuid():N}")
            .Options;
        return new OralExamDbContext(options);
    }

    private static byte[] CreateValidPngBytes()
    {
        // 8-byte PNG signature + minimal IHDR/IEND bytes
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
        };
    }

    [Fact]
    public async Task UploadQuestionImage_ShouldAcceptValidPng_AndResolveInGeminiMultimodalGrading()
    {
        var pngBytes = CreateValidPngBytes();
        using var stream = new MemoryStream(pngBytes);
        var handler = new UploadQuestionImageCommandHandler(storageService: null);

        var uploadResult = await handler.Handle(new UploadQuestionImageCommand(
            stream,
            "clean-arch-diagram.png",
            "image/png",
            pngBytes.Length,
            "Sơ đồ kiến trúc Clean Architecture 4 tầng: Domain, Application, Infrastructure, API"),
            CancellationToken.None);

        uploadResult.IsSuccess.Should().BeTrue();
        uploadResult.Value.Should().NotBeNull();
        uploadResult.Value.ImageUrl.Should().StartWith("/uploads/questions/");
        uploadResult.Value.ImageUrl.Should().EndWith(".png");
        uploadResult.Value.ContentType.Should().Be("image/png");
        uploadResult.Value.SizeBytes.Should().Be(pngBytes.Length);
        uploadResult.Value.Description.Should().Contain("Clean Architecture 4 tầng");

        var relativePath = uploadResult.Value.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var savedFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
        File.Exists(savedFullPath).Should().BeTrue();

        try
        {
            string? capturedRequestBody = null;
            var fakeHttpHandler = new CaptureHttpMessageHandler(reqBody =>
            {
                capturedRequestBody = reqBody;
                var geminiResponse = new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                parts = new[]
                                {
                                    new
                                    {
                                        text = JsonSerializer.Serialize(new
                                        {
                                            Score = 9.0m,
                                            ConfidenceScore = 0.96m,
                                            IsSuspicious = false,
                                            Feedback = "Sinh viên phân tích đúng sơ đồ Clean Architecture 4 tầng đính kèm.",
                                            CotTrace = "Đã đối chiếu sơ đồ PNG và câu trả lời.",
                                            Details = new[]
                                            {
                                                new { CriterionId = "crit-1", Score = 5.0m, Comment = "Đúng chiều phụ thuộc vào Domain." },
                                                new { CriterionId = "crit-2", Score = 4.0m, Comment = "Giải thích rõ Infrastructure và API." }
                                            }
                                        })
                                    }
                                }
                            }
                        }
                    }
                };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
                };
            });

            using var httpClient = new HttpClient(fakeHttpHandler);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Gemini:ApiKey"] = "test-gemini-key-multimodal"
                })
                .Build();

            var gradingService = new GeminiAiGradingService(httpClient, configuration);
            var gradingResult = await gradingService.GradeAnswerAsync(new AiGradingRequest
            {
                QuestionContent = "Quan sát sơ đồ kiến trúc đính kèm và giải thích chiều phụ thuộc giữa các tầng.",
                QuestionImageUrl = uploadResult.Value.ImageUrl,
                QuestionImageDescription = uploadResult.Value.Description,
                SampleAnswer = "Trong sơ đồ Clean Architecture, các tầng bên ngoài (API, Infrastructure) phụ thuộc hướng vào tầng Application và tầng lõi Domain.",
                Transcript = "Theo sơ đồ đính kèm, tầng Domain nằm ở tâm và không phụ thuộc bất kỳ tầng nào, còn Infrastructure và API phụ thuộc vào Application và Domain.",
                Criteria = new List<AiGradingCriterion>
                {
                    new() { Id = "crit-1", Name = "Chiều phụ thuộc", Description = "Giải thích đúng chiều mũi tên vào Domain", MaxScore = 5.0m, Weight = 0.5m },
                    new() { Id = "crit-2", Name = "Vai trò các tầng", Description = "Phân tích đúng Infrastructure và API", MaxScore = 5.0m, Weight = 0.5m }
                }
            }, CancellationToken.None);

            gradingResult.Score.Should().Be(9.0m);
            capturedRequestBody.Should().NotBeNullOrWhiteSpace();
            capturedRequestBody.Should().Contain("inline_data");
            capturedRequestBody.Should().Contain("image/png");
            capturedRequestBody.Should().Contain(Convert.ToBase64String(pngBytes));
            capturedRequestBody.Should().Contain("Clean Architecture 4 tầng");
        }
        finally
        {
            if (File.Exists(savedFullPath))
            {
                File.Delete(savedFullPath);
            }
        }
    }

    [Fact]
    public async Task UploadQuestionImage_ShouldRejectInvalidMagicBytes_AndOversizedPayload()
    {
        var handler = new UploadQuestionImageCommandHandler(storageService: null);

        // 1. Fake .png with executable/text magic bytes
        var fakeBytes = Encoding.UTF8.GetBytes("MZ-fake-executable-content-not-an-image");
        using var fakeStream = new MemoryStream(fakeBytes);
        var fakeResult = await handler.Handle(new UploadQuestionImageCommand(
            fakeStream,
            "malicious.png",
            "image/png",
            fakeBytes.Length), CancellationToken.None);

        fakeResult.IsSuccess.Should().BeFalse();
        fakeResult.Error.Should().StartWith("UNSUPPORTED_MEDIA_TYPE:");

        // 2. Oversized > 5MB
        using var smallStream = new MemoryStream(CreateValidPngBytes());
        var oversizedResult = await handler.Handle(new UploadQuestionImageCommand(
            smallStream,
            "huge.png",
            "image/png",
            5 * 1024 * 1024 + 1), CancellationToken.None);

        oversizedResult.IsSuccess.Should().BeFalse();
        oversizedResult.Error.Should().StartWith("PAYLOAD_TOO_LARGE:");
    }

    [Fact]
    public async Task CreateDraftQuestion_ShouldAllowImageForManualQuestion_AndRejectImageForFlmApiQuestion()
    {
        using var context = CreateDbContext();
        var courseId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        context.Courses.Add(new Course
        {
            Id = courseId,
            Code = "SWD392",
            Name = "Software Architecture and Design",
            Credits = 3,
            IsActive = true
        });
        context.Users.Add(new User
        {
            Id = lecturerId,
            Email = "lecturer.swd@fpt.edu.vn",
            FullName = "Giảng viên SWD",
            Role = "lecturer",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var rubric = new RubricDraftDto(
            "Barem Sơ Đồ UML",
            "Đánh giá đọc hiểu sơ đồ Sequence Diagram",
            10.00m,
            new List<RubricCriterionDraftDto>
            {
                new("Luồng thông điệp", 5.00m, 0.5m, "Analyze", "Giải thích đúng thứ tự message"),
                new("Xử lý bất đồng bộ", 5.00m, 0.5m, "Evaluate", "Chỉ ra điểm nghẽn queue")
            });

        var createHandler = new CreateDraftQuestionCommandHandler(context);

        // 1. Chặn nếu Source = "flm_api" mà cố tình gắn ImageUrl
        var flmWithImageResult = await createHandler.Handle(new CreateDraftQuestionCommand(
            courseId,
            lecturerId,
            "Câu hỏi AI không được gắn ảnh tay",
            "Phân tích sơ đồ sau?",
            "Câu trả lời mẫu dài hơn 50 ký tự để thỏa mãn điều kiện kiểm tra độ dài tối thiểu của hệ thống.",
            new List<string> { "Ý 1", "Ý 2" },
            "medium",
            "Analyze",
            "flm_api",
            "both",
            false,
            null,
            rubric,
             null,
            null,
            "/uploads/questions/202610/test.png",
            "Mô tả ảnh"), CancellationToken.None);

        flmWithImageResult.IsSuccess.Should().BeFalse();
        flmWithImageResult.Error.Should().Contain("manual");

        // 2. Cho phép khi Source = "manual" và lưu vào cả PracticeQuestions lẫn ExamQuestions
        var manualWithImageResult = await createHandler.Handle(new CreateDraftQuestionCommand(
            courseId,
            lecturerId,
            "Phân tích Sequence Diagram OAuth2",
            "Dựa vào hình ảnh Sequence Diagram đính kèm, hãy giải thích bước trao đổi Authorization Code lấy Access Token.",
            "Sinh viên cần chỉ rõ Client gửi Authorization Code kèm Client Secret lên Token Endpoint để nhận JWT Access Token.",
            new List<string> { "Authorization Code", "Token Endpoint" },
            "hard",
            "Analyze",
            "manual",
            "both",
            true,
            "Nếu Authorization Code bị đánh cắp trên đường truyền thì PKCE bảo vệ như thế nào?",
            rubric,
            null,
            null,
            "/uploads/questions/202610/oauth2-sequence.png",
            "Sơ đồ tuần tự OAuth 2.0 Authorization Code Flow với PKCE"), CancellationToken.None);

        manualWithImageResult.IsSuccess.Should().BeTrue();

        var savedExamQuestion = await context.ExamQuestions.FirstAsync(q => q.Id == manualWithImageResult.Value);
        savedExamQuestion.ImageUrl.Should().Be("/uploads/questions/202610/oauth2-sequence.png");
        savedExamQuestion.ImageDescription.Should().Contain("PKCE");

        var savedPracticeQuestion = await context.PracticeQuestions.FirstAsync(q => q.CourseId == courseId);
        savedPracticeQuestion.ImageUrl.Should().Be("/uploads/questions/202610/oauth2-sequence.png");
        savedPracticeQuestion.ImageDescription.Should().Contain("PKCE");

        // 3. Kiểm tra Query chi tiết câu hỏi (MF-03) trả về đúng ImageUrl & ImageDescription
        var getByIdHandler = new GetQuestionByIdQueryHandler(context);
        var detailResult = await getByIdHandler.Handle(new GetQuestionByIdQuery(savedExamQuestion.Id), CancellationToken.None);
        detailResult.IsSuccess.Should().BeTrue();
        detailResult.Value.ImageUrl.Should().Be("/uploads/questions/202610/oauth2-sequence.png");
        detailResult.Value.ImageDescription.Should().Contain("PKCE");

        // 4. Kiểm tra StartPracticeSession (MF-01) trả về đúng ImageUrl & ImageDescription
        var startPracticeHandler = new StartPracticeSessionCommandHandler(context);
        var practiceResult = await startPracticeHandler.Handle(new StartPracticeSessionCommand(
            Guid.NewGuid(),
            courseId,
            "hard",
            1,
            false), CancellationToken.None);

        practiceResult.IsSuccess.Should().BeTrue();
        practiceResult.Value.Questions.Should().ContainSingle();
        practiceResult.Value.Questions[0].ImageUrl.Should().Be("/uploads/questions/202610/oauth2-sequence.png");
        practiceResult.Value.Questions[0].ImageDescription.Should().Contain("PKCE");
    }

    [Fact]
    public async Task ReviewQuestionDecision_ShouldSyncImageToPracticePool_WhenApprovedForBothScopes()
    {
        using var context = CreateDbContext();
        var courseId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        context.Courses.Add(new Course { Id = courseId, Code = "PRN231", Name = "Backend .NET", Credits = 3, IsActive = true });
        context.Users.Add(new User { Id = headId, Email = "head@fpt.edu.vn", FullName = "Trưởng BM", Role = "department_head", IsActive = true });
        context.Rubrics.Add(new Rubric
        {
            Id = rubricId,
            CourseId = courseId,
            Name = "Rubric Chuẩn 10đ",
            TotalMaxScore = 10.00m,
            IsActive = true,
            Criteria = new List<RubricCriterion>
            {
                new() { Id = Guid.NewGuid(), RubricId = rubricId, CriterionName = "Ý 1", MaxScore = 5.00m, Weight = 0.50m, OrderIndex = 1 },
                new() { Id = Guid.NewGuid(), RubricId = rubricId, CriterionName = "Ý 2", MaxScore = 5.00m, Weight = 0.50m, OrderIndex = 2 }
            }
        });
        context.ExamQuestions.Add(new ExamQuestion
        {
            Id = questionId,
            CourseId = courseId,
            RubricId = rubricId,
            Title = "Câu hỏi sơ đồ ERD",
            Content = "Quan sát sơ đồ ERD đính kèm và phân tích mối quan hệ 1-N.",
            SampleAnswer = "Đáp án mẫu chi tiết dài trên 50 ký tự giải thích khóa ngoại và chuẩn hóa 3NF.",
            KeyPoints = "[\"3NF\",\"Foreign Key\"]",
            Difficulty = "medium",
            BloomLevel = "Analyze",
            Source = "manual",
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            ImageUrl = "/uploads/questions/202610/erd-3nf.png",
            ImageDescription = "Sơ đồ ERD 30 bảng chuẩn 3NF",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var reviewHandler = new ReviewQuestionDecisionCommandHandler(context);
        var reviewResult = await reviewHandler.Handle(new ReviewQuestionDecisionCommand(
            questionId,
            headId,
            ApprovalStatus.Approved,
            "Sơ đồ ERD rất rõ ràng, duyệt cho cả 2 kho.",
            "both"), CancellationToken.None);

        reviewResult.IsSuccess.Should().BeTrue();
        var syncedPracticeQuestion = await context.PracticeQuestions.FirstOrDefaultAsync(p => p.CourseId == courseId && p.Title == "Câu hỏi sơ đồ ERD");
        syncedPracticeQuestion.Should().NotBeNull();
        syncedPracticeQuestion!.ImageUrl.Should().Be("/uploads/questions/202610/erd-3nf.png");
        syncedPracticeQuestion.ImageDescription.Should().Be("Sơ đồ ERD 30 bảng chuẩn 3NF");
    }

    [Fact]
    public async Task OfficialExamKioskAndAudit_ShouldAutoAssignExamSet_AndDeliverQuestionImageMetadata()
    {
        using var context = CreateDbContext();
        var courseId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var structureId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var examQuestionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Courses.Add(new Course { Id = courseId, Code = "SWD392", Name = "Software Architecture", Credits = 3, IsActive = true });
        context.Users.AddRange(
            new User { Id = headId, Email = "head@fpt.edu.vn", FullName = "Trưởng BM", Role = UserRole.DepartmentHead, IsActive = true },
            new User { Id = studentId, Email = "sv@fpt.edu.vn", StudentCode = "SE170999", FullName = "Nguyễn Văn A", Role = UserRole.Student, IsActive = true });
        context.Rubrics.Add(new Rubric { Id = rubricId, CourseId = courseId, Name = "Rubric 10", TotalMaxScore = 10m, IsActive = true });
        context.ExamStructures.Add(new ExamStructure { Id = structureId, CourseId = courseId, CreatedBy = headId, Name = "Cấu trúc Kiosk", TotalQuestions = 1, DurationMinutes = 15, IsActive = true });
        context.ExamQuestions.Add(new ExamQuestion
        {
            Id = examQuestionId,
            CourseId = courseId,
            RubricId = rubricId,
            Title = "Sơ đồ kiến trúc Microservices",
            Content = "Quan sát sơ đồ API Gateway đính kèm và phân tích luồng định tuyến.",
            SampleAnswer = "API Gateway tiếp nhận request từ client, xác thực JWT và định tuyến tới các microservice nội bộ.",
            Difficulty = "hard",
            BloomLevel = "Analyze",
            Source = "manual",
            ApprovalStatus = ApprovalStatus.Approved,
            ImageUrl = "/uploads/questions/202610/api-gateway.png",
            ImageDescription = "Sơ đồ kiến trúc API Gateway & Microservices",
            IsActive = true
        });
        context.OfficialExamSessions.Add(new OfficialExamSession
        {
            Id = sessionId,
            CourseId = courseId,
            ExamStructureId = structureId,
            Title = "Kỳ thi Cuối kỳ FA26",
            ExamDate = DateOnly.FromDateTime(now),
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            ExamInputMode = "VoiceWithTranscriptEdit",
            TranscriptBufferSeconds = 90,
            Status = "in_progress"
        });
        context.RealExamSessionShifts.Add(new RealExamSessionShift
        {
            Id = shiftId,
            SessionId = sessionId,
            ShiftName = "Ca 1 Phòng Lab 301",
            RoomLab = "Lab 301",
            StartTime = now.AddMinutes(-15),
            EndTime = now.AddMinutes(45),
            Status = "in_progress"
        });
        await context.SaveChangesAsync();

        // 1. AssignExamTicket tự động khởi tạo & gán ExamSetId từ câu hỏi đã APPROVED
        var assignHandler = new AssignExamTicketCommandHandler(context, new AssignExamTicketCommandValidator());
        var assignResult = await assignHandler.Handle(new AssignExamTicketCommand(
            headId,
            shiftId,
            studentId,
            1,
            "192.168.1.105"), CancellationToken.None);

        assignResult.IsSuccess.Should().BeTrue();
        assignResult.Value.ExamSetId.Should().NotBeNull();

        // 2. GetMyWorkstationTicket trả về danh sách câu hỏi kèm ImageUrl & ImageDescription cho Kiosk
        var workstationHandler = new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator());
        var workstationResult = await workstationHandler.Handle(new GetMyWorkstationTicketQuery(
            studentId,
            "192.168.1.105",
            now), CancellationToken.None);

        workstationResult.IsSuccess.Should().BeTrue();
        workstationResult.Value.ExamSetId.Should().Be(assignResult.Value.ExamSetId);
        workstationResult.Value.TranscriptBufferSeconds.Should().Be(90);
        workstationResult.Value.Questions.Should().NotBeNull().And.ContainSingle();
        workstationResult.Value.Questions![0].QuestionId.Should().Be(examQuestionId);
        workstationResult.Value.Questions[0].ImageUrl.Should().Be("/uploads/questions/202610/api-gateway.png");
        workstationResult.Value.Questions[0].ImageDescription.Should().Be("Sơ đồ kiến trúc API Gateway & Microservices");

        // 3. Giả lập bài nộp và công bố điểm -> kiểm tra GetAuditEvidence & GetOfficialExamGrade
        var ticket = await context.StudentExamTickets.FirstAsync(t => t.Id == assignResult.Value.Id);
        ticket.Status = ExamTicketStatus.Published;
        ticket.IsLocked = true;
        context.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = examQuestionId,
            TranscriptWhisper = "API Gateway định tuyến request và kiểm tra JWT.",
            AiScore = 8.5m,
            FinalScore = 8.5m,
            GradingStatus = "completed",
            SubmittedAt = now
        });
        await context.SaveChangesAsync();

        var auditHandler = new GetAuditEvidenceQueryHandler(context);
        var auditResult = await auditHandler.Handle(new GetAuditEvidenceQuery(shiftId), CancellationToken.None);
        auditResult.IsSuccess.Should().BeTrue();
        auditResult.Value.Should().ContainSingle();
        auditResult.Value[0].QuestionId.Should().Be(examQuestionId);
        auditResult.Value[0].QuestionTitle.Should().Be("Sơ đồ kiến trúc Microservices");
        auditResult.Value[0].QuestionImageUrl.Should().Be("/uploads/questions/202610/api-gateway.png");

        var gradeHandler = new GetOfficialExamGradeQueryHandler(context);
        var gradeResult = await gradeHandler.Handle(new GetOfficialExamGradeQuery(ticket.Id, studentId), CancellationToken.None);
        gradeResult.IsSuccess.Should().BeTrue();
        gradeResult.Value.DetailedScorecard.Should().ContainSingle();
        gradeResult.Value.DetailedScorecard[0].QuestionTitle.Should().Be("Sơ đồ kiến trúc Microservices");
        gradeResult.Value.DetailedScorecard[0].QuestionImageUrl.Should().Be("/uploads/questions/202610/api-gateway.png");
        gradeResult.Value.DetailedScorecard[0].QuestionImageDescription.Should().Be("Sơ đồ kiến trúc API Gateway & Microservices");
    }

    [Fact]
    public async Task PracticeController_StartSession_MapsContractAliases_PracticeMode_SelectedDifficulties_TotalQuestions()
    {
        OralExamination.Application.Features.Practice.Commands.StartPracticeSession.StartPracticeSessionCommand? capturedCommand = null;
        var senderMock = new Moq.Mock<MediatR.ISender>();
        senderMock
            .Setup(s => s.Send(
                Moq.It.IsAny<OralExamination.Application.Features.Practice.Commands.StartPracticeSession.StartPracticeSessionCommand>(),
                Moq.It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) =>
            {
                capturedCommand = (OralExamination.Application.Features.Practice.Commands.StartPracticeSession.StartPracticeSessionCommand)cmd;
            })
            .Returns(Task.FromResult(OralExamination.Application.Common.Models.Result<OralExamination.Application.Features.Practice.DTOs.StartPracticeSessionResponse>.Success(
                new OralExamination.Application.Features.Practice.DTOs.StartPracticeSessionResponse(
                    Guid.NewGuid(),
                    60,
                    new List<OralExamination.Application.Features.Practice.DTOs.PracticeQuestionDto>()))));

        var controller = new global::API.Controllers.v1.PracticeController(senderMock.Object);
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var request = new OralExamination.Application.Features.Practice.DTOs.StartPracticeSessionRequest(
            CourseId: courseId,
            StudentId: studentId,
            PracticeMode: "full_session",
            SelectedDifficulties: new List<string> { "easy", "medium" },
            TotalQuestions: 6);

        var actionResult = await controller.StartSession(request);

        actionResult.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        capturedCommand.Should().NotBeNull();
        capturedCommand!.CourseId.Should().Be(courseId);
        capturedCommand.StudentId.Should().Be(studentId);
        capturedCommand.IsFullSession.Should().BeTrue();
        capturedCommand.QuestionCount.Should().Be(6);
        capturedCommand.Difficulty.Should().Be("easy");
        capturedCommand.Difficulties.Should().BeEquivalentTo(new[] { "easy", "medium" });
    }

    private sealed class CaptureHttpMessageHandler(Func<string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;
            return responder(body);
        }
    }
}
