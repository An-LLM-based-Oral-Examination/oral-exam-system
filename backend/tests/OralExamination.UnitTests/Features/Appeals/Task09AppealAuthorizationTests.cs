using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Appeals.Queries.GetAppeals;
using OralExamination.Application.Features.Appeals.Queries.GetAppealById;
using OralExamination.Application.Features.Appeals.Queries.GetAppealAllocationProposal;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Appeals;

public sealed class Task09AppealAuthorizationTests
{
    [Theory]
    [InlineData(UserRole.Student, false)]
    [InlineData(UserRole.Lecturer, false)]
    [InlineData(UserRole.DepartmentHead, false)]
    [InlineData(UserRole.ExaminationOfficer, true)]
    [InlineData(UserRole.Proctor, false)]
    [InlineData(UserRole.Admin, false)]
    public async Task Assignment_OnlyActiveOfficer_CanMutate(string role, bool allowed)
    {
        using var db = NewDb();
        var (actor, lecturer, _, appeal) = await Seed(db, role);
        var result = await new AssignAppealLecturerCommandHandler(db).Handle(
            new AssignAppealLecturerCommand(appeal.Id, actor.Id, lecturer.Id), default);
        result.IsSuccess.Should().Be(allowed);
        if (!allowed)
        {
            result.Error.Should().StartWith("FORBIDDEN:");
            appeal.Status.Should().Be(AppealStatus.Pending);
            appeal.AssignedTo.Should().Be(lecturer.Id);
        }
    }

    [Theory]
    [InlineData(UserRole.Student, false)]
    [InlineData(UserRole.Lecturer, false)]
    [InlineData(UserRole.DepartmentHead, false)]
    [InlineData(UserRole.ExaminationOfficer, true)]
    [InlineData(UserRole.Proctor, false)]
    [InlineData(UserRole.Admin, false)]
    public async Task Decision_OnlyActiveOfficer_CanMutate(string role, bool allowed)
    {
        using var db = NewDb();
        var (actor, _, _, appeal) = await Seed(db, role);
        appeal.Status = AppealStatus.RegradedByLecturer;
        await db.SaveChangesAsync();
        var result = await new ReviewAppealDecisionCommandHandler(db).Handle(
            new ReviewAppealDecisionCommand(appeal.Id, actor.Id, AppealStatus.Rejected, null, "Task09 permission check"), default);
        result.IsSuccess.Should().Be(allowed);
        if (!allowed)
        {
            result.Error.Should().StartWith("FORBIDDEN:");
            appeal.Status.Should().Be(AppealStatus.RegradedByLecturer);
            appeal.Decision.Should().BeNull();
        }
    }

    [Fact]
    public async Task InactiveOfficer_CannotDecide()
    {
        using var db = NewDb();
        var (actor, _, _, appeal) = await Seed(db, UserRole.ExaminationOfficer);
        appeal.Status = AppealStatus.RegradedByLecturer;
        actor.IsActive = false;
        await db.SaveChangesAsync();
        var result = await new ReviewAppealDecisionCommandHandler(db).Handle(
            new ReviewAppealDecisionCommand(appeal.Id, actor.Id, AppealStatus.Rejected, null, "Task09 permission check"), default);
        result.IsSuccess.Should().BeFalse();
        appeal.Status.Should().Be(AppealStatus.RegradedByLecturer);
    }

    [Fact]
    public async Task ReadsAndProposal_MissingActor_FailClosed()
    {
        using var db = NewDb();
        var (_, _, _, appeal) = await Seed(db, UserRole.ExaminationOfficer);
        (await new GetAppealsQueryHandler(db).Handle(new GetAppealsQuery(), default)).IsSuccess.Should().BeFalse();
        (await new GetAppealByIdQueryHandler(db).Handle(new GetAppealByIdQuery(appeal.Id), default)).IsSuccess.Should().BeFalse();
        (await new GetAppealAllocationProposalQueryHandler(db).Handle(new GetAppealAllocationProposalQuery(), default)).IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public async Task DepartmentHead_ReadsOnlyOwnedCourse_WithProgramOwnerCanonical(bool legacyOwner, bool differentProgramOwner, bool allowed)
    {
        using var db = NewDb();
        var (actor, _, course, appeal) = await Seed(db, UserRole.DepartmentHead);
        course.DepartmentHeadUserId = legacyOwner ? actor.Id : null;
        if (differentProgramOwner)
        {
            course.AcademicProgram = new AcademicProgram { Code = "OTHER", Name = "Other program", DepartmentHeadUserId = Guid.NewGuid() };
            db.AcademicPrograms.Add(course.AcademicProgram);
        }
        await db.SaveChangesAsync();
        var list = await new GetAppealsQueryHandler(db).Handle(new GetAppealsQuery(RequesterId: actor.Id, RequesterRole: actor.Role), default);
        list.IsSuccess.Should().BeTrue();
        list.Value.Should().HaveCount(allowed ? 1 : 0);
        var detail = await new GetAppealByIdQueryHandler(db).Handle(new GetAppealByIdQuery(appeal.Id, actor.Id, actor.Role), default);
        detail.IsSuccess.Should().Be(allowed);
    }

    [Fact]
    public async Task ClaimedOfficer_CannotPromoteStudent()
    {
        using var db = NewDb();
        var (actor, _, _, appeal) = await Seed(db, UserRole.Student);
        (await new GetAppealsQueryHandler(db).Handle(new GetAppealsQuery(RequesterId: actor.Id, RequesterRole: UserRole.ExaminationOfficer), default)).IsSuccess.Should().BeFalse();
        (await new GetAppealByIdQueryHandler(db).Handle(new GetAppealByIdQuery(appeal.Id, actor.Id, UserRole.ExaminationOfficer), default)).IsSuccess.Should().BeFalse();
    }

    private static OralExamDbContext NewDb() => new(new DbContextOptionsBuilder<OralExamDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(User Actor, User Lecturer, Course Course, AppealRequest Appeal)> Seed(OralExamDbContext db, string role)
    {
        var actor = new User { Email = "actor@task09.test", FullName = "Actor", Role = role, IsActive = true };
        var lecturer = new User { Email = "lecturer@task09.test", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Email = "student@task09.test", FullName = "Student", Role = UserRole.Student, IsActive = true };
        var semester = new Semester { Code = "FA26", Name = "Fall 2026" };
        var course = new Course { Code = "CSD201", Name = "Algorithms", Semester = semester };
        var session = new OfficialExamSession { Course = course, Title = "Task09", ExamStructureId = Guid.NewGuid() };
        var shift = new RealExamSessionShift { Session = session, ShiftName = "Shift", RoomLab = "Lab" };
        var ticket = new StudentExamTicket { Shift = shift, Student = student, Status = "PUBLISHED" };
        var appeal = new AppealRequest { Ticket = ticket, TicketId = ticket.Id, Student = student, StudentId = student.Id,
            SessionId = session.Id, Reason = "Review requested", Status = AppealStatus.Pending,
            AssignedTo = lecturer.Id, AssignedToUser = lecturer };
        db.Users.AddRange(actor, lecturer, student);
        db.Courses.Add(course);
        db.AppealRequests.Add(appeal);
        await db.SaveChangesAsync();
        return (actor, lecturer, course, appeal);
    }
}
