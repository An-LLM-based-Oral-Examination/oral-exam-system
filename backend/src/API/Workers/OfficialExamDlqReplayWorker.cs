using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.API.Workers;

/// <summary>
/// Converts due official-exam and practice DLQ entries back into pending jobs/tasks.
/// </summary>
public sealed class OfficialExamDlqReplayWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfficialExamDlqReplayWorker> _logger;
    private readonly IGradingQueueChannel? _gradingQueueChannel;

    public OfficialExamDlqReplayWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OfficialExamDlqReplayWorker> logger,
        IGradingQueueChannel? gradingQueueChannel = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _gradingQueueChannel = gradingQueueChannel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ReplayDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to replay official-exam DLQ entries.");
            }
        }
    }

    public async Task ReplayDueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var now = DateTime.UtcNow;
        var entries = await context.DeadLetterQueues
            .Where(entry => (entry.TaskType == "OfficialExamGrading" || entry.TaskType == "PracticeGrading") &&
                entry.Status == "pending" && entry.NextRetryAt != null && entry.NextRetryAt <= now)
            .OrderBy(entry => entry.NextRetryAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var entry in entries)
        {
            if (string.Equals(entry.TaskType, "PracticeGrading", StringComparison.OrdinalIgnoreCase))
            {
                GradingTask? practiceTask;
                try
                {
                    practiceTask = JsonSerializer.Deserialize<GradingTask>(
                        entry.PayloadJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException exception)
                {
                    entry.Status = "abandoned";
                    entry.LastAttemptAt = now;
                    entry.ErrorMessage = $"Invalid DLQ payload: {exception.Message}";
                    continue;
                }

                if (practiceTask is null)
                {
                    entry.Status = "abandoned";
                    entry.LastAttemptAt = now;
                    entry.ErrorMessage = "Invalid DLQ payload: Deserialized task is null.";
                    continue;
                }

                var answer = await context.PracticeAnswers
                    .FirstOrDefaultAsync(a => a.Id == practiceTask.AnswerId, cancellationToken);
                if (answer == null || answer.Status == "graded")
                {
                    entry.Status = answer?.Status == "graded" ? "resolved" : "abandoned";
                    entry.ResolvedAt = now;
                    entry.LastAttemptAt = now;
                    continue;
                }

                var channel = _gradingQueueChannel ?? scope.ServiceProvider.GetService<IGradingQueueChannel>();
                if (channel != null)
                {
                    await channel.EnqueueAsync(practiceTask, cancellationToken);
                }

                answer.Status = "pending";
                entry.Status = "resolved";
                entry.ResolvedAt = now;
                entry.LastAttemptAt = now;
                continue;
            }

            OfficialExamGradingTask? task;
            try
            {
                task = JsonSerializer.Deserialize<OfficialExamGradingTask>(entry.PayloadJson);
            }
            catch (JsonException exception)
            {
                entry.Status = "abandoned";
                entry.LastAttemptAt = now;
                entry.ErrorMessage = $"Invalid DLQ payload: {exception.Message}";
                continue;
            }

            var job = task is null
                ? null
                : await context.GradingJobs.FirstOrDefaultAsync(item => item.Id == task.JobId, cancellationToken);
            if (job is null || job.Status == "completed")
            {
                entry.Status = job?.Status == "completed" ? "resolved" : "abandoned";
                entry.ResolvedAt = now;
                entry.LastAttemptAt = now;
                continue;
            }

            job.Status = "pending";
            job.NextAttemptAt = now;
            job.LockedUntil = null;
            job.UpdatedAt = now;
            var submission = await context.ExamQuestionSubmissions
                .FirstOrDefaultAsync(item => item.Id == job.SubmissionId, cancellationToken);
            if (submission is not null && submission.GradingStatus == "failed")
            {
                submission.GradingStatus = "pending";
            }
            entry.Status = "resolved";
            entry.ResolvedAt = now;
            entry.LastAttemptAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
