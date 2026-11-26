using System;
using System.Collections.Generic;
using System.Linq;
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
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.Commands.DeleteCourseClo;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.ReturnExamSetForRevision;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAvailableExamSetQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.Commands.UpdateRubric;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank;

public sealed class P1HardeningMf03ExamSetsRubricAndCloTests
{
    private const string ValidSampleAnswer =
        "Đây là câu trả lời mẫu đạt chuẩn học thuật tối thiểu 50 ký tự cho bài thi vấn đáp chuyên ngành kỹ thuật phần mềm.";

    private static OralExamDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"p1-mf03-{Guid.NewGuid():N}")
            .Options);

    private sealed class ConcurrencyThrowingDbContext(DbContextOptions<OralExamDbContext> options)
        : OralExamDbContext(options)
    {
        public bool ThrowOnNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnNextSave)
            {
                ThrowOnNextSave = false;
                throw new DbUpdateConcurrencyException("Simulated concurrent update conflict.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    private static List<RubricCriterionDraftDto> ValidCriteria() =>
    [
        new("Hiểu khái niệm cốt lõi", 5.00m, 0.50m, BloomLevel.Understand, "Trình bày chính xác bản chất", 1),
        new("Phân tích & ví dụ thực tế", 5.00m, 0.50m, BloomLevel.Analyze, "Đưa ví dụ rõ ràng", 2)
    ];

    private static Rubric BuildRubricEntity(Guid courseId, decimal score1 = 5.00m, decimal score2 = 5.00m, decimal weight1 = 0.50m, decimal weight2 = 0.50m)
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = "Rubric 10.00 chuẩn",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Tiêu chí 1",
            MaxScore = score1,
            Weight = weight1,
            BloomLevel = BloomLevel.Understand,
            OrderIndex = 1
        });
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Tiêu chí 2",
            MaxScore = score2,
            Weight = weight2,
            BloomLevel = BloomLevel.Analyze,
            OrderIndex = 2
        });
        return rubric;
    }

    [Fact(DisplayName = "P1-01A: UpdateExamSet cho phép sửa DRAFT/REJECTED về DRAFT, kiểm tra TotalQuestions/M3 guards & chặn trạng thái khóa")]
    public async Task P1_01_UpdateExamSet_AllowsOwnerEditRejectedBackToDraft_EnforcesM3Guards_AndTotalQuestions()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p101a@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec1 = new User { Id = Guid.NewGuid(), Email = "lec1-p101a@fpt.edu.vn", FullName = "GV 1", Role = UserRole.Lecturer, IsActive = true };
        var lec2 = new User { Id = Guid.NewGuid(), Email = "lec2-p101a@fpt.edu.vn", FullName = "GV 2", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct 2Q", TotalQuestions = 2, RequiredSetCount = 3, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);

        var qSample = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q Sample", Content = "S", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "C1", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q2", Content = "C2", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q3 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q3", Content = "C3", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q4 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q4", Content = "C4", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q5 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q5", Content = "C5", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var q6 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q6", Content = "C6", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        var sampleSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = head.Id, SetCode = "SAMPLE-1", Status = ExamSetStatus.Draft, IsSampleSet = true, IsActive = true };
        sampleSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = sampleSet.Id, ExamQuestionId = qSample.Id, OrderIndex = 1 });

        var officialSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec2.Id, SetCode = "OFFICIAL-1", Status = ExamSetStatus.Official, IsActive = true };
        officialSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = officialSet.Id, ExamQuestionId = q4.Id, OrderIndex = 1 });

        var otherOwnDraftSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "OWN-OTHER", Status = ExamSetStatus.Draft, IsActive = true };
        otherOwnDraftSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = otherOwnDraftSet.Id, ExamQuestionId = q5.Id, OrderIndex = 1 });

        var rejectedSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "SET-REJ-01", Status = ExamSetStatus.Rejected, ReviewNotes = "Thay câu bị trùng", IsActive = true };
        rejectedSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = rejectedSet.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });
        rejectedSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = rejectedSet.Id, ExamQuestionId = q2.Id, OrderIndex = 2 });

        var ticketBoundDraft = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "SET-TICKET", Status = ExamSetStatus.Draft, IsActive = true };
        ticketBoundDraft.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = ticketBoundDraft.Id, ExamQuestionId = q6.Id, OrderIndex = 1 });
        db.StudentExamTickets.Add(new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = Guid.NewGuid(), StudentId = Guid.NewGuid(), ExamSetId = ticketBoundDraft.Id, SeatNumber = 1, Status = ExamTicketStatus.Scheduled });

        db.Users.AddRange(head, lec1, lec2);
        db.Courses.Add(course);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec1.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec2.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(qSample, q1, q2, q3, q4, q5, q6);
        db.ExamSets.AddRange(sampleSet, officialSet, otherOwnDraftSet, rejectedSet, ticketBoundDraft);
        await db.SaveChangesAsync();

        var updateHandler = new UpdateExamSetCommandHandler(db);

        // 1. GV khác không được sửa bộ đề của lec1 -> FORBIDDEN
        (await updateHandler.Handle(new UpdateExamSetCommand(rejectedSet.Id, "SET-HACK", [q1.Id, q3.Id], lec2.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);

        // 2. Chặn sửa bộ đề OFFICIAL hoặc đã gán StudentExamTickets -> CONFLICT
        (await updateHandler.Handle(new UpdateExamSetCommand(officialSet.Id, "OFFICIAL-MOD", [q1.Id, q2.Id], head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");
        (await updateHandler.Handle(new UpdateExamSetCommand(ticketBoundDraft.Id, "SET-TICKET-2", [q1.Id, q3.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");

        // 3. Chặn lệch TotalQuestions (structure.TotalQuestions = 2, gửi 1 câu) -> UNPROCESSABLE
        (await updateHandler.Handle(new UpdateExamSetCommand(rejectedSet.Id, null, [q1.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");

        // 4. Chặn chọn câu Đề mẫu, câu thuộc bộ đề OFFICIAL cùng đợt, câu thuộc bộ đề khác của chính GV, và trùng SetCode active
        (await updateHandler.Handle(new UpdateExamSetCommand(rejectedSet.Id, null, [q1.Id, qSample.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");
        (await updateHandler.Handle(new UpdateExamSetCommand(rejectedSet.Id, null, [q1.Id, q4.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");
        (await updateHandler.Handle(new UpdateExamSetCommand(rejectedSet.Id, null, [q1.Id, q5.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");
        (await updateHandler.Handle(new UpdateExamSetCommand(rejectedSet.Id, "OWN-OTHER", [q1.Id, q3.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");

        // 5. Sửa hợp lệ bộ đề REJECTED (giữ lại q1 của chính nó + thay q2 bằng q3, đổi SetCode) -> Status về DRAFT và nộp duyệt lại thành công
        var updated = await updateHandler.Handle(new UpdateExamSetCommand(
            rejectedSet.Id, "SET-REJ-FIXED", [q1.Id, q3.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        updated.IsSuccess.Should().BeTrue(because: updated.Error);
        updated.Value.SetCode.Should().Be("SET-REJ-FIXED");
        updated.Value.Status.Should().Be(ExamSetStatus.Draft);
        updated.Value.Questions.Select(q => q.QuestionId).Should().ContainInOrder(q1.Id, q3.Id);

        db.AuditLogs.Any(a => a.Action == "UPDATE_EXAM_SET" && a.EntityId == rejectedSet.Id).Should().BeTrue();

        var resubmit = await new SubmitExamSetForReviewCommandHandler(db).Handle(
            new SubmitExamSetForReviewCommand(rejectedSet.Id, lec1.Id, UserRole.Lecturer), CancellationToken.None);
        resubmit.IsSuccess.Should().BeTrue(because: resubmit.Error);
    }

    [Fact(DisplayName = "P1-01B: DeleteExamSet xóa mềm DRAFT/REJECTED, giải phóng câu hỏi về available pool & cho phép tái sử dụng SetCode")]
    public async Task P1_01_DeleteExamSet_SoftDeletesDraftOrRejected_ReleasesQuestions_AndAllowsReusingSetCode()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p101b@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec1 = new User { Id = Guid.NewGuid(), Email = "lec1-p101b@fpt.edu.vn", FullName = "GV 1", Role = UserRole.Lecturer, IsActive = true };
        var lec2 = new User { Id = Guid.NewGuid(), Email = "lec2-p101b@fpt.edu.vn", FullName = "GV 2", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct Del", TotalQuestions = 1, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "C1", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        var draftSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "PRN-REUSE-01", Status = ExamSetStatus.Draft, IsActive = true };
        draftSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = draftSet.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });

        var submittedSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec1.Id, SetCode = "PRN-SUB-01", Status = ExamSetStatus.SubmittedForReview, IsActive = true };

        db.Users.AddRange(head, lec1, lec2);
        db.Courses.Add(course);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec1.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec2.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(q1);
        db.ExamSets.AddRange(draftSet, submittedSet);
        await db.SaveChangesAsync();

        var deleteHandler = new DeleteExamSetCommandHandler(db);

        // Chặn GV khác xóa (FORBIDDEN) & chặn xóa bộ đề SUBMITTED_FOR_REVIEW (CONFLICT)
        (await deleteHandler.Handle(new DeleteExamSetCommand(draftSet.Id, lec2.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await deleteHandler.Handle(new DeleteExamSetCommand(submittedSet.Id, lec1.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");

        // Xóa mềm hợp lệ -> IsActive = false, ExamSetQuestions được gỡ sạch, ghi AuditLog
        var deleted = await deleteHandler.Handle(new DeleteExamSetCommand(draftSet.Id, lec1.Id, UserRole.Lecturer), CancellationToken.None);
        deleted.IsSuccess.Should().BeTrue(because: deleted.Error);

        var reloaded = await db.ExamSets.AsNoTracking().SingleAsync(es => es.Id == draftSet.Id);
        reloaded.IsActive.Should().BeFalse();
        (await db.ExamSetQuestions.CountAsync(esq => esq.ExamSetId == draftSet.Id)).Should().Be(0);
        db.AuditLogs.Any(a => a.Action == "DELETE_EXAM_SET" && a.EntityId == draftSet.Id).Should().BeTrue();

        // Câu hỏi q1 được trả lại danh sách khả dụng của lec1
        var available = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new GetAvailableExamSetQuestionsQuery(structure.Id, lec1.Id, UserRole.Lecturer), CancellationToken.None);
        available.IsSuccess.Should().BeTrue();
        available.Value.Items.Select(i => i.QuestionId).Should().Contain(q1.Id);

        // Tái sử dụng đúng SetCode "PRN-REUSE-01" của bộ đề đã xóa mềm -> thành công!
        var recreated = await new CreateExamSetCommandHandler(db).Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "PRN-REUSE-01", [q1.Id], lec1.Id, UserRole.Lecturer), CancellationToken.None);
        recreated.IsSuccess.Should().BeTrue(because: recreated.Error);
        recreated.Value.SetCode.Should().Be("PRN-REUSE-01");
    }

    [Fact(DisplayName = "P1-01C: OfficialExamCurationController ánh xạ PUT (200/403/404/409/422) và DELETE (204/403/404/409) chuẩn RFC 7807")]
    public async Task P1_01_OfficialExamCurationController_PutAndDeleteExamSet_Return200_204_AndRfc7807Problems()
    {
        var actorId = Guid.NewGuid();
        var examSetId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        var controller = new OfficialExamCurationController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(actorId, UserRole.Lecturer) }
            }
        };

        // PUT 200 OK
        senderMock.Setup(s => s.Send(It.IsAny<UpdateExamSetCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamSetDto>.Success(new ExamSetDto { Id = examSetId, SetCode = "SET-OK", Status = ExamSetStatus.Draft }));
        (await controller.UpdateExamSet(examSetId, new UpdateExamSetRequest { SetCode = "SET-OK", QuestionIds = [Guid.NewGuid()] }))
            .Should().BeOfType<OkObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status200OK);

        // PUT 403 / 404 / 409 / 422 ProblemDetails
        foreach (var (error, expectedStatus) in new[]
        {
            (OfficialExamErrors.Forbidden, StatusCodes.Status403Forbidden),
            (OfficialExamErrors.ExamSetNotFound, StatusCodes.Status404NotFound),
            ("CONFLICT: Bộ đề đã duyệt.", StatusCodes.Status409Conflict),
            ("UNPROCESSABLE: Lệch số lượng câu.", StatusCodes.Status422UnprocessableEntity)
        })
        {
            senderMock.Setup(s => s.Send(It.IsAny<UpdateExamSetCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ExamSetDto>.Failure(error));
            var res = (await controller.UpdateExamSet(examSetId, new UpdateExamSetRequest { SetCode = "X" }))
                .Should().BeOfType<ObjectResult>().Subject;
            res.StatusCode.Should().Be(expectedStatus);
            res.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(expectedStatus);
        }

        // DELETE 204 NoContent
        senderMock.Setup(s => s.Send(It.IsAny<DeleteExamSetCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        (await controller.DeleteExamSet(examSetId))
            .Should().BeOfType<NoContentResult>();

        // DELETE 409 Conflict ProblemDetails
        senderMock.Setup(s => s.Send(It.IsAny<DeleteExamSetCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("CONFLICT: Không thể xóa bộ đề OFFICIAL."));
        var delConflict = (await controller.DeleteExamSet(examSetId)).Should().BeOfType<ObjectResult>().Subject;
        delConflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        delConflict.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact(DisplayName = "P1-01D: ReturnExamSetForRevision đưa câu hỏi inline riêng tư PENDING_CURATION sang NEEDS_REVISION kèm ReviewNotes")]
    public async Task P1_01_ReturnExamSetForRevision_TransitionsPrivateInlineQuestionsToNeedsRevision_WithNotes()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p101d@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-p101d@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWR302", Name = "Req", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct Inline Rev", TotalQuestions = 2, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);
        var bankQ = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Bank Q", Content = "Bank", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(bankQ);
        await db.SaveChangesAsync();

        var created = await new CreateExamSetCommandHandler(db).Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWR-INLINE-REV", [bankQ.Id], lec.Id, UserRole.Lecturer, false,
            [
                new CreateInlineExamQuestionDto
                {
                    Title = "Inline cần chỉnh sửa",
                    Content = "Phân tích kỹ thuật elicitation bằng workshop.",
                    SampleAnswer = ValidSampleAnswer,
                    Difficulty = QuestionDifficulty.Medium,
                    BloomLevel = BloomLevel.Analyze,
                    Rubric = new RubricDraftDto("Rubric Inline", null, 10.00m, ValidCriteria())
                }
            ]), CancellationToken.None);
        created.IsSuccess.Should().BeTrue(because: created.Error);
        var inlineQId = created.Value.Questions.Single(q => q.QuestionId != bankQ.Id).QuestionId;

        (await new SubmitExamSetForReviewCommandHandler(db).Handle(
            new SubmitExamSetForReviewCommand(created.Value.Id, lec.Id, UserRole.Lecturer), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var returned = await new ReturnExamSetForRevisionCommandHandler(db).Handle(
            new ReturnExamSetForRevisionCommand(created.Value.Id, "Vui lòng bổ sung tiêu chí phân tích cho câu inline.", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        returned.IsSuccess.Should().BeTrue(because: returned.Error);

        var reloadedInline = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == inlineQId);
        reloadedInline.ApprovalStatus.Should().Be(ApprovalStatus.NeedsRevision);
        reloadedInline.ReviewNotes.Should().Be("Vui lòng bổ sung tiêu chí phân tích cho câu inline.");
        reloadedInline.IsActive.Should().BeFalse();

        // Câu kho APPROVED không bị ảnh hưởng
        var reloadedBank = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == bankQ.Id);
        reloadedBank.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        reloadedBank.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "P1-02A: ApproveExamSet giữ nguyên ApprovedBy/ApprovedAt của câu đã APPROVED và đóng QuestionReviewRequestItem PENDING")]
    public async Task P1_02_ApproveExamSet_PreservesAlreadyApprovedQuestionMetadata_AndClosesPendingReviewItems()
    {
        await using var db = CreateDbContext();

        var originalHeadId = Guid.NewGuid();
        var originalApprovedAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var head = new User { Id = Guid.NewGuid(), Email = "head-p102a@fpt.edu.vn", FullName = "Head New", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-p102a@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN222", Name = "PRN222", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct P102", TotalQuestions = 2, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);

        var qAlreadyApproved = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu đã duyệt trước đó",
            Content = "Nội dung cũ",
            SampleAnswer = ValidSampleAnswer,
            ApprovalStatus = ApprovalStatus.Approved,
            ApprovedBy = originalHeadId,
            ApprovedAt = originalApprovedAt,
            IsActive = true
        };
        var qPending = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu đang chờ duyệt",
            Content = "Nội dung mới",
            SampleAnswer = ValidSampleAnswer,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            SubmittedBy = lec.Id,
            IsActive = false
        };

        var reviewRequest = new QuestionReviewRequest
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SubmittedBy = lec.Id,
            SubmittedAt = DateTime.UtcNow,
            Version = 0,
            Items =
            [
                new QuestionReviewRequestItem
                {
                    Id = Guid.NewGuid(),
                    QuestionId = qPending.Id,
                    UsageScope = "exam",
                    Decision = "PENDING"
                }
            ]
        };

        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            CreatedBy = lec.Id,
            SetCode = "PRN222-P102-01",
            Status = ExamSetStatus.SubmittedForReview,
            IsActive = true
        };
        examSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = qAlreadyApproved.Id, OrderIndex = 1 });
        examSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = qPending.Id, OrderIndex = 2 });

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(qAlreadyApproved, qPending);
        db.QuestionReviewRequests.Add(reviewRequest);
        db.ExamSets.Add(examSet);
        await db.SaveChangesAsync();

        var result = await new ApproveExamSetCommandHandler(db).Handle(
            new ApproveExamSetCommand(examSet.Id, "Duyệt hợp lệ", head.Id, UserRole.DepartmentHead), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value.QuestionsCascadedCount.Should().Be(1);

        var reloadedOld = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == qAlreadyApproved.Id);
        reloadedOld.ApprovedBy.Should().Be(originalHeadId);
        reloadedOld.ApprovedAt.Should().Be(originalApprovedAt);

        var reloadedPending = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == qPending.Id);
        reloadedPending.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        reloadedPending.ApprovedBy.Should().Be(head.Id);
        reloadedPending.IsActive.Should().BeTrue();

        var closedItem = await db.QuestionReviewRequestItems.Include(i => i.Request).SingleAsync(i => i.QuestionId == qPending.Id);
        closedItem.Decision.Should().Be(ApprovalStatus.Approved);
        closedItem.ReviewedBy.Should().Be(head.Id);
        closedItem.ReviewedAt.Should().NotBeNull();
        closedItem.Request.Version.Should().Be(1);
    }

    [Fact(DisplayName = "P1-02B: ApproveExamSet chặn tự duyệt câu lẻ (FORBIDDEN) và chặn câu chưa duyệt vi phạm Invariant #7 (UNPROCESSABLE)")]
    public async Task P1_02_ApproveExamSet_BlocksSelfApprovalOfIndividualQuestion_AndEnforcesInvariant7()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p102b@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-p102b@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "Testing", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct Guard", TotalQuestions = 1, IsActive = true };

        var validRubric = BuildRubricEntity(course.Id);
        var threeDecimalRubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Bad 3 decimals",
            TotalMaxScore = 10.00m,
            IsActive = true,
            Criteria =
            {
                new RubricCriterion { CriterionName = "C1", MaxScore = 3.334m, Weight = 0.34m, OrderIndex = 1 },
                new RubricCriterion { CriterionName = "C2", MaxScore = 3.333m, Weight = 0.33m, OrderIndex = 2 },
                new RubricCriterion { CriterionName = "C3", MaxScore = 3.333m, Weight = 0.33m, OrderIndex = 3 }
            }
        };
        var wrongSumRubric = BuildRubricEntity(course.Id, 4.00m, 4.00m);

        var qSelfAuthored = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = validRubric.Id, Title = "Self Q", Content = "C", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.SubmittedForReview, SubmittedBy = head.Id, IsActive = false };
        var qShortAnswer = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = validRubric.Id, Title = "Short Q", Content = "C", SampleAnswer = "Quá ngắn", ApprovalStatus = ApprovalStatus.SubmittedForReview, SubmittedBy = lec.Id, IsActive = false };
        var qThreeDecimals = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = threeDecimalRubric.Id, Title = "3Dec Q", Content = "C", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.SubmittedForReview, SubmittedBy = lec.Id, IsActive = false };
        var qWrongSum = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = wrongSumRubric.Id, Title = "WrongSum Q", Content = "C", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.SubmittedForReview, SubmittedBy = lec.Id, IsActive = false };

        ExamSet MakeSet(string code, ExamQuestion q)
        {
            var es = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec.Id, SetCode = code, Status = ExamSetStatus.SubmittedForReview, IsActive = true };
            es.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = es.Id, ExamQuestionId = q.Id, OrderIndex = 1 });
            return es;
        }

        var setSelfQ = MakeSet("S-SELF-Q", qSelfAuthored);
        var setShort = MakeSet("S-SHORT", qShortAnswer);
        var set3Dec = MakeSet("S-3DEC", qThreeDecimals);
        var setWrong = MakeSet("S-WRONG", qWrongSum);

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.Rubrics.AddRange(validRubric, threeDecimalRubric, wrongSumRubric);
        db.ExamQuestions.AddRange(qSelfAuthored, qShortAnswer, qThreeDecimals, qWrongSum);
        db.ExamSets.AddRange(setSelfQ, setShort, set3Dec, setWrong);
        await db.SaveChangesAsync();

        var handler = new ApproveExamSetCommandHandler(db);

        (await handler.Handle(new ApproveExamSetCommand(setSelfQ.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await handler.Handle(new ApproveExamSetCommand(setShort.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");
        (await handler.Handle(new ApproveExamSetCommand(set3Dec.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");
        (await handler.Handle(new ApproveExamSetCommand(setWrong.Id, null, head.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("UNPROCESSABLE:");

        (await db.ExamSets.AsNoTracking().CountAsync(es => es.Status == ExamSetStatus.Official)).Should().Be(0);
    }

    [Fact(DisplayName = "P1-03A: Thống nhất chặn MaxScore 3 chữ số thập phân, CriterionName rỗng/>200 và clamp Weight >= 0.01 trên mọi handler Rubric")]
    public async Task P1_03_RubricPrecisionAndWeightNormalization_UnifiedAcrossHandlers()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p103a@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-p103a@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", DepartmentHeadUserId = head.Id, IsActive = true };
        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        await db.SaveChangesAsync();

        var threeDecimalCriteria = new List<RubricCriterionDraftDto>
        {
            new("C1", 3.334m, 0.34m, BloomLevel.Understand, "D1", 1),
            new("C2", 3.333m, 0.33m, BloomLevel.Apply, "D2", 2),
            new("C3", 3.333m, 0.33m, BloomLevel.Analyze, "D3", 3)
        };
        var blankNameCriteria = new List<RubricCriterionDraftDto>
        {
            new("   ", 5.00m, 0.50m, BloomLevel.Understand, "D1", 1),
            new("C2", 5.00m, 0.50m, BloomLevel.Apply, "D2", 2)
        };
        var tinyWeightCriteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí chính", 9.50m, 0.999m, BloomLevel.Analyze, "D1", 1),
            new("Tiêu chí nhỏ", 0.50m, 0.001m, BloomLevel.Remember, "D2", 2)
        };

        // 1. CreateRubricCommandHandler
        var createRubricHandler = new CreateRubricCommandHandler(db);
        (await createRubricHandler.Handle(new CreateRubricCommand(lec.Id, course.Id, "Bad 3Dec", null, threeDecimalCriteria), CancellationToken.None))
            .Error.Should().StartWith("INVARIANT:");
        (await createRubricHandler.Handle(new CreateRubricCommand(lec.Id, course.Id, "Bad Name", null, blankNameCriteria), CancellationToken.None))
            .Error.Should().StartWith("VALIDATION:");

        var createdTiny = await createRubricHandler.Handle(new CreateRubricCommand(lec.Id, course.Id, "Tiny Weight", null, tinyWeightCriteria), CancellationToken.None);
        createdTiny.IsSuccess.Should().BeTrue(because: createdTiny.Error);
        createdTiny.Value.Criteria.Should().OnlyContain(c => c.Weight >= 0.01m && c.Weight <= 1.00m);
        createdTiny.Value.Criteria.Single(c => c.CriterionName == "Tiêu chí nhỏ").Weight.Should().Be(0.01m);

        // 2. CreateDraftQuestionCommandHandler + Validator
        var draftHandler = new CreateDraftQuestionCommandHandler(db);
        var draft3DecCmd = new CreateDraftQuestionCommand(
            course.Id, lec.Id, "Draft 3Dec", "Content", ValidSampleAnswer, ["K1"],
            QuestionDifficulty.Medium, BloomLevel.Apply, QuestionSource.Manual, "practice", false, null,
            new RubricDraftDto("R-3Dec", null, 10.00m, threeDecimalCriteria));
        (await draftHandler.Handle(draft3DecCmd, CancellationToken.None)).IsSuccess.Should().BeFalse();
        new CreateDraftQuestionCommandValidator().Validate(draft3DecCmd).IsValid.Should().BeFalse();

        var draftTinyRes = await draftHandler.Handle(draft3DecCmd with { Rubric = new RubricDraftDto("R-Tiny", null, 10.00m, tinyWeightCriteria) }, CancellationToken.None);
        draftTinyRes.IsSuccess.Should().BeTrue(because: draftTinyRes.Error);
        var savedDraftQuestion = await db.PracticeQuestions.Include(q => q.Rubric).ThenInclude(r => r.Criteria).SingleAsync(q => q.Id == draftTinyRes.Value);
        savedDraftQuestion.Rubric.Criteria.Should().OnlyContain(c => c.Weight >= 0.01m && c.Weight <= 1.00m);

        // 3. BatchApproveQuestionsCommandHandler
        var batchApproveHandler = new BatchApproveQuestionsCommandHandler(db);
        var batch3DecRes = await batchApproveHandler.Handle(new BatchApproveQuestionsCommand(
            course.Id, head.Id,
            [
                new ApprovedQuestionItemDto("Batch 3Dec", "Content", QuestionDifficulty.Medium, BloomLevel.Analyze, "exam", QuestionSource.Manual, ValidSampleAnswer, ["K1"], false, null, new RubricDraftDto("R", null, 10.00m, threeDecimalCriteria))
            ]), CancellationToken.None);
        batch3DecRes.IsSuccess.Should().BeFalse();
        batch3DecRes.Error.Should().StartWith("INVARIANT:");

        var batchTinyRes = await batchApproveHandler.Handle(new BatchApproveQuestionsCommand(
            course.Id, head.Id,
            [
                new ApprovedQuestionItemDto("Batch Tiny", "Content", QuestionDifficulty.Medium, BloomLevel.Analyze, "exam", QuestionSource.Manual, ValidSampleAnswer, ["K1"], false, null, new RubricDraftDto("R-Batch-Tiny", null, 10.00m, tinyWeightCriteria))
            ]), CancellationToken.None);
        batchTinyRes.IsSuccess.Should().BeTrue(because: batchTinyRes.Error);
        var savedBatchRubric = await db.Rubrics.Include(r => r.Criteria).SingleAsync(r => r.Id == batchTinyRes.Value.ApprovedQuestions[0].RubricId);
        savedBatchRubric.Criteria.Should().OnlyContain(c => c.Weight >= 0.01m && c.Weight <= 1.00m);
    }

    [Fact(DisplayName = "P1-03B: BatchApproveQuestions chặn CloId/StructureId khác môn hoặc không hoạt động (INVARIANT)")]
    public async Task P1_03_BatchApproveQuestions_ValidatesCloIdAndStructureIdBelongToCourseAndAreActive()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p103b@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = head.Id, IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", DepartmentHeadUserId = head.Id, IsActive = true };

        var activeCloA = new CourseClo { Id = Guid.NewGuid(), CourseId = courseA.Id, CloCode = "CLO1", Description = "A1", BloomLevel = BloomLevel.Apply, IsActive = true };
        var inactiveCloA = new CourseClo { Id = Guid.NewGuid(), CourseId = courseA.Id, CloCode = "CLO2", Description = "A2", BloomLevel = BloomLevel.Apply, IsActive = false };
        var foreignCloB = new CourseClo { Id = Guid.NewGuid(), CourseId = courseB.Id, CloCode = "CLO1", Description = "B1", BloomLevel = BloomLevel.Apply, IsActive = true };

        var activeStructA = new ExamStructure { Id = Guid.NewGuid(), CourseId = courseA.Id, CreatedBy = head.Id, Name = "SA", IsActive = true };
        var inactiveStructA = new ExamStructure { Id = Guid.NewGuid(), CourseId = courseA.Id, CreatedBy = head.Id, Name = "SA-Off", IsActive = false };
        var foreignStructB = new ExamStructure { Id = Guid.NewGuid(), CourseId = courseB.Id, CreatedBy = head.Id, Name = "SB", IsActive = true };

        db.Users.Add(head);
        db.Courses.AddRange(courseA, courseB);
        db.CourseClos.AddRange(activeCloA, inactiveCloA, foreignCloB);
        db.ExamStructures.AddRange(activeStructA, inactiveStructA, foreignStructB);
        await db.SaveChangesAsync();

        var handler = new BatchApproveQuestionsCommandHandler(db);
        var rubric = new RubricDraftDto("R-Valid", null, 10.00m, ValidCriteria());

        ApprovedQuestionItemDto MakeItem(Guid? cloId, Guid? structId) =>
            new("Title", "Content", QuestionDifficulty.Medium, BloomLevel.Apply, "exam", QuestionSource.Manual, ValidSampleAnswer, ["K"], false, null, rubric, null, null, cloId, structId);

        (await handler.Handle(new BatchApproveQuestionsCommand(courseA.Id, head.Id, [MakeItem(foreignCloB.Id, activeStructA.Id)]), CancellationToken.None))
            .Error.Should().StartWith("INVARIANT:");
        (await handler.Handle(new BatchApproveQuestionsCommand(courseA.Id, head.Id, [MakeItem(inactiveCloA.Id, activeStructA.Id)]), CancellationToken.None))
            .Error.Should().StartWith("INVARIANT:");
        (await handler.Handle(new BatchApproveQuestionsCommand(courseA.Id, head.Id, [MakeItem(activeCloA.Id, foreignStructB.Id)]), CancellationToken.None))
            .Error.Should().StartWith("INVARIANT:");
        (await handler.Handle(new BatchApproveQuestionsCommand(courseA.Id, head.Id, [MakeItem(activeCloA.Id, inactiveStructA.Id)]), CancellationToken.None))
            .Error.Should().StartWith("INVARIANT:");

        (await db.ExamQuestions.CountAsync()).Should().Be(0);

        var ok = await handler.Handle(new BatchApproveQuestionsCommand(courseA.Id, head.Id, [MakeItem(activeCloA.Id, activeStructA.Id)]), CancellationToken.None);
        ok.IsSuccess.Should().BeTrue(because: ok.Error);
        (await db.ExamQuestions.CountAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "P1-03C: UpdateRubric chặn GV sửa Rubric câu hỏi của GV khác (FORBIDDEN) hoặc câu SUBMITTED_FOR_REVIEW (CONFLICT)")]
    public async Task P1_03_UpdateRubric_BlocksLecturerEditingOtherLecturersOrSubmittedOrApprovedRubric()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p103c@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec1 = new User { Id = Guid.NewGuid(), Email = "lec1-p103c@fpt.edu.vn", FullName = "GV 1", Role = UserRole.Lecturer, IsActive = true };
        var lec2 = new User { Id = Guid.NewGuid(), Email = "lec2-p103c@fpt.edu.vn", FullName = "GV 2", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRJ301", Name = "PRJ", DepartmentHeadUserId = head.Id, IsActive = true };

        var rubricLec1Draft = BuildRubricEntity(course.Id);
        var rubricLec1Submitted = BuildRubricEntity(course.Id);

        var qLec1Draft = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubricLec1Draft.Id, Title = "Draft L1", Content = "C", ApprovalStatus = ApprovalStatus.Draft, SubmittedBy = lec1.Id, IsActive = false };
        var qLec1Submitted = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubricLec1Submitted.Id, Title = "Submitted L1", Content = "C", ApprovalStatus = ApprovalStatus.SubmittedForReview, SubmittedBy = lec1.Id, IsActive = false };

        db.Users.AddRange(head, lec1, lec2);
        db.Courses.Add(course);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec1.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec2.Id, AssignedByUserId = head.Id, IsActive = true });
        db.Rubrics.AddRange(rubricLec1Draft, rubricLec1Submitted);
        db.ExamQuestions.Add(qLec1Draft);
        db.PracticeQuestions.Add(qLec1Submitted);
        await db.SaveChangesAsync();

        var handler = new UpdateRubricCommandHandler(db);

        // lec2 cố sửa rubric gắn câu nháp của lec1 -> FORBIDDEN
        var crossLecturer = await handler.Handle(new UpdateRubricCommand(lec2.Id, rubricLec1Draft.Id, "Hack L1 Rubric", null, ValidCriteria()), CancellationToken.None);
        crossLecturer.IsSuccess.Should().BeFalse();
        crossLecturer.Error.Should().StartWith("FORBIDDEN:");

        // lec1 cố sửa rubric đang gắn câu SUBMITTED_FOR_REVIEW -> CONFLICT
        var editSubmitted = await handler.Handle(new UpdateRubricCommand(lec1.Id, rubricLec1Submitted.Id, "Tamper Submitted", null, ValidCriteria()), CancellationToken.None);
        editSubmitted.IsSuccess.Should().BeFalse();
        editSubmitted.Error.Should().StartWith("CONFLICT:");

        // lec1 được sửa rubric của chính câu DRAFT mình tạo
        var ownDraftEdit = await handler.Handle(new UpdateRubricCommand(lec1.Id, rubricLec1Draft.Id, "Updated Draft Rubric", null, ValidCriteria()), CancellationToken.None);
        ownDraftEdit.IsSuccess.Should().BeTrue(because: ownDraftEdit.Error);

        // TBM được sửa rubric gắn câu SUBMITTED_FOR_REVIEW trước khi phát sinh bài thi
        var headEditSubmitted = await handler.Handle(new UpdateRubricCommand(head.Id, rubricLec1Submitted.Id, "TBM Polish Rubric", null, ValidCriteria()), CancellationToken.None);
        headEditSubmitted.IsSuccess.Should().BeTrue(because: headEditSubmitted.Error);
    }

    [Fact(DisplayName = "P1-03D: MutateQuestion chặn sửa/xóa mềm câu thực hành đã dùng thi trong lớp MF-05 (InClassExamSubmissions)")]
    public async Task P1_03_MutateQuestion_BlocksUpdateAndSoftDeleteWhenPracticeQuestionUsedInInClassExamSubmission()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p103d@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-p103d@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "CSD201", Name = "CSD", DepartmentHeadUserId = head.Id, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);
        var pq = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Used InClass Q",
            Content = "Content",
            SampleAnswer = ValidSampleAnswer,
            SubmittedBy = lec.Id,
            ApprovalStatus = ApprovalStatus.Draft,
            IsActive = false
        };
        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.Rubrics.Add(rubric);
        db.PracticeQuestions.Add(pq);
        db.InClassExamSubmissions.Add(new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            QuestionId = pq.Id,
            Transcript = "Bài nộp kiểm tra trên lớp của sinh viên"
        });
        await db.SaveChangesAsync();

        var handler = new MutateQuestionCommandHandler(db);
        var updateDraft = new UpdateQuestionDraftDto(
            "Updated", "Updated content", ValidSampleAnswer, ["K1"],
            QuestionDifficulty.Medium, BloomLevel.Apply,
            new RubricDraftDto("R", null, 10.00m, ValidCriteria()), null);

        var updateRes = await handler.Handle(new MutateQuestionCommand(pq.Id, lec.Id, UserRole.Lecturer, QuestionMutationKind.Update, updateDraft), CancellationToken.None);
        updateRes.IsSuccess.Should().BeFalse();
        updateRes.Error.Should().StartWith("CONFLICT:");

        var deleteRes = await handler.Handle(new MutateQuestionCommand(pq.Id, lec.Id, UserRole.Lecturer, QuestionMutationKind.SoftDelete), CancellationToken.None);
        deleteRes.IsSuccess.Should().BeFalse();
        deleteRes.Error.Should().StartWith("CONFLICT:");

        (await db.PracticeQuestions.AsNoTracking().SingleAsync(q => q.Id == pq.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "P1-03E: ReviewQuestionDecision bắt DbUpdateConcurrencyException trả CONFLICT và ReviewQuestionSnapshot.Parse an toàn JSON lỗi")]
    public async Task P1_03_ReviewQuestionDecision_MapsConcurrencyExceptionToConflict_AndSnapshotParseHandlesInvalidJsonSafely()
    {
        // 1. ReviewQuestionSnapshot.Parse an toàn với null, rỗng, whitespace, JSON hỏng
        ReviewQuestionSnapshot.Parse(null).Should().BeNull();
        ReviewQuestionSnapshot.Parse(string.Empty).Should().BeNull();
        ReviewQuestionSnapshot.Parse("   ").Should().BeNull();
        ReviewQuestionSnapshot.Parse("{invalid-json").Should().BeNull();
        ReviewQuestionSnapshot.Parse("null").Should().BeNull();

        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"p1-concurrency-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ConcurrencyThrowingDbContext(options);

        var head = new User { Id = Guid.NewGuid(), Email = "head-conc@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-conc@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", DepartmentHeadUserId = head.Id, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);

        var examQ = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Exam Concurrency Q",
            Content = "Content",
            SampleAnswer = ValidSampleAnswer,
            SubmittedBy = lec.Id,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            IsActive = false
        };
        var practiceQ = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Practice Concurrency Q",
            Content = "Content",
            SampleAnswer = ValidSampleAnswer,
            SubmittedBy = lec.Id,
            ApprovalStatus = ApprovalStatus.SubmittedForReview,
            IsActive = false
        };
        var corruptReq = new QuestionReviewRequest
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SubmittedBy = lec.Id,
            Version = 0,
            Items =
            [
                new QuestionReviewRequestItem
                {
                    Id = Guid.NewGuid(),
                    QuestionId = practiceQ.Id,
                    UsageScope = "practice",
                    SnapshotJson = "{corrupt-json",
                    Decision = "PENDING"
                }
            ]
        };

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(examQ);
        db.PracticeQuestions.Add(practiceQ);
        db.QuestionReviewRequests.Add(corruptReq);
        await db.SaveChangesAsync();

        // 2. QuestionReviewRequestService.Decide fail-closed CONFLICT trên corrupt SnapshotJson thay vì ném NullReferenceException / JsonException
        var corruptDecision = await new QuestionReviewRequestService(db).Decide(
            head.Id, corruptReq.Id, new ReviewRequestDecision([corruptReq.Items.Single().Id], ApprovalStatus.Approved, null, 0), CancellationToken.None);
        corruptDecision.IsSuccess.Should().BeFalse();
        corruptDecision.Error.Should().StartWith("CONFLICT:");

        // 3. ReviewQuestionDecisionCommandHandler bắt DbUpdateConcurrencyException -> CONFLICT cho cả nhánh Exam và Practice
        var reviewHandler = new ReviewQuestionDecisionCommandHandler(db);

        db.ThrowOnNextSave = true;
        var examConflict = await reviewHandler.Handle(
            new ReviewQuestionDecisionCommand(examQ.Id, head.Id, ApprovalStatus.Approved, "Duyệt đồng thời", "exam"), CancellationToken.None);
        examConflict.IsSuccess.Should().BeFalse();
        examConflict.Error.Should().StartWith("CONFLICT:");

        db.ThrowOnNextSave = true;
        var practiceConflict = await reviewHandler.Handle(
            new ReviewQuestionDecisionCommand(practiceQ.Id, head.Id, ApprovalStatus.Approved, "Duyệt đồng thời", "practice"), CancellationToken.None);
        practiceConflict.IsSuccess.Should().BeFalse();
        practiceConflict.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "P1-03F: UpdateExamStructure kiểm tra Bloom keys/tổng > 0 và chặn đổi TotalQuestions/BloomDistribution khi đã có bộ đề OFFICIAL/HANDED_OVER")]
    public async Task P1_03_UpdateExamStructure_ValidatesBloomAndBlocksMutationWhenOfficialSetExists()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p103f@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = head.Id,
            Name = "Cấu trúc gốc",
            TotalQuestions = 5,
            DurationMinutes = 30,
            BloomDistribution = "{\"Understand\":2,\"Apply\":3}",
            IsActive = true
        };

        db.Users.Add(head);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        await db.SaveChangesAsync();

        var handler = new UpdateExamStructureCommandHandler(db);

        // 1. Key Bloom không hợp lệ -> VALIDATION
        (await handler.Handle(new UpdateExamStructureCommand(head.Id, structure.Id, "Bad Key", 5, 30, "{\"InvalidBloom\":2}"), CancellationToken.None))
            .Error.Should().StartWith("VALIDATION:");

        // 2. Tổng phân bổ Bloom == 0 khi đã khai báo -> VALIDATION
        (await handler.Handle(new UpdateExamStructureCommand(head.Id, structure.Id, "Zero Sum", 5, 30, "{\"Understand\":0,\"Apply\":0}"), CancellationToken.None))
            .Error.Should().StartWith("VALIDATION:");

        // 3. Key không phân biệt hoa thường hợp lệ khi chưa có bộ đề OFFICIAL -> Success
        var caseInsensitiveOk = await handler.Handle(new UpdateExamStructureCommand(
            head.Id, structure.Id, "Case OK", 5, 35, "{\"understand\":2,\"APPLY\":3}"), CancellationToken.None);
        caseInsensitiveOk.IsSuccess.Should().BeTrue(because: caseInsensitiveOk.Error);

        // 4. Thêm bộ đề OFFICIAL đang active -> chặn sửa TotalQuestions hoặc BloomDistribution (CONFLICT), vẫn cho đổi tên/thời lượng nếu giữ nguyên
        db.ExamSets.Add(new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = course.Id,
            SetCode = "SWD-OFF-01",
            Status = ExamSetStatus.Official,
            IsActive = true
        });
        await db.SaveChangesAsync();

        (await handler.Handle(new UpdateExamStructureCommand(head.Id, structure.Id, "Change Total", 6, 35, "{\"understand\":2,\"APPLY\":3}"), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");
        (await handler.Handle(new UpdateExamStructureCommand(head.Id, structure.Id, "Change Bloom", 5, 35, "{\"Understand\":3,\"Apply\":2}"), CancellationToken.None))
            .Error.Should().StartWith("CONFLICT:");

        var metadataOnlyUpdate = await handler.Handle(new UpdateExamStructureCommand(
            head.Id, structure.Id, "Rename Only", 5, 40, "{ \"Understand\": 2, \"Apply\": 3 }"), CancellationToken.None);
        metadataOnlyUpdate.IsSuccess.Should().BeTrue(because: metadataOnlyUpdate.Error);
        metadataOnlyUpdate.Value.Name.Should().Be("Rename Only");
        metadataOnlyUpdate.Value.DurationMinutes.Should().Be(40);

        // 5. ExamStructuresController ánh xạ CONFLICT -> HTTP 409 ProblemDetails
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateExamStructureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamStructureDto>.Failure("CONFLICT: Cấu trúc đã có bộ đề OFFICIAL."));
        var controller = new ExamStructuresController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(head.Id, UserRole.DepartmentHead) }
            }
        };
        var conflictAction = (await controller.UpdateExamStructure(structure.Id, new UpdateExamStructureRequest("X", 6, 30, "{}")))
            .Should().BeOfType<ObjectResult>().Subject;
        conflictAction.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        conflictAction.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact(DisplayName = "P1-10: DeleteCourseClo chặn xóa CLO đang được tham chiếu trong in_class_exam_shifts.selected_clo_ids (CONFLICT: CLO_IN_USE)")]
    public async Task P1_10_DeleteCourseClo_BlocksDeletionWhenReferencedByInClassExamShiftSelectedCloIds()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p110@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-p110@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", DepartmentHeadUserId = head.Id, SemesterId = semester.Id, IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1701", Name = "SE1701", CourseId = course.Id, SemesterId = semester.Id, LecturerId = lec.Id, IsActive = true };

        var cloUsedLower = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Used lower", BloomLevel = BloomLevel.Apply, OrderIndex = 1, IsActive = true };
        var cloUsedUpper = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "Used upper", BloomLevel = BloomLevel.Analyze, OrderIndex = 2, IsActive = true };
        var cloUnused = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO3", Description = "Unused", BloomLevel = BloomLevel.Understand, OrderIndex = 3, IsActive = true };

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lec.Id,
            ShiftName = "Ca kiểm tra CLO1-2",
            QuestionPoolMode = "SELECTED_CLOS",
            SelectedCloIdsJson = $"[\"{cloUsedLower.Id:D}\",\"{cloUsedUpper.Id.ToString("D").ToUpperInvariant()}\"]",
            TotalQuestionsPerStudent = 2,
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            TotpSecretKey = "JBSWY3DPEHPK3PXP"
        };

        db.Users.AddRange(head, lec);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.CourseClos.AddRange(cloUsedLower, cloUsedUpper, cloUnused);
        db.InClassExamShifts.Add(shift);
        await db.SaveChangesAsync();

        var handler = new DeleteCourseCloCommandHandler(db);
        var actor = $"{UserRole.DepartmentHead}:{head.Id}";

        var delLower = await handler.Handle(new DeleteCourseCloCommand(course.Id, cloUsedLower.Id, actor), CancellationToken.None);
        delLower.IsSuccess.Should().BeFalse();
        delLower.Error.Should().StartWith("CONFLICT: CLO_IN_USE:");

        var delUpper = await handler.Handle(new DeleteCourseCloCommand(course.Id, cloUsedUpper.Id, actor), CancellationToken.None);
        delUpper.IsSuccess.Should().BeFalse();
        delUpper.Error.Should().StartWith("CONFLICT: CLO_IN_USE:");

        var delUnused = await handler.Handle(new DeleteCourseCloCommand(course.Id, cloUnused.Id, actor), CancellationToken.None);
        delUnused.IsSuccess.Should().BeTrue(because: delUnused.Error);

        (await db.CourseClos.AsNoTracking().AnyAsync(c => c.Id == cloUsedLower.Id)).Should().BeTrue();
        (await db.CourseClos.AsNoTracking().AnyAsync(c => c.Id == cloUsedUpper.Id)).Should().BeTrue();
        (await db.CourseClos.AsNoTracking().AnyAsync(c => c.Id == cloUnused.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "P1-01E: UpdateExamSet hỗ trợ thêm câu tạo tay inline, chuẩn hóa Weight >= 0.01 và giải phóng câu cũ về pool")]
    public async Task P1_01_UpdateExamSet_SupportsInlineManualQuestionsAndReleasesReplacedBankQuestion()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p101e@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-p101e@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct 1Q", TotalQuestions = 1, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);
        var qOld = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q Old", Content = "Old", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        var draftSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec.Id, SetCode = "SET-INLINE-UP", Status = ExamSetStatus.Draft, IsActive = true };
        draftSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = draftSet.Id, ExamQuestionId = qOld.Id, OrderIndex = 1 });

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(qOld);
        db.ExamSets.Add(draftSet);
        await db.SaveChangesAsync();

        var updateRes = await new UpdateExamSetCommandHandler(db).Handle(new UpdateExamSetCommand(
            draftSet.Id,
            "SET-INLINE-UP-V2",
            [],
            lec.Id,
            UserRole.Lecturer,
            [
                new CreateInlineExamQuestionDto
                {
                    Title = "Inline Replacement",
                    Content = "Phân tích kiến trúc Mediator và Pipeline Behavior.",
                    SampleAnswer = ValidSampleAnswer,
                    Difficulty = QuestionDifficulty.Hard,
                    BloomLevel = BloomLevel.Analyze,
                    Rubric = new RubricDraftDto("Rubric Inline Clamp", null, 10.00m,
                    [
                        new("Ý chính", 9.50m, 0.999m, BloomLevel.Analyze, "D1", 1),
                        new("Ví dụ phụ", 0.50m, 0.001m, BloomLevel.Understand, "D2", 2)
                    ])
                }
            ]), CancellationToken.None);

        updateRes.IsSuccess.Should().BeTrue(because: updateRes.Error);
        updateRes.Value.SetCode.Should().Be("SET-INLINE-UP-V2");
        updateRes.Value.Questions.Should().ContainSingle();
        var newInlineId = updateRes.Value.Questions[0].QuestionId;
        newInlineId.Should().NotBe(qOld.Id);

        var storedInline = await db.ExamQuestions.Include(q => q.Rubric).ThenInclude(r => r.Criteria).SingleAsync(q => q.Id == newInlineId);
        storedInline.BankCurationStatus.Should().Be(BankCurationStatus.PendingCuration);
        storedInline.Rubric.Criteria.Should().OnlyContain(c => c.Weight >= 0.01m && c.Weight <= 1.00m);

        // Câu cũ qOld đã được gỡ khỏi bộ đề và quay lại available pool của lec
        (await db.ExamSetQuestions.AsNoTracking().Where(l => l.ExamSetId == draftSet.Id).Select(l => l.ExamQuestionId).ToListAsync())
            .Should().ContainSingle().Which.Should().Be(newInlineId);
        var available = await new GetAvailableExamSetQuestionsQueryHandler(db).Handle(
            new GetAvailableExamSetQuestionsQuery(structure.Id, lec.Id, UserRole.Lecturer), CancellationToken.None);
        available.IsSuccess.Should().BeTrue();
        available.Value.Items.Select(i => i.QuestionId).Should().Contain(qOld.Id);
    }

    [Fact(DisplayName = "P1-01F: SubmitExamSetForReview chặn nộp duyệt bộ đề đã bị xóa mềm (CONFLICT)")]
    public async Task P1_01_SubmitExamSetForReview_BlocksSoftDeletedExamSet()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-p101f@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-p101f@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct SoftDel", TotalQuestions = 1, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);
        var q = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q", Content = "C", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        var deletedSet = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec.Id, SetCode = "SET-DEL", Status = ExamSetStatus.Draft, IsActive = false };
        deletedSet.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = deletedSet.Id, ExamQuestionId = q.Id, OrderIndex = 1 });

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(q);
        db.ExamSets.Add(deletedSet);
        await db.SaveChangesAsync();

        var submitRes = await new SubmitExamSetForReviewCommandHandler(db).Handle(
            new SubmitExamSetForReviewCommand(deletedSet.Id, lec.Id, UserRole.Lecturer), CancellationToken.None);
        submitRes.IsSuccess.Should().BeFalse();
        submitRes.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "Remediation A4: Bộ đề REJECTED chứa câu inline NEEDS_REVISION không bị deadlock — cho phép UpdateExamSet (diff in-place), MutateQuestion (validate Bloom + Weight 2 decimals) và nộp duyệt lại")]
    public async Task Remediation_A4_RejectedExamSetWithInlineNeedsRevision_AllowsUpdateExamSetInPlace_MutateQuestion_AndResubmit()
    {
        await using var db = CreateDbContext();

        var head = new User { Id = Guid.NewGuid(), Email = "head-a4@fpt.edu.vn", FullName = "Head A4", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-a4@fpt.edu.vn", FullName = "GV A4", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct A4", TotalQuestions = 2, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);
        var bankQ1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Bank Q1", Content = "Content 1", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };
        var bankQ2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Bank Q2", Content = "Content 2", SampleAnswer = ValidSampleAnswer, ApprovalStatus = ApprovalStatus.Approved, BankCurationStatus = BankCurationStatus.BankReady, IsActive = true };

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lec.Id, AssignedByUserId = head.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(bankQ1, bankQ2);
        await db.SaveChangesAsync();

        // 1. Tạo bộ đề kèm 1 câu inline -> Nộp duyệt -> TBM trả về chỉnh sửa (ExamSet -> REJECTED, inline -> NEEDS_REVISION)
        var created = await new CreateExamSetCommandHandler(db).Handle(new CreateExamSetCommand(
            structure.Id, course.Id, null, "SWD-A4-01", [bankQ1.Id], lec.Id, UserRole.Lecturer, false,
            [
                new CreateInlineExamQuestionDto
                {
                    Title = "Inline A4 gốc",
                    Content = "Phân tích kiến trúc Microservices và Eventual Consistency.",
                    SampleAnswer = ValidSampleAnswer,
                    Difficulty = QuestionDifficulty.Medium,
                    BloomLevel = BloomLevel.Analyze,
                    Rubric = new RubricDraftDto("Rubric Inline A4", null, 10.00m, ValidCriteria())
                }
            ]), CancellationToken.None);
        created.IsSuccess.Should().BeTrue(because: created.Error);
        var examSetId = created.Value.Id;
        var inlineQuestionId = created.Value.Questions.Single(q => q.QuestionId != bankQ1.Id).QuestionId;

        (await new SubmitExamSetForReviewCommandHandler(db).Handle(
            new SubmitExamSetForReviewCommand(examSetId, lec.Id, UserRole.Lecturer), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var returned = await new ReturnExamSetForRevisionCommandHandler(db).Handle(
            new ReturnExamSetForRevisionCommand(examSetId, "Cần chỉnh sửa câu inline và đổi câu ngân hàng số 1.", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        returned.IsSuccess.Should().BeTrue(because: returned.Error);

        var originalInlineLinkId = await db.ExamSetQuestions
            .AsNoTracking()
            .Where(l => l.ExamSetId == examSetId && l.ExamQuestionId == inlineQuestionId)
            .Select(l => l.Id)
            .SingleAsync();

        // 2. GV gọi PUT exam-sets/{id}: giữ câu inline NEEDS_REVISION (đổi thứ tự lên 1) + thay bankQ1 bằng bankQ2 + đổi SetCode
        var updateSetRes = await new UpdateExamSetCommandHandler(db).Handle(new UpdateExamSetCommand(
            examSetId,
            "SWD-A4-01-V2",
            [inlineQuestionId, bankQ2.Id],
            lec.Id,
            UserRole.Lecturer), CancellationToken.None);
        updateSetRes.IsSuccess.Should().BeTrue(because: updateSetRes.Error);
        updateSetRes.Value.SetCode.Should().Be("SWD-A4-01-V2");
        updateSetRes.Value.Status.Should().Be(ExamSetStatus.Draft);

        // Xác minh diff in-place trên ExamSetQuestions (giữ nguyên Id link của câu giữ lại, chỉ cập nhật OrderIndex)
        var currentLinks = await db.ExamSetQuestions
            .AsNoTracking()
            .Where(l => l.ExamSetId == examSetId)
            .OrderBy(l => l.OrderIndex)
            .ToListAsync();
        currentLinks.Should().HaveCount(2);
        currentLinks[0].Id.Should().Be(originalInlineLinkId, "Link ExamSetQuestion của câu giữ lại phải được cập nhật in-place thay vì xóa-thêm lại");
        currentLinks[0].ExamQuestionId.Should().Be(inlineQuestionId);
        currentLinks[0].OrderIndex.Should().Be(1);
        currentLinks[1].ExamQuestionId.Should().Be(bankQ2.Id);
        currentLinks[1].OrderIndex.Should().Be(2);

        // 3. MutateQuestion(Update) chặn criterion.BloomLevel sai, sau đó cập nhật thành công câu inline NEEDS_REVISION (không bị 409)
        var mutateHandler = new MutateQuestionCommandHandler(db);
        var invalidBloomDraft = new UpdateQuestionDraftDto(
            "Inline A4 sửa sai Bloom",
            "Nội dung câu hỏi đã cập nhật",
            ValidSampleAnswer,
            ["K1"],
            QuestionDifficulty.Medium,
            BloomLevel.Analyze,
            new RubricDraftDto("Rubric sai Bloom", null, 10.00m,
            [
                new("Tiêu chí 1", 5.00m, 0.50m, "InvalidBloomLevel", "D1", 1),
                new("Tiêu chí 2", 5.00m, 0.50m, BloomLevel.Apply, "D2", 2)
            ]),
            null);
        var badBloomRes = await mutateHandler.Handle(
            new MutateQuestionCommand(inlineQuestionId, lec.Id, UserRole.Lecturer, QuestionMutationKind.Update, invalidBloomDraft),
            CancellationToken.None);
        badBloomRes.IsSuccess.Should().BeFalse();
        badBloomRes.Error.Should().StartWith("VALIDATION:");

        var validDraft = new UpdateQuestionDraftDto(
            "Inline A4 đã chỉnh sửa hoàn chỉnh",
            "Phân tích chi tiết Saga Pattern và Outbox Pattern trong hệ thống phân tán.",
            ValidSampleAnswer,
            ["Saga", "Outbox"],
            QuestionDifficulty.Hard,
            BloomLevel.Analyze,
            new RubricDraftDto("Rubric Inline A4 mới", null, 10.00m,
            [
                new("Phân tích Saga", 6.67m, 0.6666m, "analyze", "Đúng bản chất", 1),
                new("Phân tích Outbox", 3.33m, 0.3334m, "APPLY", "Đưa ví dụ rõ", 2)
            ]),
            null);
        var mutateOk = await mutateHandler.Handle(
            new MutateQuestionCommand(inlineQuestionId, lec.Id, UserRole.Lecturer, QuestionMutationKind.Update, validDraft),
            CancellationToken.None);
        mutateOk.IsSuccess.Should().BeTrue(because: mutateOk.Error);

        var updatedInline = await db.ExamQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .SingleAsync(q => q.Id == inlineQuestionId);
        updatedInline.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
        var orderedCriteria = updatedInline.Rubric.Criteria.OrderBy(c => c.OrderIndex).ToList();
        orderedCriteria[0].BloomLevel.Should().Be(BloomLevel.Analyze);
        orderedCriteria[0].Weight.Should().Be(0.67m, "Weight phải được làm tròn 2 chữ số thập phân khớp numeric(3,2)");
        orderedCriteria[1].BloomLevel.Should().Be(BloomLevel.Apply);
        orderedCriteria[1].Weight.Should().Be(0.33m, "Weight phải được làm tròn 2 chữ số thập phân khớp numeric(3,2)");

        // 4. SoftDelete vẫn phải bị chặn (CONFLICT) vì câu vẫn đang gắn với ExamSet active (bảo toàn FK)
        var softDeleteRes = await mutateHandler.Handle(
            new MutateQuestionCommand(inlineQuestionId, lec.Id, UserRole.Lecturer, QuestionMutationKind.SoftDelete),
            CancellationToken.None);
        softDeleteRes.IsSuccess.Should().BeFalse();
        softDeleteRes.Error.Should().StartWith("CONFLICT:");

        // 5. Nộp duyệt lại và TBM phê duyệt bộ đề thành công
        var resubmitRes = await new SubmitExamSetForReviewCommandHandler(db).Handle(
            new SubmitExamSetForReviewCommand(examSetId, lec.Id, UserRole.Lecturer), CancellationToken.None);
        resubmitRes.IsSuccess.Should().BeTrue(because: resubmitRes.Error);

        var approveRes = await new ApproveExamSetCommandHandler(db).Handle(
            new ApproveExamSetCommand(examSetId, "Đã chỉnh sửa đạt chuẩn.", head.Id, UserRole.DepartmentHead), CancellationToken.None);
        approveRes.IsSuccess.Should().BeTrue(because: approveRes.Error);
        approveRes.Value.Status.Should().Be(ExamSetStatus.Official);
    }

    [Fact(DisplayName = "Remediation A4: ApproveExamSet chặn câu APPROVED nhưng IsActive=false và ánh xạ DbUpdateConcurrencyException sang CONFLICT")]
    public async Task Remediation_A4_ApproveExamSet_BlocksApprovedInactiveQuestion_AndHandlesConcurrencyConflict()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"p1-a4-approve-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ConcurrencyThrowingDbContext(options);

        var head = new User { Id = Guid.NewGuid(), Email = "head-a4b@fpt.edu.vn", FullName = "Head A4B", Role = UserRole.DepartmentHead, IsActive = true };
        var lec = new User { Id = Guid.NewGuid(), Email = "lec-a4b@fpt.edu.vn", FullName = "GV A4B", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = head.Id, Name = "Struct A4B", TotalQuestions = 1, IsActive = true };
        var rubric = BuildRubricEntity(course.Id);

        var inactiveApprovedQ = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu đã duyệt nhưng bị tạm tắt",
            Content = "Nội dung câu hỏi",
            SampleAnswer = ValidSampleAnswer,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = false
        };
        var activeApprovedQ = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu đã duyệt đang hoạt động",
            Content = "Nội dung hợp lệ",
            SampleAnswer = ValidSampleAnswer,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        var setWithInactiveQ = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec.Id, SetCode = "SET-INACT-Q", Status = ExamSetStatus.SubmittedForReview, IsActive = true };
        setWithInactiveQ.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = setWithInactiveQ.Id, ExamQuestionId = inactiveApprovedQ.Id, OrderIndex = 1 });

        var setForConcurrency = new ExamSet { Id = Guid.NewGuid(), StructureId = structure.Id, CourseId = course.Id, CreatedBy = lec.Id, SetCode = "SET-CONC-Q", Status = ExamSetStatus.SubmittedForReview, IsActive = true };
        setForConcurrency.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = setForConcurrency.Id, ExamQuestionId = activeApprovedQ.Id, OrderIndex = 1 });

        db.Users.AddRange(head, lec);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(inactiveApprovedQ, activeApprovedQ);
        db.ExamSets.AddRange(setWithInactiveQ, setForConcurrency);
        await db.SaveChangesAsync();

        var handler = new ApproveExamSetCommandHandler(db);

        // 1. Chặn bộ đề chứa câu APPROVED nhưng IsActive == false
        var inactiveRes = await handler.Handle(
            new ApproveExamSetCommand(setWithInactiveQ.Id, "Duyệt thử", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        inactiveRes.IsSuccess.Should().BeFalse();
        inactiveRes.Error.Should().StartWith("CONFLICT:");
        (await db.ExamSets.AsNoTracking().SingleAsync(es => es.Id == setWithInactiveQ.Id)).Status
            .Should().Be(ExamSetStatus.SubmittedForReview);

        // 2. Bắt DbUpdateConcurrencyException khi lưu và trả CONFLICT sạch
        db.ThrowOnNextSave = true;
        var concRes = await handler.Handle(
            new ApproveExamSetCommand(setForConcurrency.Id, "Duyệt đồng thời", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        concRes.IsSuccess.Should().BeFalse();
        concRes.Error.Should().StartWith("CONFLICT:");
    }
}
