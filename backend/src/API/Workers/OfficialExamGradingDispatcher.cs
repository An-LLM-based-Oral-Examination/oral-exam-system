using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.API.Workers;

/// <summary>
/// Claims durable jobs in small batches and feeds the bounded in-memory channel.
/// Expired leases are claimable again, so a process restart cannot lose submissions.
/// </summary>
public sealed class OfficialExamGradingDispatcher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOfficialExamGradingQueue _queue;
    private readonly ILogger<OfficialExamGradingDispatcher> _logger;

    public OfficialExamGradingDispatcher(
        IServiceScopeFactory scopeFactory,
        IOfficialExamGradingQueue queue,
        ILogger<OfficialExamGradingDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to dispatch official-exam grading jobs.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown khi Host dừng
        }
    }

    internal async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var now = DateTime.UtcNow;

        var candidates = await context.GradingJobs
            .AsNoTracking()
            .Where(job =>
                (job.Status == "pending" || job.Status == "processing") &&
                (job.NextAttemptAt == null || job.NextAttemptAt <= now) &&
                (job.LockedUntil == null || job.LockedUntil <= now))
            .OrderBy(job => job.CreatedAt)
            .Select(job => new { job.Id, job.SubmissionId })
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var lockedUntil = now.Add(LeaseDuration);
            int claimed;
            if (context.Database.IsRelational())
            {
                claimed = await context.GradingJobs
                    .Where(job => job.Id == candidate.Id &&
                        (job.Status == "pending" || job.Status == "processing") &&
                        (job.NextAttemptAt == null || job.NextAttemptAt <= now) &&
                        (job.LockedUntil == null || job.LockedUntil <= now))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(job => job.Status, "processing")
                        .SetProperty(job => job.LockedUntil, lockedUntil)
                        .SetProperty(job => job.UpdatedAt, now)
                        .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1), cancellationToken);
            }
            else
            {
                var job = await context.GradingJobs.FindAsync([candidate.Id], cancellationToken);
                claimed = job is null ? 0 : 1;
                if (job is not null)
                {
                    job.Status = "processing";
                    job.LockedUntil = lockedUntil;
                    job.UpdatedAt = now;
                    job.AttemptCount++;
                    await context.SaveChangesAsync(cancellationToken);
                }
            }

            if (claimed == 1)
            {
                await _queue.EnqueueAsync(
                    new OfficialExamGradingTask(candidate.Id, candidate.SubmissionId),
                    cancellationToken);
            }
        }
    }
}
