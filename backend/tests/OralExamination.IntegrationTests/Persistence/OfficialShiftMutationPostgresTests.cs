using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.PublishExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public class OfficialShiftMutationPostgresTests(PostgresFixture fixture)
{
    private static async Task<(OfficialData Data, User Officer)> Seed(TestDatabase database, bool empty = false)
    {
        await using var context = database.CreateContext();
        var data = await TestData.OfficialAsync(context);
        var officer = new User { Email = $"{Guid.NewGuid():N}@officer.test", FullName = "Officer",
            Role = UserRole.ExaminationOfficer, IsActive = true };
        context.Users.Add(officer);
        data.Ticket.Shift.StartTime = DateTime.UtcNow.AddDays(2);
        data.Ticket.Shift.EndTime = DateTime.UtcNow.AddDays(2).AddHours(1);
        data.Ticket.Shift.ProctorUserId = officer.Id;
        data.Ticket.Shift.ExaminerUserId = data.Academic.Lecturer.Id;
        data.Ticket.Status = ExamTicketStatus.Scheduled;
        data.Ticket.AudioObjectKey = null;
        data.Ticket.AudioHashSha256 = null;
        data.Ticket.AudioVerifiedAt = null;
        if (empty) context.StudentExamTickets.Remove(data.Ticket);
        await context.SaveChangesAsync();
        return (data, officer);
    }

    [Fact]
    public async Task Delete_EmptyShift_RetainsSessionAndQuestionBank()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (data, officer) = await Seed(database, true);
        await using var context = database.CreateContext();
        var result = await new DeleteExamShiftCommandHandler(context).Handle(new(officer.Id, officer.Role, data.Ticket.ShiftId), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        (await context.RealExamSessionShifts.CountAsync(s => s.Id == data.Ticket.ShiftId)).Should().Be(0);
        (await context.OfficialExamSessions.CountAsync(s => s.Id == data.Ticket.Shift.SessionId)).Should().Be(1);
        (await context.ExamQuestions.CountAsync(q => q.Id == data.Question.Id)).Should().Be(1);
        (await context.ExamSets.CountAsync(s => s.Id == data.Ticket.ExamSetId)).Should().Be(1);
    }

    [Fact]
    public async Task Delete_WithUnusedTicket_ReturnsSpecificConflictAndPreservesRecords()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (data, officer) = await Seed(database);
        await using var context = database.CreateContext();
        var result = await new DeleteExamShiftCommandHandler(context).Handle(new(officer.Id, officer.Role, data.Ticket.ShiftId), default);
        result.Error.Should().Be(ShiftMutationErrors.HasTickets);
        (await context.RealExamSessionShifts.CountAsync(s => s.Id == data.Ticket.ShiftId)).Should().Be(1);
        (await context.StudentExamTickets.CountAsync(t => t.Id == data.Ticket.Id)).Should().Be(1);
        (await context.ExamSets.CountAsync(s => s.Id == data.Ticket.ExamSetId)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentTicketInsert_DeleteWaitsForForeignKeyLockThenReturnsTicketConflict()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (data, officer) = await Seed(database, true);
        await using var inserting = database.CreateContext();
        await using var transaction = await inserting.Database.BeginTransactionAsync();
        inserting.StudentExamTickets.Add(new StudentExamTicket { ShiftId = data.Ticket.ShiftId,
            StudentId = data.Academic.Student.Id, SeatNumber = 2 });
        await inserting.SaveChangesAsync(); // FK key-share stays held until commit.
        await using var deleting = database.CreateContext();
        await deleting.Database.OpenConnectionAsync();
        var pid = ((NpgsqlConnection)deleting.Database.GetDbConnection()).ProcessID;
        var delete = new DeleteExamShiftCommandHandler(deleting).Handle(new(officer.Id, officer.Role, data.Ticket.ShiftId), default);
        try { await AssertBlocked(database, pid); }
        finally { await transaction.CommitAsync(); }
        (await delete.WaitAsync(TimeSpan.FromSeconds(20))).Error.Should().Be(ShiftMutationErrors.HasTickets);
        await using var verify = database.CreateContext();
        (await verify.RealExamSessionShifts.CountAsync(s => s.Id == data.Ticket.ShiftId)).Should().Be(1);
        (await verify.StudentExamTickets.CountAsync(t => t.ShiftId == data.Ticket.ShiftId)).Should().Be(1);
    }

    [Fact]
    public async Task PublishWins_DeleteWaitsThenReturnsPublishedConflict()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (data, officer) = await Seed(database, true);
        var barrier = new SavingBarrier();
        await using var publishing = database.CreateContext(barrier);
        var publish = new PublishExamShiftCommandHandler(publishing).Handle(new(data.Ticket.ShiftId, officer.Id, officer.Role), default);
        await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await using var deleting = database.CreateContext();
        await deleting.Database.OpenConnectionAsync();
        var pid = ((NpgsqlConnection)deleting.Database.GetDbConnection()).ProcessID;
        var delete = new DeleteExamShiftCommandHandler(deleting).Handle(new(officer.Id, officer.Role, data.Ticket.ShiftId), default);
        try { await AssertBlocked(database, pid); }
        finally { barrier.Release.TrySetResult(); }
        (await publish.WaitAsync(TimeSpan.FromSeconds(20))).IsSuccess.Should().BeTrue();
        (await delete.WaitAsync(TimeSpan.FromSeconds(20))).Error.Should().Be(ShiftMutationErrors.Published);
        await using var verify = database.CreateContext();
        (await verify.RealExamSessionShifts.SingleAsync(s => s.Id == data.Ticket.ShiftId)).IsExamPublished.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentTicketUse_UpdateWaitsOnTicketThenRejectsUsedTicket()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (data, officer) = await Seed(database);
        // Use a valid proctor independently of the officer who administers this shift.
        await using (var seed = database.CreateContext())
        {
            var proctor = new User { Email = "proctor@official.test", FullName = "Proctor", Role = UserRole.Proctor, IsActive = true };
            seed.Users.Add(proctor);
            var shift = await seed.RealExamSessionShifts.SingleAsync(s => s.Id == data.Ticket.ShiftId);
            shift.ProctorUserId = proctor.Id;
            data.Ticket.Shift.ProctorUserId = proctor.Id;
            await seed.SaveChangesAsync();
        }
        await using var usingTicket = database.CreateContext();
        await using var transaction = await usingTicket.Database.BeginTransactionAsync();
        var ticket = await usingTicket.StudentExamTickets.SingleAsync(t => t.Id == data.Ticket.Id);
        ticket.Status = ExamTicketStatus.InProgress;
        await usingTicket.SaveChangesAsync(); // Hold ticket row lock before management checks.
        await using var updating = database.CreateContext();
        await updating.Database.OpenConnectionAsync();
        var pid = ((NpgsqlConnection)updating.Database.GetDbConnection()).ProcessID;
        var shiftData = data.Ticket.Shift;
        var update = new UpdateExamShiftCommandHandler(updating).Handle(new(officer.Id, officer.Role, shiftData.Id,
            "Changed", "Changed", DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(3).AddHours(1),
            shiftData.ProctorUserId!.Value, data.Academic.Lecturer.Id), default);
        try { await AssertBlocked(database, pid); }
        finally { await transaction.CommitAsync(); }
        (await update.WaitAsync(TimeSpan.FromSeconds(20))).Error.Should().Be(ShiftMutationErrors.HasUsedTickets);
        await using var verify = database.CreateContext();
        (await verify.RealExamSessionShifts.SingleAsync(s => s.Id == data.Ticket.ShiftId)).ShiftName.Should().Be("Integration shift");
        (await verify.StudentExamTickets.SingleAsync(t => t.Id == data.Ticket.Id)).Status.Should().Be(ExamTicketStatus.InProgress);
    }

    [Theory]
    [InlineData("status=IN_PROGRESS", false)]
    [InlineData("status=SCHEDULED", true)]
    public async Task Update_PersistedJsonbAttendanceHistory_DistinguishesUsedAndUnusedTickets(string oldValues, bool allowed)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (data, officer) = await Seed(database);
        Guid proctorId;
        await using (var seed = database.CreateContext())
        {
            var proctor = new User { Email = "audit-proctor@official.test", FullName = "Proctor", Role = UserRole.Proctor, IsActive = true };
            proctorId = proctor.Id;
            seed.Users.Add(proctor);
            var shift = await seed.RealExamSessionShifts.SingleAsync(s => s.Id == data.Ticket.ShiftId);
            shift.ProctorUserId = proctorId;
            seed.AuditLogs.Add(new AuditLog { UserId = proctorId, Action = "PROCTOR_ATTENDANCE_UPDATED",
                EntityName = "student_exam_tickets", EntityId = data.Ticket.Id,
                OldValues = oldValues, NewValues = "status=SCHEDULED;seat_number=1" });
            await seed.SaveChangesAsync();
        }
        // Read through a new context to exercise the PostgreSQL jsonb value converter.
        await using var updating = database.CreateContext();
        var result = await new UpdateExamShiftCommandHandler(updating).Handle(new(officer.Id, officer.Role,
            data.Ticket.ShiftId, "Updated after attendance", "Lab 2", DateTime.UtcNow.AddDays(3),
            DateTime.UtcNow.AddDays(3).AddHours(1), proctorId, data.Academic.Lecturer.Id), default);
        result.IsSuccess.Should().Be(allowed, result.Error);
        if (!allowed) result.Error.Should().Be(ShiftMutationErrors.HasUsedTickets);
        await using var verify = database.CreateContext();
        (await verify.RealExamSessionShifts.SingleAsync(s => s.Id == data.Ticket.ShiftId)).ShiftName
            .Should().Be(allowed ? "Updated after attendance" : "Integration shift");
        var audit = await verify.AuditLogs.SingleAsync(a => a.EntityId == data.Ticket.Id && a.Action == "PROCTOR_ATTENDANCE_UPDATED");
        audit.OldValues.Should().Be(oldValues);
        audit.NewValues.Should().Be("status=SCHEDULED;seat_number=1");
    }

    private static async Task AssertBlocked(TestDatabase database, int pid)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var observer = new NpgsqlConnection(database.ConnectionString);
        await observer.OpenAsync(timeout.Token);
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = @pid AND wait_event_type = 'Lock')", observer);
            command.Parameters.AddWithValue("pid", pid);
            if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!) return;
            await Task.Delay(25, timeout.Token);
        }
    }

    private sealed class SavingBarrier : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }
}

