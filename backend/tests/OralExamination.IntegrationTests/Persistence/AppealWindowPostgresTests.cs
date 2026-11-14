using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OralExamination.Application.Features.Appeals;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Student.Commands.AcknowledgeOfficialGrade;
using OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;
using OralExamination.Domain.Entities;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class AppealWindowPostgresTests(PostgresFixture fixture)
{
    private static readonly DateTime PublishedAt = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task AppealDeadline_UsesPersistedPublicationAndStrict72Hours(long ticksFromDeadline, bool allowed)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedPublishedAsync(database);
        var clock = new FixedClock(PublishedAt.AddHours(72).AddTicks(ticksFromDeadline));
        await using var context = database.CreateContext();

        var grade = await new GetOfficialExamGradeQueryHandler(context, clock: clock)
            .Handle(new(data.Ticket.Id, data.Academic.Student.Id), default);
        grade.IsSuccess.Should().BeTrue();
        grade.Value.PublishedAt.Should().Be(PublishedAt);
        grade.Value.InternalAppeal.CanAppeal.Should().Be(allowed);
        grade.Value.InternalAppeal.AppealDeadlineAtUtc.Should().Be(PublishedAt.AddHours(72));

        var result = await new CreateAppealCommandHandler(context, clock: clock).Handle(Appeal(data), default);
        result.IsSuccess.Should().Be(allowed);
        if (!allowed) result.Error.Should().StartWith("CONFLICT:");
        await using var verify = database.CreateContext();
        (await verify.AppealRequests.CountAsync(a => a.TicketId == data.Ticket.Id)).Should().Be(allowed ? 1 : 0);
        (await verify.StudentExamTickets.SingleAsync(t => t.Id == data.Ticket.Id)).GradesPublishedAt.Should().Be(PublishedAt);
    }

    [Fact]
    public async Task Acceptance_FreshlyPublishedGrade_ShowsDeadlineAndAllowsBothInitialChoices()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedPublishedAsync(database);
        await using var context = database.CreateContext();

        var grade = await new GetOfficialExamGradeQueryHandler(
                context, clock: new FixedClock(PublishedAt.AddHours(1)))
            .Handle(new(data.Ticket.Id, data.Academic.Student.Id), default);

        grade.IsSuccess.Should().BeTrue();
        grade.Value.PublishedAt.Should().Be(PublishedAt);
        grade.Value.InternalAppeal.AppealDeadlineAtUtc.Should().Be(PublishedAt.AddHours(72));
        grade.Value.InternalAppeal.CanAppeal.Should().BeTrue();
        grade.Value.InternalAppeal.CanAcknowledge.Should().BeTrue();
    }

    [Fact]
    public async Task Acceptance_AppealSubmitted_DisablesBothChoicesAndAcknowledgementConflicts()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedPublishedAsync(database);
        await using (var appealContext = database.CreateContext())
        {
            var created = await new CreateAppealCommandHandler(
                    appealContext, clock: new FixedClock(PublishedAt.AddHours(1)))
                .Handle(Appeal(data), default);
            created.IsSuccess.Should().BeTrue(created.Error);
        }

        await using (var gradeContext = database.CreateContext())
        {
            var grade = await new GetOfficialExamGradeQueryHandler(
                    gradeContext, clock: new FixedClock(PublishedAt.AddHours(1)))
                .Handle(new(data.Ticket.Id, data.Academic.Student.Id), default);
            grade.IsSuccess.Should().BeTrue();
            grade.Value.InternalAppeal.CanAppeal.Should().BeFalse();
            grade.Value.InternalAppeal.CanAcknowledge.Should().BeFalse();
            grade.Value.InternalAppeal.Instruction.Should().Be("Đang có đơn phúc khảo được xử lý.");
        }

        await using var acknowledgementContext = database.CreateContext();
        var acknowledgement = await new AcknowledgeOfficialGradeCommandHandler(acknowledgementContext)
            .Handle(new(data.Ticket.Id, data.Academic.Student.Id), default);
        acknowledgement.IsSuccess.Should().BeFalse();
        acknowledgement.Error.Should().StartWith("CONFLICT:");
        (await acknowledgementContext.StudentExamTickets.SingleAsync(t => t.Id == data.Ticket.Id))
            .StudentAcknowledgementStatus.Should().Be("PENDING");
        (await acknowledgementContext.AppealRequests.CountAsync(a => a.TicketId == data.Ticket.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Acceptance_GradeAcknowledged_DisablesBothChoicesAndAppealConflicts()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedPublishedAsync(database);
        await using (var acknowledgementContext = database.CreateContext())
        {
            var acknowledged = await new AcknowledgeOfficialGradeCommandHandler(acknowledgementContext)
                .Handle(new(data.Ticket.Id, data.Academic.Student.Id), default);
            acknowledged.IsSuccess.Should().BeTrue(acknowledged.Error);
        }

        await using (var gradeContext = database.CreateContext())
        {
            var grade = await new GetOfficialExamGradeQueryHandler(
                    gradeContext, clock: new FixedClock(PublishedAt.AddHours(1)))
                .Handle(new(data.Ticket.Id, data.Academic.Student.Id), default);
            grade.IsSuccess.Should().BeTrue();
            grade.Value.StudentAcknowledgementStatus.Should().Be("ACKNOWLEDGED");
            grade.Value.InternalAppeal.CanAppeal.Should().BeFalse();
            grade.Value.InternalAppeal.CanAcknowledge.Should().BeFalse();
        }

        await using var appealContext = database.CreateContext();
        var appeal = await new CreateAppealCommandHandler(
                appealContext, clock: new FixedClock(PublishedAt.AddHours(1)))
            .Handle(Appeal(data), default);
        appeal.IsSuccess.Should().BeFalse();
        appeal.Error.Should().StartWith("CONFLICT:");
        (await appealContext.AppealRequests.CountAsync(a => a.TicketId == data.Ticket.Id)).Should().Be(0);
    }

    [Fact]
    public async Task AcknowledgedGrade_CannotAppeal_AndRepeatedAcknowledgementIsIdempotent()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedPublishedAsync(database);
        await using (var context = database.CreateContext())
        {
            var handler = new AcknowledgeOfficialGradeCommandHandler(context);
            (await handler.Handle(new(data.Ticket.Id, data.Academic.Student.Id), default)).IsSuccess.Should().BeTrue();
            (await handler.Handle(new(data.Ticket.Id, data.Academic.Student.Id), default)).IsSuccess.Should().BeTrue();
        }
        await using var appealContext = database.CreateContext();
        var result = await new CreateAppealCommandHandler(appealContext, clock: new FixedClock(PublishedAt.AddHours(1)))
            .Handle(Appeal(data), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
        (await appealContext.AppealRequests.CountAsync(a => a.TicketId == data.Ticket.Id)).Should().Be(0);
    }

    [Theory]
    [InlineData("SUBMITTED_TO_EXAM_OFFICE")]
    [InlineData("ASSIGNED_TO_LECTURER")]
    [InlineData("REGRADED_BY_LECTURER")]
    public async Task OpenAppeal_CannotAcknowledge(string appealStatus)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedPublishedAsync(database);
        await using (var context = database.CreateContext())
            (await new CreateAppealCommandHandler(context, clock: new FixedClock(PublishedAt.AddHours(1)))
                .Handle(Appeal(data), default)).IsSuccess.Should().BeTrue();
        await database.ExecuteAsync($"UPDATE appeal_requests SET status = '{appealStatus}' WHERE ticket_id = '{data.Ticket.Id}'");

        await using var decisionContext = database.CreateContext();
        var result = await new AcknowledgeOfficialGradeCommandHandler(decisionContext)
            .Handle(new(data.Ticket.Id, data.Academic.Student.Id), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
        (await decisionContext.StudentExamTickets.SingleAsync(t => t.Id == data.Ticket.Id))
            .StudentAcknowledgementStatus.Should().Be("PENDING");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentAppealAndAcknowledgement_OnlyOneCommits_EvenWithStaleTrackedTickets(bool startAppealFirst)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedPublishedAsync(database);
        var arrivals = new LockArrivals();
        await using var appealContext = database.CreateContext(arrivals);
        await using var acknowledgementContext = database.CreateContext(arrivals);
        await appealContext.StudentExamTickets.SingleAsync(t => t.Id == data.Ticket.Id);
        await acknowledgementContext.StudentExamTickets.SingleAsync(t => t.Id == data.Ticket.Id);

        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM student_exam_tickets WHERE id = @id FOR UPDATE", blocker, transaction);
        command.Parameters.AddWithValue("id", data.Ticket.Id);
        await command.ExecuteScalarAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        async Task<bool> AppealAsync() => (await new CreateAppealCommandHandler(appealContext, clock: new FixedClock(PublishedAt.AddHours(1)))
            .Handle(Appeal(data), timeout.Token)).IsSuccess;
        async Task<bool> AcknowledgeAsync() => (await new AcknowledgeOfficialGradeCommandHandler(acknowledgementContext)
            .Handle(new(data.Ticket.Id, data.Academic.Student.Id), timeout.Token)).IsSuccess;

        var first = startAppealFirst ? AppealAsync() : AcknowledgeAsync();
        var second = startAppealFirst ? AcknowledgeAsync() : AppealAsync();
        await arrivals.Both.WaitAsync(timeout.Token);
        await AssertBothDatabaseDecisionsBlockedAsync(database, timeout.Token);
        await transaction.CommitAsync(timeout.Token);
        var results = await Task.WhenAll(first, second);
        results.Count(success => success).Should().Be(1);

        await using var verify = database.CreateContext();
        var acknowledged = (await verify.StudentExamTickets.SingleAsync(t => t.Id == data.Ticket.Id))
            .StudentAcknowledgementStatus == "ACKNOWLEDGED";
        var appeals = await verify.AppealRequests.CountAsync(a => a.TicketId == data.Ticket.Id);
        (acknowledged ? 1 : 0).Should().Be(1 - appeals);
        appeals.Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task ConcurrentDuplicateAppeals_OnlyOneRequestAndItsNotificationsCommit()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedPublishedAsync(database);
        int baselineNotifications;
        int expectedNotifications;
        await using (var baseline = database.CreateContext())
        {
            baselineNotifications = await baseline.Notifications.CountAsync();
            // The department-head fallback is an examination officer in this fixture;
            // delivery is one idempotent notification per recipient, not a duplicate.
            expectedNotifications = await baseline.Users.CountAsync(u => u.Role == "examination_officer" && u.IsActive);
        }
        var arrivals = new LockArrivals();
        await using var firstContext = database.CreateContext(arrivals);
        await using var secondContext = database.CreateContext(arrivals);
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM student_exam_tickets WHERE id = @id FOR UPDATE", blocker, transaction);
        command.Parameters.AddWithValue("id", data.Ticket.Id);
        await command.ExecuteScalarAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var first = new CreateAppealCommandHandler(firstContext, clock: new FixedClock(PublishedAt.AddHours(1)))
            .Handle(Appeal(data), timeout.Token);
        var second = new CreateAppealCommandHandler(secondContext, clock: new FixedClock(PublishedAt.AddHours(1)))
            .Handle(Appeal(data), timeout.Token);
        await arrivals.Both.WaitAsync(timeout.Token);
        await AssertBothDatabaseDecisionsBlockedAsync(database, timeout.Token);
        await transaction.CommitAsync(timeout.Token);
        var results = await Task.WhenAll(first, second);
        results.Count(result => result.IsSuccess).Should().Be(1);
        await using var verify = database.CreateContext();
        var appeal = await verify.AppealRequests.SingleAsync(a => a.TicketId == data.Ticket.Id);
        (await verify.Notifications.CountAsync(n => n.RelatedEntityId == appeal.Id)).Should().Be(expectedNotifications);
        (await verify.Notifications.CountAsync()).Should().Be(baselineNotifications + expectedNotifications);
    }

    [Fact]
    public async Task LegacyMigration_GrantsFreshWindowOnlyToPublishedGrades_AndDoesNotResetOnRerun()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var published = await SeedPublishedAsync(database);
        OfficialData locked;
        OfficialData prepublication;
        await using (var context = database.CreateContext())
        {
            locked = await TestData.OfficialAsync(context, locked: true);
            prepublication = await TestData.OfficialAsync(context);
        }
        // Construct the previous deployed schema, not an EnsureCreated approximation.
        await using (var context = database.CreateContext())
            await context.GetService<IMigrator>().MigrateAsync("20261114020000_RepairDuplicateCourseOfferings");
        await database.ExecuteAsync($"UPDATE student_exam_tickets SET status = 'AI_GRADED', is_locked = true WHERE id = '{prepublication.Ticket.Id}'");
        var before = await database.ScalarAsync<DateTime>("SELECT clock_timestamp()");
        await using (var context = database.CreateContext()) await context.Database.MigrateAsync();
        var after = await database.ScalarAsync<DateTime>("SELECT clock_timestamp()");

        DateTime migrationPublication;
        await using (var context = database.CreateContext())
        {
            var tickets = await context.StudentExamTickets.AsNoTracking().ToDictionaryAsync(t => t.Id);
            migrationPublication = tickets[published.Ticket.Id].GradesPublishedAt!.Value;
            migrationPublication.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
            tickets[locked.Ticket.Id].GradesPublishedAt.Should().Be(migrationPublication);
            tickets[prepublication.Ticket.Id].GradesPublishedAt.Should().BeNull();
        }
        // An explicit trustworthy timestamp and the fresh legacy window both survive reruns.
        await database.ExecuteAsync($"UPDATE student_exam_tickets SET grades_published_at = '2026-01-01T00:00:00Z' WHERE id = '{locked.Ticket.Id}'");
        await using (var context = database.CreateContext()) await context.Database.MigrateAsync();
        await using var verify = database.CreateContext();
        (await verify.StudentExamTickets.SingleAsync(t => t.Id == published.Ticket.Id)).GradesPublishedAt.Should().Be(migrationPublication);
        (await verify.StudentExamTickets.SingleAsync(t => t.Id == locked.Ticket.Id)).GradesPublishedAt
            .Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        (await verify.StudentExamTickets.SingleAsync(t => t.Id == prepublication.Ticket.Id)).GradesPublishedAt.Should().BeNull();
    }

    private static async Task<OfficialData> SeedPublishedAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        var data = await TestData.OfficialAsync(context);
        data.Ticket.Status = "PUBLISHED";
        data.Ticket.GradesPublishedAt = PublishedAt;
        context.Users.Add(new User
        {
            Email = Guid.NewGuid().ToString("N") + "@officer.example.test", FullName = "Appeal Integration Officer",
            Role = "examination_officer", IsActive = true
        });
        await context.SaveChangesAsync();
        return data;
    }

    private static CreateAppealCommand Appeal(OfficialData data) =>
        new(data.Academic.Student.Id, data.Ticket.Id, null, "Please review the official grade.");

    private static async Task AssertBothDatabaseDecisionsBlockedAsync(TestDatabase database, CancellationToken cancellationToken)
    {
        await using var observer = new NpgsqlConnection(database.ConnectionString);
        await observer.OpenAsync(cancellationToken);
        while (true)
        {
            await using var command = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                  AND query ILIKE '%FOR UPDATE%'
                """, observer);
            var waiting = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
            if (waiting == 2) return;
            await Task.Delay(25, cancellationToken);
        }
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    // Signals before the database lock, never from SaveChanges (which would deadlock the winner).
    private sealed class LockArrivals : DbCommandInterceptor
    {
        private int _count;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Both => _both.Task;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase) && Interlocked.Increment(ref _count) == 2)
                _both.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}
