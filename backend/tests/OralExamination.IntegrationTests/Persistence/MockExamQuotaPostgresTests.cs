using System.Collections.Concurrent;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Domain.Entities;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class MockExamQuotaPostgresTests(PostgresFixture fixture)
{
    private static readonly TimeProvider Clock = new FixedClock(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly QuotaDate = new(2026, 11, 14);

    [Theory]
    [InlineData("student")]
    [InlineData("course")]
    [InlineData("day")]
    public async Task HeldStartLock_DoesNotBlockOtherQuotaKeys(string dimension)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        AcademicData data;
        AcademicData other;
        await using (var seed = database.CreateContext())
        {
            data = await SeedAsync(seed);
            other = await SeedAsync(seed);
            // Biến thể tranh chấp theo student dùng cùng course nhưng sinh viên khác.
            seed.ClassEnrollments.Add(new ClassEnrollment
            {
                ClassId = await seed.Classes.Where(c => c.CourseId == data.Course.Id).Select(c => c.Id).SingleAsync(),
                StudentId = other.Student.Id,
                Status = "enrolled"
            });
            seed.ClassEnrollments.Add(new ClassEnrollment
            {
                ClassId = await seed.Classes.Where(c => c.CourseId == other.Course.Id).Select(c => c.Id).SingleAsync(),
                StudentId = data.Student.Id,
                Status = "enrolled"
            });
            await seed.SaveChangesAsync();
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var holder = database.CreateContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var held = holder.ExecuteMockExamStartAsync(data.Student.Id, data.Course.Id, QuotaDate, async ct =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return 0;
        }, timeout.Token);
        try
        {
            await entered.Task.WaitAsync(timeout.Token);
            await using var independent = database.CreateContext();
            var student = dimension == "student" ? other.Student.Id : data.Student.Id;
            var course = dimension == "course" ? other.Course.Id : data.Course.Id;
            var clock = dimension == "day"
                ? new FixedClock(Clock.GetUtcNow().AddDays(1)) : Clock;
            var result = await new StartMockExamCommandHandler(independent, clock)
                .Handle(new(course, student, false), timeout.Token).WaitAsync(TimeSpan.FromSeconds(10));
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        finally
        {
            release.TrySetResult();
            await held;
        }
    }

    [Fact]
    public async Task CancelledStartWhileLockHeld_DoesNotConsumeQuota_AndLaterStartSucceeds()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        AcademicData data;
        await using (var seed = database.CreateContext()) data = await SeedAsync(seed);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var holder = database.CreateContext();
        var held = holder.ExecuteMockExamStartAsync(data.Student.Id, data.Course.Id, QuotaDate, async ct =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return 0;
        }, timeout.Token);
        try
        {
            await entered.Task.WaitAsync(timeout.Token);
            var signal = new LockAttemptSignal();
            await using var waiting = database.CreateContext(signal);
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var pending = new StartMockExamCommandHandler(waiting, Clock)
                .Handle(new(data.Course.Id, data.Student.Id, false), cancel.Token);
            await signal.Attempted.Task.WaitAsync(timeout.Token);
            cancel.Cancel();
            var act = async () => await pending;
            await act.Should().ThrowAsync<OperationCanceledException>();
            await using var verify = database.CreateContext();
            (await verify.MockExamQuotas.AnyAsync(q => q.CourseId == data.Course.Id)).Should().BeFalse();
            (await verify.MockExamSessions.AnyAsync(q => q.CourseId == data.Course.Id)).Should().BeFalse();
        }
        finally
        {
            release.TrySetResult();
            await held;
        }
        await using var retry = database.CreateContext();
        (await new StartMockExamCommandHandler(retry, Clock).Handle(new(data.Course.Id, data.Student.Id, false), default))
            .IsSuccess.Should().BeTrue();
    }
    [Fact]
    public async Task SixConcurrentStarts_ThreeSlots_ExactlyThreeCommitsAndNoLostCount()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        AcademicData data;
        await using (var seed = database.CreateContext())
        {
            data = await SeedAsync(seed);
            data.Course.MaxMockExamsPerDay = 3;
            await seed.SaveChangesAsync();
        }
        var race = new StartRace(6);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var context = database.CreateContext(race);
            return await new StartMockExamCommandHandler(context, Clock).Handle(new(data.Course.Id, data.Student.Id, false), timeout.Token);
        }));
        race.Connections.Should().HaveCount(6);
        results.Count(r => r.IsSuccess).Should().Be(3);
        results.Where(r => !r.IsSuccess).Should().OnlyContain(r => r.Error.StartsWith("QUOTA_EXCEEDED:"));
        results.Where(r => r.IsSuccess).Select(r => r.Value.UsedQuota).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        await using var verify = database.CreateContext();
        (await verify.MockExamQuotas.SingleAsync(q => q.CourseId == data.Course.Id)).UsedCount.Should().Be(3);
        (await verify.MockExamSessions.CountAsync(s => s.CourseId == data.Course.Id)).Should().Be(3);
        (await verify.MockExamAnswers.CountAsync(a => a.Session.CourseId == data.Course.Id)).Should().Be(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionWriteFails_QuotaAndGeneratedSetRollBack_AndNewContextCanRetry(bool existingQuota)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        AcademicData data;
        await using (var seed = database.CreateContext())
        {
            data = await SeedAsync(seed);
            if (existingQuota)
            {
                seed.MockExamQuotas.Add(new MockExamQuota
                {
                    CourseId = data.Course.Id, StudentId = data.Student.Id,
                    QuotaDate = QuotaDate, UsedCount = 0
                });
                await seed.SaveChangesAsync();
            }
        }
        await database.ExecuteAsync("ALTER TABLE mock_exam_sessions ADD CONSTRAINT ck_it_start_failure CHECK (status <> 'in_progress')");
        await using (var attempt = database.CreateContext())
        {
            var act = async () => await new StartMockExamCommandHandler(attempt, Clock).Handle(new(data.Course.Id, data.Student.Id, false), default);
            var error = await act.Should().ThrowAsync<DbUpdateException>();
            var postgres = error.Which.InnerException.Should().BeOfType<PostgresException>().Which;
            postgres.SqlState.Should().Be("23514");
            postgres.ConstraintName.Should().Be("ck_it_start_failure");
        }
        await using (var verify = database.CreateContext())
        {
            (await verify.MockExamSessions.CountAsync(s => s.CourseId == data.Course.Id)).Should().Be(0);
            (await verify.ExamSets.CountAsync(s => s.CourseId == data.Course.Id)).Should().Be(0);
            var quota = await verify.MockExamQuotas.SingleOrDefaultAsync(q => q.CourseId == data.Course.Id);
            if (existingQuota) quota!.UsedCount.Should().Be(0);
            else quota.Should().BeNull();
        }
        await database.ExecuteAsync("ALTER TABLE mock_exam_sessions DROP CONSTRAINT ck_it_start_failure");
        await using var retry = database.CreateContext();
        (await new StartMockExamCommandHandler(retry, Clock).Handle(new(data.Course.Id, data.Student.Id, false), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InsufficientQuestionBank_DoesNotConsumeQuotaOrPersistDefaultStructure()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var data = await TestData.AcademicAsync(context);
        var result = await new StartMockExamCommandHandler(context, Clock).Handle(new(data.Course.Id, data.Student.Id, false), default);
        result.IsSuccess.Should().BeFalse();
        await using var verify = database.CreateContext();
        (await verify.MockExamQuotas.AnyAsync(q => q.CourseId == data.Course.Id)).Should().BeFalse();
        (await verify.ExamStructures.AnyAsync(q => q.CourseId == data.Course.Id)).Should().BeFalse();
        (await verify.MockExamSessions.AnyAsync(q => q.CourseId == data.Course.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task StaleTrackedQuota_IsRefreshedAfterLock_AndCannotAdmitAnotherStart()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var stale = database.CreateContext();
        var data = await SeedAsync(stale);
        var quota = new MockExamQuota
        {
            CourseId = data.Course.Id, StudentId = data.Student.Id,
            QuotaDate = QuotaDate, UsedCount = 0
        };
        stale.MockExamQuotas.Add(quota);
        await stale.SaveChangesAsync();
        await using (var other = database.CreateContext())
            (await new StartMockExamCommandHandler(other, Clock).Handle(new(data.Course.Id, data.Student.Id, false), default)).IsSuccess.Should().BeTrue();
        quota.UsedCount.Should().Be(0, "the reused context still has the old snapshot before acquiring the guard");
        var result = await new StartMockExamCommandHandler(stale, Clock).Handle(new(data.Course.Id, data.Student.Id, false), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("QUOTA_EXCEEDED:");
        await using var verify = database.CreateContext();
        (await verify.MockExamSessions.CountAsync(s => s.CourseId == data.Course.Id)).Should().Be(1);
        (await verify.MockExamQuotas.SingleAsync(q => q.Id == quota.Id)).UsedCount.Should().Be(1);
    }

    [Theory]
    [InlineData("2026-11-14T16:59:59.9999999Z", 14)]
    [InlineData("2026-11-14T17:00:00Z", 15)]
    [InlineData("2026-11-14T17:00:00.0000001Z", 15)]
    public async Task VietnamMidnight_StartAndQuotaQueryUseSameDay(string utc, int day)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var data = await SeedAsync(context);
        var date = new DateOnly(2026, 11, day);
        context.MockExamQuotas.Add(new MockExamQuota
        {
            StudentId = data.Student.Id, CourseId = data.Course.Id,
            QuotaDate = date.AddDays(-1), UsedCount = 1
        });
        await context.SaveChangesAsync();
        var clock = new FixedClock(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));
        var query = new GetMockExamQuotaQuery(data.Course.Id, data.Student.Id);
        var before = await new GetMockExamQuotaQueryHandler(context, clock).Handle(query, default);
        before.IsSuccess.Should().BeTrue();
        before.Value.QuotaDate.Should().Be(date);
        before.Value.UsedCount.Should().Be(0);
        var start = await new StartMockExamCommandHandler(context, clock).Handle(new(data.Course.Id, data.Student.Id, false), default);
        start.IsSuccess.Should().BeTrue(start.Error);
        start.Value.StartedAt.Should().Be(clock.GetUtcNow().UtcDateTime);
        var after = await new GetMockExamQuotaQueryHandler(context, clock).Handle(query, default);
        after.Value.QuotaDate.Should().Be(date);
        after.Value.UsedCount.Should().Be(1);
        after.Value.CanStartExam.Should().BeFalse();
        await using var verify = database.CreateContext();
        var quotas = await verify.MockExamQuotas.Where(q => q.CourseId == data.Course.Id).ToListAsync();
        quotas.Should().HaveCount(2);
        quotas.Should().OnlyContain(q => q.UsedCount == 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentStarts_LastAvailableSlot_OnlyOneSessionCommits(bool existingQuota)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        AcademicData data;
        await using (var seed = database.CreateContext())
        {
            data = await SeedAsync(seed);
            if (existingQuota)
            {
                seed.MockExamQuotas.Add(new MockExamQuota
                {
                    CourseId = data.Course.Id, StudentId = data.Student.Id,
                    QuotaDate = QuotaDate, UsedCount = 0
                });
                await seed.SaveChangesAsync();
            }
        }
        var race = new StartRace();
        await using var first = database.CreateContext(race);
        await using var second = database.CreateContext(race);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var command = new StartMockExamCommand(data.Course.Id, data.Student.Id, false);

        var results = await Task.WhenAll(
            new StartMockExamCommandHandler(first, Clock).Handle(command, timeout.Token),
            new StartMockExamCommandHandler(second, Clock).Handle(command, timeout.Token));

        race.Connections.Should().HaveCount(2, "two independent PostgreSQL connections must race");
        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Single(result => !result.IsSuccess).Error.Should().StartWith("QUOTA_EXCEEDED:");
        await using var verify = database.CreateContext();
        (await verify.MockExamQuotas.SingleAsync(q => q.CourseId == data.Course.Id)).UsedCount.Should().Be(1);
        (await verify.MockExamSessions.CountAsync(s => s.CourseId == data.Course.Id)).Should().Be(1);
        (await verify.ExamSets.CountAsync(s => s.CourseId == data.Course.Id)).Should().Be(0,
            "thi thử không tạo ExamSet DRAFT rác cho từng lần bắt đầu");
        (await verify.MockExamSessions.SingleAsync(s => s.CourseId == data.Course.Id)).ExamSetId.Should().BeNull();
        (await verify.MockExamAnswers.CountAsync(a => a.Session.CourseId == data.Course.Id)).Should().Be(1);
    }

    internal static async Task<AcademicData> SeedAsync(OralExamination.Infrastructure.Persistence.OralExamDbContext context)
    {
        var data = await TestData.AcademicAsync(context);
        data.Course.MaxMockExamsPerDay = 1;
        data.Course.MockExamTotalQuestions = 1;
        data.Course.MockExamBloomDistribution = "{\"Understand\":1}";
        context.ExamStructures.Add(new ExamStructure
        {
            CourseId = data.Course.Id, CreatedBy = data.Lecturer.Id,
            Name = "Quota structure", TotalQuestions = 1, DurationMinutes = 15,
            BloomDistribution = "{\"Understand\":1}"
        });
        context.PracticeQuestions.Add(new PracticeQuestion
        {
            CourseId = data.Course.Id, RubricId = data.Rubric.Id, CloId = data.Clo.Id,
            Title = "Quota question", Content = "Explain atomicity.", ApprovalStatus = "APPROVED", BloomLevel = "Understand"
        });
        var courseClass = new Class
        {
            CourseId = data.Course.Id,
            SemesterId = data.Course.SemesterId,
            Code = "IT" + Guid.NewGuid().ToString("N")[..8],
            Name = "Quota integration class",
            IsActive = true
        };
        context.Classes.Add(courseClass);
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Class = courseClass,
            StudentId = data.Student.Id,
            Status = "enrolled"
        });
        await context.SaveChangesAsync();
        return data;
    }

    // Before a transaction guard exists, synchronize both quota SELECT results so
    // both handlers observe the same old count. With a guard, synchronize BEFORE
    // acquiring the lock: a barrier inside the locked region would deadlock.
    private sealed class StartRace(int contenders = 2) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<int, byte> _guarded = new();
        public ConcurrentDictionary<int, byte> Connections { get; } = new();

        private async Task ArriveAsync(DbCommand command, CancellationToken token)
        {
            var id = ((NpgsqlConnection)command.Connection!).ProcessID;
            Connections.TryAdd(id, 0);
            if (Connections.Count == contenders) _both.TrySetResult();
            await _both.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.OrdinalIgnoreCase))
            {
                _guarded.TryAdd(((NpgsqlConnection)command.Connection!).ProcessID, 0);
                await ArriveAsync(command, cancellationToken);
            }
            return result;
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("mock_exam_quotas", StringComparison.OrdinalIgnoreCase)
                && !_guarded.ContainsKey(((NpgsqlConnection)command.Connection!).ProcessID))
                await ArriveAsync(command, cancellationToken);
            return result;
        }
    }

    private sealed class LockAttemptSignal : DbCommandInterceptor
    {
        public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.OrdinalIgnoreCase)) Attempted.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedClock(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }
}
