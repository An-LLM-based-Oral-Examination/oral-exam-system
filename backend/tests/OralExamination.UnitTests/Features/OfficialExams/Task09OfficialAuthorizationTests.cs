using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.ConfigureOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;
using OralExamination.Application.Features.OfficialExams.Commands.HandOverExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.PublishExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class Task09OfficialAuthorizationTests
{
    public static IEnumerable<object[]> Roles => UserRole.All.Select(role => new object[] { role });

    private static OralExamDbContext Context() => new(new DbContextOptionsBuilder<OralExamDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User Actor(string role, bool active = true) => new()
    {
        Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@task09.test", FullName = "Task09 actor",
        Role = role, IsActive = active
    };

    private static async Task<(Course Course, OfficialExamSession Session, RealExamSessionShift Shift)> Seed(
        OralExamDbContext context, User actor, Guid? programOwner = null, Guid? legacyOwner = null)
    {
        var program = new AcademicProgram { Code = "TASK09", Name = "Task09", DepartmentHeadUserId = programOwner };
        var course = new Course { Code = "TASK09", Name = "Task09", AcademicProgramId = program.Id, DepartmentHeadUserId = legacyOwner };
        var session = new OfficialExamSession
        {
            CourseId = course.Id, SemesterId = course.SemesterId, ExamAttemptType = OfficialExamAttemptType.RetakeExam,
            Title = "Task09 session", CreatedBy = Guid.NewGuid(),
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), HasFollowUp = false, MaxFollowUpQuestions = 2
        };
        var shift = new RealExamSessionShift
        {
            SessionId = session.Id, ShiftName = "Task09", RoomLab = "Lab", Status = "scheduled",
            StartTime = DateTime.UtcNow.AddHours(-2), EndTime = DateTime.UtcNow.AddHours(-1),
            ExaminerUserId = Guid.NewGuid(), ProctorUserId = Guid.NewGuid()
        };
        if (course.SemesterId == Guid.Empty)
        {
            course.SemesterId = Guid.NewGuid();
            session.SemesterId = course.SemesterId;
        }
        context.Users.Add(actor);
        context.AcademicPrograms.Add(program);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        await context.SaveChangesAsync();
        return (course, session, shift);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task CreateSession_OnlyActiveExaminationOfficer_WithSafeAcademicDefaults(string role)
    {
        await using var context = Context();
        var actor = Actor(role);
        var (course, _, _) = await Seed(context, actor);
        var result = await new CreateOfficialExamSessionCommandHandler(context, new CreateOfficialExamSessionCommandValidator())
            .Handle(new(actor.Id, course.Id, null, "New session", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                false, 2, ExamInputMode.VoiceOnly, 60), default);
        result.IsSuccess.Should().Be(role == UserRole.ExaminationOfficer);
        (await context.OfficialExamSessions.CountAsync()).Should().Be(role == UserRole.ExaminationOfficer ? 2 : 1);
        if (!result.IsSuccess) result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    [InlineData(false, 5)]
    public async Task Officer_CannotBypassAcademicFollowUpPolicy_ThroughCreateSession(bool hasFollowUp, int maxFollowUp)
    {
        await using var context = Context();
        var actor = Actor(UserRole.ExaminationOfficer);
        var (course, _, _) = await Seed(context, actor);
        var result = await new CreateOfficialExamSessionCommandHandler(context, new CreateOfficialExamSessionCommandValidator())
            .Handle(new(actor.Id, course.Id, null, "New session", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                hasFollowUp, maxFollowUp, ExamInputMode.VoiceOnly, 60), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
        (await context.OfficialExamSessions.CountAsync()).Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task OpenShift_OnlyOfficer_GlobalScopeDoesNotDependOnSessionCreator(string role)
    {
        await using var context = Context();
        var actor = Actor(role);
        var (_, session, _) = await Seed(context, actor);
        var examiner = Actor(UserRole.Lecturer);
        var proctor = Actor(UserRole.Proctor);
        context.Users.AddRange(examiner, proctor);
        await context.SaveChangesAsync();
        var result = await new OpenExamShiftCommandHandler(context, new OpenExamShiftCommandValidator())
            .Handle(new(actor.Id, session.Id, "New shift", "Lab", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2),
                proctor.Id, examiner.Id), default);
        result.IsSuccess.Should().Be(role == UserRole.ExaminationOfficer);
        (await context.RealExamSessionShifts.CountAsync()).Should().Be(role == UserRole.ExaminationOfficer ? 2 : 1);
        if (!result.IsSuccess) result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task PublishShift_OnlyOfficer_PreservesAssignedExaminer(string role)
    {
        await using var context = Context();
        var actor = Actor(role);
        var (_, _, shift) = await Seed(context, actor);
        var examinerId = shift.ExaminerUserId;
        var result = await new PublishExamShiftCommandHandler(context).Handle(new(shift.Id, actor.Id, role), default);
        result.IsSuccess.Should().Be(role == UserRole.ExaminationOfficer);
        shift.IsExamPublished.Should().Be(role == UserRole.ExaminationOfficer);
        shift.ExaminerUserId.Should().Be(examinerId);
        if (!result.IsSuccess) result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task PublishGrades_OnlyOfficer_WithoutSessionCreatorRestriction(string role)
    {
        await using var context = Context();
        var actor = Actor(role);
        var (_, _, shift) = await Seed(context, actor);
        shift.Status = "in_progress";
        var student = Actor(UserRole.Student);
        var ticket = new StudentExamTicket
        {
            ShiftId = shift.Id, StudentId = student.Id, SeatNumber = 1, Status = ExamTicketStatus.AiGraded,
            Submissions = { new ExamQuestionSubmission { ExamQuestionId = Guid.NewGuid(), AiScore = 8m, GradingStatus = "ai_graded" } }
        };
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();
        var result = await new PublishGradesCommandHandler(context).Handle(new(shift.Id, actor.Id), default);
        result.IsSuccess.Should().Be(role == UserRole.ExaminationOfficer);
        ticket.IsLocked.Should().Be(role == UserRole.ExaminationOfficer);
        if (role == UserRole.ExaminationOfficer) ticket.GradesPublishedAt.Should().NotBeNull();
        else result.Error.Should().Be(PublishGradesErrors.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task AcademicActions_OnlyDepartmentHeadOwner(string role)
    {
        await using var context = Context();
        var actor = Actor(role);
        var (course, session, _) = await Seed(context, actor, actor.Id);
        var approveSet = new ExamSet { CourseId = course.Id, StructureId = Guid.NewGuid(), SetCode = "APPROVE", Status = ExamSetStatus.SubmittedForReview };
        var handedSet = new ExamSet { CourseId = course.Id, StructureId = Guid.NewGuid(), SetCode = "HANDOVER", Status = ExamSetStatus.Official };
        context.ExamSets.AddRange(approveSet, handedSet);
        await context.SaveChangesAsync();
        await ExamPreparationTestData.AttachAsync(context, approveSet);
        await ExamPreparationTestData.AttachAsync(context, handedSet);
        var configure = await new ConfigureOfficialExamFollowUpCommandHandler(context, new ConfigureOfficialExamFollowUpCommandValidator())
            .Handle(new(actor.Id, session.Id, true, 3), default);
        var approve = await new ApproveExamSetCommandHandler(context).Handle(new(approveSet.Id, null, actor.Id, role), default);
        var handover = await new HandOverExamSetCommandHandler(context).Handle(new(handedSet.Id, actor.Id, role), default);
        configure.IsSuccess.Should().Be(role == UserRole.DepartmentHead);
        approve.IsSuccess.Should().Be(role == UserRole.DepartmentHead);
        handover.IsSuccess.Should().Be(role == UserRole.DepartmentHead);
        session.HasFollowUp.Should().Be(role == UserRole.DepartmentHead);
        approveSet.Status.Should().Be(role == UserRole.DepartmentHead ? ExamSetStatus.Official : ExamSetStatus.SubmittedForReview);
        handedSet.Status.Should().Be(role == UserRole.DepartmentHead ? ExamSetStatus.HandedOver : ExamSetStatus.Official);
    }

    [Theory]
    [InlineData("program-owner", true)]
    [InlineData("legacy-owner", true)]
    [InlineData("program-overrides-legacy", false)]
    [InlineData("other-owner", false)]
    [InlineData("no-owner", false)]
    public async Task AcademicActions_UseCanonicalOwner_AndFailClosed(string ownerCase, bool allowed)
    {
        await using var context = Context();
        var actor = Actor(UserRole.DepartmentHead);
        Guid? programOwner = ownerCase == "program-owner" ? actor.Id :
            ownerCase is "program-overrides-legacy" or "other-owner" ? Guid.NewGuid() : null;
        Guid? legacyOwner = ownerCase is "legacy-owner" or "program-overrides-legacy" ? actor.Id : null;
        var (course, session, _) = await Seed(context, actor, programOwner, legacyOwner);
        var approveSet = new ExamSet { CourseId = course.Id, StructureId = Guid.NewGuid(), SetCode = "APPROVE", Status = ExamSetStatus.SubmittedForReview };
        var handedSet = new ExamSet { CourseId = course.Id, StructureId = Guid.NewGuid(), SetCode = "HANDOVER", Status = ExamSetStatus.Official };
        context.ExamSets.AddRange(approveSet, handedSet);
        await context.SaveChangesAsync();
        await ExamPreparationTestData.AttachAsync(context, approveSet);
        await ExamPreparationTestData.AttachAsync(context, handedSet);
        var configure = await new ConfigureOfficialExamFollowUpCommandHandler(context, new ConfigureOfficialExamFollowUpCommandValidator())
            .Handle(new(actor.Id, session.Id, true, 3), default);
        var approve = await new ApproveExamSetCommandHandler(context).Handle(new(approveSet.Id, null, actor.Id, actor.Role), default);
        var handover = await new HandOverExamSetCommandHandler(context).Handle(new(handedSet.Id, actor.Id, actor.Role), default);
        configure.IsSuccess.Should().Be(allowed);
        approve.IsSuccess.Should().Be(allowed);
        handover.IsSuccess.Should().Be(allowed);
        if (!allowed)
        {
            configure.Error.Should().Be(OfficialExamErrors.Forbidden);
            approve.Error.Should().Be(OfficialExamErrors.Forbidden);
            handover.Error.Should().Be(OfficialExamErrors.Forbidden);
            session.HasFollowUp.Should().BeFalse();
            approveSet.Status.Should().Be(ExamSetStatus.SubmittedForReview);
            handedSet.Status.Should().Be(ExamSetStatus.Official);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("examination_officer")]
    public async Task AcademicCommands_RejectMissingOrMismatchedRoleClaims(string? claimedRole)
    {
        await using var context = Context();
        var actor = Actor(UserRole.DepartmentHead);
        var (course, _, _) = await Seed(context, actor, actor.Id);
        var examSet = new ExamSet { CourseId = course.Id, StructureId = Guid.NewGuid(), SetCode = "SET", Status = ExamSetStatus.Official };
        context.ExamSets.Add(examSet);
        await context.SaveChangesAsync();
        var approve = await new ApproveExamSetCommandHandler(context).Handle(new(examSet.Id, null, actor.Id, claimedRole), default);
        var handover = await new HandOverExamSetCommandHandler(context).Handle(new(examSet.Id, actor.Id, claimedRole), default);
        approve.Error.Should().Be(OfficialExamErrors.Forbidden);
        handover.Error.Should().Be(OfficialExamErrors.Forbidden);
        examSet.Status.Should().Be(ExamSetStatus.Official);
    }

    [Fact]
    public async Task InactiveActors_CannotWriteOperationalOrAcademicResources()
    {
        await using var context = Context();
        var officer = Actor(UserRole.ExaminationOfficer, active: false);
        var (course, session, shift) = await Seed(context, officer);
        var create = await new CreateOfficialExamSessionCommandHandler(context, new CreateOfficialExamSessionCommandValidator())
            .Handle(new(officer.Id, course.Id, null, "New", DateOnly.FromDateTime(DateTime.UtcNow), false, 2, ExamInputMode.VoiceOnly, 60), default);
        var open = await new OpenExamShiftCommandHandler(context, new OpenExamShiftCommandValidator())
            .Handle(new(officer.Id, session.Id, "New", "Lab", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), Guid.NewGuid(), Guid.NewGuid()), default);
        var publish = await new PublishExamShiftCommandHandler(context).Handle(new(shift.Id, officer.Id, officer.Role), default);
        var grades = await new PublishGradesCommandHandler(context).Handle(new(shift.Id, officer.Id), default);
        create.Error.Should().Be(OfficialExamErrors.Forbidden);
        open.Error.Should().Be(OfficialExamErrors.Forbidden);
        publish.Error.Should().Be(OfficialExamErrors.Forbidden);
        grades.Error.Should().Be(PublishGradesErrors.Forbidden);
        shift.IsExamPublished.Should().BeFalse();
        (await context.RealExamSessionShifts.CountAsync()).Should().Be(1);
        (await context.OfficialExamSessions.CountAsync()).Should().Be(1);

        var head = Actor(UserRole.DepartmentHead, active: false);
        context.Users.Add(head);
        course.DepartmentHeadUserId = head.Id;
        var examSet = new ExamSet { CourseId = course.Id, StructureId = Guid.NewGuid(), SetCode = "SET", Status = ExamSetStatus.Official };
        context.ExamSets.Add(examSet);
        await context.SaveChangesAsync();
        var config = await new ConfigureOfficialExamFollowUpCommandHandler(context, new ConfigureOfficialExamFollowUpCommandValidator())
            .Handle(new(head.Id, session.Id, true, 3), default);
        var approve = await new ApproveExamSetCommandHandler(context).Handle(new(examSet.Id, null, head.Id, head.Role), default);
        var handover = await new HandOverExamSetCommandHandler(context).Handle(new(examSet.Id, head.Id, head.Role), default);
        config.Error.Should().Be(OfficialExamErrors.Forbidden);
        approve.Error.Should().Be(OfficialExamErrors.Forbidden);
        handover.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("department_head")]
    [InlineData("admin")]
    public async Task PublishShift_RejectsMissingOrMismatchedRoleClaims(string? claimedRole)
    {
        await using var context = Context();
        var actor = Actor(UserRole.ExaminationOfficer);
        var (_, _, shift) = await Seed(context, actor);
        var result = await new PublishExamShiftCommandHandler(context).Handle(new(shift.Id, actor.Id, claimedRole), default);
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
        shift.IsExamPublished.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClaimedRole_CannotPromoteMissingOrLowerPrivilegeDatabaseActor(bool missingActor)
    {
        await using var context = Context();
        var actor = Actor(UserRole.Student);
        var (course, _, shift) = await Seed(context, actor, actor.Id);
        var examSet = new ExamSet { CourseId = course.Id, StructureId = Guid.NewGuid(), SetCode = "SET", Status = ExamSetStatus.Official };
        context.ExamSets.Add(examSet);
        await context.SaveChangesAsync();
        var actorId = missingActor ? Guid.NewGuid() : actor.Id;
        var publish = await new PublishExamShiftCommandHandler(context).Handle(new(shift.Id, actorId, UserRole.ExaminationOfficer), default);
        var approve = await new ApproveExamSetCommandHandler(context).Handle(new(examSet.Id, null, actorId, UserRole.DepartmentHead), default);
        var handover = await new HandOverExamSetCommandHandler(context).Handle(new(examSet.Id, actorId, UserRole.DepartmentHead), default);
        publish.Error.Should().Be(OfficialExamErrors.Forbidden);
        approve.Error.Should().Be(OfficialExamErrors.Forbidden);
        handover.Error.Should().Be(OfficialExamErrors.Forbidden);
        shift.IsExamPublished.Should().BeFalse();
        examSet.Status.Should().Be(ExamSetStatus.Official);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task AssignTicket_OnlyExaminationOfficer(string role)
    {
        await using var context = Context();
        var actor = Actor(role);
        var (course, _, shift) = await Seed(context, actor);
        var student = Actor(UserRole.Student);
        context.Users.Add(student);
        context.ExamSets.Add(new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = Guid.NewGuid(),
            SetCode = "TASK09-HANDED-01",
            Status = ExamSetStatus.HandedOver,
            SessionId = shift.SessionId,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var result = await new AssignExamTicketCommandHandler(context, new AssignExamTicketCommandValidator())
            .Handle(new(actor.Id, shift.Id, student.Id, 1, "192.168.1.101"), default);
        result.IsSuccess.Should().Be(role == UserRole.ExaminationOfficer);
        (await context.StudentExamTickets.CountAsync()).Should().Be(role == UserRole.ExaminationOfficer ? 1 : 0);
        if (!result.IsSuccess) result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task ExamStructureWrites_OnlyActiveDepartmentHeadOwner(string role)
    {
        await using var context = Context();
        var actor = Actor(role);
        var (course, _, _) = await Seed(context, actor, actor.Id);
        var structure = new ExamStructure { CourseId = course.Id, Name = "Original", TotalQuestions = 2, DurationMinutes = 30 };
        context.ExamStructures.Add(structure);
        await context.SaveChangesAsync();
        var create = await new CreateExamStructureCommandHandler(context).Handle(new(actor.Id, course.Id, "New", 2, 30, "{}"), default);
        var update = await new UpdateExamStructureCommandHandler(context).Handle(new(actor.Id, structure.Id, "Changed", 2, 30, "{}"), default);
        var allowed = role == UserRole.DepartmentHead;
        create.IsSuccess.Should().Be(allowed);
        update.IsSuccess.Should().Be(allowed);
        structure.Name.Should().Be(allowed ? "Changed" : "Original");
        (await context.ExamStructures.CountAsync()).Should().Be(allowed ? 2 : 1);
    }

    [Theory]
    [InlineData("program-owner", true)]
    [InlineData("legacy-owner", true)]
    [InlineData("program-overrides-legacy", false)]
    [InlineData("no-owner", false)]
    [InlineData("inactive", false)]
    public async Task ExamStructureWrites_EnforceCanonicalOwnershipWithoutAdminOverride(string ownerCase, bool allowed)
    {
        await using var context = Context();
        var actor = Actor(UserRole.DepartmentHead, active: ownerCase != "inactive");
        Guid? programOwner = ownerCase == "program-owner" ? actor.Id : ownerCase == "program-overrides-legacy" ? Guid.NewGuid() : null;
        Guid? legacyOwner = ownerCase is "legacy-owner" or "program-overrides-legacy" or "inactive" ? actor.Id : null;
        var (course, _, _) = await Seed(context, actor, programOwner, legacyOwner);
        var structure = new ExamStructure { CourseId = course.Id, Name = "Original", TotalQuestions = 2, DurationMinutes = 30 };
        context.ExamStructures.Add(structure);
        await context.SaveChangesAsync();
        var create = await new CreateExamStructureCommandHandler(context).Handle(new(actor.Id, course.Id, "New", 2, 30, "{}"), default);
        var update = await new UpdateExamStructureCommandHandler(context).Handle(new(actor.Id, structure.Id, "Changed", 2, 30, "{}"), default);
        create.IsSuccess.Should().Be(allowed);
        update.IsSuccess.Should().Be(allowed);
        structure.Name.Should().Be(allowed ? "Changed" : "Original");
        if (!allowed)
        {
            create.Error.Should().StartWith("FORBIDDEN:");
            update.Error.Should().StartWith("FORBIDDEN:");
            (await context.AuditLogs.CountAsync()).Should().Be(0);
        }
    }
}
