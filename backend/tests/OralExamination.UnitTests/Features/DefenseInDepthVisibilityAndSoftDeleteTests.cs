using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerCourseOfferings;
using OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.ResumeInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Application.Features.InClassShifts.Commands.UpdateInClassShift;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftPoolStats;
using OralExamination.Application.Features.InClassShifts.Queries.GetStudentInClassSubmission;
using OralExamination.Application.Features.Lecturer.Queries.GetLecturerDashboard;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromClo;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features;

public class DefenseInDepthVisibilityAndSoftDeleteTests
{
    private static OralExamDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact(DisplayName = "GetMockExamSessionDetail: Không chèn câu IsDeleted hoặc chưa Approved vào phần bù chi tiết thi thử")]
    public async Task GetMockExamSessionDetail_Fallback_ExcludesDeletedAndUnapprovedQuestions()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var course = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "Testing", MockExamTotalQuestions = 2, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10m };
        var validQ = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Valid", Content = "Valid", IsActive = true, IsDeleted = false, ApprovalStatus = ApprovalStatus.Approved };
        var deletedQ = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Deleted", Content = "Deleted", IsActive = true, IsDeleted = true, ApprovalStatus = ApprovalStatus.Approved };
        var draftQ = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Draft", Content = "Draft", IsActive = true, IsDeleted = false, ApprovalStatus = ApprovalStatus.Draft };
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            Status = "graded",
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow
        };

        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.AddRange(deletedQ, draftQ, validQ);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var result = await new GetMockExamSessionDetailQueryHandler(context)
            .Handle(new GetMockExamSessionDetailQuery(session.Id, studentId, false, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Questions.Should().ContainSingle(q => q.QuestionId == validQ.Id);
    }

    [Fact(DisplayName = "InClassShifts: Candidate/PoolStats/Enter/Update loại IsDeleted nhưng Resume & StudentSubmission vẫn đọc được câu đã gán trước đó")]
    public async Task InClassShifts_CandidateQueriesExcludeDeleted_WhileReadBackPreservesAssignedQuestions()
    {
        using var context = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv.mf05@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.mf05@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "Testing", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "SE1801", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        context.Users.AddRange(lecturer, student);
        context.Courses.Add(course);
        context.Classes.Add(cls);
        context.ClassEnrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = student.Id, Status = "enrolled" });

        var activeQ = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Active", Content = "Active", Difficulty = "easy", ApprovalStatus = ApprovalStatus.Approved, IsActive = true, IsDeleted = false };
        var deletedQ = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Deleted", Content = "Deleted", Difficulty = "easy", ApprovalStatus = ApprovalStatus.Approved, IsActive = true, IsDeleted = true };
        context.PracticeQuestions.AddRange(activeQ, deletedQ);

        var secret = TotpPasswordHelper.GenerateSecretKey(32);
        var scheduledShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Scheduled",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            ExamDurationMinutes = 20,
            TotalQuestionsPerStudent = 2,
            TotpSecretKey = secret,
            Status = "SCHEDULED"
        };
        var activeShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Active",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddHours(1),
            ExamDurationMinutes = 20,
            TotalQuestionsPerStudent = 1,
            TotpSecretKey = secret,
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.AddRange(scheduledShift, activeShift);
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = activeShift.Id,
            StudentId = student.Id,
            AssignedQuestionId = deletedQ.Id,
            AssignedQuestionIdsJson = $"[\"{deletedQ.Id}\"]",
            JoinedAt = DateTime.UtcNow.AddMinutes(-2),
            Status = "IN_PROGRESS"
        });
        context.InClassExamSubmissions.Add(new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = activeShift.Id,
            StudentId = student.Id,
            QuestionId = deletedQ.Id,
            Transcript = "Historical transcript",
            Status = "SUBMITTED"
        });
        await context.SaveChangesAsync();

        // 1. PoolStats chỉ đếm 1 câu active (bỏ qua deletedQ)
        var stats = await new GetInClassShiftPoolStatsQueryHandler(context)
            .Handle(new GetInClassShiftPoolStatsQuery(lecturer.Id, cls.Id), CancellationToken.None);
        stats.IsSuccess.Should().BeTrue();
        stats.Value!.CourseBankCounts.Total.Should().Be(1);

        // 2. CreateInClassShift yêu cầu 2 câu -> thất bại vì chỉ còn 1 câu non-deleted
        var createRes = await new CreateInClassShiftCommandHandler(context, new CreateInClassShiftCommandValidator())
            .Handle(new CreateInClassShiftCommand(
                lecturer.Id, cls.Id, "Ca thiếu", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2),
                TotalQuestionsPerStudent: 2), CancellationToken.None);
        createRes.IsSuccess.Should().BeFalse();

        // 3. UpdateInClassShift yêu cầu 2 câu -> thất bại vì chỉ còn 1 câu non-deleted
        var updateRes = await new UpdateInClassShiftCommandHandler(context)
            .Handle(new UpdateInClassShiftCommand(
                lecturer.Id, scheduledShift.Id, "Ca update", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2),
                TotalQuestionsPerStudent: 2), CancellationToken.None);
        updateRes.IsSuccess.Should().BeFalse();

        // 4. ResumeInClassShift và GetStudentInClassSubmission vẫn đọc được deletedQ đã giao trước đó
        var queueMock = new Mock<IGradingQueueChannel>();
        var resumeRes = await new ResumeInClassShiftCommandHandler(context, queueMock.Object)
            .Handle(new ResumeInClassShiftCommand(student.Id, activeShift.Id), CancellationToken.None);
        resumeRes.IsSuccess.Should().BeTrue();
        resumeRes.Value!.AssignedQuestions.Should().ContainSingle(q => q.QuestionId == deletedQ.Id);

        var subRes = await new GetStudentInClassSubmissionQueryHandler(context)
            .Handle(new GetStudentInClassSubmissionQuery(student.Id, activeShift.Id), CancellationToken.None);
        subRes.IsSuccess.Should().BeTrue();
        subRes.Value!.QuestionTitle.Should().Be("Deleted");

        // 5. EnterInClassShift chỉ bốc activeQ (không bốc deletedQ) và SubmitInClassShiftAnswer từ chối deletedQ
        var student2 = new User { Id = Guid.NewGuid(), Email = "sv2.mf05@fpt.edu.vn", FullName = "SV2", Role = UserRole.Student, IsActive = true };
        context.Users.Add(student2);
        context.ClassEnrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = student2.Id, Status = "enrolled" });
        await context.SaveChangesAsync();

        var otp = TotpPasswordHelper.GenerateCode(secret, DateTime.UtcNow);
        var enterRes = await new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator(), queueMock.Object)
            .Handle(new EnterInClassShiftCommand(student2.Id, activeShift.Id, otp), CancellationToken.None);
        enterRes.IsSuccess.Should().BeTrue();
        enterRes.Value!.AssignedQuestionIds.Should().ContainSingle().Which.Should().Be(activeQ.Id);

        var submitDeletedRes = await new SubmitInClassShiftAnswerCommandHandler(context, queueMock.Object, new SubmitInClassShiftAnswerCommandValidator())
            .Handle(new SubmitInClassShiftAnswerCommand(student.Id, activeShift.Id, deletedQ.Id, "Late submit on deleted"), CancellationToken.None);
        submitDeletedRes.IsSuccess.Should().BeFalse();
    }

    [Fact(DisplayName = "GetLecturerCourseOfferings & GetExamStructures: Ẩn môn học bị tắt mềm (Course.IsActive = false)")]
    public async Task OfferingsAndExamStructures_ExcludeInactiveCourses()
    {
        using var context = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv.vis@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var head = new User { Id = Guid.NewGuid(), Email = "head.vis@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", IsActive = true };
        var inactiveCourse = new Course { Id = Guid.NewGuid(), Code = "PRF192", Name = "PRF", SemesterId = semester.Id, DepartmentHeadUserId = head.Id, IsActive = false };
        var activeCourse = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "SWT", SemesterId = semester.Id, DepartmentHeadUserId = head.Id, IsActive = true };
        var inactiveOffering = new CourseOffering { Id = Guid.NewGuid(), CourseId = inactiveCourse.Id, SemesterId = semester.Id, IsEnabled = true };
        var activeOffering = new CourseOffering { Id = Guid.NewGuid(), CourseId = activeCourse.Id, SemesterId = semester.Id, IsEnabled = true };

        context.Users.AddRange(lecturer, head);
        context.Semesters.Add(semester);
        context.Courses.AddRange(inactiveCourse, activeCourse);
        context.CourseOfferings.AddRange(inactiveOffering, activeOffering);
        context.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = inactiveCourse.Id, CourseOfferingId = inactiveOffering.Id, LecturerUserId = lecturer.Id, AssignedByUserId = head.Id, SemesterId = semester.Id, IsActive = true },
            new CourseLecturer { CourseId = activeCourse.Id, CourseOfferingId = activeOffering.Id, LecturerUserId = lecturer.Id, AssignedByUserId = head.Id, SemesterId = semester.Id, IsActive = true });
        context.ExamStructures.AddRange(
            new ExamStructure { Id = Guid.NewGuid(), CourseId = inactiveCourse.Id, CreatedBy = head.Id, Name = "Old PRF", TotalQuestions = 5, DurationMinutes = 30, IsActive = true },
            new ExamStructure { Id = Guid.NewGuid(), CourseId = activeCourse.Id, CreatedBy = head.Id, Name = "Active SWT", TotalQuestions = 5, DurationMinutes = 30, IsActive = true });
        await context.SaveChangesAsync();

        var offerings = await new GetLecturerCourseOfferingsQueryHandler(context)
            .Handle(new GetLecturerCourseOfferingsQuery(lecturer.Id, IncludeInactive: false), CancellationToken.None);
        offerings.IsSuccess.Should().BeTrue();
        offerings.Value.Should().ContainSingle(o => o.CourseCode == "SWT301");

        var structures = await new GetExamStructuresQueryHandler(context)
            .Handle(new GetExamStructuresQuery(CourseId: null, IsActive: true), CancellationToken.None);
        structures.IsSuccess.Should().BeTrue();
        structures.Value.Should().ContainSingle(s => s.CourseCode == "SWT301");
    }

    [Fact(DisplayName = "CreateDraftQuestion, ReviewQuestionDecision, QuestionCourseAccess, LecturerDashboard, ReviewRequests & GenerateFromClo: Kiểm soát Course.IsActive, IsEnabled và !IsDeleted")]
    public async Task QuestionBankAndDashboard_EnforceCourseActiveOfferingEnabledAndSoftDelete()
    {
        using var context = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Email = "head.qb@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv.qb@fpt.edu.vn", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", IsActive = true };
        var inactiveCourse = new Course { Id = Guid.NewGuid(), Code = "PRF192", Name = "PRF", SemesterId = semester.Id, DepartmentHeadUserId = head.Id, IsActive = false };
        var activeCourseDisabledOffering = new Course { Id = Guid.NewGuid(), Code = "CSD201", Name = "CSD", SemesterId = semester.Id, DepartmentHeadUserId = head.Id, IsActive = true };
        var activeCourse = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "SWT", SemesterId = semester.Id, DepartmentHeadUserId = head.Id, IsActive = true };
        var disabledOffering = new CourseOffering { Id = Guid.NewGuid(), CourseId = activeCourseDisabledOffering.Id, SemesterId = semester.Id, IsEnabled = false };
        var enabledOffering = new CourseOffering { Id = Guid.NewGuid(), CourseId = activeCourse.Id, SemesterId = semester.Id, IsEnabled = true };

        context.Users.AddRange(head, lecturer);
        context.Semesters.Add(semester);
        context.Courses.AddRange(inactiveCourse, activeCourseDisabledOffering, activeCourse);
        context.CourseOfferings.AddRange(disabledOffering, enabledOffering);
        context.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = inactiveCourse.Id, LecturerUserId = lecturer.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { CourseId = activeCourseDisabledOffering.Id, CourseOfferingId = disabledOffering.Id, LecturerUserId = lecturer.Id, AssignedByUserId = head.Id, IsActive = true },
            new CourseLecturer { CourseId = activeCourse.Id, CourseOfferingId = enabledOffering.Id, LecturerUserId = lecturer.Id, AssignedByUserId = head.Id, IsActive = true });

        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = activeCourse.Id, Name = "R", TotalMaxScore = 10m };
        rubric.Criteria.Add(new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "C1", MaxScore = 5m, Weight = 0.5m, OrderIndex = 1 });
        rubric.Criteria.Add(new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "C2", MaxScore = 5m, Weight = 0.5m, OrderIndex = 2 });
        context.Rubrics.Add(rubric);

        var clo = new CourseClo { Id = Guid.NewGuid(), CourseId = activeCourse.Id, CloCode = "CLO1", Description = "Testing", BloomLevel = "Understand", OrderIndex = 1, IsActive = true };
        context.CourseClos.Add(clo);

        var deletedExamQ = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = activeCourse.Id, RubricId = rubric.Id, Title = "Deleted Exam Title",
            Content = "Content", SampleAnswer = new string('a', 60), SubmittedBy = lecturer.Id,
            ApprovalStatus = ApprovalStatus.Approved, IsActive = true, IsDeleted = true
        };
        var inactiveCourseExamQ = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = inactiveCourse.Id, RubricId = rubric.Id, Title = "Inactive Course Q",
            Content = "Content", SampleAnswer = new string('a', 60), SubmittedBy = lecturer.Id,
            ApprovalStatus = ApprovalStatus.NeedsRevision, IsActive = true, IsDeleted = false
        };
        var revisionExamQ = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = activeCourse.Id, Rubric = rubric, RubricId = rubric.Id, Title = "Revision Exam Q",
            Content = "Content", SampleAnswer = new string('a', 60), SubmittedBy = lecturer.Id,
            ApprovalStatus = ApprovalStatus.SubmittedForReview, IsActive = true, IsDeleted = false
        };
        context.ExamQuestions.AddRange(deletedExamQ, inactiveCourseExamQ, revisionExamQ);
        context.QuestionReviewRequests.Add(new QuestionReviewRequest
        {
            Id = Guid.NewGuid(),
            CourseId = inactiveCourse.Id,
            SubmittedBy = lecturer.Id,
            Items = [new QuestionReviewRequestItem { QuestionId = inactiveCourseExamQ.Id, UsageScope = "exam" }]
        });
        await context.SaveChangesAsync();

        // 1. CreateDraftQuestion rejects inactive course and disabled offering
        var draftRubric = new RubricDraftDto("R", "D", 10m, [new("C1", 5m, 0.5m, "Understand", "D1", 1), new("C2", 5m, 0.5m, "Apply", "D2", 2)]);
        var createHandler = new CreateDraftQuestionCommandHandler(context);
        var inactiveCourseDraft = await createHandler.Handle(new CreateDraftQuestionCommand(
            inactiveCourse.Id, lecturer.Id, "T", "C", new string('a', 60), ["k"], "easy", "Understand", "manual", "exam", false, null, draftRubric), CancellationToken.None);
        inactiveCourseDraft.IsSuccess.Should().BeFalse();
        inactiveCourseDraft.Error.Should().Contain("ngừng hoạt động");

        var disabledOfferingDraft = await createHandler.Handle(new CreateDraftQuestionCommand(
            activeCourseDisabledOffering.Id, lecturer.Id, "T", "C", new string('a', 60), ["k"], "easy", "Understand", "manual", "exam", false, null, draftRubric), CancellationToken.None);
        disabledOfferingDraft.IsSuccess.Should().BeFalse();

        // 2. ReviewQuestionDecision sets ExamQuestion.IsActive = false on NeedsRevision
        var reviewRes = await new ReviewQuestionDecisionCommandHandler(context)
            .Handle(new ReviewQuestionDecisionCommand(revisionExamQ.Id, head.Id, ApprovalStatus.NeedsRevision, "Cần chỉnh sửa thêm nội dung chi tiết", null), CancellationToken.None);
        reviewRes.IsSuccess.Should().BeTrue();
        (await context.ExamQuestions.FindAsync(revisionExamQ.Id))!.IsActive.Should().BeFalse();

        // 3. GetQuestions (via QuestionCourseAccess) excludes inactiveCourse & disabledOffering
        var qList = await new GetQuestionsQueryHandler(context)
            .Handle(new GetQuestionsQuery(ActorId: lecturer.Id, ActorRole: UserRole.Lecturer), CancellationToken.None);
        qList.IsSuccess.Should().BeTrue();
        qList.Value!.Items.Should().ContainSingle(q => q.Id == revisionExamQ.Id);

        // 4. GetLecturerDashboard excludes !IsDeleted & inactiveCourse
        var dashboard = await new GetLecturerDashboardQueryHandler(context)
            .Handle(new GetLecturerDashboardQuery(lecturer.Id), CancellationToken.None);
        dashboard.IsSuccess.Should().BeTrue();
        dashboard.Value!.QuestionStats.TotalExamQuestions.Should().Be(1);
        dashboard.Value.QuestionsNeedingRevision.Should().ContainSingle(q => q.QuestionId == revisionExamQ.Id);

        // 5. QuestionReviewRequestService hides inactiveCourse requests
        var reviewList = await new QuestionReviewRequestService(context).List(head.Id, null, null, null, 1, CancellationToken.None);
        reviewList.Items.Should().BeEmpty();

        // 6. GenerateQuestionsFromClo duplicate-title check ignores IsDeleted = true questions
        var aiMock = new Mock<IAiQuestionGenerationService>();
        aiMock.Setup(a => a.GenerateQuestionsAsync(It.IsAny<AiQuestionGenerationParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GeneratedQuestionDraftDto(
                "t1", "CLO1", "Deleted Exam Title", "Content", "easy", "Understand", QuestionSource.AiGenerated, UsageScope.Exam,
                new string('a', 60), ["k"], false, null, draftRubric)]);
        var genRes = await new GenerateQuestionsFromCloCommandHandler(context, aiMock.Object, new QuestionGenerationGuidanceProvider())
            .Handle(new GenerateQuestionsFromCloCommand(
                activeCourse.Id, lecturer.Id, ["CLO1"], ["Testing"], 1, UsageScope.Exam, ["Understand"], new DifficultyDistributionDto(1, 0, 0)),
                CancellationToken.None);
        genRes.IsSuccess.Should().BeTrue();
        genRes.Value!.Questions.Should().ContainSingle().Which.IsFlagged.Should().BeFalse();
    }

    [Fact(DisplayName = "GetQuestions (ApprovedBy / my-approved) & GetLecturerDashboard (ShiftsPendingAudit): Ẩn môn bị tắt mềm (PRF192)")]
    public async Task ApprovedQuestionsAndLecturerDashboardShifts_ExcludeInactiveCourses()
    {
        using var context = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Email = "head.myapproved@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv.myapproved@fpt.edu.vn", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", IsActive = true };
        var inactiveCourse = new Course { Id = Guid.NewGuid(), Code = "PRF192", Name = "PRF", SemesterId = semester.Id, DepartmentHeadUserId = head.Id, IsActive = false };
        var activeCourse = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "SWT", SemesterId = semester.Id, DepartmentHeadUserId = head.Id, IsActive = true };

        var inactiveRubric = new Rubric { Id = Guid.NewGuid(), CourseId = inactiveCourse.Id, Name = "R-PRF", TotalMaxScore = 10m };
        var activeRubric = new Rubric { Id = Guid.NewGuid(), CourseId = activeCourse.Id, Name = "R-SWT", TotalMaxScore = 10m };

        var inactiveApprovedExamQ = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = inactiveCourse.Id, RubricId = inactiveRubric.Id, Title = "PRF Approved Exam",
            Content = "Content", SampleAnswer = new string('a', 60), SubmittedBy = lecturer.Id,
            ApprovedBy = head.Id, ApprovedAt = DateTime.UtcNow, ApprovalStatus = ApprovalStatus.Approved, IsActive = true, IsDeleted = false
        };
        var activeApprovedExamQ = new ExamQuestion
        {
            Id = Guid.NewGuid(), CourseId = activeCourse.Id, RubricId = activeRubric.Id, Title = "SWT Approved Exam",
            Content = "Content", SampleAnswer = new string('a', 60), SubmittedBy = lecturer.Id,
            ApprovedBy = head.Id, ApprovedAt = DateTime.UtcNow, ApprovalStatus = ApprovalStatus.Approved, IsActive = true, IsDeleted = false
        };
        var inactiveApprovedPracticeQ = new PracticeQuestion
        {
            Id = Guid.NewGuid(), CourseId = inactiveCourse.Id, RubricId = inactiveRubric.Id, Title = "PRF Approved Practice",
            Content = "Content", SampleAnswer = new string('a', 60), SubmittedBy = lecturer.Id,
            ApprovedBy = head.Id, ApprovedAt = DateTime.UtcNow, ApprovalStatus = ApprovalStatus.Approved, IsActive = true, IsDeleted = false
        };
        var activeApprovedPracticeQ = new PracticeQuestion
        {
            Id = Guid.NewGuid(), CourseId = activeCourse.Id, RubricId = activeRubric.Id, Title = "SWT Approved Practice",
            Content = "Content", SampleAnswer = new string('a', 60), SubmittedBy = lecturer.Id,
            ApprovedBy = head.Id, ApprovedAt = DateTime.UtcNow, ApprovalStatus = ApprovalStatus.Approved, IsActive = true, IsDeleted = false
        };

        var inactiveSession = new OfficialExamSession
        {
            Id = Guid.NewGuid(), CourseId = inactiveCourse.Id, Course = inactiveCourse, ExamStructureId = Guid.NewGuid(),
            Title = "PRF Final", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        var activeSession = new OfficialExamSession
        {
            Id = Guid.NewGuid(), CourseId = activeCourse.Id, Course = activeCourse, ExamStructureId = Guid.NewGuid(),
            Title = "SWT Final", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        var inactiveShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(), SessionId = inactiveSession.Id, Session = inactiveSession,
            ShiftName = "Ca PRF", RoomLab = "Lab 1", StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(1)
        };
        var activeShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(), SessionId = activeSession.Id, Session = activeSession,
            ShiftName = "Ca SWT", RoomLab = "Lab 2", StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(1)
        };

        context.Users.AddRange(head, lecturer);
        context.Semesters.Add(semester);
        context.Courses.AddRange(inactiveCourse, activeCourse);
        context.Rubrics.AddRange(inactiveRubric, activeRubric);
        context.ExamQuestions.AddRange(inactiveApprovedExamQ, activeApprovedExamQ);
        context.PracticeQuestions.AddRange(inactiveApprovedPracticeQ, activeApprovedPracticeQ);
        context.OfficialExamSessions.AddRange(inactiveSession, activeSession);
        context.RealExamSessionShifts.AddRange(inactiveShift, activeShift);
        await context.SaveChangesAsync();

        var myApprovedResult = await new GetQuestionsQueryHandler(context)
            .Handle(new GetQuestionsQuery(ActorId: head.Id, ActorRole: UserRole.DepartmentHead, ApprovedBy: head.Id), CancellationToken.None);
        myApprovedResult.IsSuccess.Should().BeTrue();
        myApprovedResult.Value!.Items.Should().HaveCount(2);
        myApprovedResult.Value.Items.Should().OnlyContain(q => q.CourseCode == "SWT301");

        var dashboardResult = await new GetLecturerDashboardQueryHandler(context)
            .Handle(new GetLecturerDashboardQuery(lecturer.Id), CancellationToken.None);
        dashboardResult.IsSuccess.Should().BeTrue();
        dashboardResult.Value!.ShiftsPendingAudit.Should().ContainSingle(s => s.ShiftId == activeShift.Id && s.CourseCode == "SWT301");
    }
}
