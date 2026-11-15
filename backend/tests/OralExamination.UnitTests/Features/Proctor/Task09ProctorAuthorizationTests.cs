using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Proctor.Commands.RecordProctorViolation;
using OralExamination.Application.Features.Proctor.Commands.SuspendExamTicket;
using OralExamination.Application.Features.Proctor.Commands.UpdateTicketAttendance;
using OralExamination.Application.Features.Proctor.Queries.GetShiftAttendance;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Proctor;

public class Task09ProctorAuthorizationTests
{
    [Theory]
    [InlineData("proctor", true, true)]
    [InlineData("proctor", false, false)]
    [InlineData("examination_officer", false, true)]
    [InlineData("admin", false, false)]
    [InlineData("department_head", false, false)]
    [InlineData("lecturer", false, false)]
    [InlineData("student", false, false)]
    public async Task AttendanceRead_OnlyAssignedProctorOrGlobalOfficer(string role, bool assigned, bool allowed)
    {
        await using var db = Create(); var actor = Actor(role);
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "QA", RoomLab = "QA", ProctorUserId = assigned ? actor.Id : Guid.NewGuid() };
        db.Users.Add(actor); db.RealExamSessionShifts.Add(shift); await db.SaveChangesAsync();
        var result = await new GetShiftAttendanceQueryHandler(db).Handle(new(shift.Id, actor.Id), default);
        result.IsSuccess.Should().Be(allowed);
    }
    [Fact]
    public void SuspendEndpoint_AllowsOnlyExaminationOfficer()
    {
        var endpoint = typeof(global::API.Controllers.v1.ProctorController).GetMethod("SuspendTicket")!;
        endpoint.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("examination_officer");
    }
    [Theory]
    [InlineData("proctor", true, true)]
    [InlineData("proctor", false, false)]
    [InlineData("examination_officer", false, false)]
    [InlineData("admin", false, false)]
    [InlineData("department_head", false, false)]
    [InlineData("lecturer", false, false)]
    [InlineData("student", false, false)]
    public async Task Attendance_RequiresActiveAssignedProctor(string role, bool assigned, bool allowed)
    {
        await using var db = Create();
        var actor = Actor(role);
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "QA", RoomLab = "QA", ProctorUserId = assigned ? actor.Id : Guid.NewGuid() };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = Guid.NewGuid(), SeatNumber = 1, Status = "scheduled" };
        var student = Actor("student"); student.Id = ticket.StudentId;
        db.Users.AddRange(actor, student); db.RealExamSessionShifts.Add(shift); db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();
        var result = await new UpdateTicketAttendanceCommandHandler(db).Handle(new(actor.Id, ticket.Id), default);
        result.IsSuccess.Should().Be(allowed);
        if (!allowed) { ticket.Status.Should().Be("scheduled"); (await db.AuditLogs.CountAsync()).Should().Be(0); }
    }

    [Fact]
    public async Task Violation_OtherProctorsShift_DeniesWithoutWriting()
    {
        await using var db = Create();
        var actor = Actor("proctor");
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "QA", RoomLab = "QA", ProctorUserId = Guid.NewGuid() };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = Guid.NewGuid(), SeatNumber = 1 };
        db.Users.Add(actor); db.RealExamSessionShifts.Add(shift); db.StudentExamTickets.Add(ticket); await db.SaveChangesAsync();
        var result = await new RecordProctorViolationCommandHandler(db).Handle(new(actor.Id, shift.Id, ticket.Id, null, "TAB_SWITCH", "medium", "QA"), default);
        result.IsSuccess.Should().BeFalse();
        (await db.ExamAntiCheatLogs.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Attendance_RouteShiftAndTicketMismatch_DeniesBeforeMutation()
    {
        await using var db = Create(); var actor = Actor("proctor"); var student = Actor("student");
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "QA", RoomLab = "QA", ProctorUserId = actor.Id };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, SeatNumber = 1, Status = "scheduled" };
        db.Users.AddRange(actor, student); db.RealExamSessionShifts.Add(shift); db.StudentExamTickets.Add(ticket); await db.SaveChangesAsync();
        var result = await new UpdateTicketAttendanceCommandHandler(db).Handle(new(actor.Id, ticket.Id, SeatNumber: 2, ExpectedShiftId: Guid.NewGuid()), default);
        result.IsSuccess.Should().BeFalse(); ticket.SeatNumber.Should().Be(1); ticket.Status.Should().Be("scheduled");
        (await db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task InactiveAssignedProctor_CannotReadOrMutate()
    {
        await using var db = Create(); var actor = Actor("proctor"); actor.IsActive = false; var student = Actor("student");
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "QA", RoomLab = "QA", ProctorUserId = actor.Id };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, SeatNumber = 1, Status = "scheduled" };
        db.Users.AddRange(actor, student); db.RealExamSessionShifts.Add(shift); db.StudentExamTickets.Add(ticket); await db.SaveChangesAsync();
        (await new GetShiftAttendanceQueryHandler(db).Handle(new(shift.Id, actor.Id), default)).IsSuccess.Should().BeFalse();
        (await new UpdateTicketAttendanceCommandHandler(db).Handle(new(actor.Id, ticket.Id), default)).IsSuccess.Should().BeFalse();
        (await new RecordProctorViolationCommandHandler(db).Handle(new(actor.Id, shift.Id, ticket.Id, null, "TAB_SWITCH", "medium", "QA"), default)).IsSuccess.Should().BeFalse();
        ticket.Status.Should().Be("scheduled"); (await db.AuditLogs.CountAsync()).Should().Be(0); (await db.ExamAntiCheatLogs.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("proctor", false)]
    [InlineData("admin", false)]
    [InlineData("lecturer", false)]
    [InlineData("department_head", false)]
    [InlineData("student", false)]
    [InlineData("examination_officer", true)]
    public async Task Suspend_OnlyActiveExaminationOfficer(string role, bool allowed)
    {
        await using var db = Create(); var actor = Actor(role);
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = Guid.NewGuid(), StudentId = Guid.NewGuid(), SeatNumber = 1, Status = "in_progress" };
        var student = Actor("student"); student.Id = ticket.StudentId;
        db.Users.AddRange(actor, student); db.StudentExamTickets.Add(ticket); await db.SaveChangesAsync();
        var result = await new SuspendExamTicketCommandHandler(db).Handle(new(actor.Id, ticket.Id, "A valid QA suspension reason"), default);
        result.IsSuccess.Should().Be(allowed);
        if (!allowed) { ticket.Status.Should().Be("in_progress"); (await db.LecturerAudits.CountAsync()).Should().Be(0); }
    }

    private static OralExamDbContext Create() => new(new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static User Actor(string role) => new() { Id = Guid.NewGuid(), Role = role, Email = $"{Guid.NewGuid()}@qa.invalid", FullName = "QA", IsActive = true };
}
