using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.API.Middlewares;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Notifications.Queries.GetNotifications;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.HandOverExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.ReturnExamSetForRevision;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamSet;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSetById;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
using OralExamination.Application.Features.QuestionBank;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromClo;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank;

public sealed class M2Mf03RemediationRegressionTests
{
    private static OralExamDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static List<RubricCriterionDraftDto> ValidCriteria() =>
    [
        new("Tiêu chí 1", 5.00m, 0.50m, BloomLevel.Understand, "Mô tả 1", 1),
        new("Tiêu chí 2", 5.00m, 0.50m, BloomLevel.Analyze, "Mô tả 2", 2)
    ];

    private static Rubric BuildRubricEntity(Guid courseId)
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = "Rubric M2",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "C1",
            MaxScore = 5.00m,
            Weight = 0.50m,
            OrderIndex = 1
        });
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "C2",
            MaxScore = 5.00m,
            Weight = 0.50m,
            OrderIndex = 2
        });
        return rubric;
    }

    private static async Task<(User Head, User Lecturer, User OtherLecturer, User Officer, User Admin, Course Course, ExamStructure Structure, Rubric Rubric)> SeedOfficialExamCourseAsync(
        OralExamDbContext db,
        int totalQuestions = 2,
        string bloomDistribution = "{}")
    {
        var head = new User { Id = Guid.NewGuid(), Email = "head-m2@fpt.edu.vn", FullName = "Head M2", Role = UserRole.DepartmentHead, IsActive = true };
        var lec1 = new User { Id = Guid.NewGuid(), Email = "lec1-m2@fpt.edu.vn", FullName = "Lecturer 1", Role = UserRole.Lecturer, IsActive = true };
        var lec2 = new User { Id = Guid.NewGuid(), Email = "lec2-m2@fpt.edu.vn", FullName = "Lecturer 2", Role = UserRole.Lecturer, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "eo-m2@fpt.edu.vn", FullName = "Officer M2", Role = UserRole.ExaminationOfficer, IsActive = true };
        var admin = new User { Id = Guid.NewGuid(), Email = "admin-m2@fpt.edu.vn", FullName = "Admin M2", Role = UserRole.Admin, IsActive = true };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            DepartmentHeadUserId = head.Id,
            IsActive = true
        };

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = head.Id,
            Name = "Struct M2",
            TotalQuestions = totalQuestions,
            BloomDistribution = bloomDistribution,
            RequiredSetCount = 2,
            IsActive = true
        };

        var rubric = BuildRubricEntity(course.Id);

        db.Users.AddRange(head, lec1, lec2, officer, admin);
        db.Courses.Add(course);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec1.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec2.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        await db.SaveChangesAsync();

        return (head, lec1, lec2, officer, admin, course, structure, rubric);
    }

    private static ControllerContext CreateAuthContext(Guid userId, string role) =>
        new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, role)
                ], "TestAuth"))
            }
        };

    #region Item 12 — Shared QuestionImageUrlValidator SSRF & Path-Traversal Guard

    [Theory(DisplayName = "M2-12A: QuestionImageUrlValidator chặn triệt để SSRF, path traversal, scheme nguy hiểm và chấp nhận URL an toàn")]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<script>alert(1)</script>", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("http://cdn.example.com/q.png", false)]
    [InlineData("http://169.254.169.254/latest/meta-data/", false)]
    [InlineData("https://169.254.169.254/latest/meta-data/", false)]
    [InlineData("https://127.0.0.1/a.png", false)]
    [InlineData("https://localhost/a.png", false)]
    [InlineData("https://api.localhost/a.png", false)]
    [InlineData("https://metadata.internal/a.png", false)]
    [InlineData("https://10.0.0.1/a.png", false)]
    [InlineData("https://172.16.0.1/a.png", false)]
    [InlineData("https://192.168.1.10/a.png", false)]
    [InlineData("https://[::1]/a.png", false)]
    [InlineData("https://[fc00::1]/a.png", false)]
    [InlineData("https://[fe80::1]/a.png", false)]
    [InlineData("https://user:pass@cdn.example.com/a.png", false)]
    [InlineData("/uploads/questions/../secret.png", false)]
    [InlineData("/uploads/questions/a.svg", false)]
    [InlineData("/uploads/questions/202611/a.png?v=1", false)]
    [InlineData("/uploads/questions/202611/abc_01.png", true)]
    [InlineData("/uploads/questions/ent/ENT001_p3_Im2.webp", true)]
    [InlineData("https://cdn.example.com/images/diagram.png", true)]
    public void QuestionImageUrlValidator_Enforces_Ssrf_And_PathTraversal_Rules(string? url, bool expected)
    {
        QuestionImageUrlValidator.IsSafeImageUrl(url).Should().Be(expected);
    }

    [Theory(DisplayName = "M2-12B: CreateDraftQuestion (Handler + Validator), Inline ExamSet và BatchApprove chặn ImageUrl không an toàn")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("/uploads/questions/../secret.png")]
    [InlineData("/uploads/questions/payload.svg")]
    public async Task CreateDraft_InlineExamSet_And_BatchApprove_Reject_Unsafe_ImageUrl(string unsafeUrl)
    {
        await using var db = CreateDbContext();
        var (head, lec1, _, _, _, course, structure, rubric) = await SeedOfficialExamCourseAsync(db, totalQuestions: 1);

        var draftCmd = new CreateDraftQuestionCommand(
            CourseId: course.Id,
            LecturerId: lec1.Id,
            Title: "SSRF Test Question",
            Content: "Nội dung kiểm thử bảo mật đường dẫn hình ảnh câu hỏi.",
            SampleAnswer: new string('A', 60),
            KeyPoints: ["SSRF"],
            Difficulty: QuestionDifficulty.Medium,
            BloomLevel: BloomLevel.Understand,
            Source: "manual",
            UsageScope: "exam",
            HasFollowUp: false,
            FollowUpPrompt: null,
            Rubric: new RubricDraftDto("Rubric SSRF", null, 10.00m, ValidCriteria()),
            ImageUrl: unsafeUrl);

        var validatorResult = new CreateDraftQuestionCommandValidator().Validate(draftCmd);
        validatorResult.IsValid.Should().BeFalse();
        validatorResult.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.ImageUrl));

        var draftHandlerResult = await new CreateDraftQuestionCommandHandler(db).Handle(draftCmd, CancellationToken.None);
        draftHandlerResult.IsSuccess.Should().BeFalse();
        draftHandlerResult.Error.Should().StartWith("VALIDATION:");

        var inlineDto = new CreateInlineExamQuestionDto
        {
            Title = "Inline Unsafe Image",
            Content = "Nội dung câu hỏi tạo tay kiểm thử SSRF.",
            SampleAnswer = new string('B', 60),
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            ImageUrl = unsafeUrl,
            Rubric = new RubricDraftDto("Rubric Inline", null, 10.00m, ValidCriteria())
        };

        var createSetResult = await new CreateExamSetCommandHandler(db).Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SET-SSRF", [], lec1.Id, UserRole.Lecturer, false, [inlineDto]), CancellationToken.None);
        createSetResult.IsSuccess.Should().BeFalse();
        createSetResult.Error.Should().StartWith("UNPROCESSABLE:");

        var qValid = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Valid Q",
            Content = "Valid Content",
            SampleAnswer = new string('C', 60),
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        db.ExamQuestions.Add(qValid);
        await db.SaveChangesAsync();

        var createdDraftSet = await new CreateExamSetCommandHandler(db).Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SET-VALID", [qValid.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        createdDraftSet.IsSuccess.Should().BeTrue(because: createdDraftSet.Error);

        var updateSetResult = await new UpdateExamSetCommandHandler(db).Handle(new UpdateExamSetCommand(
            createdDraftSet.Value.Id, null, [], lec1.Id, UserRole.Lecturer, [inlineDto]), CancellationToken.None);
        updateSetResult.IsSuccess.Should().BeFalse();
        updateSetResult.Error.Should().StartWith("UNPROCESSABLE:");

        var batchApproveResult = await new BatchApproveQuestionsCommandHandler(db).Handle(new BatchApproveQuestionsCommand(
            course.Id,
            head.Id,
            [
                new ApprovedQuestionItemDto(
                    Title: "Batch Unsafe Image",
                    Content: "Nội dung kiểm thử batch approve SSRF.",
                    Difficulty: QuestionDifficulty.Medium,
                    BloomLevel: BloomLevel.Understand,
                    UsageScope: "exam",
                    Source: "manual",
                    ModelAnswer: new string('D', 60),
                    KeyPoints: ["Security"],
                    HasFollowUp: false,
                    FollowUpPrompt: null,
                    Rubric: new RubricDraftDto("Batch Rubric", null, 10.00m, ValidCriteria()),
                    ImageUrl: unsafeUrl)
            ]), CancellationToken.None);
        batchApproveResult.IsSuccess.Should().BeFalse();
        batchApproveResult.Error.Should().StartWith("INVARIANT:");
    }

    #endregion

    #region Item 13 — GenerateFromClo Upstream HttpRequestException Pipeline

    [Fact(DisplayName = "M2-13: GenerateFromClo để HttpRequestException(503) thoát tới GlobalExceptionMiddleware -> 503 SERVICE_UNAVAILABLE")]
    public async Task GenerateFromClo_WhenAiThrowsHttpRequestException503_MapsTo503ThroughGlobalMiddleware()
    {
        await using var db = CreateDbContext();
        var (head, lec1, _, _, _, course, _, _) = await SeedOfficialExamCourseAsync(db);
        db.CourseClos.Add(new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloCode = "CLO1",
            Description = "Hiểu kiến trúc phần mềm",
            BloomLevel = BloomLevel.Understand,
            OrderIndex = 1,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var aiMock = new Mock<IAiQuestionGenerationService>();
        aiMock.Setup(s => s.GenerateQuestionsAsync(It.IsAny<AiQuestionGenerationParams>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Gemini overloaded", null, HttpStatusCode.ServiceUnavailable));

        var guidanceMock = new Mock<IQuestionGenerationGuidanceProvider>();
        guidanceMock.Setup(g => g.Get(It.IsAny<string>(), It.IsAny<IEnumerable<string>>())).Returns((AiQuestionGenerationGuidance?)null);

        var handler = new GenerateQuestionsFromCloCommandHandler(db, aiMock.Object, guidanceMock.Object);
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GenerateQuestionsFromCloCommand>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<Result<GenerateQuestionsFromCloResponseDto>>, CancellationToken>((req, ct) =>
                handler.Handle((GenerateQuestionsFromCloCommand)req, ct));

        var controller = new QuestionsController(senderMock.Object)
        {
            ControllerContext = CreateAuthContext(lec1.Id, UserRole.Lecturer)
        };

        var httpContext = controller.ControllerContext.HttpContext;
        httpContext.Request.Path = "/api/v1/questions/generate-from-clo";
        httpContext.Response.Body = new System.IO.MemoryStream();

        var middleware = new GlobalExceptionMiddleware(
            async _ => await controller.GenerateFromClo(new GenerateQuestionsFromCloRequestDto(
                course.Id,
                ["CLO1"],
                ["Clean Architecture"],
                2,
                "exam",
                [BloomLevel.Understand],
                new DifficultyDistributionDto(1, 1, 0))),
            new Mock<Microsoft.Extensions.Logging.ILogger<GlobalExceptionMiddleware>>().Object);

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    #endregion

    #region Item 14 — ReviewQuestionDecision Nullable ReviewNotes

    [Fact(DisplayName = "M2-14: ReviewQuestionDecisionHandler cho phép APPROVED với ReviewNotes null/khoảng trắng và trim ReviewNotes")]
    public async Task ReviewQuestionDecisionHandler_Allows_Null_ReviewNotes_On_Approved_And_Trims()
    {
        await using var db = CreateDbContext();
        var (head, lec1, _, _, _, course, _, rubric) = await SeedOfficialExamCourseAsync(db);

        var q1 = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q Approve Null Note",
            Content = "Content",
            SampleAnswer = new string('E', 60),
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            Source = QuestionSource.Manual,
            SubmittedBy = lec1.Id,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            IsActive = false
        };
        var q2 = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q Revise Trimmed Note",
            Content = "Content 2",
            SampleAnswer = new string('F', 60),
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            Source = QuestionSource.Manual,
            SubmittedBy = lec1.Id,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            IsActive = false
        };
        db.ExamQuestions.AddRange(q1, q2);
        await db.SaveChangesAsync();

        var handler = new ReviewQuestionDecisionCommandHandler(db);

        var approvedRes = await handler.Handle(new ReviewQuestionDecisionCommand(
            q1.Id, head.Id, ApprovalStatus.Approved, "   ", "official_exam"), CancellationToken.None);
        approvedRes.IsSuccess.Should().BeTrue(because: approvedRes.Error);
        approvedRes.Value.ReviewNotes.Should().BeNull();
        (await db.ExamQuestions.FindAsync(q1.Id))!.ReviewNotes.Should().BeNull();

        var revisionRes = await handler.Handle(new ReviewQuestionDecisionCommand(
            q2.Id, head.Id, ApprovalStatus.NeedsRevision, "  Cần bổ sung dẫn chứng cụ thể.  ", "official_exam"), CancellationToken.None);
        revisionRes.IsSuccess.Should().BeTrue(because: revisionRes.Error);
        revisionRes.Value.ReviewNotes.Should().Be("Cần bổ sung dẫn chứng cụ thể.");
    }

    #endregion

    #region Item 15 — ExamSet Validation Consistency (TotalQuestions & BloomDistribution)

    [Fact(DisplayName = "M2-15: CreateExamSet giữ draft linh hoạt, UpdateExamSet và SubmitExamSetForReview khóa chặt TotalQuestions & BloomDistribution (422)")]
    public async Task ExamSet_ValidationConsistency_Across_Create_Update_And_SubmitReview()
    {
        await using var db = CreateDbContext();
        var (_, lec1, lec2, _, _, course, structure, rubric) = await SeedOfficialExamCourseAsync(
            db,
            totalQuestions: 2,
            bloomDistribution: "{\"Understand\":1,\"Analyze\":1}");

        var qUnderstand1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "U1", Content = "CU1", SampleAnswer = new string('A', 60), BloomLevel = BloomLevel.Understand, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var qUnderstand2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "U2", Content = "CU2", SampleAnswer = new string('A', 60), BloomLevel = BloomLevel.Understand, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var qAnalyze1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "A1", Content = "CA1", SampleAnswer = new string('A', 60), BloomLevel = BloomLevel.Analyze, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        db.ExamQuestions.AddRange(qUnderstand1, qUnderstand2, qAnalyze1);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(db);
        var updateHandler = new UpdateExamSetCommandHandler(db);
        var submitHandler = new SubmitExamSetForReviewCommandHandler(db);

        // 1. CreateExamSet: cho phép lưu nháp chưa đủ TotalQuestions (1 < 2)
        var partialDraft = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SET-PARTIAL", [qUnderstand1.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        partialDraft.IsSuccess.Should().BeTrue(because: partialDraft.Error);

        // 2. CreateExamSet: chặn vượt quá TotalQuestions (3 > 2) -> UNPROCESSABLE (422)
        var overLimit = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SET-OVER", [qUnderstand1.Id, qUnderstand2.Id, qAnalyze1.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        overLimit.IsSuccess.Should().BeFalse();
        overLimit.Error.Should().StartWith("UNPROCESSABLE:");

        // 3. CreateExamSet: khi đạt đúng TotalQuestions (2) nhưng sai BloomDistribution (2 Understand, 0 Analyze) -> UNPROCESSABLE (422)
        var badBloomCreate = await createHandler.Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SET-BAD-BLOOM", [qUnderstand1.Id, qUnderstand2.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        badBloomCreate.IsSuccess.Should().BeFalse();
        badBloomCreate.Error.Should().StartWith("UNPROCESSABLE:");

        // 4. SubmitExamSetForReview: bộ đề nháp thiếu câu (1 != 2) -> UNPROCESSABLE (422)
        var submitPartial = await submitHandler.Handle(new SubmitExamSetForReviewCommand(
            partialDraft.Value.Id, lec1.Id, UserRole.Lecturer), CancellationToken.None);
        submitPartial.IsSuccess.Should().BeFalse();
        submitPartial.Error.Should().StartWith("UNPROCESSABLE:");

        // 5. UpdateExamSet: cập nhật đủ 2 câu nhưng sai Bloom (2 Understand) -> UNPROCESSABLE (422)
        var updateWrongBloom = await updateHandler.Handle(new UpdateExamSetCommand(
            partialDraft.Value.Id, null, [qUnderstand1.Id, qUnderstand2.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        updateWrongBloom.IsSuccess.Should().BeFalse();
        updateWrongBloom.Error.Should().StartWith("UNPROCESSABLE:");

        // 6. Legacy/corrupt draft đủ 2 câu nhưng sai Bloom trong DB -> SubmitExamSetForReview phải chặn UNPROCESSABLE (422)
        var legacyMismatchSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = lec2.Id,
            SetCode = "SET-LEGACY-BLOOM",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };
        legacyMismatchSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = legacyMismatchSet.Id, ExamQuestionId = qUnderstand1.Id, OrderIndex = 1 });
        legacyMismatchSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = legacyMismatchSet.Id, ExamQuestionId = qUnderstand2.Id, OrderIndex = 2 });
        db.ExamSets.Add(legacyMismatchSet);
        await db.SaveChangesAsync();

        var submitWrongBloom = await submitHandler.Handle(new SubmitExamSetForReviewCommand(
            legacyMismatchSet.Id, lec2.Id, UserRole.Lecturer), CancellationToken.None);
        submitWrongBloom.IsSuccess.Should().BeFalse();
        submitWrongBloom.Error.Should().StartWith("UNPROCESSABLE:");

        // 7. UpdateExamSet đúng TotalQuestions (2) & đúng Bloom (1 Understand + 1 Analyze) -> SubmitExamSetForReview thành công
        var updateValid = await updateHandler.Handle(new UpdateExamSetCommand(
            partialDraft.Value.Id, null, [qUnderstand1.Id, qAnalyze1.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        updateValid.IsSuccess.Should().BeTrue(because: updateValid.Error);

        var submitValid = await submitHandler.Handle(new SubmitExamSetForReviewCommand(
            partialDraft.Value.Id, lec1.Id, UserRole.Lecturer), CancellationToken.None);
        submitValid.IsSuccess.Should().BeTrue(because: submitValid.Error);

        // 8. Controller mapping kiểm chứng HTTP 422 UnprocessableEntity cho SubmitExamSetReview
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SubmitExamSetForReviewCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("UNPROCESSABLE: Tổng số câu hỏi không khớp cấu trúc đề."));
        var controller = new OfficialExamsController(senderMock.Object)
        {
            ControllerContext = CreateAuthContext(lec1.Id, UserRole.Lecturer)
        };
        var actionResult = (await controller.SubmitExamSetReview(partialDraft.Value.Id, CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    #endregion

    #region Item 16 — GetExamSets Filters & GET /exam-sets/{id}

    [Fact(DisplayName = "M2-16: GetExamSets lọc IsActive, hỗ trợ structureId + courseId tùy chọn, và GetExamSetById bảo vệ RBAC/BOLA/404")]
    public async Task GetExamSets_And_GetExamSetById_FiltersActive_SupportsStructureId_And_EnforcesRbac()
    {
        await using var db = CreateDbContext();
        var (head, lec1, lec2, officer, _, course, structure1, rubric) = await SeedOfficialExamCourseAsync(db, totalQuestions: 1);

        var structure2 = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = head.Id,
            Name = "Struct 2",
            TotalQuestions = 1,
            IsActive = true
        };
        var otherCourse = new Course { Id = Guid.NewGuid(), Code = "PRN222", Name = "PRN222", DepartmentHeadUserId = Guid.NewGuid(), IsActive = true };
        var otherStructure = new ExamStructure { Id = Guid.NewGuid(), CourseId = otherCourse.Id, Name = "Foreign Struct", TotalQuestions = 1, IsActive = true };
        db.ExamStructures.AddRange(structure2, otherStructure);
        db.Courses.Add(otherCourse);

        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "C1", SampleAnswer = new string('A', 60), BloomLevel = BloomLevel.Understand, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        db.ExamQuestions.Add(q1);

        var activeSetS1 = new ExamSet { Id = Guid.NewGuid(), StructureId = structure1.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "S1-ACTIVE", Status = ExamSetStatus.SubmittedForReview, IsActive = true };
        activeSetS1.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = activeSetS1.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });

        var inactiveSetS1 = new ExamSet { Id = Guid.NewGuid(), StructureId = structure1.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "S1-DELETED", Status = ExamSetStatus.Draft, IsActive = false };
        inactiveSetS1.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = inactiveSetS1.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });

        var handedOverSetS2 = new ExamSet { Id = Guid.NewGuid(), StructureId = structure2.Id, CourseId = course.Id, CreatedBy = lec2.Id, SetCode = "S2-HANDED", Status = ExamSetStatus.HandedOver, IsActive = true };
        handedOverSetS2.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = handedOverSetS2.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });

        var handedOverInactiveS2 = new ExamSet { Id = Guid.NewGuid(), StructureId = structure2.Id, CourseId = course.Id, CreatedBy = lec2.Id, SetCode = "S2-HANDED-DEL", Status = ExamSetStatus.HandedOver, IsActive = false };
        handedOverInactiveS2.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = handedOverInactiveS2.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });

        db.ExamSets.AddRange(activeSetS1, inactiveSetS1, handedOverSetS2, handedOverInactiveS2);
        await db.SaveChangesAsync();

        var listHandler = new GetExamSetsQueryHandler(db);
        var detailHandler = new GetExamSetByIdQueryHandler(db);

        // 1. Thiếu cả CourseId và StructureId -> VALIDATION (400)
        var missingFilters = await listHandler.Handle(new GetExamSetsQuery(null, null, null, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        missingFilters.IsSuccess.Should().BeFalse();
        missingFilters.Error.Should().StartWith("VALIDATION:");

        // 2. Mặc định lọc IsActive == true (ẩn S1-DELETED và S2-HANDED-DEL)
        var headDefault = await listHandler.Handle(new GetExamSetsQuery(course.Id, null, null, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        headDefault.IsSuccess.Should().BeTrue(because: headDefault.Error);
        headDefault.Value.Select(s => s.SetCode).Should().BeEquivalentTo(["S1-ACTIVE", "S2-HANDED"]);

        // 3. IncludeInactive = true hiển thị bộ đề đã xóa mềm cho DeptHead/Lecturer, NHƯNG bị bỏ qua cho ExaminationOfficer
        var headWithInactive = await listHandler.Handle(new GetExamSetsQuery(course.Id, null, null, head.Id, UserRole.DepartmentHead, null, IncludeInactive: true), CancellationToken.None);
        headWithInactive.Value.Select(s => s.SetCode).Should().BeEquivalentTo(["S1-ACTIVE", "S1-DELETED", "S2-HANDED", "S2-HANDED-DEL"]);

        var eoWithInactive = await listHandler.Handle(new GetExamSetsQuery(course.Id, null, null, officer.Id, UserRole.ExaminationOfficer, null, IncludeInactive: true), CancellationToken.None);
        eoWithInactive.IsSuccess.Should().BeTrue();
        eoWithInactive.Value.Select(s => s.SetCode).Should().BeEquivalentTo(["S2-HANDED"]);

        // 4. Lọc chỉ bằng StructureId (không truyền CourseId) + kiểm tra lệch StructureId/CourseId -> 404
        var byStructureOnly = await listHandler.Handle(new GetExamSetsQuery(null, null, null, head.Id, UserRole.DepartmentHead, structure1.Id), CancellationToken.None);
        byStructureOnly.IsSuccess.Should().BeTrue(because: byStructureOnly.Error);
        byStructureOnly.Value.Should().ContainSingle().Which.Id.Should().Be(activeSetS1.Id);

        var foreignStructureOnly = await listHandler.Handle(new GetExamSetsQuery(null, null, null, head.Id, UserRole.DepartmentHead, otherStructure.Id), CancellationToken.None);
        foreignStructureOnly.IsSuccess.Should().BeFalse();
        foreignStructureOnly.Error.Should().Be(OfficialExamErrors.Forbidden);

        var mismatchedStructure = await listHandler.Handle(new GetExamSetsQuery(course.Id, null, null, head.Id, UserRole.DepartmentHead, otherStructure.Id), CancellationToken.None);
        mismatchedStructure.IsSuccess.Should().BeFalse();
        mismatchedStructure.Error.Should().Be(OfficialExamErrors.StructureNotFound);

        // 5. GetExamSetById: chủ sở hữu/TBM/EO đúng điều kiện xem được; GV khác/EO trước HANDED_OVER/bộ đề xóa mềm bị chặn đúng chuẩn
        var lec1Detail = await detailHandler.Handle(new GetExamSetByIdQuery(activeSetS1.Id, lec1.Id, UserRole.Lecturer), CancellationToken.None);
        lec1Detail.IsSuccess.Should().BeTrue(because: lec1Detail.Error);
        lec1Detail.Value.Id.Should().Be(activeSetS1.Id);

        var lec2Forbidden = await detailHandler.Handle(new GetExamSetByIdQuery(activeSetS1.Id, lec2.Id, UserRole.Lecturer), CancellationToken.None);
        lec2Forbidden.IsSuccess.Should().BeFalse();
        lec2Forbidden.Error.Should().Be(OfficialExamErrors.Forbidden);

        var eoBeforeHandOver = await detailHandler.Handle(new GetExamSetByIdQuery(activeSetS1.Id, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None);
        eoBeforeHandOver.IsSuccess.Should().BeFalse();
        eoBeforeHandOver.Error.Should().Be(OfficialExamErrors.Forbidden);

        var eoHandedOver = await detailHandler.Handle(new GetExamSetByIdQuery(handedOverSetS2.Id, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None);
        eoHandedOver.IsSuccess.Should().BeTrue(because: eoHandedOver.Error);

        var inactiveById = await detailHandler.Handle(new GetExamSetByIdQuery(inactiveSetS1.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        inactiveById.IsSuccess.Should().BeFalse();
        inactiveById.Error.Should().Be(OfficialExamErrors.ExamSetNotFound);

        var missingById = await detailHandler.Handle(new GetExamSetByIdQuery(Guid.NewGuid(), head.Id, UserRole.DepartmentHead), CancellationToken.None);
        missingById.IsSuccess.Should().BeFalse();
        missingById.Error.Should().Be(OfficialExamErrors.ExamSetNotFound);

        // 6. Controller routing & authorization attributes cho GET /exam-sets và GET /exam-sets/{id}
        var getSetByIdAuth = typeof(OfficialExamsController)
            .GetMethod(nameof(OfficialExamsController.GetExamSetById))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();
        getSetByIdAuth.Roles.Should().Be("lecturer,department_head,examination_officer");

        var ctrlSender = new Mock<ISender>();
        ctrlSender.Setup(s => s.Send(It.IsAny<GetExamSetsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ExamSetDto>>.Success([byStructureOnly.Value.Single()]));
        ctrlSender.Setup(s => s.Send(It.Is<GetExamSetByIdQuery>(q => q.ExamSetId == activeSetS1.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamSetDto>.Success(lec1Detail.Value));
        ctrlSender.Setup(s => s.Send(It.Is<GetExamSetByIdQuery>(q => q.ExamSetId != activeSetS1.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNotFound));

        var ctrl = new OfficialExamsController(ctrlSender.Object)
        {
            ControllerContext = CreateAuthContext(head.Id, UserRole.DepartmentHead)
        };
        var listAction = (await ctrl.GetExamSets(courseId: null, structureId: structure1.Id, includeInactive: true, cancellationToken: CancellationToken.None))
            .Should().BeOfType<OkObjectResult>().Subject;
        ((List<ExamSetDto>)listAction.Value!).Should().ContainSingle();
        ctrlSender.Verify(s => s.Send(
            It.Is<GetExamSetsQuery>(q => q.CourseId == null && q.StructureId == structure1.Id && q.IncludeInactive),
            It.IsAny<CancellationToken>()), Times.Once);

        (await ctrl.GetExamSetById(activeSetS1.Id, cancellationToken: CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        var notFoundCtrl = (await ctrl.GetExamSetById(Guid.NewGuid(), cancellationToken: CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject;
        notFoundCtrl.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region Item 17 — ReturnExamSetForRevision Notification & HandOverExamSet 409

    [Fact(DisplayName = "M2-17A: ReturnExamSetForRevision tạo Notification 3NF tương thích GetNotificationsQueryHandler")]
    public async Task ReturnExamSetForRevision_Creates_3NF_Notification_Deserializable_By_GetNotifications()
    {
        await using var db = CreateDbContext();
        var (head, lec1, _, _, _, course, structure, _) = await SeedOfficialExamCourseAsync(db, totalQuestions: 1);

        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = lec1.Id,
            SetCode = "SWD392-REV-01",
            Status = ExamSetStatus.SubmittedForReview,
            IsActive = true
        };
        db.ExamSets.Add(examSet);
        await db.SaveChangesAsync();

        var returnResult = await new ReturnExamSetForRevisionCommandHandler(db).Handle(
            new ReturnExamSetForRevisionCommand(examSet.Id, "Cần thay câu hỏi trùng và bổ sung barem chi tiết.", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);

        returnResult.IsSuccess.Should().BeTrue(because: returnResult.Error);

        var notification = await db.Notifications.AsNoTracking().SingleAsync(n => n.UserId == lec1.Id);
        notification.Type.Should().Be("EXAM_SET_RETURNED_FOR_REVISION");
        notification.RelatedEntityName.Should().Be("exam_sets");
        notification.RelatedEntityId.Should().Be(examSet.Id);

        var fetched = await new GetNotificationsQueryHandler(db).Handle(new GetNotificationsQuery(lec1.Id), CancellationToken.None);
        fetched.IsSuccess.Should().BeTrue(because: fetched.Error);
        var item = fetched.Value.Items.Should().ContainSingle().Subject;
        item.Metadata.Should().NotBeNull();
        item.Metadata!["examSetId"].Should().Be(examSet.Id.ToString());
        item.Metadata["setCode"].Should().Be("SWD392-REV-01");
        item.Metadata["courseId"].Should().Be(course.Id.ToString());
        item.Metadata["reviewNotes"].Should().Be("Cần thay câu hỏi trùng và bổ sung barem chi tiết.");
    }

    [Fact(DisplayName = "M2-17B: HandOverExamSet trả CONFLICT (409) khi bộ đề không ở OFFICIAL hoặc đã xóa mềm")]
    public async Task HandOverExamSet_Returns_Conflict409_When_NotOfficial_Or_Inactive()
    {
        await using var db = CreateDbContext();
        var (head, _, _, _, _, course, structure, _) = await SeedOfficialExamCourseAsync(db, totalQuestions: 1);

        var draftSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            SetCode = "SET-DRAFT-HO",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };
        var deletedOfficialSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            SetCode = "SET-DELETED-HO",
            Status = ExamSetStatus.Official,
            IsActive = false
        };
        db.ExamSets.AddRange(draftSet, deletedOfficialSet);
        await db.SaveChangesAsync();

        var handler = new HandOverExamSetCommandHandler(db);

        var draftResult = await handler.Handle(new HandOverExamSetCommand(draftSet.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        draftResult.IsSuccess.Should().BeFalse();
        draftResult.Error.Should().StartWith("CONFLICT:");
        draftResult.Error.Should().Contain("OFFICIAL");

        var deletedResult = await handler.Handle(new HandOverExamSetCommand(deletedOfficialSet.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        deletedResult.IsSuccess.Should().BeFalse();
        deletedResult.Error.Should().StartWith("CONFLICT:");

        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<HandOverExamSetCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(draftResult);
        var controller = new OfficialExamsController(senderMock.Object)
        {
            ControllerContext = CreateAuthContext(head.Id, UserRole.DepartmentHead)
        };

        var actionResult = (await controller.HandOverExamSet(draftSet.Id, CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    #endregion

    #region Iteration 2 Edge-Case Regressions (Items 12, 13, 14, 16)

    [Theory(DisplayName = "M2-12C: QuestionImageUrlValidator chặn empty/non-empty userinfo '@' trong authority nhưng cho phép '@' trong path/query")]
    [InlineData("https://@example.com/a.png", false)]
    [InlineData("https://:@example.com/a.png", false)]
    [InlineData("https://user@example.com/a.png", false)]
    [InlineData("https://@example.com", false)]
    [InlineData("https://@example.com?q=1", false)]
    [InlineData("https://@example.com#frag", false)]
    [InlineData("https://cdn.example.com/@user/a.png", true)]
    [InlineData("https://cdn.example.com/a.png?u=@user", true)]
    public void QuestionImageUrlValidator_Rejects_AtSign_In_Authority_Allows_In_Path_Or_Query(string url, bool expected)
    {
        QuestionImageUrlValidator.IsSafeImageUrl(url).Should().Be(expected);
    }

    [Fact(DisplayName = "M2-13B: GlobalExceptionMiddleware giải nén AggregateException cho HttpRequestException(503/500), TaskCanceledException và OperationCanceledException")]
    public async Task GlobalExceptionMiddleware_Unwraps_AggregateException_For_Upstream_And_Cancellation_Mappings()
    {
        static async Task<(int StatusCode, ProblemDetails Problem)> InvokeWithAsync(
            Exception thrown,
            CancellationToken requestAborted = default)
        {
            var context = new DefaultHttpContext { RequestAborted = requestAborted };
            context.Request.Path = "/api/v1/questions/generate-from-clo";
            context.Response.Body = new System.IO.MemoryStream();

            var middleware = new GlobalExceptionMiddleware(
                _ => throw thrown,
                new Mock<Microsoft.Extensions.Logging.ILogger<GlobalExceptionMiddleware>>().Object);

            await middleware.InvokeAsync(context);
            context.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
            var problem = await System.Text.Json.JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);
            return (context.Response.StatusCode, problem!);
        }

        var (status503, problem503) = await InvokeWithAsync(
            new AggregateException(new HttpRequestException("Gemini secret-key-999 unavailable", null, HttpStatusCode.ServiceUnavailable)));
        status503.Should().Be(StatusCodes.Status503ServiceUnavailable);
        problem503.Detail.Should().NotContain("secret-key-999");
        problem503.Extensions["errorCode"]?.ToString().Should().Be("SERVICE_UNAVAILABLE");

        var (statusNested503, _) = await InvokeWithAsync(
            new AggregateException(new AggregateException(new HttpRequestException("Nested 503", null, HttpStatusCode.ServiceUnavailable))));
        statusNested503.Should().Be(StatusCodes.Status503ServiceUnavailable);

        var (status502, problem502) = await InvokeWithAsync(
            new AggregateException(new HttpRequestException("Upstream 500", null, HttpStatusCode.InternalServerError)));
        status502.Should().Be(StatusCodes.Status502BadGateway);
        problem502.Extensions["errorCode"]?.ToString().Should().Be("BAD_GATEWAY");

        var (status504, problem504) = await InvokeWithAsync(
            new AggregateException(new TaskCanceledException("Upstream timeout")));
        status504.Should().Be(StatusCodes.Status504GatewayTimeout);
        problem504.Extensions["errorCode"]?.ToString().Should().Be("GATEWAY_TIMEOUT");

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (status499, _) = await InvokeWithAsync(
            new AggregateException(new TaskCanceledException("Client disconnected")),
            cts.Token);
        status499.Should().Be(499);
    }

    [Fact(DisplayName = "M2-14B: ReviewQuestionDecisionHandler xử lý đồng nhất với Validator khi Decision có khoảng trắng bao quanh")]
    public async Task ReviewQuestionDecisionHandler_Trims_Decision_Consistently_With_Validator()
    {
        await using var db = CreateDbContext();
        var (head, lec1, _, _, _, course, _, rubric) = await SeedOfficialExamCourseAsync(db);

        var examQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q Exam Padded Approve",
            Content = "Content Exam",
            SampleAnswer = new string('E', 60),
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            Source = QuestionSource.Manual,
            SubmittedBy = lec1.Id,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            IsActive = false
        };
        var practiceQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q Practice Padded Revision",
            Content = "Content Practice",
            SampleAnswer = new string('P', 60),
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            Source = QuestionSource.Manual,
            SubmittedBy = lec1.Id,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            IsActive = false
        };
        var reviewRequest = new QuestionReviewRequest
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SubmittedBy = lec1.Id,
            SubmittedAt = DateTime.UtcNow,
            Version = 1
        };
        var reqItem = new QuestionReviewRequestItem
        {
            Id = Guid.NewGuid(),
            RequestId = reviewRequest.Id,
            QuestionId = examQuestion.Id,
            UsageScope = "exam",
            Decision = "PENDING"
        };
        db.ExamQuestions.Add(examQuestion);
        db.PracticeQuestions.Add(practiceQuestion);
        db.QuestionReviewRequests.Add(reviewRequest);
        db.QuestionReviewRequestItems.Add(reqItem);
        await db.SaveChangesAsync();

        var validator = new ReviewQuestionDecisionCommandValidator();
        var handler = new ReviewQuestionDecisionCommandHandler(db);

        var paddedApproveCmd = new ReviewQuestionDecisionCommand(
            examQuestion.Id, head.Id, " APPROVED ", null, "official_exam");
        validator.Validate(paddedApproveCmd).IsValid.Should().BeTrue();

        var approveRes = await handler.Handle(paddedApproveCmd, CancellationToken.None);
        approveRes.IsSuccess.Should().BeTrue(because: approveRes.Error);
        approveRes.Value.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        (await db.QuestionReviewRequestItems.FindAsync(reqItem.Id))!.Decision.Should().Be(ApprovalStatus.Approved);

        var paddedRevisionCmd = new ReviewQuestionDecisionCommand(
            practiceQuestion.Id, head.Id, " NEEDS_REVISION ", "Cần làm rõ thêm tiêu chí chấm điểm.", "practice");
        validator.Validate(paddedRevisionCmd).IsValid.Should().BeTrue();

        var revisionRes = await handler.Handle(paddedRevisionCmd, CancellationToken.None);
        revisionRes.IsSuccess.Should().BeTrue(because: revisionRes.Error);
        revisionRes.Value.ApprovalStatus.Should().Be(ApprovalStatus.NeedsRevision);

        var lowercaseCmd = new ReviewQuestionDecisionCommand(
            practiceQuestion.Id, head.Id, "approved", null, "practice");
        validator.Validate(lowercaseCmd).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "M2-16B: GetExamSetsQueryHandler chuẩn hóa bộ lọc Status không phân biệt hoa thường cho cả ExaminationOfficer và DepartmentHead")]
    public async Task GetExamSetsQueryHandler_Normalizes_Lowercase_Status_Filter()
    {
        await using var db = CreateDbContext();
        var (head, lec1, _, officer, _, course, structure, rubric) = await SeedOfficialExamCourseAsync(db, totalQuestions: 1);

        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "C1", SampleAnswer = new string('A', 60), BloomLevel = BloomLevel.Understand, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        db.ExamQuestions.Add(q1);

        var draftSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "SET-DRAFT", Status = ExamSetStatus.Draft, IsActive = true };
        draftSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = draftSet.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });

        var handedOverSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "SET-HANDED", Status = ExamSetStatus.HandedOver, IsActive = true };
        handedOverSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = handedOverSet.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });

        db.ExamSets.AddRange(draftSet, handedOverSet);
        await db.SaveChangesAsync();

        var handler = new GetExamSetsQueryHandler(db);

        var eoLower = await handler.Handle(
            new GetExamSetsQuery(course.Id, null, "handed_over", officer.Id, UserRole.ExaminationOfficer),
            CancellationToken.None);
        eoLower.IsSuccess.Should().BeTrue(because: eoLower.Error);
        eoLower.Value.Should().ContainSingle().Which.Id.Should().Be(handedOverSet.Id);

        var headDraftLower = await handler.Handle(
            new GetExamSetsQuery(course.Id, null, " draft ", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        headDraftLower.IsSuccess.Should().BeTrue(because: headDraftLower.Error);
        headDraftLower.Value.Should().ContainSingle().Which.Id.Should().Be(draftSet.Id);
    }

    #endregion
}
