using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class OfficialSubmissionTransactionTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ConcurrentSubmissions_OnTwoConnections_UniqueConstraintPreventsDuplicateAnswersAndJobs()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var command = await SeedCommandAsync(database);
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await first.Database.OpenConnectionAsync(timeout.Token);
        await second.Database.OpenConnectionAsync(timeout.Token);
        ((NpgsqlConnection)first.Database.GetDbConnection()).ProcessID.Should()
            .NotBe(((NpgsqlConnection)second.Database.GetDbConnection()).ProcessID);

        // ExecuteTicketDecisionAsync serializes concurrent retries via SELECT ... FOR UPDATE,
        // so both calls resolve idempotently without creating duplicate answers or jobs.
        var results = await Task.WhenAll(SubmitAsync(first), SubmitAsync(second));
        results.Should().OnlyContain(response => response.SubmissionCount == 1 && response.IsKioskLocked);
        results[0].SubmittedAt.Should().BeCloseTo(results[1].SubmittedAt, TimeSpan.FromMilliseconds(1));

        await using var verify = database.CreateContext();
        (await verify.ExamQuestionSubmissions.CountAsync()).Should().Be(1);
        (await verify.GradingJobs.CountAsync()).Should().Be(1);
        (await verify.StudentExamTickets.SingleAsync(x => x.Id == command.TicketId)).Status
            .Should().Be(ExamTicketStatus.Submitted);

        async Task<SubmitOfficialExamResponse> SubmitAsync(OralExamination.Infrastructure.Persistence.OralExamDbContext context)
        {
            var result = await new SubmitOfficialExamCommandHandler(context).Handle(command, timeout.Token);
            result.IsSuccess.Should().BeTrue(result.Error);
            return result.Value;
        }
    }

    [Fact]
    public async Task ConcurrentSubmissionInserts_BypassingTicketLock_UniqueConstraintPreventsDuplicateRows()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var command = await SeedCommandAsync(database);
        var questionId = (command.Answers ?? []).Single().ExamQuestionId;
        var barrier = new SubmissionBarrier();
        await using var first = database.CreateContext(barrier);
        await using var second = database.CreateContext(barrier);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await first.Database.OpenConnectionAsync(timeout.Token);
        await second.Database.OpenConnectionAsync(timeout.Token);
        ((NpgsqlConnection)first.Database.GetDbConnection()).ProcessID.Should()
            .NotBe(((NpgsqlConnection)second.Database.GetDbConnection()).ProcessID);

        var results = await Task.WhenAll(InsertAsync(first), InsertAsync(second));
        results.Count(state => state == "committed").Should().Be(1);
        results.Count(state => state == PostgresErrorCodes.UniqueViolation).Should().Be(1);

        await using var verify = database.CreateContext();
        (await verify.ExamQuestionSubmissions.CountAsync()).Should().Be(1);

        async Task<string> InsertAsync(OralExamination.Infrastructure.Persistence.OralExamDbContext context)
        {
            context.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = command.TicketId,
                ExamQuestionId = questionId,
                IsFollowUp = false,
                FollowUpIndex = 0,
                TranscriptWhisper = "Direct DB constraint test.",
                AudioR2Url = "integration/test.webm",
                TimeSpentSeconds = 20,
                GradingStatus = "pending",
                SubmittedAt = DateTime.UtcNow
            });

            try
            {
                await context.SaveChangesAsync(timeout.Token);
                return "committed";
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return PostgresErrorCodes.UniqueViolation;
            }
        }
    }

    [Fact]
    public async Task SubmitAndRetry_OnSeparateConnections_PersistsOneSubmissionAndOneDurableJob()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var command = await SeedCommandAsync(database);
        DateTime submittedAt;
        await using (var first = database.CreateContext())
        {
            var result = await new SubmitOfficialExamCommandHandler(first).Handle(command, CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value.SubmissionCount.Should().Be(1);
            submittedAt = result.Value.SubmittedAt;
        }

        await using (var retry = database.CreateContext())
        {
            var result = await new SubmitOfficialExamCommandHandler(retry).Handle(command, CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value.SubmittedAt.Should().BeCloseTo(submittedAt, TimeSpan.FromMilliseconds(1));
        }

        await using var verify = database.CreateContext();
        var ticket = await verify.StudentExamTickets.SingleAsync(x => x.Id == command.TicketId);
        ticket.Status.Should().Be(ExamTicketStatus.Submitted);
        ticket.SubmittedAt.Should().NotBeNull();
        var submission = await verify.ExamQuestionSubmissions.SingleAsync();
        submission.TicketId.Should().Be(ticket.Id);
        submission.TranscriptWhisper.Should().Be("A transaction commits atomically.");
        submission.AudioR2Url.Should().Be("integration/test.webm");
        var job = await verify.GradingJobs.SingleAsync();
        job.SubmissionId.Should().Be(submission.Id);
        job.Status.Should().Be("pending");
        job.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task JobInsertFailsInPostgres_TicketAndSubmissionAreRolledBack_ThenCleanRetrySucceeds()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var command = await SeedCommandAsync(database);
        // Restrictive test-only constraint causes a real server-side insert error.
        // No production constraint is removed or weakened.
        await database.ExecuteAsync("ALTER TABLE grading_jobs ADD CONSTRAINT ck_it_submission_failure CHECK (status <> 'pending')");
        await using (var attempt = database.CreateContext())
        {
            var action = async () => await new SubmitOfficialExamCommandHandler(attempt).Handle(command, CancellationToken.None);
            var error = await action.Should().ThrowAsync<DbUpdateException>();
            error.Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }

        await using (var verify = database.CreateContext())
        {
            var ticket = await verify.StudentExamTickets.SingleAsync(x => x.Id == command.TicketId);
            ticket.Status.Should().Be(ExamTicketStatus.InProgress);
            ticket.SubmittedAt.Should().BeNull();
            (await verify.ExamQuestionSubmissions.CountAsync()).Should().Be(0);
            (await verify.GradingJobs.CountAsync()).Should().Be(0);
        }

        await database.ExecuteAsync("ALTER TABLE grading_jobs DROP CONSTRAINT ck_it_submission_failure");
        await using var retry = database.CreateContext();
        var result = await new SubmitOfficialExamCommandHandler(retry).Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        await using var final = database.CreateContext();
        (await final.ExamQuestionSubmissions.CountAsync()).Should().Be(1);
        (await final.GradingJobs.CountAsync()).Should().Be(1);
    }

    private static async Task<SubmitOfficialExamCommand> SeedCommandAsync(TestDatabase database)
    {
        await using var seed = database.CreateContext();
        var data = await TestData.OfficialAsync(seed);
        return new SubmitOfficialExamCommand(data.Academic.Student.Id, data.Ticket.Id, "127.0.0.1",
            [new OfficialExamAnswerSubmissionRequest(data.Question.Id, " A transaction commits atomically. ", null, 20)]);
    }

    private sealed class SubmissionBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothSaving = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _bothSaving.TrySetResult();
            await _bothSaving.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }
}
