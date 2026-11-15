using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.ActivateInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.DeleteInClassShift;
using OralExamination.Domain.Entities;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class InClassShiftMutationPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Delete_EmptyScheduledFutureShift_SucceedsInRelationalDatabase()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedAsync(database);
        await using var context = database.CreateContext();
        var result = await new DeleteInClassShiftCommandHandler(context).Handle(new(data.ActorId, data.ShiftId), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = database.CreateContext();
        (await verify.InClassExamShifts.AnyAsync(x => x.Id == data.ShiftId)).Should().BeFalse();
        (await verify.Classes.AnyAsync(x => x.Id == data.ClassId)).Should().BeTrue();
    }

    [Theory]
    [InlineData("participant")]
    [InlineData("submission")]
    [InlineData("password")]
    public async Task Delete_ExistingEvidence_IsRejectedAndEvidenceSurvives(string evidence)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedAsync(database, evidence);
        await using var context = database.CreateContext();
        var result = await new DeleteInClassShiftCommandHandler(context).Handle(new(data.ActorId, data.ShiftId), default);
        result.Error.Should().Be(evidence switch
        {
            "participant" => ShiftMutationErrors.HasParticipants,
            "submission" => ShiftMutationErrors.HasSubmissions,
            _ => ShiftMutationErrors.HasPasswords
        });
        await using var verify = database.CreateContext();
        (await verify.InClassExamShifts.AnyAsync(x => x.Id == data.ShiftId)).Should().BeTrue();
        if (evidence == "participant")
            (await verify.InClassShiftParticipants.CountAsync(x => x.ShiftId == data.ShiftId)).Should().Be(1);
        if (evidence == "submission")
            (await verify.InClassExamSubmissions.SingleAsync(x => x.ShiftId == data.ShiftId)).Transcript.Should().Be("Preserve this exam answer");
        if (evidence == "password")
            (await verify.InClassShiftPasswords.CountAsync(x => x.ShiftId == data.ShiftId)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentActivateThenDelete_DeleteWaitsForRowLockAndSeesCommittedActivation()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedAsync(database);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var activationLock = new RowLockProbe(pauseAfterLock: true);
        var deletionLock = new RowLockProbe(pauseAfterLock: false);
        await using var activating = database.CreateContext(activationLock);
        await using var deleting = database.CreateContext(deletionLock);
        var activation = new ActivateInClassShiftCommandHandler(activating)
            .Handle(new(data.ActorId, data.ShiftId), timeout.Token);
        await activationLock.Acquired.Task.WaitAsync(timeout.Token);
        var deletion = new DeleteInClassShiftCommandHandler(deleting)
            .Handle(new(data.ActorId, data.ShiftId), timeout.Token);
        var deletingPid = await deletionLock.Executing.Task.WaitAsync(timeout.Token);
        try
        {
            deletingPid.Should().NotBe(await activationLock.Executing.Task);
            await WaitForBlockedLockAsync(database, deletingPid, timeout.Token);
            deletion.IsCompleted.Should().BeFalse("PostgreSQL reports a real ungranted lock held behind activation");
        }
        finally
        {
            activationLock.Release.TrySetResult();
        }
        (await activation).IsSuccess.Should().BeTrue();
        (await deletion).Error.Should().Be(ShiftMutationErrors.NotScheduled);
        await using var verify = database.CreateContext();
        (await verify.InClassExamShifts.SingleAsync(x => x.Id == data.ShiftId)).Status.Should().Be("IN_PROGRESS");
    }

    private static async Task WaitForBlockedLockAsync(TestDatabase database, int processId, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(token);
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @pid AND NOT granted)", connection);
            command.Parameters.AddWithValue("pid", processId);
            if ((bool)(await command.ExecuteScalarAsync(token))!) return;
            await Task.Delay(20, token);
        }
    }

    private static async Task<ShiftData> SeedAsync(TestDatabase database, string? evidence = null)
    {
        await using var context = database.CreateContext();
        var academic = await TestData.AcademicAsync(context);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var cls = new Class { Code = "M" + suffix, Name = "Mutation class", JoinCode = "J" + suffix,
            CourseId = academic.Course.Id, SemesterId = academic.Course.SemesterId, LecturerId = academic.Lecturer.Id };
        var shift = new InClassExamShift { Class = cls, LecturerId = academic.Lecturer.Id,
            ShiftName = "Future mutable shift", StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1), TotpSecretKey = "test-secret", Status = "SCHEDULED" };
        context.Add(shift);
        if (evidence == "participant") context.Add(new InClassShiftParticipant { Shift = shift, StudentId = academic.Student.Id });
        if (evidence == "password") context.Add(new InClassShiftPassword { Shift = shift, PlainCode = "123456", PasswordHash = "test-hash",
            ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow.AddMinutes(5) });
        if (evidence == "submission")
        {
            var question = new PracticeQuestion { CourseId = academic.Course.Id, CloId = academic.Clo.Id,
                RubricId = academic.Rubric.Id, Title = "Question", Content = "Explain preservation" };
            context.Add(new InClassExamSubmission { Shift = shift, StudentId = academic.Student.Id,
                Question = question, Transcript = "Preserve this exam answer" });
        }
        await context.SaveChangesAsync();
        return new(shift.Id, academic.Lecturer.Id, cls.Id);
    }

    private sealed record ShiftData(Guid ShiftId, Guid ActorId, Guid ClassId);

    private sealed class RowLockProbe(bool pauseAfterLock) : DbCommandInterceptor
    {
        public TaskCompletionSource<int> Executing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Acquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static bool IsShiftLock(DbCommand command) => command.CommandText.Contains("in_class_exam_shifts", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (IsShiftLock(command)) Executing.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (IsShiftLock(command))
            {
                Acquired.TrySetResult();
                if (pauseAfterLock) await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
