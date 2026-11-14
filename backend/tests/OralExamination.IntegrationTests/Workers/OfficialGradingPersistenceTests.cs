using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Workers;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class OfficialGradingPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ConcurrentDispatchers_ClaimSameCandidateOnTwoConnections_EnqueueExactlyOnce()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var jobId = await SeedJobAsync(database);
        var barrier = new ClaimBarrier();
        var queue = new RecordingOfficialQueue();
        using var services = CreateServices(database, barrier);
        using var first = CreateDispatcher(services, queue);
        using var second = CreateDispatcher(services, queue);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // The interceptor pauses only the UPDATE: both independent dispatchers have
        // already read the same candidate before either may acquire its lease.
        await Task.WhenAll(DispatchAsync(first, timeout.Token), DispatchAsync(second, timeout.Token));

        barrier.ConnectionIds.Should().HaveCount(2, "the claim race must use two physical PostgreSQL connections");
        queue.Tasks.Should().ContainSingle().Which.JobId.Should().Be(jobId);
        await using var verify = database.CreateContext();
        var job = await verify.GradingJobs.SingleAsync();
        job.Status.Should().Be("processing");
        job.AttemptCount.Should().Be(1, "the conditional database UPDATE permits only one successful claimant");
        job.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Theory]
    [InlineData("unexpired-lease")]
    [InlineData("future-retry")]
    [InlineData("completed")]
    [InlineData("failed")]
    public async Task Dispatcher_DoesNotClaimIneligibleDurableJob(string condition)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await SeedJobAsync(database, job =>
        {
            job.AttemptCount = 3;
            if (condition == "unexpired-lease")
            {
                job.Status = "processing";
                job.LockedUntil = DateTime.UtcNow.AddMinutes(10);
            }
            else if (condition == "future-retry") job.NextAttemptAt = DateTime.UtcNow.AddMinutes(10);
            else job.Status = condition;
        });
        using var services = CreateServices(database);
        var queue = new RecordingOfficialQueue();
        using var dispatcher = CreateDispatcher(services, queue);

        await DispatchAsync(dispatcher, CancellationToken.None);

        queue.Tasks.Should().BeEmpty();
        await using var verify = database.CreateContext();
        var persisted = await verify.GradingJobs.SingleAsync();
        persisted.AttemptCount.Should().Be(3);
        if (condition == "unexpired-lease") persisted.LockedUntil.Should().BeAfter(DateTime.UtcNow);
        else if (condition == "future-retry") persisted.NextAttemptAt.Should().BeAfter(DateTime.UtcNow);
        else persisted.Status.Should().Be(condition);
    }

    [Fact]
    public async Task NewDispatcherInstance_RecoversExpiredLeaseFromDatabase_WithoutReusingOldQueue()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var jobId = await SeedJobAsync(database, job =>
        {
            job.Status = "processing";
            job.AttemptCount = 2;
            job.LockedUntil = DateTime.UtcNow.AddMinutes(-10);
            job.NextAttemptAt = DateTime.UtcNow.AddMinutes(-20);
        });
        // This verifies service re-creation against durable state, not an OS-process
        // crash, PostgreSQL restart, or the external AI worker's idempotency.
        var recoveredQueue = new RecordingOfficialQueue();
        using var freshServices = CreateServices(database);
        using var freshDispatcher = CreateDispatcher(freshServices, recoveredQueue);

        await DispatchAsync(freshDispatcher, CancellationToken.None);
        await DispatchAsync(freshDispatcher, CancellationToken.None);

        recoveredQueue.Tasks.Should().ContainSingle().Which.JobId.Should().Be(jobId);
        await using var verify = database.CreateContext();
        var recovered = await verify.GradingJobs.SingleAsync();
        recovered.Status.Should().Be("processing");
        recovered.AttemptCount.Should().Be(3);
        recovered.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task FreshWorkerInstance_ProcessesJobRecoveredAfterPreviousHostLeaseExpired()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var jobId = await SeedJobAsync(database, job =>
        {
            // Represents a previous host that stopped after claiming but before completing the job.
            job.Status = "processing";
            job.AttemptCount = 1;
            job.LockedUntil = DateTime.UtcNow.AddMinutes(-10);
            job.NextAttemptAt = DateTime.UtcNow.AddMinutes(-20);
        });
        var recoveredQueue = new RecordingOfficialQueue();
        var gradingService = new SuccessfulAiGradingService();
        using var freshServices = CreateServices(database, gradingService: gradingService);
        using var freshDispatcher = CreateDispatcher(freshServices, recoveredQueue);

        await DispatchAsync(freshDispatcher, CancellationToken.None);
        recoveredQueue.Tasks.Should().ContainSingle();

        // A new worker instance consumes only the task rediscovered from durable PostgreSQL state.
        using var freshWorker = new OfficialExamGradingWorker(
            recoveredQueue,
            freshServices.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamGradingWorker>.Instance);
        await ProcessAsync(freshWorker, recoveredQueue.Tasks.Single(), CancellationToken.None);

        gradingService.CallCount.Should().Be(1);
        await using var verify = database.CreateContext();
        var job = await verify.GradingJobs.Include(item => item.Submission).SingleAsync(item => item.Id == jobId);
        job.Status.Should().Be("completed");
        job.LockedUntil.Should().BeNull();
        job.CompletedAt.Should().NotBeNull();
        job.Submission.GradingStatus.Should().Be("ai_graded");
        (await verify.AiEvaluations.CountAsync(item => item.ExamSubmissionId == job.SubmissionId)).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfficialDlqReplay_PersistsResetAndResolutionAtomically_OrRollsBackAllChanges(bool forceDatabaseFailure)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var jobId = await SeedJobAsync(database, job =>
        {
            job.Status = "failed";
            job.Submission.GradingStatus = "failed";
            job.AttemptCount = 4;
            job.NextAttemptAt = DateTime.UtcNow.AddMinutes(10);
            job.LockedUntil = DateTime.UtcNow.AddMinutes(10);
            job.LastError = "Transient provider failure";
        });
        Guid dlqId;
        Guid officerId;
        await using (var seed = database.CreateContext())
        {
            var job = await seed.GradingJobs.SingleAsync();
            var officer = new User
            {
                Email = $"{Guid.NewGuid():N}@officer.example.test", FullName = "Integration Officer",
                Role = "examination_officer"
            };
            var dlq = new DeadLetterQueue
            {
                TaskType = "OfficialExamGrading", FlowType = "OFFICIAL_EXAM",
                ManagedByRole = "examination_officer", CorrelationId = job.Id,
                PayloadJson = JsonSerializer.Serialize(new OfficialExamGradingTask(job.Id, job.SubmissionId)),
                ErrorMessage = "Transient provider failure", Status = "pending", RetryCount = 4,
                NextRetryAt = DateTime.UtcNow.AddMinutes(10)
            };
            seed.AddRange(officer, dlq);
            await seed.SaveChangesAsync();
            dlqId = dlq.Id;
            officerId = officer.Id;
        }

        if (forceDatabaseFailure)
        {
            // Extra restrictive test-only constraint injects an actual PostgreSQL
            // write failure. It never replaces/removes production constraints.
            await database.ExecuteAsync("ALTER TABLE grading_jobs ADD CONSTRAINT ck_it_replay_failure CHECK (status <> 'pending')");
        }

        await using (var replay = database.CreateContext())
        {
            var handler = new ReplayDlqTaskCommandHandler(replay, new UnexpectedPracticeQueue());
            var command = new ReplayDlqTaskCommand(dlqId, officerId, "examination_officer");
            if (forceDatabaseFailure)
            {
                var action = async () => await handler.Handle(command, CancellationToken.None);
                var error = await action.Should().ThrowAsync<DbUpdateException>();
                error.Which.InnerException.Should().BeOfType<PostgresException>()
                    .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            }
            else
            {
                var result = await handler.Handle(command, CancellationToken.None);
                result.IsSuccess.Should().BeTrue(result.Error);
            }
        }

        await using var verify = database.CreateContext();
        var persistedJob = await verify.GradingJobs.Include(job => job.Submission).SingleAsync(job => job.Id == jobId);
        var persistedDlq = await verify.DeadLetterQueues.SingleAsync(item => item.Id == dlqId);
        persistedJob.AttemptCount.Should().Be(4, "manual replay does not erase prior attempt history");
        if (forceDatabaseFailure)
        {
            persistedJob.Status.Should().Be("failed");
            persistedJob.Submission.GradingStatus.Should().Be("failed");
            persistedJob.LockedUntil.Should().NotBeNull();
            persistedDlq.Status.Should().Be("pending");
            persistedDlq.ResolvedAt.Should().BeNull();
            persistedDlq.ResolvedByUserId.Should().BeNull();
            persistedDlq.NextRetryAt.Should().NotBeNull();
        }
        else
        {
            persistedJob.Status.Should().Be("pending");
            persistedJob.Submission.GradingStatus.Should().Be("pending");
            persistedJob.LockedUntil.Should().BeNull();
            persistedJob.NextAttemptAt.Should().NotBeNull().And.BeOnOrBefore(DateTime.UtcNow);
            persistedDlq.Status.Should().Be("resolved");
            persistedDlq.ResolvedAt.Should().NotBeNull();
            persistedDlq.ResolvedByUserId.Should().Be(officerId);
            persistedDlq.NextRetryAt.Should().BeNull();
        }
    }

    private static async Task<Guid> SeedJobAsync(TestDatabase database, Action<GradingJob>? configure = null)
    {
        await using var context = database.CreateContext();
        var official = await TestData.OfficialAsync(context);
        context.RubricCriteria.Add(new RubricCriterion
        {
            RubricId = official.Academic.Rubric.Id,
            CriterionName = "Technical correctness",
            MaxScore = 10m,
            Weight = 1m,
            OrderIndex = 1
        });
        var submission = new ExamQuestionSubmission
        {
            TicketId = official.Ticket.Id, ExamQuestionId = official.Question.Id,
            TranscriptWhisper = "A database transaction commits atomically.", GradingStatus = "pending"
        };
        var job = new GradingJob { Submission = submission };
        configure?.Invoke(job);
        context.Add(job);
        await context.SaveChangesAsync();
        return job.Id;
    }

    private static ServiceProvider CreateServices(
        TestDatabase database,
        DbCommandInterceptor? interceptor = null,
        IAiGradingService? gradingService = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => interceptor is null ? database.CreateContext() : new OralExamDbContext(
            new DbContextOptionsBuilder<OralExamDbContext>().UseNpgsql(database.ConnectionString)
                .AddInterceptors(new OneWayLockInterceptor(), interceptor).Options));
        if (gradingService is not null)
        {
            services.AddSingleton(gradingService);
        }
        return services.BuildServiceProvider();
    }

    private static OfficialExamGradingDispatcher CreateDispatcher(ServiceProvider services, IOfficialExamGradingQueue queue) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), queue, NullLogger<OfficialExamGradingDispatcher>.Instance);

    private static Task DispatchAsync(OfficialExamGradingDispatcher dispatcher, CancellationToken token) =>
        (Task)(typeof(OfficialExamGradingDispatcher)
            .GetMethod("DispatchBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dispatcher, [token]) ?? throw new InvalidOperationException("Dispatcher returned no Task."));

    private static Task ProcessAsync(
        OfficialExamGradingWorker worker,
        OfficialExamGradingTask task,
        CancellationToken token) =>
        (Task)(typeof(OfficialExamGradingWorker)
            .GetMethod("ProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(worker, [task, token]) ?? throw new InvalidOperationException("Worker returned no Task."));

    private sealed class ClaimBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothClaiming = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public ConcurrentDictionary<int, byte> ConnectionIds { get; } = new();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("grading_jobs", StringComparison.OrdinalIgnoreCase))
            {
                ConnectionIds.TryAdd(((NpgsqlConnection)command.Connection!).ProcessID, 0);
                if (Interlocked.Increment(ref _arrivals) == 2) _bothClaiming.TrySetResult();
                await _bothClaiming.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }

    private sealed class RecordingOfficialQueue : IOfficialExamGradingQueue
    {
        public ConcurrentQueue<OfficialExamGradingTask> Tasks { get; } = new();
        public ValueTask EnqueueAsync(OfficialExamGradingTask task, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Tasks.Enqueue(task);
            return ValueTask.CompletedTask;
        }
        public IAsyncEnumerable<OfficialExamGradingTask> ReadAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("These tests inspect dispatch; they do not run the external AI worker.");
    }

    private sealed class UnexpectedPracticeQueue : IGradingQueueChannel
    {
        public ValueTask EnqueueAsync(GradingTask task, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Official DLQ replay must use the durable job, not the practice queue.");
        public IAsyncEnumerable<GradingTask> ReadAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SuccessfulAiGradingService : IAiGradingService
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        public Task<AiGradingResult> GradeAnswerAsync(
            AiGradingRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(new AiGradingResult
            {
                Score = 8m,
                ConfidenceScore = 0.95m,
                Feedback = "Recovered worker completed grading.",
                CotTrace = "integration-test",
                Details = request.Criteria.Select(criterion => new GradingDetailDto
                {
                    CriterionId = criterion.Id,
                    Score = criterion.MaxScore * 0.8m,
                    Comment = "Recovered worker criterion result."
                }).ToList()
            });
        }
    }
}
