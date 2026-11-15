using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class ShiftMutationTests
{
    private static OralExamDbContext Context() => new(new DbContextOptionsBuilder<OralExamDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(User Actor, RealExamSessionShift Shift)> Seed(OralExamDbContext context)
    {
        var actor = new User { Email = "officer@test.local", FullName = "Officer", Role = UserRole.ExaminationOfficer, IsActive = true };
        var proctor = new User { Email = "proctor@test.local", FullName = "Proctor", Role = UserRole.Proctor, IsActive = true };
        var examiner = new User { Email = "lecturer@test.local", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var shift = new RealExamSessionShift { ShiftName = "Morning", RoomLab = "Lab", SessionId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddDays(2), EndTime = DateTime.UtcNow.AddDays(2).AddHours(1),
            ProctorUserId = proctor.Id, ExaminerUserId = examiner.Id };
        context.Users.AddRange(actor, proctor, examiner);
        context.RealExamSessionShifts.Add(shift);
        await context.SaveChangesAsync();
        return (actor, shift);
    }

    private static UpdateExamShiftCommand Update(User actor, RealExamSessionShift shift) => new(actor.Id,
        UserRole.ExaminationOfficer, shift.Id, " Updated ", " Room 2 ", DateTime.UtcNow.AddDays(3),
        DateTime.UtcNow.AddDays(3).AddHours(1), shift.ProctorUserId!.Value, shift.ExaminerUserId!.Value);

    [Fact]
    public async Task Delete_EmptyFutureScheduledShift_RemovesOnlyThatShift()
    {
        await using var context = Context();
        var (actor, shift) = await Seed(context);
        var other = new RealExamSessionShift { ShiftName = "Other", RoomLab = "Other", SessionId = shift.SessionId };
        context.RealExamSessionShifts.Add(other);
        await context.SaveChangesAsync();
        var result = await new DeleteExamShiftCommandHandler(context).Handle(new(actor.Id, actor.Role, shift.Id), default);
        result.IsSuccess.Should().BeTrue();
        (await context.RealExamSessionShifts.Select(s => s.Id).ToListAsync()).Should().Equal(other.Id);
    }

    [Theory]
    [InlineData("published")]
    [InlineData("active")]
    [InlineData("started")]
    [InlineData("ticket")]
    [InlineData("submission")]
    [InlineData("log")]
    public async Task Delete_Conflict_PreservesShiftAndAllEvidence(string scenario)
    {
        await using var context = Context();
        var (actor, shift) = await Seed(context);
        var expected = scenario switch {
            "published" => ShiftMutationErrors.Published, "active" => ShiftMutationErrors.NotScheduled,
            "started" => ShiftMutationErrors.AlreadyStarted, "submission" => ShiftMutationErrors.HasSubmissions,
            "log" => ShiftMutationErrors.HasAntiCheatLogs, _ => ShiftMutationErrors.HasTickets };
        if (scenario == "published") shift.IsExamPublished = true;
        if (scenario == "active") shift.Status = "active";
        if (scenario == "started") shift.StartTime = DateTime.UtcNow;
        if (scenario is "ticket" or "submission" or "log")
        {
            var ticket = new StudentExamTicket { ShiftId = shift.Id, StudentId = Guid.NewGuid(), SeatNumber = 1 };
            context.StudentExamTickets.Add(ticket);
            if (scenario == "submission") context.ExamQuestionSubmissions.Add(new() { TicketId = ticket.Id, ExamQuestionId = Guid.NewGuid() });
            if (scenario == "log") context.ExamAntiCheatLogs.Add(new() { TicketId = ticket.Id, ShiftId = shift.Id,
                StudentId = ticket.StudentId, EventType = "violation" });
        }
        await context.SaveChangesAsync();
        var ticketCount = await context.StudentExamTickets.CountAsync();
        var submissionCount = await context.ExamQuestionSubmissions.CountAsync();
        var logCount = await context.ExamAntiCheatLogs.CountAsync();
        var result = await new DeleteExamShiftCommandHandler(context).Handle(new(actor.Id, actor.Role, shift.Id), default);
        result.Error.Should().Be(expected);
        (await context.RealExamSessionShifts.CountAsync()).Should().Be(1);
        (await context.StudentExamTickets.CountAsync()).Should().Be(ticketCount);
        (await context.ExamQuestionSubmissions.CountAsync()).Should().Be(submissionCount);
        (await context.ExamAntiCheatLogs.CountAsync()).Should().Be(logCount);
    }

    [Fact]
    public async Task Update_UnusedTicketAllowed_PreservesSessionAndAllocation()
    {
        await using var context = Context();
        var (actor, shift) = await Seed(context);
        var sessionId = shift.SessionId;
        var ticket = new StudentExamTicket { ShiftId = shift.Id, StudentId = Guid.NewGuid(), SeatNumber = 7, ExamSetId = Guid.NewGuid() };
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();
        var command = Update(actor, shift);
        var result = await new UpdateExamShiftCommandHandler(context).Handle(command, default);
        result.IsSuccess.Should().BeTrue();
        shift.ShiftName.Should().Be("Updated");
        shift.RoomLab.Should().Be("Room 2");
        shift.StartTime.Should().Be(command.StartTime);
        shift.SessionId.Should().Be(sessionId);
        ticket.SeatNumber.Should().Be(7);
        (await context.StudentExamTickets.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("status=IN_PROGRESS", "status=SCHEDULED;seat_number=1", false)]
    [InlineData("status=SCHEDULED", "status=IN_PROGRESS;seat_number=1", false)]
    [InlineData("status=SCHEDULED", "status=SCHEDULED;seat_number=2", true)]
    public async Task Update_AttendanceHistory_PreventsResetBypassButAllowsUnusedSeatChanges(string oldValues, string newValues, bool allowed)
    {
        await using var context = Context();
        var (actor, shift) = await Seed(context);
        var ticket = new StudentExamTicket { ShiftId = shift.Id, StudentId = Guid.NewGuid(), SeatNumber = 1 };
        context.StudentExamTickets.Add(ticket);
        context.AuditLogs.Add(new AuditLog { Action = "PROCTOR_ATTENDANCE_UPDATED", EntityName = "student_exam_tickets",
            EntityId = ticket.Id, OldValues = oldValues, NewValues = newValues });
        await context.SaveChangesAsync();
        var result = await new UpdateExamShiftCommandHandler(context).Handle(Update(actor, shift), default);
        result.IsSuccess.Should().Be(allowed);
        if (!allowed)
        {
            result.Error.Should().Be(ShiftMutationErrors.HasUsedTickets);
            shift.ShiftName.Should().Be("Morning");
        }
        (await context.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Update_NormalizesTimesToUtc(DateTimeKind kind)
    {
        await using var context = Context();
        var (actor, shift) = await Seed(context);
        var command = Update(actor, shift);
        var start = DateTime.SpecifyKind(command.StartTime, kind);
        var end = DateTime.SpecifyKind(command.EndTime, kind);
        var result = await new UpdateExamShiftCommandHandler(context).Handle(command with { StartTime = start, EndTime = end }, default);
        result.IsSuccess.Should().BeTrue();
        shift.StartTime.Kind.Should().Be(DateTimeKind.Utc);
        shift.EndTime.Kind.Should().Be(DateTimeKind.Utc);
        shift.StartTime.Should().Be(kind == DateTimeKind.Local ? start.ToUniversalTime() : DateTime.SpecifyKind(start, DateTimeKind.Utc));
        shift.EndTime.Should().Be(kind == DateTimeKind.Local ? end.ToUniversalTime() : DateTime.SpecifyKind(end, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("checked_in")]
    [InlineData("locked")]
    [InlineData("published")]
    [InlineData("audio")]
    public async Task Update_UsedTicket_RefusesMutation(string scenario)
    {
        await using var context = Context();
        var (actor, shift) = await Seed(context);
        var ticket = new StudentExamTicket { ShiftId = shift.Id, StudentId = Guid.NewGuid(), SeatNumber = 1 };
        if (scenario == "checked_in") ticket.Status = "checked_in";
        if (scenario == "locked") ticket.IsLocked = true;
        if (scenario == "published") ticket.GradesPublishedAt = DateTime.UtcNow;
        if (scenario == "audio") ticket.AudioObjectKey = "evidence";
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();
        var result = await new UpdateExamShiftCommandHandler(context).Handle(Update(actor, shift), default);
        result.Error.Should().Be(ShiftMutationErrors.HasUsedTickets);
        shift.ShiftName.Should().Be("Morning");
    }

    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.ExaminationOfficer, false)]
    public async Task Mutations_RejectUnauthorizedPersistedActor(string role, bool active)
    {
        await using var context = Context();
        var (actor, shift) = await Seed(context);
        actor.Role = role;
        actor.IsActive = active;
        await context.SaveChangesAsync();
        (await new DeleteExamShiftCommandHandler(context).Handle(new(actor.Id, UserRole.ExaminationOfficer, shift.Id), default))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await new UpdateExamShiftCommandHandler(context).Handle(Update(actor, shift), default))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await context.RealExamSessionShifts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Update_InvalidNewTimeAndAssignment_LeavesShiftUnchanged()
    {
        await using var context = Context();
        var (actor, shift) = await Seed(context);
        var handler = new UpdateExamShiftCommandHandler(context);
        (await handler.Handle(Update(actor, shift) with { StartTime = DateTime.UtcNow.AddHours(-1) }, default)).IsSuccess.Should().BeFalse();
        (await handler.Handle(Update(actor, shift) with { ExaminerUserId = shift.ProctorUserId!.Value }, default)).IsSuccess.Should().BeFalse();
        (await handler.Handle(Update(actor, shift) with { ProctorUserId = Guid.NewGuid() }, default)).IsSuccess.Should().BeFalse();
        shift.ShiftName.Should().Be("Morning");
    }
}
