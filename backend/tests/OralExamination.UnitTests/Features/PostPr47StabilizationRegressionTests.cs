using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Hubs;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AddClassStudent;
using OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;
using OralExamination.Application.Features.Courses.Commands.CreateCourse;
using OralExamination.Application.Features.Courses.Commands.DeleteCourseClo;
using OralExamination.Application.Features.Courses.Commands.UpdateCourse;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Common.Validation;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.Semesters.Commands.CreateSemester;
using OralExamination.Application.Features.Semesters.Commands.UpdateSemester;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;
using OralExamination.Application.Features.OfficialExams.Commands.CurateExamQuestionBank;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.GenerateOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries;
using OralExamination.Application.Features.OfficialExams.Queries.GetBankCurationCandidates;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features;

public sealed class PostPr47StabilizationRegressionTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static ServiceProvider BuildScopedProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        return services.BuildServiceProvider();
    }

    [Fact(DisplayName = "PR47-01: Model EF và PostgresSchemaReconciler loại bỏ index không lọc uq_exam_sets_course_code, giữ partial unique index và đối soát NEEDS_REVISION")]
    public void Pr47RootCause_EfModelAndReconciler_DropUnfilteredExamSetCourseCodeIndex_AndKeepPartialActiveIndex()
    {
        using var db = CreateDbContext();

        var examSetEntity = db.Model.FindEntityType(typeof(ExamSet))!;
        var indexes = examSetEntity.GetIndexes().ToList();

        indexes.Should().NotContain(idx => idx.GetDatabaseName() == "uq_exam_sets_course_code");
        indexes.Should().Contain(idx =>
            idx.GetDatabaseName() == "uq_exam_sets_active_course_set_code"
            && idx.IsUnique
            && idx.GetFilter() == "is_active = true");

        var postSql = PostgresSchemaReconciler.GetPostMigrationSql();
        postSql.Should().Contain("DROP INDEX IF EXISTS uq_exam_sets_course_code;");
        postSql.Should().Contain("uq_exam_sets_active_course_set_code");
        postSql.Should().Contain("ck_exam_sets_status");
        postSql.Should().Contain("'NEEDS_REVISION'");

        Action formatCheck = () => _ = string.Format(CultureInfo.InvariantCulture, postSql, Array.Empty<object>());
        formatCheck.Should().NotThrow();
    }

    [Fact(DisplayName = "PR47-02: CreateExamSet theo phân công cho phép tái sử dụng SetCode của bộ đề đã xóa mềm (IsActive = false)")]
    public async Task CreateExamSet_AssignmentFlow_AllowsReusingSetCodeOfSoftDeletedExamSet()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = DateOnly.FromDateTime(now.AddDays(-10)), EndDate = DateOnly.FromDateTime(now.AddDays(90)), IsActive = true };
        var deptHead = new User { Id = Guid.NewGuid(), Email = "dh.pr47@fpt.edu.vn", FullName = "Dept Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.pr47@fpt.edu.vn", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.pr47@fpt.edu.vn", FullName = "Officer", Role = UserRole.ExaminationOfficer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWP391", Name = "SWP391", SemesterId = semester.Id, DepartmentHeadUserId = deptHead.Id, IsActive = true };
        var courseLecturer = new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecturer.Id, AssignedByUserId = deptHead.Id, SemesterId = semester.Id, IsActive = true };
        var clo = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Design", BloomLevel = BloomLevel.Apply, OrderIndex = 1, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = deptHead.Id, Name = "Struct 1", TotalQuestions = 1, DurationMinutes = 20, EasyCount = 1, MediumCount = 0, HardCount = 0, RequiredSetCount = 2, IsActive = true };
        var structureClo = new ExamStructureClo { StructureId = structure.Id, CloId = clo.Id };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), SemesterId = semester.Id, CourseId = course.Id, ExamStructureId = structure.Id, Title = "FE SWP391", ExamDate = DateOnly.FromDateTime(now.AddDays(5)), Status = "scheduled", CreatedBy = officer.Id };
        var preparation = new ExamPreparation { Id = Guid.NewGuid(), SessionId = session.Id, RequiredSetCount = 2, ReusePercent = 30, Deadline = now.AddDays(3), RequestedBy = officer.Id };
        var assignment = new ExamAuthoringAssignment { Id = Guid.NewGuid(), PreparationId = preparation.Id, StructureId = structure.Id, LecturerId = lecturer.Id, AssignedBy = deptHead.Id, SetCount = 1, Deadline = now.AddDays(2) };

        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric 10", TotalMaxScore = 10m, IsActive = true };
        rubric.Criteria.Add(new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Accuracy", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Apply, OrderIndex = 1 });
        rubric.Criteria.Add(new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Depth", MaxScore = 5m, Weight = 0.5m, BloomLevel = BloomLevel.Apply, OrderIndex = 2 });

        var bankQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloId = clo.Id,
            RubricId = rubric.Id,
            Rubric = rubric,
            Title = "Clean Architecture",
            Content = "Explain dependency rule in Clean Architecture.",
            SampleAnswer = new string('A', 80),
            Difficulty = "easy",
            BloomLevel = BloomLevel.Apply,
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.BankReady,
            IsActive = true,
            IsDeleted = false,
            IsPaperCopy = false
        };

        var softDeletedSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SessionId = session.Id,
            CreatedBy = lecturer.Id,
            SetCode = "DE-01",
            Status = ExamSetStatus.Draft,
            IsActive = false
        };

        db.Semesters.Add(semester);
        db.Users.AddRange(deptHead, lecturer, officer);
        db.Courses.Add(course);
        db.CourseLecturers.Add(courseLecturer);
        db.CourseClos.Add(clo);
        db.ExamStructures.Add(structure);
        db.ExamStructureClos.Add(structureClo);
        db.OfficialExamSessions.Add(session);
        db.ExamPreparations.Add(preparation);
        db.ExamAuthoringAssignments.Add(assignment);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(bankQuestion);
        db.ExamSets.Add(softDeletedSet);
        await db.SaveChangesAsync();

        var handler = new CreateExamSetCommandHandler(db);
        var result = await handler.Handle(
            new CreateExamSetCommand(
                StructureId: structure.Id,
                CourseId: course.Id,
                SessionId: session.Id,
                SetCode: "DE-01",
                QuestionIds: [bankQuestion.Id],
                ActorUserId: lecturer.Id,
                ActorRole: UserRole.Lecturer,
                AssignmentId: assignment.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.SetCode.Should().Be("DE-01");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "PR47-03: DeleteExamSet chặn xóa bộ đề theo phân công (AssignmentId) và chặn Trưởng BM xóa DRAFT của giảng viên khác")]
    public async Task DeleteExamSet_BlocksAssignmentBackedPaper_AndBlocksDeptHeadDeletingLecturerDraft()
    {
        await using var db = CreateDbContext();

        var deptHead = new User { Id = Guid.NewGuid(), Email = "dh.del@fpt.edu.vn", FullName = "DH", Role = UserRole.DepartmentHead, IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.del@fpt.edu.vn", FullName = "Lec", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = deptHead.Id, IsActive = true };
        var cl = new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecturer.Id, AssignedByUserId = deptHead.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = deptHead.Id, Name = "S1", TotalQuestions = 2, DurationMinutes = 30, IsActive = true };

        var assignedDraft = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            AssignmentId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CreatedBy = lecturer.Id,
            SetCode = "ASSIGN-01",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };

        var legacyLecturerDraft = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            AssignmentId = null,
            CreatedBy = lecturer.Id,
            SetCode = "LEGACY-DRAFT-01",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };

        db.Users.AddRange(deptHead, lecturer);
        db.Courses.Add(course);
        db.CourseLecturers.Add(cl);
        db.ExamStructures.Add(structure);
        db.ExamSets.AddRange(assignedDraft, legacyLecturerDraft);
        await db.SaveChangesAsync();

        var handler = new DeleteExamSetCommandHandler(db);

        var assignedDeleteResult = await handler.Handle(
            new DeleteExamSetCommand(assignedDraft.Id, lecturer.Id, UserRole.Lecturer),
            CancellationToken.None);
        assignedDeleteResult.IsSuccess.Should().BeFalse();
        assignedDeleteResult.Error.Should().StartWith("CONFLICT:");

        var deptHeadDeleteLecturerDraftResult = await handler.Handle(
            new DeleteExamSetCommand(legacyLecturerDraft.Id, deptHead.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        deptHeadDeleteLecturerDraftResult.IsSuccess.Should().BeFalse();
        deptHeadDeleteLecturerDraftResult.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "PR47-04: DeleteCourseClo trả về 409 CONFLICT: CLO_IN_USE khi CLO đang được tham chiếu bởi ExamStructureClo")]
    public async Task DeleteCourseClo_ReturnsConflictCloInUse_WhenReferencedByExamStructureClo()
    {
        await using var db = CreateDbContext();

        var course = new Course { Id = Guid.NewGuid(), Code = "PRN222", Name = "PRN222", IsActive = true };
        var clo = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "EF Core", BloomLevel = BloomLevel.Apply, OrderIndex = 1, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct", TotalQuestions = 2, DurationMinutes = 30, IsActive = true };
        var link = new ExamStructureClo { StructureId = structure.Id, CloId = clo.Id };

        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        db.ExamStructures.Add(structure);
        db.ExamStructureClos.Add(link);
        await db.SaveChangesAsync();

        var handler = new DeleteCourseCloCommandHandler(db);
        var result = await handler.Handle(
            new DeleteCourseCloCommand(course.Id, clo.Id, UserRole.Admin),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CONFLICT: CLO_IN_USE");
    }

    [Fact(DisplayName = "PR47-05: ExamPreparationWorkflow kiểm tra trần 1-20 câu / 5-180 phút và chặn RequestChanges/Reject với bộ đề xóa mềm hoặc SampleSet")]
    public async Task ExamPreparationWorkflow_ConfigureEnforcesUpperBounds_AndReviewRejectsInactiveOrSampleSet()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var deptHead = new User { Id = Guid.NewGuid(), Email = "dh.wf@fpt.edu.vn", FullName = "DH", Role = UserRole.DepartmentHead, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.wf@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "SWT301", DepartmentHeadUserId = deptHead.Id, IsActive = true };
        var clo = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Testing", BloomLevel = BloomLevel.Apply, OrderIndex = 1, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, Title = "FE SWT301", ExamDate = DateOnly.FromDateTime(now.AddDays(5)), Status = "scheduled", CreatedBy = officer.Id };
        var preparation = new ExamPreparation { Id = Guid.NewGuid(), SessionId = session.Id, RequiredSetCount = 2, ReusePercent = 30, Deadline = now.AddDays(4), RequestedBy = officer.Id };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = deptHead.Id, Name = "S1", TotalQuestions = 2, DurationMinutes = 30, IsActive = true };
        var assignment = new ExamAuthoringAssignment { Id = Guid.NewGuid(), PreparationId = preparation.Id, StructureId = structure.Id, LecturerId = Guid.NewGuid(), AssignedBy = deptHead.Id, SetCount = 1, Deadline = now.AddDays(2) };

        var inactiveSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SessionId = session.Id, AssignmentId = assignment.Id, SetCode = "INACTIVE-01", Status = ExamSetStatus.SubmittedForReview, IsActive = false };
        var sampleSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SessionId = session.Id, AssignmentId = assignment.Id, SetCode = "SAMPLE-01", Status = ExamSetStatus.SubmittedForReview, IsActive = true, IsSampleSet = true };

        db.Users.AddRange(deptHead, officer);
        db.Courses.Add(course);
        db.CourseClos.Add(clo);
        db.OfficialExamSessions.Add(session);
        db.ExamPreparations.Add(preparation);
        db.ExamStructures.Add(structure);
        db.ExamAuthoringAssignments.Add(assignment);
        db.ExamSets.AddRange(inactiveSet, sampleSet);
        await db.SaveChangesAsync();

        var handler = new ExamPreparationCommandHandler(db);

        var overQuestionLimit = await handler.Handle(
            new ExamPreparationCommand(session.Id, deptHead.Id, UserRole.DepartmentHead, PreparationAction.Configure,
                Blueprint: new ConfigureExamBlueprint("Too Many", 21, 30, 7, 7, 7, [clo.Id], 30)),
            CancellationToken.None);
        overQuestionLimit.IsSuccess.Should().BeFalse();
        overQuestionLimit.Error.Should().StartWith("VALIDATION:");

        var overDurationLimit = await handler.Handle(
            new ExamPreparationCommand(session.Id, deptHead.Id, UserRole.DepartmentHead, PreparationAction.Configure,
                Blueprint: new ConfigureExamBlueprint("Too Long", 3, 181, 1, 1, 1, [clo.Id], 30)),
            CancellationToken.None);
        overDurationLimit.IsSuccess.Should().BeFalse();
        overDurationLimit.Error.Should().StartWith("VALIDATION:");

        var underDurationLimit = await handler.Handle(
            new ExamPreparationCommand(session.Id, deptHead.Id, UserRole.DepartmentHead, PreparationAction.Configure,
                Blueprint: new ConfigureExamBlueprint("Too Short", 3, 4, 1, 1, 1, [clo.Id], 30)),
            CancellationToken.None);
        underDurationLimit.IsSuccess.Should().BeFalse();
        underDurationLimit.Error.Should().StartWith("VALIDATION:");

        var reviewInactive = await handler.Handle(
            new ExamPreparationCommand(inactiveSet.Id, deptHead.Id, UserRole.DepartmentHead, PreparationAction.RequestChanges,
                Review: new ReviewExamSet("Cần chỉnh sửa lại.")),
            CancellationToken.None);
        reviewInactive.IsSuccess.Should().BeFalse();
        reviewInactive.Error.Should().Be(OfficialExamErrors.ExamSetStatusInvalid);

        var rejectSample = await handler.Handle(
            new ExamPreparationCommand(sampleSet.Id, deptHead.Id, UserRole.DepartmentHead, PreparationAction.Reject,
                Review: new ReviewExamSet("Từ chối đề mẫu.")),
            CancellationToken.None);
        rejectSample.IsSuccess.Should().BeFalse();
        rejectSample.Error.Should().Be(OfficialExamErrors.ExamSetStatusInvalid);
    }

    [Fact(DisplayName = "PR47-06: GetExamPreparation(Detail=true) trả về NOT_FOUND sạch thay vì ném InvalidOperationException khi bộ đề đã bị xóa mềm")]
    public async Task GetExamPreparation_Detail_ReturnsNotFoundForSoftDeletedSet_WithoutThrowing()
    {
        await using var db = CreateDbContext();

        var deptHead = new User { Id = Guid.NewGuid(), Email = "dh.detail@fpt.edu.vn", FullName = "DH", Role = UserRole.DepartmentHead, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", DepartmentHeadUserId = deptHead.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = deptHead.Id, Name = "S1", TotalQuestions = 1, DurationMinutes = 20, IsActive = true };
        var softDeletedSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SOFT-DEL-01",
            Status = ExamSetStatus.SubmittedForReview,
            IsActive = false
        };

        db.Users.Add(deptHead);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.ExamSets.Add(softDeletedSet);
        await db.SaveChangesAsync();

        var handler = new GetExamPreparationQueryHandler(db);
        var result = await handler.Handle(
            new GetExamPreparationQuery(null, deptHead.Id, UserRole.DepartmentHead, ExamSetId: softDeletedSet.Id, Detail: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.ExamSetNotFound);
    }

    [Fact(DisplayName = "PR47-07: Bank Curation (Query & Command) lọc bỏ hoàn toàn bản sao đề thi (IsPaperCopy = true)")]
    public async Task BankCuration_IgnoresPaperCopies_InBothQueryAndCommand()
    {
        await using var db = CreateDbContext();

        var deptHead = new User { Id = Guid.NewGuid(), Email = "dh.curate@fpt.edu.vn", FullName = "DH", Role = UserRole.DepartmentHead, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRJ301", Name = "PRJ301", DepartmentHeadUserId = deptHead.Id, IsActive = true };
        var realCandidate = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Servlet Lifecycle",
            Content = "Explain init, service, destroy methods in Jakarta Servlet lifecycle.",
            SampleAnswer = new string('B', 70),
            Difficulty = "medium",
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.PendingCuration,
            IsActive = true,
            IsDeleted = false,
            IsPaperCopy = false
        };
        var paperCopy = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Paper Copy Snapshot",
            Content = "Immutable paper snapshot copy should never appear in curation.",
            SampleAnswer = new string('C', 70),
            Difficulty = "medium",
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            BankCurationStatus = BankCurationStatus.PendingCuration,
            IsActive = true,
            IsDeleted = false,
            IsPaperCopy = true
        };

        db.Users.Add(deptHead);
        db.Courses.Add(course);
        db.ExamQuestions.AddRange(realCandidate, paperCopy);
        await db.SaveChangesAsync();

        var queryHandler = new GetBankCurationCandidatesQueryHandler(db);
        var queryResult = await queryHandler.Handle(
            new GetBankCurationCandidatesQuery(course.Id, deptHead.Id, UserRole.DepartmentHead),
            CancellationToken.None);

        queryResult.IsSuccess.Should().BeTrue(queryResult.Error);
        queryResult.Value.Items.Should().ContainSingle(c => c.QuestionId == realCandidate.Id);
        queryResult.Value.Items.Should().NotContain(c => c.QuestionId == paperCopy.Id);

        var cmdHandler = new CurateExamQuestionBankCommandHandler(db);
        var cmdResult = await cmdHandler.Handle(
            new CurateExamQuestionBankCommand(course.Id, [paperCopy.Id], BankCurationStatus.CuratedIntoBank, "Try curate copy", deptHead.Id, UserRole.DepartmentHead),
            CancellationToken.None);

        cmdResult.IsSuccess.Should().BeFalse();
        cmdResult.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "MF04-RACE-01: SubmitOfficialQuestionAnswer hoãn GradingJob khi có follow-up; GenerateFollowUp giải phóng NextAttemptAt hoặc tái dùng AiEvaluation.FollowUpPrompt")]
    public async Task Mf04_SubmitAnswer_DefersGradingJobWhenFollowUpEligible_AndFollowUpHandlerReusesOrReleases()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        const string deviceId = "BYOD-LAPTOP-01-STABILIZE";
        const string rawSessionToken = "kiosk-session-token-1234567890";
        var sha256Hex = new string('a', 64);

        var student = new User { Id = Guid.NewGuid(), Email = "sv.race@fpt.edu.vn", FullName = "SV Race", StudentCode = "SE180101", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWP391", Name = "SWP391", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "S1", TotalQuestions = 2, DurationMinutes = 30, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R10", TotalMaxScore = 10m, IsActive = true };
        rubric.Criteria.Add(new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "C1", MaxScore = 5m, Weight = 0.5m, OrderIndex = 1 });
        rubric.Criteria.Add(new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "C2", MaxScore = 5m, Weight = 0.5m, OrderIndex = 2 });

        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Rubric = rubric, Title = "Q1", Content = "Explain CAP theorem", SampleAnswer = new string('X', 80), Difficulty = "medium", BloomLevel = BloomLevel.Apply, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Rubric = rubric, Title = "Q2", Content = "Explain ACID properties", SampleAnswer = new string('Y', 80), Difficulty = "medium", BloomLevel = BloomLevel.Apply, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "RACE-01", Status = ExamSetStatus.HandedOver, IsActive = true };
        var esq1 = new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = q1.Id, OrderIndex = 1 };
        var esq2 = new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = q2.Id, OrderIndex = 2 };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = structure.Id, Title = "FE Race", ExamDate = DateOnly.FromDateTime(now), HasFollowUp = true, MaxFollowUpQuestions = 2, ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit, Status = "in_progress" };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", StartTime = now.AddMinutes(-10), EndTime = now.AddMinutes(50), Status = ExamShiftWindow.InProgress, IsExamPublished = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress,
            DeviceId = deviceId,
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(rawSessionToken)
        };

        db.Users.Add(student);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(q1, q2);
        db.ExamSets.Add(examSet);
        db.ExamSetQuestions.AddRange(esq1, esq2);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var expectedKeyQ1 = OfficialExamAudioObjectKey.CreateForQuestion(shift.Id, ticket.Id, ticket.SeatNumber, student.StudentCode!, 1, 0);
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.GetObjectMetadataAsync(expectedKeyQ1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectMetadata(expectedKeyQ1, 1024, "audio/webm", "etag", now));
        storageMock.Setup(s => s.VerifySha256Async(expectedKeyQ1, sha256Hex, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var beforeSubmit = DateTime.UtcNow;
        var submitHandler = new SubmitOfficialQuestionAnswerCommandHandler(db, storageMock.Object);
        var submitResult = await submitHandler.Handle(
            new SubmitOfficialQuestionAnswerCommand(
                student.Id, ticket.Id, q1.Id, 0, null, expectedKeyQ1, sha256Hex,
                "CAP theorem nói về Consistency, Availability và Partition Tolerance.",
                "CAP theorem nói về Consistency, Availability và Partition Tolerance.",
                null, 45, false, deviceId, rawSessionToken),
            CancellationToken.None);

        submitResult.IsSuccess.Should().BeTrue(submitResult.Error);
        submitResult.Value.CanRequestFollowUp.Should().BeTrue();

        var q1Job = await db.GradingJobs.SingleAsync(j => j.SubmissionId == submitResult.Value.SubmissionId);
        q1Job.NextAttemptAt.Should().BeAfter(beforeSubmit.AddSeconds(OfficialExamRules.FollowUpGradingDeferralSeconds - 5));

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 6.5m,
                ConfidenceScore = 0.90m,
                Feedback = "Good base answer.",
                FollowUpPrompt = "Khi xảy ra network partition trong hệ phân tán, vì sao không thể đạt đồng thời Consistency và Availability?"
            });

        var followUpHandler = new GenerateOfficialExamFollowUpCommandHandler(db, aiMock.Object, storageMock.Object);
        var followUpResult = await followUpHandler.Handle(
            new GenerateOfficialExamFollowUpCommand(student.Id, ticket.Id, submitResult.Value.SubmissionId, deviceId, rawSessionToken),
            CancellationToken.None);

        followUpResult.IsSuccess.Should().BeTrue(followUpResult.Error);
        followUpResult.Value.HasMoreFollowUp.Should().BeTrue();
        followUpResult.Value.FollowUpPrompt.Should().Contain("network partition");

        var releasedJob = await db.GradingJobs.SingleAsync(j => j.SubmissionId == submitResult.Value.SubmissionId);
        releasedJob.NextAttemptAt.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(2));

        // Case B: Worker đã chấm xong trước và lưu sẵn AiEvaluation.FollowUpPrompt -> GenerateFollowUp tái dùng 0 lần gọi STT/LLM!
        var q2Sub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = q2.Id,
            FollowUpIndex = 0,
            IsFollowUp = false,
            AudioObjectKey = OfficialExamAudioObjectKey.CreateForQuestion(shift.Id, ticket.Id, ticket.SeatNumber, student.StudentCode!, 2, 0),
            AudioHashSha256 = sha256Hex,
            TranscriptWhisper = "ACID gồm Atomicity, Consistency, Isolation, Durability.",
            GradingStatus = "ai_graded",
            AiScore = 7.0m,
            SubmittedAt = now
        };
        var q2Eval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            ExamSubmissionId = q2Sub.Id,
            StudentId = student.Id,
            TotalScore = 7.0m,
            ConfidenceScore = 0.92m,
            FollowUpPrompt = "Hãy phân biệt Repeatable Read và Serializable khi tránh phantom read?"
        };
        db.ExamQuestionSubmissions.Add(q2Sub);
        db.AiEvaluations.Add(q2Eval);
        await db.SaveChangesAsync();

        var strictAiMock = new Mock<IAiGradingService>(MockBehavior.Strict);
        var strictSttMock = new Mock<ISpeechToTextService>(MockBehavior.Strict);
        var reuseFollowUpHandler = new GenerateOfficialExamFollowUpCommandHandler(db, strictAiMock.Object, storageMock.Object, strictSttMock.Object);

        var reuseResult = await reuseFollowUpHandler.Handle(
            new GenerateOfficialExamFollowUpCommand(student.Id, ticket.Id, q2Sub.Id, deviceId, rawSessionToken),
            CancellationToken.None);

        reuseResult.IsSuccess.Should().BeTrue(reuseResult.Error);
        reuseResult.Value.HasMoreFollowUp.Should().BeTrue();
        reuseResult.Value.FollowUpPrompt.Should().Be(q2Eval.FollowUpPrompt);
        strictAiMock.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "MF04-RACE-02: SubmitOfficialExam(FinalizePerTurn=true) giải phóng ngay NextAttemptAt của các GradingJob đang hoãn follow-up")]
    public async Task Mf04_SubmitOfficialExam_FinalizePerTurn_ImmediatelyReleasesDeferredGradingJobs()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        const string deviceId = "BYOD-FINAL-01-STABILIZE";
        const string rawSessionToken = "kiosk-final-token-99999999";

        var student = new User { Id = Guid.NewGuid(), Email = "sv.final@fpt.edu.vn", FullName = "SV Final", StudentCode = "SE180202", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "S1", TotalQuestions = 1, DurationMinutes = 20, IsActive = true };
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Explain JWT", SampleAnswer = new string('Z', 70), ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "FIN-01", Status = ExamSetStatus.HandedOver, IsActive = true };
        var esq1 = new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = q1.Id, OrderIndex = 1 };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = structure.Id, Title = "FE Final", ExamDate = DateOnly.FromDateTime(now), HasFollowUp = true, MaxFollowUpQuestions = 2, Status = "in_progress" };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", StartTime = now.AddMinutes(-10), EndTime = now.AddMinutes(40), Status = ExamShiftWindow.InProgress, IsExamPublished = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress,
            DeviceId = deviceId,
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(rawSessionToken)
        };
        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = q1.Id,
            FollowUpIndex = 0,
            IsFollowUp = false,
            AudioObjectKey = "official-exams/key.webm",
            AudioHashSha256 = new string('b', 64),
            TranscriptWhisper = "JWT là JSON Web Token dùng ký số bằng HMAC hoặc RSA.",
            GradingStatus = "pending",
            SubmittedAt = now
        };
        var deferredJob = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Status = "pending",
            AttemptCount = 0,
            NextAttemptAt = now.AddMinutes(5),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(student);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.ExamQuestions.Add(q1);
        db.ExamSets.Add(examSet);
        db.ExamSetQuestions.Add(esq1);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.Add(submission);
        db.GradingJobs.Add(deferredJob);
        await db.SaveChangesAsync();

        var handler = new SubmitOfficialExamCommandHandler(db);
        var result = await handler.Handle(
            new SubmitOfficialExamCommand(student.Id, ticket.Id, "127.0.0.1", null, deviceId, rawSessionToken, FinalizePerTurn: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var updatedTicket = await db.StudentExamTickets.SingleAsync(t => t.Id == ticket.Id);
        updatedTicket.Status.Should().Be(ExamTicketStatus.Submitted);

        var updatedJob = await db.GradingJobs.SingleAsync(j => j.Id == deferredJob.Id);
        updatedJob.NextAttemptAt.Should().NotBeNull();
        updatedJob.NextAttemptAt!.Value.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(2));
    }

    [Fact(DisplayName = "MF04-DLQ-01: OfficialExamGradingWorker dừng retry ngay với AUDIO_INTEGRITY_MISMATCH, gắn cờ nghi vấn và đặt DLQ = abandoned")]
    public async Task OfficialExamGradingWorker_AudioIntegrityMismatch_MarksTerminalAbandoned_AndPreventsDlqReplay()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var provider = BuildScopedProvider(dbName);
        var now = DateTime.UtcNow;

        var ticketId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.StudentExamTickets.Add(new StudentExamTicket
            {
                Id = ticketId,
                ShiftId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                SeatNumber = 1,
                Status = ExamTicketStatus.Submitted
            });
            db.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
            {
                Id = submissionId,
                TicketId = ticketId,
                ExamQuestionId = Guid.NewGuid(),
                FollowUpIndex = 0,
                GradingStatus = "pending",
                IsSuspicious = false,
                SubmittedAt = now
            });
            db.GradingJobs.Add(new GradingJob
            {
                Id = jobId,
                SubmissionId = submissionId,
                Status = "processing",
                AttemptCount = 1,
                LockedUntil = now.AddMinutes(5),
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        var queueMock = new Mock<IOfficialExamGradingQueue>();
        var worker = new OfficialExamGradingWorker(
            queueMock.Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamGradingWorker>.Instance);

        await worker.MarkFailedAsync(
            new OfficialExamGradingTask(jobId, submissionId, ClaimedAttemptCount: 1),
            new InvalidOperationException("AUDIO_INTEGRITY_MISMATCH: SHA-256 trên R2 không khớp bản ghi khi nộp."),
            CancellationToken.None);

        using (var verifyScope = provider.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var sub = await db.ExamQuestionSubmissions.SingleAsync(s => s.Id == submissionId);
            sub.IsSuspicious.Should().BeTrue();
            sub.GradingStatus.Should().Be("failed");

            var job = await db.GradingJobs.SingleAsync(j => j.Id == jobId);
            job.Status.Should().Be("failed");
            job.NextAttemptAt.Should().BeNull();

            var dlq = await db.DeadLetterQueues.SingleAsync();
            dlq.Status.Should().Be("abandoned");
            dlq.NextRetryAt.Should().BeNull();
            dlq.ResolutionNotes.Should().NotBeNullOrWhiteSpace();
        }

        var replayWorker = new OfficialExamDlqReplayWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamDlqReplayWorker>.Instance);
        await replayWorker.ReplayDueAsync(CancellationToken.None);

        using (var postReplayScope = provider.CreateScope())
        {
            var db = postReplayScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var jobAfterReplay = await db.GradingJobs.SingleAsync(j => j.Id == jobId);
            jobAfterReplay.Status.Should().Be("failed");
            jobAfterReplay.NextAttemptAt.Should().BeNull();
            var dlqAfterReplay = await db.DeadLetterQueues.SingleAsync();
            dlqAfterReplay.Status.Should().Be("abandoned");
        }
    }

    [Fact(DisplayName = "OPS-01: MockExamsController.SubmitAnswer khớp trần RequestSizeLimit 26 MiB với Nginx/UploadBudgetMiddleware")]
    public void MockExamsController_SubmitAnswer_Has26MiBRequestSizeLimit()
    {
        var method = typeof(MockExamsController).GetMethod(nameof(MockExamsController.SubmitAnswer));
        method.Should().NotBeNull();

        var sizeAttr = method!.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(RequestSizeLimitAttribute));
        sizeAttr.Should().NotBeNull();
        sizeAttr!.ConstructorArguments.Should().ContainSingle();
        Convert.ToInt64(sizeAttr.ConstructorArguments[0].Value, CultureInfo.InvariantCulture)
            .Should().Be(26L * 1024L * 1024L);
    }

    [Fact(DisplayName = "OPS-02: OfficialExamDlqReplayWorker giữ chu kỳ DLQ 5 phút nhưng cứu hộ nhanh >20 bài MF-05 bị kẹt sau 3 phút")]
    public async Task OfficialExamDlqReplayWorker_Preserves5MinDlqPoll_AndRecoversOver20StuckInClassSubmissionsFast()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var provider = BuildScopedProvider(dbName);
        var stuckTime = DateTime.UtcNow.AddMinutes(-4);

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var shiftId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            for (var i = 0; i < 25; i++)
            {
                db.InClassExamSubmissions.Add(new InClassExamSubmission
                {
                    Id = Guid.NewGuid(),
                    ShiftId = shiftId,
                    StudentId = studentId,
                    QuestionId = Guid.NewGuid(),
                    Transcript = $"Answer {i}",
                    Status = "SUBMITTED",
                    SubmittedAt = stuckTime
                });
            }

            await db.SaveChangesAsync();
        }

        var enqueued = new List<GradingTask>();
        var channelMock = new Mock<IGradingQueueChannel>();
        channelMock.Setup(c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Callback<GradingTask, CancellationToken>((t, _) => enqueued.Add(t))
            .Returns(ValueTask.CompletedTask);

        var worker = new OfficialExamDlqReplayWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            channelMock.Object);

        worker.PollInterval.Should().Be(TimeSpan.FromMinutes(5));
        worker.RecoveryPollInterval.Should().Be(TimeSpan.FromSeconds(30));
        worker.StuckRecoveryWindow.Should().Be(TimeSpan.FromMinutes(3));

        await worker.RecoverInterruptedAsync(CancellationToken.None);
        enqueued.Should().HaveCount(25);
    }

    [Fact(DisplayName = "OPS-03: InClassShiftHub.JoinShift chỉ chấp nhận ClassEnrollment.Status == 'enrolled' và từ chối 'ACTIVE' giống REST handler")]
    public async Task InClassShiftHub_JoinShift_AcceptsEnrolled_AndRejectsLegacyActiveStatus()
    {
        await using var db = CreateDbContext();

        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.hub@fpt.edu.vn", FullName = "Lec", Role = UserRole.Lecturer, IsActive = true };
        var enrolledStudent = new User { Id = Guid.NewGuid(), Email = "sv.enrolled@fpt.edu.vn", FullName = "SV Enrolled", Role = UserRole.Student, IsActive = true };
        var legacyActiveStudent = new User { Id = Guid.NewGuid(), Email = "sv.active@fpt.edu.vn", FullName = "SV Active", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN212", Name = "PRN212", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), CourseId = course.Id, LecturerId = lecturer.Id, Code = "SE1801", Name = "SE1801", JoinCode = "CLS001", IsActive = true };
        var shift = new InClassExamShift { Id = Guid.NewGuid(), ClassId = cls.Id, LecturerId = lecturer.Id, ShiftName = "Quiz 1", TotalQuestionsPerStudent = 2, Status = "OPEN", TotpSecretKey = "JBSWY3DPEHPK3PXP" };

        db.Users.AddRange(lecturer, enrolledStudent, legacyActiveStudent);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.InClassExamShifts.Add(shift);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = enrolledStudent.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = legacyActiveStudent.Id, Status = "ACTIVE" });
        await db.SaveChangesAsync();

        static InClassShiftHub CreateHubForUser(OralExamDbContext context, User user, Mock<IGroupManager> groupsMock)
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("auth_version", user.AuthVersion.ToString(CultureInfo.InvariantCulture))
                ],
                "TestAuth");
            var callerContext = new Mock<HubCallerContext>();
            callerContext.SetupGet(c => c.User).Returns(new ClaimsPrincipal(identity));
            callerContext.SetupGet(c => c.ConnectionId).Returns($"conn-{user.Id}");
            callerContext.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);

            return new InClassShiftHub(context)
            {
                Context = callerContext.Object,
                Groups = groupsMock.Object
            };
        }

        var enrolledGroups = new Mock<IGroupManager>();
        var enrolledHub = CreateHubForUser(db, enrolledStudent, enrolledGroups);
        await enrolledHub.JoinShift(shift.Id.ToString());
        enrolledGroups.Verify(
            g => g.AddToGroupAsync($"conn-{enrolledStudent.Id}", InClassShiftHub.StudentGroupName(shift.Id, enrolledStudent.Id), It.IsAny<CancellationToken>()),
            Times.Once);

        var legacyGroups = new Mock<IGroupManager>();
        var legacyHub = CreateHubForUser(db, legacyActiveStudent, legacyGroups);
        var act = async () => await legacyHub.JoinShift(shift.Id.ToString());
        await act.Should().ThrowAsync<HubException>().WithMessage("*FORBIDDEN*");
    }

    [Fact(DisplayName = "M1-VAL-01: Course/Semester/Class/Rubric validators đồng bộ độ dài DB 3NF, cấu hình DeptHead và BloomLevel chuẩn")]
    public async Task AcademicAndQuestionValidators_EnforceDbBounds_DeptHeadMockLimits_AndCanonicalBloom()
    {
        var updateCourseValidator = new UpdateCourseCommandValidator();
        updateCourseValidator.Validate(new UpdateCourseCommand(
            Guid.NewGuid(), "PRN231", "Cross-Platform", Credits: 3, MaxMockExamsPerDay: 3,
            MockExamDurationMinutes: 1, MockExamTotalQuestions: 50)).IsValid.Should().BeTrue();
        updateCourseValidator.Validate(new UpdateCourseCommand(
            Guid.NewGuid(), "PRN231", "Cross-Platform", Credits: 3, MaxMockExamsPerDay: 3,
            MockExamDurationMinutes: 20, MockExamTotalQuestions: 101)).IsValid.Should().BeFalse();
        updateCourseValidator.Validate(new UpdateCourseCommand(
            Guid.NewGuid(), new string('C', 21), "Valid Name")).IsValid.Should().BeFalse();
        updateCourseValidator.Validate(new UpdateCourseCommand(
            Guid.NewGuid(), "PRN231", new string('N', 201))).IsValid.Should().BeFalse();

        var createCourseValidator = new CreateCourseCommandValidator();
        createCourseValidator.Validate(new CreateCourseCommand(
            "PRN231", "Cross-Platform", Credits: 3, MockExamDurationMinutes: 1, MockExamTotalQuestions: 100)).IsValid.Should().BeTrue();
        createCourseValidator.Validate(new CreateCourseCommand(
            new string('C', 21), "Valid Name")).IsValid.Should().BeFalse();

        var createClassValidator = new CreateClassCommandValidator();
        createClassValidator.Validate(new CreateClassCommand(
            "SE1801", "SE1801", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            JoinCode: new string('J', 21))).IsValid.Should().BeFalse();

        var resetPasswordValidator = new ResetClassPasswordCommandValidator();
        resetPasswordValidator.Validate(new ResetClassPasswordCommand(
            Guid.NewGuid(), Guid.NewGuid(), NewJoinCode: new string('J', 21))).IsValid.Should().BeFalse();

        await using var db = CreateDbContext();
        var admin = new User { Id = Guid.NewGuid(), Email = "admin.val@fpt.edu.vn", FullName = "Admin", Role = UserRole.Admin, IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.val@fpt.edu.vn", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", SemesterId = semester.Id, IsActive = true };
        db.Users.AddRange(admin, lecturer);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { CourseId = course.Id, SemesterId = semester.Id, LecturerUserId = lecturer.Id, IsActive = true });
        await db.SaveChangesAsync();

        var createSemRes = await new CreateSemesterCommandHandler(db).Handle(new CreateSemesterCommand(
            admin.Id, new string('S', 21), "Fall 2026", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31)), default);
        createSemRes.IsSuccess.Should().BeFalse();
        createSemRes.Error.Should().StartWith("VALIDATION:");

        var updateSemRes = await new UpdateSemesterCommandHandler(db).Handle(new UpdateSemesterCommand(
            admin.Id, semester.Id, "FA26", new string('N', 101), new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31), true), default);
        updateSemRes.IsSuccess.Should().BeFalse();
        updateSemRes.Error.Should().StartWith("VALIDATION:");

        var invalidRubricRes = await new CreateRubricCommandHandler(db).Handle(new CreateRubricCommand(
            lecturer.Id, course.Id, "Rubric Bad Bloom", null,
            [
                new RubricCriterionDraftDto("C1", 5m, 0.5m, "Mastery", "Invalid bloom", 1),
                new RubricCriterionDraftDto("C2", 5m, 0.5m, BloomLevel.Understand, "Valid bloom", 2)
            ]), default);
        invalidRubricRes.IsSuccess.Should().BeFalse();
        invalidRubricRes.Error.Should().StartWith("VALIDATION:");

        var invalidDraftRes = await new CreateDraftQuestionCommandHandler(db).Handle(new CreateDraftQuestionCommand(
            course.Id, lecturer.Id, "Draft Bad Bloom", "Valid question content for bloom check.",
            new string('A', 60), ["Key1"], QuestionDifficulty.Medium, BloomLevel.Apply, QuestionSource.Manual, "exam",
            false, null, new RubricDraftDto("R", null, 10m,
            [
                new RubricCriterionDraftDto("C1", 5m, 0.5m, "NotABloom", "Invalid", 1),
                new RubricCriterionDraftDto("C2", 5m, 0.5m, BloomLevel.Apply, "Valid", 2)
            ])), default);
        invalidDraftRes.IsSuccess.Should().BeFalse();
        invalidDraftRes.Error.Should().StartWith("VALIDATION:");
    }

    [Fact(DisplayName = "M1-CLS-01: AddClassStudent kiểm tra trùng ghi danh trước sĩ số tối đa bên trong lock")]
    public async Task AddClassStudent_ChecksDuplicateEnrollmentBeforeMaxStudents_InsideLock()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.cls@fpt.edu.vn", FullName = "Lec", Role = UserRole.Lecturer, IsActive = true };
        var s1 = new User { Id = Guid.NewGuid(), Email = "s1.cls@fpt.edu.vn", StudentCode = "SE180001", FullName = "S1", Role = UserRole.Student, IsActive = true };
        var s2 = new User { Id = Guid.NewGuid(), Email = "s2.cls@fpt.edu.vn", StudentCode = "SE180002", FullName = "S2", Role = UserRole.Student, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", SemesterId = semester.Id, IsActive = true };
        var cls = new Class
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            Code = "SE1801",
            Name = "SE1801",
            JoinCode = "JOIN01",
            MaxStudents = 1,
            IsActive = true
        };
        db.Users.AddRange(lecturer, s1, s2);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = s1.Id, Status = "enrolled" });
        await db.SaveChangesAsync();

        var handler = new AddClassStudentCommandHandler(db);

        // Class is full (1/1), adding already-enrolled s1 must report duplicate enrollment conflict rather than capacity conflict
        var dupRes = await handler.Handle(new AddClassStudentCommand(cls.Id, lecturer.Id, UserRole.Lecturer, StudentId: s1.Id), default);
        dupRes.IsSuccess.Should().BeFalse();
        dupRes.Error.Should().Contain("CONFLICT:").And.Contain("đã ghi danh vào lớp học này rồi");

        // Adding s2 at full capacity must report capacity conflict
        var capRes = await handler.Handle(new AddClassStudentCommand(cls.Id, lecturer.Id, UserRole.Lecturer, StudentId: s2.Id), default);
        capRes.IsSuccess.Should().BeFalse();
        capRes.Error.Should().Contain("CONFLICT:").And.Contain("đạt sĩ số sinh viên tối đa");
    }

    [Fact(DisplayName = "M1-R2-03: UpdateCourseConfiguration validator & handler chặn MockExamTotalQuestions ngoài [1,100] và MockExamDurationMinutes < 1 (422)")]
    public async Task UpdateCourseConfiguration_EnforcesMockExamTotalQuestionsAndDurationBounds()
    {
        var validator = new UpdateCourseConfigurationCommandValidator();
        var courseId = Guid.NewGuid();

        // Validator boundary checks
        validator.Validate(new UpdateCourseConfigurationCommand(courseId, 60, 2, true)
        {
            MockExamDurationMinutes = 1,
            MockExamTotalQuestions = 100
        }).IsValid.Should().BeTrue();

        var zeroDuration = validator.Validate(new UpdateCourseConfigurationCommand(courseId, 60, 2, true)
        {
            MockExamDurationMinutes = 0
        });
        zeroDuration.IsValid.Should().BeFalse();
        zeroDuration.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(UpdateCourseConfigurationCommand.MockExamDurationMinutes) &&
            e.ErrorCode == ValidationErrorCodes.UnprocessableEntity);

        foreach (var invalidTotal in new[] { 0, -1, 101 })
        {
            var badTotal = validator.Validate(new UpdateCourseConfigurationCommand(courseId, 60, 2, true)
            {
                MockExamTotalQuestions = invalidTotal
            });
            badTotal.IsValid.Should().BeFalse();
            badTotal.Errors.Should().ContainSingle(e =>
                e.PropertyName == nameof(UpdateCourseConfigurationCommand.MockExamTotalQuestions) &&
                e.ErrorCode == ValidationErrorCodes.UnprocessableEntity);
        }

        // Handler defense-in-depth checks (direct MediatR-bypass invocation)
        await using var db = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Email = "head.cfg@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "PRN231",
            SemesterId = semester.Id,
            DepartmentHeadUserId = head.Id,
            MockExamDurationMinutes = 30,
            MockExamTotalQuestions = 5,
            IsActive = true
        };
        db.Users.Add(head);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var handler = new UpdateCourseConfigurationCommandHandler(db);

        var overMaxRes = await handler.Handle(new UpdateCourseConfigurationCommand(course.Id, 60, 2, true)
        {
            ActorRole = $"department_head:{head.Id}",
            MockExamTotalQuestions = 101
        }, default);
        overMaxRes.IsSuccess.Should().BeFalse();
        overMaxRes.Error.Should().StartWith("VALIDATION:");

        var zeroTotalRes = await handler.Handle(new UpdateCourseConfigurationCommand(course.Id, 60, 2, true)
        {
            ActorRole = $"department_head:{head.Id}",
            MockExamTotalQuestions = 0
        }, default);
        zeroTotalRes.IsSuccess.Should().BeFalse();
        zeroTotalRes.Error.Should().StartWith("VALIDATION:");

        var zeroDurationRes = await handler.Handle(new UpdateCourseConfigurationCommand(course.Id, 60, 2, true)
        {
            ActorRole = $"department_head:{head.Id}",
            MockExamDurationMinutes = 0
        }, default);
        zeroDurationRes.IsSuccess.Should().BeFalse();
        zeroDurationRes.Error.Should().StartWith("VALIDATION:");

        var unchanged = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == course.Id);
        unchanged.MockExamTotalQuestions.Should().Be(5);
        unchanged.MockExamDurationMinutes.Should().Be(30);

        var validRes = await handler.Handle(new UpdateCourseConfigurationCommand(course.Id, 60, 2, true)
        {
            ActorRole = $"department_head:{head.Id}",
            MockExamDurationMinutes = 45,
            MockExamTotalQuestions = 20
        }, default);
        validRes.IsSuccess.Should().BeTrue(validRes.Error);
        validRes.Value.MockExamDurationMinutes.Should().Be(45);
        validRes.Value.MockExamTotalQuestions.Should().Be(20);
    }

    [Fact(DisplayName = "M1-R3-03: CreateExamStructure & UpdateExamStructure chặn Name > 200 ký tự (VALIDATION:) và chấp nhận đúng 200 ký tự")]
    public async Task CreateAndUpdateExamStructure_RejectsOver200CharName_WithValidationError()
    {
        await using var db = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Email = "head.struct200@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "SWD392",
            SemesterId = semester.Id,
            DepartmentHeadUserId = head.Id,
            IsActive = true
        };
        db.Users.Add(head);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamStructureCommandHandler(db);
        var updateHandler = new UpdateExamStructureCommandHandler(db);

        // 1. CreateExamStructure rejects 201-char trimmed Name
        var over200Create = await createHandler.Handle(
            new CreateExamStructureCommand(head.Id, course.Id, new string('N', 201), 5, 30, "{}"),
            default);
        over200Create.IsSuccess.Should().BeFalse();
        over200Create.Error.Should().StartWith("VALIDATION:");
        (await db.ExamStructures.CountAsync()).Should().Be(0);

        // 2. CreateExamStructure accepts 200-char Name with surrounding whitespace
        var max200Name = new string('M', 200);
        var validCreate = await createHandler.Handle(
            new CreateExamStructureCommand(head.Id, course.Id, $"  {max200Name}  ", 5, 30, "{}"),
            default);
        validCreate.IsSuccess.Should().BeTrue(validCreate.Error);
        validCreate.Value.Name.Should().Be(max200Name);

        // 3. UpdateExamStructure rejects 201-char trimmed Name and keeps original Name intact
        var over200Update = await updateHandler.Handle(
            new UpdateExamStructureCommand(head.Id, validCreate.Value.Id, new string('U', 201), 5, 30, "{}"),
            default);
        over200Update.IsSuccess.Should().BeFalse();
        over200Update.Error.Should().StartWith("VALIDATION:");

        var reloaded = await db.ExamStructures.AsNoTracking().SingleAsync(s => s.Id == validCreate.Value.Id);
        reloaded.Name.Should().Be(max200Name);

        // 4. UpdateExamStructure accepts 200-char Name with surrounding whitespace
        var updatedMaxName = new string('K', 200);
        var validUpdate = await updateHandler.Handle(
            new UpdateExamStructureCommand(head.Id, validCreate.Value.Id, $" {updatedMaxName} ", 6, 45, "{}"),
            default);
        validUpdate.IsSuccess.Should().BeTrue(validUpdate.Error);
        validUpdate.Value.Name.Should().Be(updatedMaxName);
    }
}
