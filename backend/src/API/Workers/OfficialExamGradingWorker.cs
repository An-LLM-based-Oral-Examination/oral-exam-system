using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.API.Workers;

public sealed class OfficialExamGradingWorker : BackgroundService
{
    private const decimal SuspiciousConfidenceThreshold = 0.70m;
    private const int MaxDeliveryAttempts = 5;
    private static readonly TimeSpan DlqReplayDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8)
    ];

    private readonly IOfficialExamGradingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfficialExamGradingWorker> _logger;

    public OfficialExamGradingWorker(
        IOfficialExamGradingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<OfficialExamGradingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var task in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(task, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Official grading failed for submission {SubmissionId}; scheduling DLQ replay.",
                    task.SubmissionId);
                await MarkFailedAsync(task, exception, CancellationToken.None);
            }
        }
    }

    internal async Task ProcessAsync(OfficialExamGradingTask task, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var gradingService = scope.ServiceProvider.GetRequiredService<IAiGradingService>();

        var job = await context.GradingJobs
            .FirstOrDefaultAsync(item => item.Id == task.JobId && item.SubmissionId == task.SubmissionId, cancellationToken)
            ?? throw new InvalidOperationException("The official grading job no longer exists.");

        if (job.Status == "completed")
        {
            return;
        }

        var submission = await context.ExamQuestionSubmissions
            .Include(item => item.ExamQuestion)
                .ThenInclude(question => question.Rubric)
                    .ThenInclude(rubric => rubric.Criteria)
            .FirstOrDefaultAsync(item => item.Id == task.SubmissionId, cancellationToken)
            ?? throw new InvalidOperationException("The official exam submission no longer exists.");

        var existingEvaluation = await context.AiEvaluations
            .AsNoTracking()
            .AnyAsync(item => item.ExamSubmissionId == submission.Id, cancellationToken);
        if (existingEvaluation || submission.GradingStatus == "ai_graded")
        {
            CompleteJob(job, DateTime.UtcNow);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(submission.TranscriptWhisper))
        {
            throw new InvalidOperationException("The submission has no transcript to grade.");
        }

        var rubric = submission.ExamQuestion.Rubric;
        var orderedCriteria = rubric.Criteria.OrderBy(item => item.OrderIndex).ToList();
        if (orderedCriteria.Count == 0)
        {
            throw new InvalidOperationException("The exam question has no rubric criteria.");
        }

        submission.GradingStatus = "transcribed";
        submission.GradingStartedAt ??= DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        var request = new AiGradingRequest
        {
            QuestionContent = submission.ExamQuestion.Content,
            SampleAnswer = submission.ExamQuestion.SampleAnswer ?? string.Empty,
            Transcript = submission.TranscriptWhisper,
            Criteria = orderedCriteria.Select(item => new AiGradingCriterion
            {
                Id = item.Id.ToString(),
                Name = item.CriterionName,
                Description = item.Description ?? string.Empty,
                MaxScore = item.MaxScore,
                Weight = item.Weight
            }).ToList()
        };

        var result = await GradeWithTransientRetryAsync(gradingService, request, cancellationToken);
        var rawDetails = result.Details ?? [];
        var details = BuildDetails(orderedCriteria, rawDetails);
        var score = rawDetails.Count > 0
            ? Math.Clamp(Math.Round(details.Sum(item => item.Score), 2, MidpointRounding.AwayFromZero), 0m, 10m)
            : Math.Clamp(Math.Round(result.Score, 2, MidpointRounding.AwayFromZero), 0m, 10m);
        var suspicious = result.ConfidenceScore < SuspiciousConfidenceThreshold;
        var now = DateTime.UtcNow;

        context.AiEvaluations.Add(new AiEvaluation
        {
            AnswerType = "exam",
            ExamSubmissionId = submission.Id,
            TotalScore = score,
            ConfidenceScore = result.ConfidenceScore,
            Feedback = result.Feedback,
            CotTrace = result.CotTrace,
            EvaluatedAt = now,
            Details = details
        });

        submission.AiScore = score;
        submission.GradingStatus = "ai_graded";
        submission.IsSuspicious = suspicious;
        submission.SuspiciousReason = suspicious
            ? (!string.IsNullOrWhiteSpace(result.SuspiciousReason)
                ? result.SuspiciousReason
                : $"AI confidence {result.ConfidenceScore:0.00} is below {SuspiciousConfidenceThreshold:0.00}.")
            : null;
        CompleteJob(job, now);

        var hasOtherIncompleteSubmissions = await context.ExamQuestionSubmissions
            .AsNoTracking()
            .AnyAsync(item => item.TicketId == submission.TicketId &&
                item.Id != submission.Id && item.GradingStatus != "ai_graded", cancellationToken);
        if (!hasOtherIncompleteSubmissions)
        {
            var ticket = await context.StudentExamTickets
                .FirstOrDefaultAsync(item => item.Id == submission.TicketId, cancellationToken);
            if (ticket?.Status == "SUBMITTED")
            {
                ticket.Status = "AI_GRADED";
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<AiGradingResult> GradeWithTransientRetryAsync(
        IAiGradingService service,
        AiGradingRequest request,
        CancellationToken cancellationToken)
    {
        for (var retry = 0; ; retry++)
        {
            try
            {
                return await service.GradeAnswerAsync(request, cancellationToken);
            }
            catch (Exception exception) when (IsTransient(exception) && retry < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[retry], cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception exception) => exception switch
    {
        TimeoutException => true,
        HttpRequestException httpException when httpException.StatusCode is null => true,
        HttpRequestException httpException when httpException.StatusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests => true,
        HttpRequestException httpException when (int?)httpException.StatusCode >= 500 => true,
        _ => false
    };

    private async Task MarkFailedAsync(
        OfficialExamGradingTask task,
        Exception exception,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var job = await context.GradingJobs.FirstOrDefaultAsync(item => item.Id == task.JobId, cancellationToken);
        if (job is null || job.Status == "completed")
        {
            return;
        }

        var now = DateTime.UtcNow;
        job.Status = "failed";
        job.LockedUntil = null;
        job.NextAttemptAt = now.Add(DlqReplayDelay);
        job.LastError = exception.Message;
        job.UpdatedAt = now;

        var submission = await context.ExamQuestionSubmissions
            .FirstOrDefaultAsync(item => item.Id == task.SubmissionId, cancellationToken);
        if (submission is not null)
        {
            submission.GradingStatus = "failed";
        }

        var hasPendingDlq = await context.DeadLetterQueues.AnyAsync(entry =>
            entry.TaskType == "OfficialExamGrading" && entry.CorrelationId == task.JobId &&
            entry.Status == "pending", cancellationToken);
        if (!hasPendingDlq)
        {
            var canReplay = job.AttemptCount < MaxDeliveryAttempts;
            context.DeadLetterQueues.Add(new DeadLetterQueue
            {
                TaskType = "OfficialExamGrading",
                CorrelationId = task.JobId,
                PayloadJson = JsonSerializer.Serialize(task),
                ErrorMessage = exception.Message,
                RetryCount = job.AttemptCount,
                Status = canReplay ? "pending" : "abandoned",
                CreatedAt = now,
                NextRetryAt = canReplay ? now.Add(DlqReplayDelay) : null,
                LastAttemptAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void CompleteJob(GradingJob job, DateTime completedAt)
    {
        job.Status = "completed";
        job.LockedUntil = null;
        job.NextAttemptAt = null;
        job.LastError = null;
        job.UpdatedAt = completedAt;
        job.CompletedAt = completedAt;
    }

    private static List<AiEvaluationDetail> BuildDetails(
        IReadOnlyList<RubricCriterion> criteria,
        IReadOnlyList<GradingDetailDto> rawDetails)
    {
        var details = new List<AiEvaluationDetail>(criteria.Count);
        for (var index = 0; index < criteria.Count; index++)
        {
            var criterion = criteria[index];
            var raw = rawDetails.FirstOrDefault(item =>
                    string.Equals(item.CriterionId?.Trim(), criterion.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                ?? rawDetails.FirstOrDefault(item =>
                    string.Equals(item.CriterionId?.Trim(), criterion.CriterionName.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? (index < rawDetails.Count ? rawDetails[index] : null);
            var score = raw is null
                ? 0m
                : Math.Clamp(Math.Round(raw.Score, 2, MidpointRounding.AwayFromZero), 0m, criterion.MaxScore);
            details.Add(new AiEvaluationDetail
            {
                CriterionId = criterion.Id,
                Score = score,
                Comment = !string.IsNullOrWhiteSpace(raw?.Comment)
                    ? raw.Comment
                    : $"Score {score:0.00}/{criterion.MaxScore:0.00} for {criterion.CriterionName}."
            });
        }

        return details;
    }
}
