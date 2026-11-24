using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OralExamination.API.Configuration;
using OralExamination.API.Controllers.v1;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerClasses;
using OralExamination.Application.Features.Lecturer.Queries.GetLecturerDashboard;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromClo;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetFlmSyllabus;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features.Security;

public sealed class P0HardeningBolaRbacAndClassJoinTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName ?? $"p0-b-sec-{Guid.NewGuid()}")
            .Options;

        return new OralExamDbContext(options);
    }

    private static User CreateUser(string role, string prefix, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{prefix}-{Guid.NewGuid():N}@fpt.edu.vn",
        FullName = $"{prefix} User",
        Role = role,
        IsActive = isActive,
        AuthVersion = 1
    };

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role) => new(
        new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    private static RubricDraftDto BuildValidRubricDraft() => new(
        "Rubric 10.0 chuẩn",
        "Đánh giá đầy đủ 2 tiêu chí",
        10.00m,
        [
            new RubricCriterionDraftDto("Lý thuyết nền tảng", 5.00m, 0.50m, BloomLevel.Understand, "Trình bày chính xác khái niệm", 1),
            new RubricCriterionDraftDto("Phân tích vận dụng", 5.00m, 0.50m, BloomLevel.Apply, "Đưa ví dụ thực tế rõ ràng", 2)
        ]);

    private static Rubric BuildValidRubricEntity(Guid courseId)
    {
        var rubricId = Guid.NewGuid();
        var rubric = new Rubric
        {
            Id = rubricId,
            CourseId = courseId,
            Name = "Rubric 10.0",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubricId,
            CriterionName = "Tiêu chí 1",
            MaxScore = 5.00m,
            Weight = 0.50m,
            BloomLevel = BloomLevel.Understand,
            OrderIndex = 1
        });
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubricId,
            CriterionName = "Tiêu chí 2",
            MaxScore = 5.00m,
            Weight = 0.50m,
            BloomLevel = BloomLevel.Apply,
            OrderIndex = 2
        });
        return rubric;
    }

    private sealed class MutatingDuringVerifyPasswordHasher(
        IPasswordHasher inner,
        Action onVerified) : IPasswordHasher
    {
        public string Hash(string password) => inner.Hash(password);

        public bool Verify(string password, string passwordHash)
        {
            var matched = inner.Verify(password, passwordHash);
            if (matched)
            {
                onVerified();
            }

            return matched;
        }
    }

    [Fact(DisplayName = "P0-05a: QuestionsController luôn bỏ qua request.LecturerId & ApprovedBy từ body và ánh xạ FORBIDDEN sang HTTP 403")]
    public async Task P0_05_QuestionsController_IgnoresSpoofedBodyLecturerId_AndMapsForbiddenTo403()
    {
        var actorId = Guid.NewGuid();
        var spoofedLecturerId = Guid.NewGuid();
        var spoofedApproverId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var capturedCommands = new List<CreateDraftQuestionCommand>();

        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(s => s.Send(It.IsAny<CreateDraftQuestionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<Guid>>, CancellationToken>((req, _) =>
            {
                capturedCommands.Add((CreateDraftQuestionCommand)req);
            })
            .ReturnsAsync(Result<Guid>.Failure("FORBIDDEN: Không phụ trách môn học này."));

        var controller = new QuestionsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(actorId, UserRole.DepartmentHead) }
            }
        };

        var payload = new CreateDraftQuestionRequestDto(
            CourseId: courseId,
            Title: "Câu hỏi mạo danh tác giả",
            Content: "Nội dung câu hỏi kiểm thử chống giả mạo LecturerId từ body.",
            SampleAnswer: new string('A', 60),
            KeyPoints: ["Dependency Inversion"],
            Difficulty: "medium",
            BloomLevel: BloomLevel.Understand,
            Source: "manual",
            UsageScope: "exam",
            HasFollowUp: false,
            FollowUpPrompt: null,
            Rubric: BuildValidRubricDraft(),
            LecturerId: spoofedLecturerId,
            RubricId: null,
            ApprovedBy: spoofedApproverId);

        var draftResponse = await controller.CreateDraftQuestion(payload);
        var practiceResponse = await controller.CreatePracticeQuestion(payload);

        capturedCommands.Should().HaveCount(2);
        capturedCommands.Should().OnlyContain(cmd =>
            cmd.LecturerId == actorId &&
            cmd.ActorUserId == actorId &&
            cmd.ApprovedBy == null);

        draftResponse.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        practiceResponse.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact(DisplayName = "P0-05b: CreateDraftQuestionCommandHandler chặn Trưởng BM khác bộ môn (kể cả khi truyền LecturerId thuộc bộ môn kia) và chặn Giảng viên mạo danh")]
    public async Task P0_05_CreateDraftQuestionHandler_BlocksCrossDepartmentHead_AndLecturerImpersonation()
    {
        await using var db = CreateDbContext();

        var headA = CreateUser(UserRole.DepartmentHead, "head-a");
        var headB = CreateUser(UserRole.DepartmentHead, "head-b");
        var lecturerA = CreateUser(UserRole.Lecturer, "lec-a");
        var lecturerB = CreateUser(UserRole.Lecturer, "lec-b");

        var programA = new AcademicProgram { Id = Guid.NewGuid(), Code = "SE", Name = "Software Engineering", DepartmentHeadUserId = headA.Id, IsActive = true };
        var programB = new AcademicProgram { Id = Guid.NewGuid(), Code = "AI", Name = "Artificial Intelligence", DepartmentHeadUserId = headB.Id, IsActive = true };

        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", AcademicProgramId = programA.Id, DepartmentHeadUserId = headA.Id, IsActive = true };
        // Course B cố tình đặt cột legacy DepartmentHeadUserId = headA.Id nhưng AcademicProgram thuộc headB.Id (canonical ownership)
        var courseB = new Course { Id = Guid.NewGuid(), Code = "AIL303", Name = "AIL303", AcademicProgramId = programB.Id, DepartmentHeadUserId = headA.Id, IsActive = true };

        db.Users.AddRange(headA, headB, lecturerA, lecturerB);
        db.AcademicPrograms.AddRange(programA, programB);
        db.Courses.AddRange(courseA, courseB);
        db.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = courseA.Id, LecturerUserId = lecturerA.Id, AssignedByUserId = headA.Id, IsActive = true },
            new CourseLecturer { CourseId = courseB.Id, LecturerUserId = lecturerB.Id, AssignedByUserId = headB.Id, IsActive = true });
        await db.SaveChangesAsync();

        var handler = new CreateDraftQuestionCommandHandler(db);

        // 1. Head A thử tạo câu hỏi vào Course B bằng cách chèn LecturerId = lecturerB.Id
        var crossDeptResult = await handler.Handle(new CreateDraftQuestionCommand(
            CourseId: courseB.Id,
            LecturerId: lecturerB.Id,
            Title: "Cross-department BOLA",
            Content: "Trưởng BM A cố tình tạo câu hỏi sang bộ môn B.",
            SampleAnswer: new string('A', 60),
            KeyPoints: ["K1"],
            Difficulty: "medium",
            BloomLevel: BloomLevel.Understand,
            Source: "manual",
            UsageScope: "exam",
            HasFollowUp: false,
            FollowUpPrompt: null,
            Rubric: BuildValidRubricDraft(),
            ActorUserId: headA.Id), CancellationToken.None);

        crossDeptResult.IsSuccess.Should().BeFalse();
        crossDeptResult.Error.Should().StartWith("FORBIDDEN:");

        // 2. Lecturer A thử mạo danh Lecturer B trong Course A
        var impersonationResult = await handler.Handle(new CreateDraftQuestionCommand(
            CourseId: courseA.Id,
            LecturerId: lecturerB.Id,
            Title: "Lecturer impersonation",
            Content: "Giảng viên A cố tình gán tác giả là giảng viên B.",
            SampleAnswer: new string('B', 60),
            KeyPoints: ["K1"],
            Difficulty: "easy",
            BloomLevel: BloomLevel.Remember,
            Source: "manual",
            UsageScope: "practice",
            HasFollowUp: false,
            FollowUpPrompt: null,
            Rubric: BuildValidRubricDraft(),
            ActorUserId: lecturerA.Id), CancellationToken.None);

        impersonationResult.IsSuccess.Should().BeFalse();
        impersonationResult.Error.Should().StartWith("FORBIDDEN:");

        // 3. Head A tạo hợp lệ trong Course A thuộc quyền quản lý của mình
        var validHeadResult = await handler.Handle(new CreateDraftQuestionCommand(
            CourseId: courseA.Id,
            LecturerId: headA.Id,
            Title: "Valid Head Question",
            Content: "Trưởng BM A biên soạn câu hỏi hợp lệ trong môn thuộc bộ môn A.",
            SampleAnswer: new string('C', 60),
            KeyPoints: ["K1"],
            Difficulty: "hard",
            BloomLevel: BloomLevel.Analyze,
            Source: "manual",
            UsageScope: "exam",
            HasFollowUp: false,
            FollowUpPrompt: null,
            Rubric: BuildValidRubricDraft(),
            ApprovedBy: headA.Id,
            ActorUserId: headA.Id), CancellationToken.None);

        validHeadResult.IsSuccess.Should().BeTrue();
        var created = await db.ExamQuestions.AsNoTracking().FirstAsync(q => q.Id == validHeadResult.Value);
        created.SubmittedBy.Should().Be(headA.Id);
        created.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
        created.ApprovedBy.Should().BeNull();
    }

    [Fact(DisplayName = "P0-06a: QuestionReviewRequestService.Decide chặn tự duyệt (FORBIDDEN) và Controller trả về HTTP 403")]
    public async Task P0_06_QuestionReviewRequestService_AndController_BlockSelfApproval()
    {
        await using var db = CreateDbContext();

        var head = CreateUser(UserRole.DepartmentHead, "self-head");
        var lecturer = CreateUser(UserRole.Lecturer, "self-lec");
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", DepartmentHeadUserId = head.Id, IsActive = true };
        var rubric = BuildValidRubricEntity(course.Id);

        var qByHeadInOwnReq = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Rubric = rubric,
            Title = "Câu hỏi TBM tự gửi",
            Content = "Nội dung câu hỏi tự gửi tự duyệt.",
            SampleAnswer = new string('S', 70),
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = head.Id,
            IsActive = false
        };
        var qByHeadInLecturerReq = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Rubric = rubric,
            Title = "Câu hỏi TBM soạn nằm trong yêu cầu của GV",
            Content = "Nội dung câu hỏi của TBM được gửi qua yêu cầu khác.",
            SampleAnswer = new string('P', 70),
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = head.Id,
            IsActive = false
        };

        var req1Item = new QuestionReviewRequestItem { Id = Guid.NewGuid(), QuestionId = qByHeadInOwnReq.Id, UsageScope = "exam", Decision = "PENDING" };
        var req1 = new QuestionReviewRequest
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SubmittedBy = head.Id,
            SubmittedAt = DateTime.UtcNow,
            Version = 1,
            Items = [req1Item]
        };
        var req2Item = new QuestionReviewRequestItem { Id = Guid.NewGuid(), QuestionId = qByHeadInLecturerReq.Id, UsageScope = "practice", Decision = "PENDING" };
        var req2 = new QuestionReviewRequest
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SubmittedBy = lecturer.Id,
            SubmittedAt = DateTime.UtcNow,
            Version = 1,
            Items = [req2Item]
        };

        db.Users.AddRange(head, lecturer);
        db.Courses.Add(course);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(qByHeadInOwnReq);
        db.PracticeQuestions.Add(qByHeadInLecturerReq);
        db.QuestionReviewRequests.AddRange(req1, req2);
        await db.SaveChangesAsync();

        var service = new QuestionReviewRequestService(db);

        // Case 1: request.SubmittedBy == actorId
        var selfReqResult = await service.Decide(
            head.Id,
            req1.Id,
            new ReviewRequestDecision([req1Item.Id], ApprovalStatus.Approved, null, 1),
            CancellationToken.None);

        selfReqResult.IsSuccess.Should().BeFalse();
        selfReqResult.Error.Should().StartWith("FORBIDDEN:");

        // Case 2: question.SubmittedBy == actorId dù request.SubmittedBy là giảng viên khác
        var selfQuestionResult = await service.Decide(
            head.Id,
            req2.Id,
            new ReviewRequestDecision([req2Item.Id], ApprovalStatus.Approved, null, 1),
            CancellationToken.None);

        selfQuestionResult.IsSuccess.Should().BeFalse();
        selfQuestionResult.Error.Should().StartWith("FORBIDDEN:");

        (await db.ExamQuestions.AsNoTracking().FirstAsync(q => q.Id == qByHeadInOwnReq.Id))
            .ApprovalStatus.Should().Be(ApprovalStatus.SubmittedForReview);
        (await db.PracticeQuestions.AsNoTracking().FirstAsync(q => q.Id == qByHeadInLecturerReq.Id))
            .ApprovalStatus.Should().Be(ApprovalStatus.SubmittedForReview);

        // Case 3: QuestionReviewRequestsController ánh xạ FORBIDDEN -> HTTP 403
        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(s => s.Send(It.IsAny<DecideQuestionReviewRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("FORBIDDEN: Không được tự phê duyệt câu hỏi của chính mình."));
        var controller = new QuestionReviewRequestsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(head.Id, UserRole.DepartmentHead) }
            }
        };

        var httpResult = await controller.Decide(
            req1.Id,
            new ReviewRequestDecision([req1Item.Id], ApprovalStatus.Approved, null, 1),
            CancellationToken.None);

        httpResult.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact(DisplayName = "P0-06b: ApproveExamSetCommandHandler chặn Trưởng BM tự duyệt bộ đề do chính mình tạo (FORBIDDEN)")]
    public async Task P0_06_ApproveExamSetHandler_BlocksDepartmentHeadSelfApproval()
    {
        await using var db = CreateDbContext();

        var head = CreateUser(UserRole.DepartmentHead, "set-head");
        var lecturer = CreateUser(UserRole.Lecturer, "set-lec");
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN222", Name = "PRN222", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = head.Id,
            Name = "FE Structure",
            TotalQuestions = 1,
            DurationMinutes = 15,
            IsActive = true
        };
        var rubric = BuildValidRubricEntity(course.Id);
        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi thi thật 1",
            Content = "Nội dung câu hỏi thi thật.",
            SampleAnswer = new string('E', 60),
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = lecturer.Id,
            IsActive = true
        };
        var selfCreatedSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = head.Id,
            SetCode = "SET-SELF-01",
            Status = ExamSetStatus.SubmittedForReview,
            IsSampleSet = false,
            IsActive = true,
            ExamSetQuestions = [new ExamSetQuestion { ExamQuestionId = question.Id, OrderIndex = 1 }]
        };
        var lecturerCreatedSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = lecturer.Id,
            SetCode = "SET-LEC-01",
            Status = ExamSetStatus.SubmittedForReview,
            IsSampleSet = false,
            IsActive = true,
            ExamSetQuestions = [new ExamSetQuestion { ExamQuestionId = question.Id, OrderIndex = 1 }]
        };

        db.Users.AddRange(head, lecturer);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(question);
        db.ExamSets.AddRange(selfCreatedSet, lecturerCreatedSet);
        await db.SaveChangesAsync();

        var handler = new ApproveExamSetCommandHandler(db);

        var selfApproveResult = await handler.Handle(
            new ApproveExamSetCommand(selfCreatedSet.Id, "Tự duyệt", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);

        selfApproveResult.IsSuccess.Should().BeFalse();
        selfApproveResult.Error.Should().StartWith("FORBIDDEN:");
        (await db.ExamSets.AsNoTracking().FirstAsync(es => es.Id == selfCreatedSet.Id))
            .Status.Should().Be(ExamSetStatus.SubmittedForReview);

        // Bộ đề do giảng viên khác tạo vẫn được Trưởng BM duyệt bình thường
        var validApproveResult = await handler.Handle(
            new ApproveExamSetCommand(lecturerCreatedSet.Id, "Đạt chuẩn", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);

        validApproveResult.IsSuccess.Should().BeTrue();
        validApproveResult.Value!.Status.Should().Be(ExamSetStatus.Official);
    }

    [Fact(DisplayName = "P0-06c: GetFlmSyllabusQueryHandler fail-closed (403/FORBIDDEN) ngoài phạm vi môn và không bao giờ lộ tiêu đề ExamQuestions")]
    public async Task P0_06_GetFlmSyllabus_FailsClosedOnUnauthorizedCourse_AndNeverLeaksExamQuestionTitles()
    {
        await using var db = CreateDbContext();

        var headA = CreateUser(UserRole.DepartmentHead, "flm-head-a");
        var headB = CreateUser(UserRole.DepartmentHead, "flm-head-b");
        var lecturerA = CreateUser(UserRole.Lecturer, "flm-lec-a");
        var student = CreateUser(UserRole.Student, "flm-student");

        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = headA.Id, IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "SWP391", Name = "SWP391", DepartmentHeadUserId = headB.Id, IsActive = true };
        var rubricA = BuildValidRubricEntity(courseA.Id);

        db.Users.AddRange(headA, headB, lecturerA, student);
        db.Courses.AddRange(courseA, courseB);
        db.CourseLecturers.Add(new CourseLecturer
        {
            CourseId = courseA.Id,
            LecturerUserId = lecturerA.Id,
            AssignedByUserId = headA.Id,
            IsActive = true
        });
        db.CourseClos.Add(new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = courseA.Id,
            CloCode = "CLO1",
            Description = "Thiết kế API chuẩn RESTful",
            BloomLevel = BloomLevel.Apply,
            OrderIndex = 1,
            IsActive = true
        });
        db.Rubrics.Add(rubricA);
        db.ExamQuestions.Add(new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = courseA.Id,
            RubricId = rubricA.Id,
            Title = "SECRET-OFFICIAL-EXAM-TITLE-2026",
            Content = "Đề thi mật không được lộ qua FLM syllabus.",
            SampleAnswer = new string('X', 60),
            ApprovalStatus = ApprovalStatus.Approved,
            SubmittedBy = lecturerA.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var handler = new GetFlmSyllabusQueryHandler(db);

        // 1. Thiếu định danh -> FORBIDDEN
        (await handler.Handle(new GetFlmSyllabusQuery(courseA.Id), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");

        // 2. Sinh viên -> FORBIDDEN
        (await handler.Handle(new GetFlmSyllabusQuery(courseA.Id, ActorUserId: student.Id, ActorRole: UserRole.Student), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");

        // 3. Giảng viên chưa được phân công Course B -> FORBIDDEN
        (await handler.Handle(new GetFlmSyllabusQuery(courseB.Id, ActorUserId: lecturerA.Id, ActorRole: UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");

        // 4. Trưởng BM A truy vấn Course B của Trưởng BM B -> FORBIDDEN
        (await handler.Handle(new GetFlmSyllabusQuery(courseB.Id, ActorUserId: headA.Id, ActorRole: UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");

        // 5. Giảng viên A hợp lệ trên Course A -> thành công và tuyệt đối không lộ tiêu đề ExamQuestion
        var allowed = await handler.Handle(
            new GetFlmSyllabusQuery(courseA.Id, ActorUserId: lecturerA.Id, ActorRole: UserRole.Lecturer),
            CancellationToken.None);

        allowed.IsSuccess.Should().BeTrue();
        allowed.Value!.Topics.Should().NotBeEmpty();
        allowed.Value.Topics.Should().NotContain(t => t.TopicName.Contains("SECRET-OFFICIAL-EXAM-TITLE-2026", StringComparison.OrdinalIgnoreCase));
        allowed.Value.Clos.Should().ContainSingle(c => c.CloCode == "CLO1");
    }

    [Fact(DisplayName = "P0-06d: GenerateQuestionsFromCloCommandHandler fail-closed khi môn chưa gán GV hoặc sai Trưởng BM và QuestionsController ánh xạ 403")]
    public async Task P0_06_GenerateQuestionsFromClo_FailsClosedOnEmptyCourseLecturers_AndControllerMaps403()
    {
        await using var db = CreateDbContext();

        var headA = CreateUser(UserRole.DepartmentHead, "gen-head-a");
        var headB = CreateUser(UserRole.DepartmentHead, "gen-head-b");
        var unassignedLecturer = CreateUser(UserRole.Lecturer, "gen-lec-unassigned");

        var programB = new AcademicProgram { Id = Guid.NewGuid(), Code = "AI", Name = "AI", DepartmentHeadUserId = headB.Id, IsActive = true };
        // Môn học hoàn toàn chưa có CourseLecturers nào (trước đây từng fail-open!) và thuộc Program B
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "CSD201",
            Name = "Data Structures",
            AcademicProgramId = programB.Id,
            DepartmentHeadUserId = headA.Id, // legacy stale owner — canonical owner là headB
            IsActive = true
        };
        var clo = new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloCode = "CLO1",
            Description = "Cấu trúc cây và đồ thị",
            BloomLevel = BloomLevel.Understand,
            OrderIndex = 1,
            IsActive = true
        };

        db.Users.AddRange(headA, headB, unassignedLecturer);
        db.AcademicPrograms.Add(programB);
        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        await db.SaveChangesAsync();

        var aiMock = new Mock<IAiQuestionGenerationService>();
        var handler = new GenerateQuestionsFromCloCommandHandler(db, aiMock.Object, new QuestionGenerationGuidanceProvider());

        // 1. Giảng viên chưa phân công trên môn có 0 CourseLecturers -> phải FORBIDDEN (fail-closed)
        var lecturerResult = await handler.Handle(new GenerateQuestionsFromCloCommand(
            course.Id,
            unassignedLecturer.Id,
            ["CLO1"],
            ["Tree"],
            1,
            UsageScope.Practice,
            [BloomLevel.Understand],
            new DifficultyDistributionDto(1, 0, 0)), CancellationToken.None);

        lecturerResult.IsSuccess.Should().BeFalse();
        lecturerResult.Error.Should().StartWith("FORBIDDEN:");

        // 2. Head A không còn sở hữu môn theo AcademicProgram -> phải FORBIDDEN
        var headResult = await handler.Handle(new GenerateQuestionsFromCloCommand(
            course.Id,
            headA.Id,
            ["CLO1"],
            ["Tree"],
            1,
            UsageScope.Exam,
            [BloomLevel.Understand],
            new DifficultyDistributionDto(1, 0, 0)), CancellationToken.None);

        headResult.IsSuccess.Should().BeFalse();
        headResult.Error.Should().StartWith("FORBIDDEN:");
        aiMock.Verify(s => s.GenerateQuestionsAsync(It.IsAny<AiQuestionGenerationParams>(), It.IsAny<CancellationToken>()), Times.Never);

        // 3. QuestionsController.GenerateFromClo ánh xạ FORBIDDEN -> HTTP 403
        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(s => s.Send(It.IsAny<GenerateQuestionsFromCloCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GenerateQuestionsFromCloResponseDto>.Failure("FORBIDDEN: Giảng viên chưa được phân công."));

        var controller = new QuestionsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(unassignedLecturer.Id, UserRole.Lecturer) }
            }
        };

        var httpRes = await controller.GenerateFromClo(new GenerateQuestionsFromCloRequestDto(
            course.Id,
            ["CLO1"],
            ["Tree"],
            1,
            UsageScope.Practice,
            [BloomLevel.Understand],
            new DifficultyDistributionDto(1, 0, 0)));

        httpRes.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact(DisplayName = "P0-07a: GetQuestions ẩn câu DRAFT/PENDING_CURATION/EXCLUDED/Đề Mẫu của GV khác, ẩn danh SubmittedBy và khóa oracle lọc theo submittedBy")]
    public async Task P0_07_GetQuestions_HidesOtherLecturersPrivateAndSampleQuestions_AnonymizesAuthor_AndBlocksSubmittedByOracle()
    {
        await using var db = CreateDbContext();

        var head = CreateUser(UserRole.DepartmentHead, "q-head");
        var lecturerA = CreateUser(UserRole.Lecturer, "q-lec-a");
        var lecturerB = CreateUser(UserRole.Lecturer, "q-lec-b");
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = head.Id, IsActive = true };
        var rubric = BuildValidRubricEntity(course.Id);
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "FE", TotalQuestions = 2, DurationMinutes = 15, IsActive = true };

        var qB_Draft = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Draft Exam",
            Content = "Private draft", SampleAnswer = new string('a', 60), ApprovalStatus = ApprovalStatus.Draft,
            BankCurationStatus = BankCurationStatus.BankReady, SubmittedBy = lecturerB.Id, IsActive = false
        };
        var qB_PendingCuration = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Pending Curation",
            Content = "Manual private question in set", SampleAnswer = new string('b', 60), ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.PendingCuration, SubmittedBy = lecturerB.Id, ApprovedBy = head.Id, IsActive = true
        };
        var qB_Excluded = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Excluded From Bank",
            Content = "Excluded question", SampleAnswer = new string('c', 60), ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.ExcludedFromBank, SubmittedBy = lecturerB.Id, ApprovedBy = head.Id, IsActive = true
        };
        var qB_InActiveSample = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Sample Set Question",
            Content = "Sample set question", SampleAnswer = new string('d', 60), ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady, SubmittedBy = lecturerB.Id, ApprovedBy = head.Id, IsActive = true
        };
        var qB_CuratedPublic = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Curated Public Exam",
            Content = "Curated public question", SampleAnswer = new string('e', 60), ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.CuratedIntoBank, SubmittedBy = lecturerB.Id, ApprovedBy = head.Id, IsActive = true
        };
        var qB_PracticeDraft = new PracticeQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Draft Practice",
            Content = "Private practice draft", SampleAnswer = new string('f', 60), ApprovalStatus = ApprovalStatus.Draft,
            SubmittedBy = lecturerB.Id, IsActive = false
        };
        var qB_PracticePublic = new PracticeQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Approved Practice",
            Content = "Shared practice", SampleAnswer = new string('g', 60), ApprovalStatus = ApprovalStatus.Approved,
            SubmittedBy = lecturerB.Id, ApprovedBy = head.Id, IsActive = true
        };
        var qA_OwnPrivate = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "A Own Pending Curation Draft",
            Content = "Own question", SampleAnswer = new string('h', 60), ApprovalStatus = ApprovalStatus.Draft,
            BankCurationStatus = BankCurationStatus.PendingCuration, SubmittedBy = lecturerA.Id, IsActive = false
        };

        var activeSampleSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = head.Id,
            SetCode = "SAMPLE-01",
            Status = ExamSetStatus.Official,
            IsSampleSet = true,
            IsActive = true,
            ExamSetQuestions = [new ExamSetQuestion { ExamQuestionId = qB_InActiveSample.Id, OrderIndex = 1 }]
        };

        db.Users.AddRange(head, lecturerA, lecturerB);
        db.Courses.Add(course);
        db.Rubrics.Add(rubric);
        db.ExamStructures.Add(structure);
        db.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = course.Id, LecturerUserId = lecturerA.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { CourseId = course.Id, LecturerUserId = lecturerB.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamQuestions.AddRange(qB_Draft, qB_PendingCuration, qB_Excluded, qB_InActiveSample, qB_CuratedPublic, qA_OwnPrivate);
        db.PracticeQuestions.AddRange(qB_PracticeDraft, qB_PracticePublic);
        db.ExamSets.Add(activeSampleSet);
        await db.SaveChangesAsync();

        var handler = new GetQuestionsQueryHandler(db);

        // 1. Lecturer A truy vấn danh sách tổng hợp -> chỉ thấy câu công khai (B Curated, B Practice Public) + câu của chính mình (A Own)
        var lecAll = await handler.Handle(
            new GetQuestionsQuery(CourseId: course.Id, ActorId: lecturerA.Id, ActorRole: UserRole.Lecturer),
            CancellationToken.None);

        lecAll.IsSuccess.Should().BeTrue();
        lecAll.Value!.Items.Select(x => x.Id).Should().BeEquivalentTo([qB_CuratedPublic.Id, qB_PracticePublic.Id, qA_OwnPrivate.Id]);
        lecAll.Value.Items.Single(x => x.Id == qB_CuratedPublic.Id).SubmittedBy.Should().BeNull();
        lecAll.Value.Items.Single(x => x.Id == qB_PracticePublic.Id).SubmittedBy.Should().BeNull();
        lecAll.Value.Items.Single(x => x.Id == qA_OwnPrivate.Id).SubmittedBy.Should().Be(lecturerA.Id);

        // 2. Lecturer A lọc theo usageScope = "exam" và "practice" đều ẩn danh tác giả khác
        var lecExam = await handler.Handle(
            new GetQuestionsQuery(CourseId: course.Id, UsageScope: "exam", ActorId: lecturerA.Id, ActorRole: UserRole.Lecturer),
            CancellationToken.None);
        lecExam.Value!.Items.Select(x => x.Id).Should().BeEquivalentTo([qB_CuratedPublic.Id, qA_OwnPrivate.Id]);
        lecExam.Value.Items.Single(x => x.Id == qB_CuratedPublic.Id).SubmittedBy.Should().BeNull();

        var lecPractice = await handler.Handle(
            new GetQuestionsQuery(CourseId: course.Id, UsageScope: "practice", ActorId: lecturerA.Id, ActorRole: UserRole.Lecturer),
            CancellationToken.None);
        lecPractice.Value!.Items.Should().ContainSingle(x => x.Id == qB_PracticePublic.Id && x.SubmittedBy == null);

        // 3. Chặn side-channel oracle: Lecturer A truyền SubmittedBy = lecturerB.Id không dò được câu nào của B
        var oracleAttempt = await handler.Handle(
            new GetQuestionsQuery(CourseId: course.Id, SubmittedBy: lecturerB.Id, ActorId: lecturerA.Id, ActorRole: UserRole.Lecturer),
            CancellationToken.None);
        oracleAttempt.IsSuccess.Should().BeTrue();
        oracleAttempt.Value!.Items.Should().BeEmpty();

        // 4. Trưởng BM vẫn thấy đầy đủ câu hỏi và tác giả gốc
        var headAll = await handler.Handle(
            new GetQuestionsQuery(CourseId: course.Id, ActorId: head.Id, ActorRole: UserRole.DepartmentHead),
            CancellationToken.None);
        headAll.IsSuccess.Should().BeTrue();
        headAll.Value!.TotalCount.Should().Be(8);
        headAll.Value.Items.Single(x => x.Id == qB_CuratedPublic.Id).SubmittedBy.Should().Be(lecturerB.Id);
    }

    [Fact(DisplayName = "P0-07b: GetQuestionById chặn câu riêng tư/PENDING_CURATION/Đề Mẫu của GV khác (FORBIDDEN) và ẩn danh tác giả câu chung")]
    public async Task P0_07_GetQuestionById_BlocksOtherLecturersPrivateOrSampleQuestions_AndAnonymizesSharedAuthor()
    {
        await using var db = CreateDbContext();

        var head = CreateUser(UserRole.DepartmentHead, "qid-head");
        var lecturerA = CreateUser(UserRole.Lecturer, "qid-lec-a");
        var lecturerB = CreateUser(UserRole.Lecturer, "qid-lec-b");
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = head.Id, IsActive = true };
        var rubric = BuildValidRubricEntity(course.Id);
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "FE", TotalQuestions = 1, DurationMinutes = 15, IsActive = true };

        var qB_PendingCuration = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Private Manual",
            Content = "Private manual", SampleAnswer = new string('a', 60), ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.PendingCuration, SubmittedBy = lecturerB.Id, IsActive = true
        };
        var qB_Sample = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Sample Question",
            Content = "Sample", SampleAnswer = new string('b', 60), ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady, SubmittedBy = lecturerB.Id, IsActive = true
        };
        var qB_Public = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Public Bank",
            Content = "Public bank", SampleAnswer = new string('c', 60), ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady, SubmittedBy = lecturerB.Id, ApprovedBy = head.Id, IsActive = true
        };
        var qB_PracticeDraft = new PracticeQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Practice Draft",
            Content = "Practice draft", SampleAnswer = new string('d', 60), ApprovalStatus = ApprovalStatus.Draft,
            SubmittedBy = lecturerB.Id, IsActive = false
        };
        var qB_PracticeApproved = new PracticeQuestion
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "B Practice Approved",
            Content = "Practice approved", SampleAnswer = new string('e', 60), ApprovalStatus = ApprovalStatus.Approved,
            SubmittedBy = lecturerB.Id, ApprovedBy = head.Id, IsActive = true
        };

        db.Users.AddRange(head, lecturerA, lecturerB);
        db.Courses.Add(course);
        db.Rubrics.Add(rubric);
        db.ExamStructures.Add(structure);
        db.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = course.Id, LecturerUserId = lecturerA.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { CourseId = course.Id, LecturerUserId = lecturerB.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamQuestions.AddRange(qB_PendingCuration, qB_Sample, qB_Public);
        db.PracticeQuestions.AddRange(qB_PracticeDraft, qB_PracticeApproved);
        db.ExamSets.Add(new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = head.Id,
            SetCode = "SAMPLE-ACTIVE",
            Status = ExamSetStatus.Official,
            IsSampleSet = true,
            IsActive = true,
            ExamSetQuestions = [new ExamSetQuestion { ExamQuestionId = qB_Sample.Id, OrderIndex = 1 }]
        });
        await db.SaveChangesAsync();

        var handler = new GetQuestionByIdQueryHandler(db);

        // Lecturer A bị chặn xem PENDING_CURATION, câu Đề Mẫu active, và Practice DRAFT của B
        (await handler.Handle(new GetQuestionByIdQuery(qB_PendingCuration.Id, lecturerA.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await handler.Handle(new GetQuestionByIdQuery(qB_Sample.Id, lecturerA.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await handler.Handle(new GetQuestionByIdQuery(qB_PracticeDraft.Id, lecturerA.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");

        // Lecturer A xem câu đã duyệt chung của B -> thành công nhưng ẩn hoàn toàn SubmittedBy & SubmittedByName
        var sharedExam = await handler.Handle(new GetQuestionByIdQuery(qB_Public.Id, lecturerA.Id, UserRole.Lecturer), CancellationToken.None);
        sharedExam.IsSuccess.Should().BeTrue();
        sharedExam.Value!.SubmittedBy.Should().BeNull();
        sharedExam.Value.SubmittedByName.Should().BeNull();

        var sharedPractice = await handler.Handle(new GetQuestionByIdQuery(qB_PracticeApproved.Id, lecturerA.Id, UserRole.Lecturer), CancellationToken.None);
        sharedPractice.IsSuccess.Should().BeTrue();
        sharedPractice.Value!.SubmittedBy.Should().BeNull();
        sharedPractice.Value.SubmittedByName.Should().BeNull();

        // Tác giả B tự xem câu PENDING_CURATION của mình và Trưởng BM xem vẫn giữ nguyên tác giả
        var ownDetail = await handler.Handle(new GetQuestionByIdQuery(qB_PendingCuration.Id, lecturerB.Id, UserRole.Lecturer), CancellationToken.None);
        ownDetail.IsSuccess.Should().BeTrue();
        ownDetail.Value!.SubmittedBy.Should().Be(lecturerB.Id);
        ownDetail.Value.SubmittedByName.Should().Be(lecturerB.FullName);

        var headDetail = await handler.Handle(new GetQuestionByIdQuery(qB_PendingCuration.Id, head.Id, UserRole.DepartmentHead), CancellationToken.None);
        headDetail.IsSuccess.Should().BeTrue();
        headDetail.Value!.SubmittedBy.Should().Be(lecturerB.Id);
        headDetail.Value.SubmittedByName.Should().Be(lecturerB.FullName);
    }

    [Fact(DisplayName = "P0-08a: GetLecturerDashboard khóa BOLA câu hỏi, ca thi và phúc khảo theo môn/Examiner/AssignedTo; LecturerController bỏ qua query lecturerId")]
    public async Task P0_08_GetLecturerDashboard_LocksCrossCourseQuestionsShiftsAndAppeals()
    {
        await using var db = CreateDbContext();

        var headA = CreateUser(UserRole.DepartmentHead, "dash-head-a");
        var headB = CreateUser(UserRole.DepartmentHead, "dash-head-b");
        var lecturerA = CreateUser(UserRole.Lecturer, "dash-lec-a");
        var lecturerB = CreateUser(UserRole.Lecturer, "dash-lec-b");
        var student = CreateUser(UserRole.Student, "dash-sv");
        student.StudentCode = "SE181001";

        var programA = new AcademicProgram { Id = Guid.NewGuid(), Code = "SE", Name = "SE", DepartmentHeadUserId = headA.Id, IsActive = true };
        var programB = new AcademicProgram { Id = Guid.NewGuid(), Code = "AI", Name = "AI", DepartmentHeadUserId = headB.Id, IsActive = true };
        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", AcademicProgramId = programA.Id, DepartmentHeadUserId = headA.Id, IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "AIL303", Name = "AIL303", AcademicProgramId = programB.Id, DepartmentHeadUserId = headB.Id, IsActive = true };

        var rubricA = BuildValidRubricEntity(courseA.Id);
        var rubricB = BuildValidRubricEntity(courseB.Id);

        var qA_OwnRev = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = courseA.Id, RubricId = rubricA.Id, Title = "A Own NeedsRevision",
            Content = "C", SampleAnswer = new string('a', 60), ApprovalStatus = ApprovalStatus.NeedsRevision,
            SubmittedBy = lecturerA.Id, IsActive = false
        };
        var qA_OtherRev = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = courseA.Id, RubricId = rubricA.Id, Title = "B NeedsRevision In Course A",
            Content = "C", SampleAnswer = new string('b', 60), ApprovalStatus = ApprovalStatus.NeedsRevision,
            SubmittedBy = lecturerB.Id, IsActive = false
        };
        var qB_Foreign = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = courseB.Id, RubricId = rubricB.Id, Title = "Course B Question",
            Content = "C", SampleAnswer = new string('c', 60), ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = lecturerB.Id, IsActive = false
        };

        var sessionA = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseA.Id, ExamStructureId = Guid.NewGuid(), Title = "FE PRN231", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        var sessionB = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseB.Id, ExamStructureId = Guid.NewGuid(), Title = "FE AIL303", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        var shiftA = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = sessionA.Id, ExaminerUserId = lecturerA.Id, ShiftName = "Ca A", RoomLab = "Lab 1", StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(1) };
        var shiftB = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = sessionB.Id, ExaminerUserId = lecturerB.Id, ShiftName = "Ca B", RoomLab = "Lab 2", StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(1) };
        var ticketA = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shiftA.Id, StudentId = student.Id, SeatNumber = 1, Status = ExamTicketStatus.Published };
        var ticketB = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shiftB.Id, StudentId = student.Id, SeatNumber = 1, Status = ExamTicketStatus.Published };

        var appealA_AssignedToA = new AppealRequest
        {
            Id = Guid.NewGuid(), TicketId = ticketA.Id, StudentId = student.Id, SessionId = sessionA.Id,
            Reason = "Phúc khảo A", Status = AppealStatus.AssignedToLecturer, AssignedTo = lecturerA.Id, OriginalScore = 6.5m
        };
        var appealA_PendingOffice = new AppealRequest
        {
            Id = Guid.NewGuid(), TicketId = ticketA.Id, StudentId = student.Id, SessionId = sessionA.Id,
            Reason = "Chờ xử lý A", Status = AppealStatus.SubmittedToExamOffice, AssignedTo = Guid.Empty, OriginalScore = 6.0m
        };
        var appealB_PendingOffice = new AppealRequest
        {
            Id = Guid.NewGuid(), TicketId = ticketB.Id, StudentId = student.Id, SessionId = sessionB.Id,
            Reason = "Chờ xử lý B", Status = AppealStatus.SubmittedToExamOffice, AssignedTo = Guid.Empty, OriginalScore = 5.5m
        };

        db.Users.AddRange(headA, headB, lecturerA, lecturerB, student);
        db.AcademicPrograms.AddRange(programA, programB);
        db.Courses.AddRange(courseA, courseB);
        db.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = courseA.Id, LecturerUserId = lecturerA.Id, AssignedByUserId = headA.Id, IsActive = true },
            new CourseLecturer { CourseId = courseB.Id, LecturerUserId = lecturerB.Id, AssignedByUserId = headB.Id, IsActive = true });
        db.Rubrics.AddRange(rubricA, rubricB);
        db.ExamQuestions.AddRange(qA_OwnRev, qA_OtherRev, qB_Foreign);
        db.OfficialExamSessions.AddRange(sessionA, sessionB);
        db.RealExamSessionShifts.AddRange(shiftA, shiftB);
        db.StudentExamTickets.AddRange(ticketA, ticketB);
        db.AppealRequests.AddRange(appealA_AssignedToA, appealA_PendingOffice, appealB_PendingOffice);
        await db.SaveChangesAsync();

        var handler = new GetLecturerDashboardQueryHandler(db);

        // 1. Lecturer A yêu cầu Course B -> FORBIDDEN
        (await handler.Handle(new GetLecturerDashboardQuery(lecturerA.Id, courseB.Id), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");

        // 2. Lecturer A xem dashboard tổng -> chỉ thấy câu của mình, ca mình làm Giám khảo, đơn giao mình và DepartmentPendingAppealsCount == 0
        var lecDash = await handler.Handle(new GetLecturerDashboardQuery(lecturerA.Id), CancellationToken.None);
        lecDash.IsSuccess.Should().BeTrue();
        lecDash.Value!.QuestionStats.TotalExamQuestions.Should().Be(1);
        lecDash.Value.QuestionsNeedingRevision.Should().ContainSingle(q => q.QuestionId == qA_OwnRev.Id);
        lecDash.Value.ShiftsPendingAudit.Should().ContainSingle(s => s.ShiftId == shiftA.Id);
        lecDash.Value.ActiveAppeals.Should().ContainSingle(a => a.AppealId == appealA_AssignedToA.Id);
        lecDash.Value.AssignedAppealsPendingCount.Should().Be(1);
        lecDash.Value.DepartmentPendingAppealsCount.Should().Be(0);

        // 3. Head A yêu cầu Course B -> FORBIDDEN
        (await handler.Handle(new GetLecturerDashboardQuery(headA.Id, courseB.Id), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");

        // 4. Head A xem dashboard tổng -> chỉ thấy dữ liệu của bộ môn A (Course A), không lộ Course B
        var headDash = await handler.Handle(new GetLecturerDashboardQuery(headA.Id), CancellationToken.None);
        headDash.IsSuccess.Should().BeTrue();
        headDash.Value!.QuestionStats.TotalExamQuestions.Should().Be(2);
        headDash.Value.ShiftsPendingAudit.Should().ContainSingle(s => s.ShiftId == shiftA.Id);
        headDash.Value.DepartmentPendingAppealsCount.Should().Be(1);
        headDash.Value.ActiveAppeals.Select(a => a.AppealId).Should().BeEquivalentTo([appealA_AssignedToA.Id, appealA_PendingOffice.Id]);

        // 5. LecturerController.GetDashboard không bao giờ fallback sang query ?lecturerId khi JWT trống hoặc khác
        var senderMock = new Mock<ISender>();
        var anonController = new LecturerController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        (await anonController.GetDashboard(courseId: null, lecturerId: headB.Id, CancellationToken.None))
            .Should().BeOfType<UnauthorizedResult>();
        senderMock.Verify(s => s.Send(It.IsAny<GetLecturerDashboardQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "P0-08b: GetLecturerClasses & ClassesController giới hạn Trưởng BM trong các môn phụ trách và chặn truy vấn môn ngoài bộ môn (403)")]
    public async Task P0_08_GetLecturerClasses_AndClassesController_ScopeDepartmentHeadToOwnedPrograms()
    {
        await using var db = CreateDbContext();

        var headA = CreateUser(UserRole.DepartmentHead, "cls-head-a");
        var headB = CreateUser(UserRole.DepartmentHead, "cls-head-b");
        var lecturerA = CreateUser(UserRole.Lecturer, "cls-lec-a");
        var lecturerB = CreateUser(UserRole.Lecturer, "cls-lec-b");
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", IsActive = true };

        var programA = new AcademicProgram { Id = Guid.NewGuid(), Code = "SE", Name = "SE", DepartmentHeadUserId = headA.Id, IsActive = true };
        var programB = new AcademicProgram { Id = Guid.NewGuid(), Code = "AI", Name = "AI", DepartmentHeadUserId = headB.Id, IsActive = true };
        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", SemesterId = semester.Id, AcademicProgramId = programA.Id, DepartmentHeadUserId = headA.Id, IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "AIL303", Name = "AIL303", SemesterId = semester.Id, AcademicProgramId = programB.Id, DepartmentHeadUserId = headB.Id, IsActive = true };

        var classA = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "Lớp SE1801", JoinCode = "SE1801", CourseId = courseA.Id, SemesterId = semester.Id, LecturerId = lecturerA.Id, IsActive = true };
        var classB = new Class { Id = Guid.NewGuid(), Code = "AI1801", Name = "Lớp AI1801", JoinCode = "AI1801", CourseId = courseB.Id, SemesterId = semester.Id, LecturerId = lecturerB.Id, IsActive = true };

        db.Users.AddRange(headA, headB, lecturerA, lecturerB);
        db.Semesters.Add(semester);
        db.AcademicPrograms.AddRange(programA, programB);
        db.Courses.AddRange(courseA, courseB);
        db.Classes.AddRange(classA, classB);
        await db.SaveChangesAsync();

        var handler = new GetLecturerClassesQueryHandler(db);

        // 1. Head A chỉ thấy Class A (không thấy Class B của ngành B)
        var headClasses = await handler.Handle(
            new GetLecturerClassesQuery(ActorUserId: headA.Id, ActorRole: UserRole.DepartmentHead),
            CancellationToken.None);
        headClasses.IsSuccess.Should().BeTrue();
        headClasses.Value!.Should().ContainSingle(c => c.ClassId == classA.Id);

        // 2. Head A lọc trực tiếp Course B -> FORBIDDEN
        var headForeignCourse = await handler.Handle(
            new GetLecturerClassesQuery(CourseId: courseB.Id, ActorUserId: headA.Id, ActorRole: UserRole.DepartmentHead),
            CancellationToken.None);
        headForeignCourse.IsSuccess.Should().BeFalse();
        headForeignCourse.Error.Should().StartWith("FORBIDDEN:");

        // 3. Lecturer A cố truyền LecturerId của Lecturer B -> FORBIDDEN
        var lecturerImpersonate = await handler.Handle(
            new GetLecturerClassesQuery(LecturerId: lecturerB.Id, ActorUserId: lecturerA.Id, ActorRole: UserRole.Lecturer),
            CancellationToken.None);
        lecturerImpersonate.IsSuccess.Should().BeFalse();
        lecturerImpersonate.Error.Should().StartWith("FORBIDDEN:");

        // 4. ClassesController truyền ActorUserId & ActorRole và ánh xạ FORBIDDEN sang HTTP 403
        GetLecturerClassesQuery? dispatchedQuery = null;
        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(s => s.Send(It.IsAny<GetLecturerClassesQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<List<LecturerClassItemDto>>>, CancellationToken>((q, _) => dispatchedQuery = (GetLecturerClassesQuery)q)
            .ReturnsAsync(Result<List<LecturerClassItemDto>>.Failure("FORBIDDEN: Không thuộc bộ môn quản lý."));

        var controller = new ClassesController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(headA.Id, UserRole.DepartmentHead) }
            }
        };

        var httpRes = await controller.GetLecturerClasses(courseId: courseB.Id, cancellationToken: CancellationToken.None);
        dispatchedQuery.Should().NotBeNull();
        dispatchedQuery!.ActorUserId.Should().Be(headA.Id);
        dispatchedQuery.ActorRole.Should().Be(UserRole.DepartmentHead);
        httpRes.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact(DisplayName = "P0-11a: AbuseProtection định tuyến bucket class-join (mặc định 15 req/phút), chặn 429 RATE_LIMITED độc lập theo sinh viên và không ảnh hưởng bucket khác")]
    public async Task P0_11_AbuseProtection_ClassJoinBucket_DefaultsTo15_ValidatesAndRateLimitsAllJoinRoutes()
    {
        // 1. Kiểm tra mặc định 15 permits & validation > 0
        new AbuseProtectionOptions().ClassJoinPermits.Should().Be(15);
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:Abuse:ClassJoinPermits"] = "0" })
            .Build();
        Action act = () => AbuseProtection.BindAndValidateOptions(invalidConfig);
        act.Should().Throw<InvalidOperationException>();

        // 2. Kiểm tra phân loại Category cho đủ 4 route join lớp và không ăn nhầm GET hoặc MF-05 shift join
        static string ResolveCategory(string method, string path)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = method;
            ctx.Request.Path = path;
            return AbuseProtection.Category(ctx);
        }

        ResolveCategory(HttpMethods.Post, "/api/v1/classes/join").Should().Be("class-join");
        ResolveCategory(HttpMethods.Post, "/api/v1/student/classes/join").Should().Be("class-join");
        ResolveCategory(HttpMethods.Post, $"/api/v1/classes/{Guid.NewGuid()}/join").Should().Be("class-join");
        ResolveCategory(HttpMethods.Post, $"/api/v1/student/classes/{Guid.NewGuid()}/join").Should().Be("class-join");
        ResolveCategory(HttpMethods.Get, "/api/v1/classes/join").Should().Be("general");
        ResolveCategory(HttpMethods.Post, $"/api/v1/student/in-class-shifts/{Guid.NewGuid()}/join").Should().Be("general");

        // 3. Kiểm thử pipeline HTTP thật với ClassJoinPermits = 3
        var dbName = $"p0-11-rate-{Guid.NewGuid()}";
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = "p0-11-class-join-rate-limit-test-key-1234567890",
            ["Security:Abuse:ClassJoinPermits"] = "3",
            ["Security:Abuse:GeneralPermits"] = "20"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(dbName));
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddApiErrorContracts();
        builder.Services.AddApiAbuseProtection(builder.Configuration);
        builder.Services.AddOralExamRateLimiting(builder.Configuration);

        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.88");
            return next(context);
        });
        app.UseRouting();
        app.UseApiErrorContracts();
        app.UseAuthentication();
        app.UseApiAbuseProtection();
        app.UseAuthorization();
        app.MapPost("/api/v1/classes/join", () => Results.Ok()).RequireAuthorization();
        app.MapPost("/api/v1/student/classes/{classId:guid}/join", () => Results.Ok()).RequireAuthorization();
        app.MapGet("/api/v1/classes/my-classes", () => Results.Ok()).RequireAuthorization();
        await app.StartAsync();

        var studentA = CreateUser(UserRole.Student, "rate-sv-a");
        var studentB = CreateUser(UserRole.Student, "rate-sv-b");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.AddRange(studentA, studentB);
            await db.SaveChangesAsync();
        }

        var issuer = new JwtAccessTokenIssuer(app.Configuration);
        var tokenA = issuer.Issue(studentA.Id, studentA.Email, studentA.FullName, UserRole.Student, authVersion: 1).Token;
        var tokenB = issuer.Issue(studentB.Id, studentB.Email, studentB.FullName, UserRole.Student, authVersion: 1).Token;

        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(url) };

        // 3 lượt đầu của student A (gồm cả 2 biến thể route) đều thành công
        for (var i = 0; i < 3; i++)
        {
            var route = i == 2 ? $"/api/v1/student/classes/{Guid.NewGuid()}/join" : "/api/v1/classes/join";
            using var req = new HttpRequestMessage(HttpMethod.Post, route);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Lượt thứ 4 của student A phải nhận 429 RATE_LIMITED kèm Retry-After
        using var throttledReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/classes/join");
        throttledReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var throttledResp = await client.SendAsync(throttledReq);
        throttledResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        throttledResp.Headers.RetryAfter.Should().NotBeNull();
        var body = JsonDocument.Parse(await throttledResp.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("errorCode").GetString().Should().Be("RATE_LIMITED");

        // Student B trên cùng IP vẫn không bị ảnh hưởng
        using var studentBReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/classes/join");
        studentBReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        (await client.SendAsync(studentBReq)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Bucket general của student A vẫn không bị cạn
        using var generalReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/classes/my-classes");
        generalReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await client.SendAsync(generalReq)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "P0-11b: JoinClassCommandHandler kiểm tra lại IsActive/IsJoinEnabled/AllowSelfEnrollment và JoinPasswordHash bên trong khóa chống TOCTOU")]
    public async Task P0_11_JoinClassHandler_ReverifiesClassStateAndPasswordHashInsideEnrollmentLock()
    {
        var realHasher = new Pbkdf2PasswordHasher();
        var initialHash = realHasher.Hash("Secret@123");
        var rotatedHash = realHasher.Hash("NewSecret@999");

        async Task<Result<JoinClassResponseDto>> RunConcurrentMutationScenarioAsync(Action<Class> mutateDuringVerify)
        {
            var dbName = $"p0-11-toctou-{Guid.NewGuid()}";
            Guid studentId;
            Guid classId;

            await using (var seedDb = CreateDbContext(dbName))
            {
                var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", IsActive = true };
                var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", SemesterId = semester.Id, IsActive = true };
                var lecturer = CreateUser(UserRole.Lecturer, "join-lec");
                var student = CreateUser(UserRole.Student, "join-sv");
                studentId = student.Id;

                var cls = new Class
                {
                    Id = Guid.NewGuid(),
                    Code = "SE1899",
                    Name = "Lớp SE1899",
                    JoinCode = "JOIN1899",
                    CourseId = course.Id,
                    SemesterId = semester.Id,
                    LecturerId = lecturer.Id,
                    MaxStudents = 30,
                    JoinPasswordHash = initialHash,
                    IsJoinEnabled = true,
                    AllowSelfEnrollment = true,
                    IsActive = true
                };
                classId = cls.Id;

                seedDb.Semesters.Add(semester);
                seedDb.Courses.Add(course);
                seedDb.Users.AddRange(lecturer, student);
                seedDb.Classes.Add(cls);
                await seedDb.SaveChangesAsync();
            }

            await using var handlerDb = CreateDbContext(dbName);
            var mutatingHasher = new MutatingDuringVerifyPasswordHasher(realHasher, () =>
            {
                using var concurrentDb = CreateDbContext(dbName);
                var fresh = concurrentDb.Classes.First(c => c.Id == classId);
                mutateDuringVerify(fresh);
                concurrentDb.SaveChanges();
            });

            var handler = new JoinClassCommandHandler(handlerDb, mutatingHasher, new JoinClassCommandValidator());
            var result = await handler.Handle(new JoinClassCommand(studentId, "JOIN1899", "Secret@123", classId), CancellationToken.None);

            await using var verifyDb = CreateDbContext(dbName);
            (await verifyDb.ClassEnrollments.CountAsync(e => e.ClassId == classId && e.StudentId == studentId))
                .Should().Be(0, "tuyệt đối không được ghi danh khi trạng thái lớp hoặc mật khẩu đã đổi trước khi vào khóa");

            return result;
        }

        // 1. Mật khẩu lớp bị reset trong khoảng thời gian chạy PBKDF2
        var passwordRotatedResult = await RunConcurrentMutationScenarioAsync(c => c.JoinPasswordHash = rotatedHash);
        passwordRotatedResult.IsSuccess.Should().BeFalse();
        passwordRotatedResult.Error.Should().StartWith("INVALID_PASSWORD:");

        // 2. Lớp bị khóa IsJoinEnabled = false trước khi vào khóa
        var joinDisabledResult = await RunConcurrentMutationScenarioAsync(c => c.IsJoinEnabled = false);
        joinDisabledResult.IsSuccess.Should().BeFalse();
        joinDisabledResult.Error.Should().StartWith("JOIN_DISABLED:");

        // 3. Lớp bị tắt IsActive = false trước khi vào khóa
        var classDeactivatedResult = await RunConcurrentMutationScenarioAsync(c => c.IsActive = false);
        classDeactivatedResult.IsSuccess.Should().BeFalse();
        classDeactivatedResult.Error.Should().Contain("vô hiệu hóa");

        // 4. Lớp bị tắt AllowSelfEnrollment = false trước khi vào khóa
        var selfEnrollDisabledResult = await RunConcurrentMutationScenarioAsync(c => c.AllowSelfEnrollment = false);
        selfEnrollDisabledResult.IsSuccess.Should().BeFalse();
        selfEnrollDisabledResult.Error.Should().Contain("tự ghi danh");
    }
}
