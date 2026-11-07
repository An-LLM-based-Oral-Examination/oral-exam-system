using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
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
        try
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown khi Host dừng
        }
    }

    public async Task ReplayDueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var now = DateTime.UtcNow;
        var entries = await context.DeadLetterQueues
            .Where(entry => (entry.TaskType == "OfficialExamGrading" ||
                             entry.TaskType == "PracticeGrading" ||
                             entry.TaskType == "MockExamGrading" ||
                             entry.TaskType == "InClassGrading") &&
                entry.Status == "pending" && entry.NextRetryAt != null && entry.NextRetryAt <= now)
            .OrderBy(entry => entry.NextRetryAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var dueMockSessionIds = new HashSet<Guid>();

        foreach (var entry in entries)
        {
            if (string.Equals(entry.TaskType, "InClassGrading", StringComparison.OrdinalIgnoreCase))
            {
                if (entry.RetryCount >= 9)
                {
                    entry.Status = "abandoned";
                    entry.NextRetryAt = null;
                    entry.LastAttemptAt = now;
                    continue;
                }

                GradingTask? inClassTask;
                try
                {
                    inClassTask = JsonSerializer.Deserialize<GradingTask>(
                        entry.PayloadJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException exception)
                {
                    entry.Status = "abandoned";
                    entry.NextRetryAt = null;
                    entry.LastAttemptAt = now;
                    entry.ErrorMessage = $"Invalid DLQ payload: {exception.Message}";
                    continue;
                }

                var submissionId = inClassTask?.InClassSubmissionId ?? entry.CorrelationId ?? Guid.Empty;
                if (inClassTask is null || submissionId == Guid.Empty)
                {
                    entry.Status = "abandoned";
                    entry.NextRetryAt = null;
                    entry.LastAttemptAt = now;
                    entry.ErrorMessage = "Invalid DLQ payload: Missing InClassSubmissionId.";
                    continue;
                }

                var inClassSubmission = await context.InClassExamSubmissions
                    .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);
                var isAlreadyGraded = inClassSubmission != null &&
                    (inClassSubmission.AiScore.HasValue ||
                     string.Equals(inClassSubmission.Status, "AI_GRADED", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(inClassSubmission.Status, "LECTURER_GRADED", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(inClassSubmission.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase));

                if (inClassSubmission == null || isAlreadyGraded)
                {
                    entry.Status = isAlreadyGraded ? "resolved" : "abandoned";
                    entry.ResolvedAt = now;
                    entry.NextRetryAt = null;
                    entry.LastAttemptAt = now;
                    continue;
                }

                var inClassChannel = _gradingQueueChannel ?? scope.ServiceProvider.GetService<IGradingQueueChannel>();
                if (inClassChannel != null)
                {
                    inClassTask.InClassSubmissionId = submissionId;
                    await inClassChannel.EnqueueAsync(inClassTask, cancellationToken);
                }

                entry.Status = "resolved";
                entry.ResolvedAt = now;
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                continue;
            }

            if (string.Equals(entry.TaskType, "PracticeGrading", StringComparison.OrdinalIgnoreCase))
            {
                if (entry.RetryCount >= 9)
                {
                    entry.Status = "abandoned";
                    entry.NextRetryAt = null;
                    entry.LastAttemptAt = now;
                    continue;
                }

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
                    entry.NextRetryAt = null;
                    entry.LastAttemptAt = now;
                    entry.ErrorMessage = $"Invalid DLQ payload: {exception.Message}";
                    continue;
                }

                if (practiceTask is null)
                {
                    entry.Status = "abandoned";
                    entry.NextRetryAt = null;
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
                    entry.NextRetryAt = null;
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
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                continue;
            }

            if (string.Equals(entry.TaskType, "MockExamGrading", StringComparison.OrdinalIgnoreCase))
            {
                if (entry.RetryCount >= 9)
                {
                    entry.Status = "abandoned";
                    entry.NextRetryAt = null;
                    entry.LastAttemptAt = now;
                    continue;
                }

                Guid sessionId = entry.CorrelationId ?? Guid.Empty;
                var session = sessionId != Guid.Empty
                    ? await context.MockExamSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
                    : null;

                var isSessionGraded = session != null &&
                    (string.Equals(session.Status, "graded", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(session.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                     session.TotalScore.HasValue);
                if (session == null || isSessionGraded)
                {
                    entry.Status = isSessionGraded ? "resolved" : "abandoned";
                    entry.ResolvedAt = now;
                    entry.NextRetryAt = null;
                    entry.LastAttemptAt = now;
                    continue;
                }

                session.Status = "submitted";
                entry.LastAttemptAt = now;
                entry.NextRetryAt = now.AddMinutes(5);
                dueMockSessionIds.Add(session.Id);
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
                entry.NextRetryAt = null;
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
                entry.NextRetryAt = null;
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
            entry.NextRetryAt = null;
            entry.LastAttemptAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        await RecoverInterruptedPracticeAndMockSessionsAsync(scope.ServiceProvider, context, now, dueMockSessionIds, cancellationToken);
    }

    private async Task RecoverInterruptedPracticeAndMockSessionsAsync(
        IServiceProvider serviceProvider,
        OralExamDbContext context,
        DateTime now,
        HashSet<Guid> dueMockSessionIds,
        CancellationToken cancellationToken)
    {
        // 1. Khôi phục các câu trả lời Luyện tập (MF-01) bị treo ở trạng thái "pending" quá 2 phút (do sập mạng / restart tiến trình)
        var channel = _gradingQueueChannel ?? serviceProvider.GetService<IGradingQueueChannel>();
        if (channel != null)
        {
            var stuckPracticeAnswers = await context.PracticeAnswers
                .AsNoTracking()
                .Include(a => a.Session)
                .Include(a => a.AiEvaluations)
                .Where(a => a.Status == "pending" && a.SubmittedAt <= now.AddMinutes(-2))
                .OrderBy(a => a.SubmittedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var stuckAnswer in stuckPracticeAnswers)
            {
                if (stuckAnswer.AiEvaluations != null && stuckAnswer.AiEvaluations.Any())
                {
                    continue;
                }

                await channel.EnqueueAsync(new GradingTask
                {
                    AnswerId = stuckAnswer.Id,
                    SessionId = stuckAnswer.SessionId,
                    QuestionId = stuckAnswer.QuestionId,
                    StudentId = stuckAnswer.StudentId,
                    AnswerText = stuckAnswer.AnswerText ?? string.Empty,
                    IsFollowUp = stuckAnswer.IsFollowUp,
                    ParentAnswerId = stuckAnswer.ParentAnswerId,
                    IsFullSession = string.Equals(stuckAnswer.Session?.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase)
                }, cancellationToken);
            }

            // 1b. Khôi phục các bài nộp Ca thi trên lớp (MF-05) bị treo ở trạng thái "SUBMITTED" quá 2 phút chưa có điểm AI
            var backoffInClassIds = await context.DeadLetterQueues
                .AsNoTracking()
                .Where(q => q.TaskType == "InClassGrading" &&
                            q.CorrelationId != null &&
                            (q.Status == "abandoned" ||
                             (q.Status == "pending" && q.NextRetryAt != null && q.NextRetryAt > now)))
                .Select(q => q.CorrelationId!.Value)
                .ToListAsync(cancellationToken);
            var excludedInClassSet = backoffInClassIds.ToHashSet();

            var stuckInClassSubmissions = await context.InClassExamSubmissions
                .AsNoTracking()
                .Where(s => s.Status == "SUBMITTED" &&
                            s.AiScore == null &&
                            s.SubmittedAt <= now.AddMinutes(-2) &&
                            !excludedInClassSet.Contains(s.Id))
                .OrderBy(s => s.SubmittedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var stuckInClass in stuckInClassSubmissions)
            {
                await channel.EnqueueAsync(new GradingTask
                {
                    AnswerId = stuckInClass.Id,
                    SessionId = stuckInClass.ShiftId,
                    QuestionId = stuckInClass.QuestionId,
                    StudentId = stuckInClass.StudentId,
                    AnswerText = stuckInClass.Transcript ?? string.Empty,
                    IsFollowUp = false,
                    IsFullSession = false,
                    InClassSubmissionId = stuckInClass.Id
                }, cancellationToken);
            }
        }

        // 2. Tự động chốt và chấm điểm các ca Thi thử (MF-02) bị mất mạng / tắt ngang quá giờ hoặc đang chờ chấm bù ("submitted" chưa có điểm)
        var aiGradingService = serviceProvider.GetService<IAiGradingService>();
        if (aiGradingService == null)
        {
            return;
        }

        var backoffMockSessionIds = await context.DeadLetterQueues
            .AsNoTracking()
            .Where(q => q.Status == "pending" &&
                        q.TaskType == "MockExamGrading" &&
                        q.CorrelationId != null &&
                        q.NextRetryAt != null &&
                        q.NextRetryAt > now)
            .Select(q => q.CorrelationId!.Value)
            .ToListAsync(cancellationToken);

        var excludedSessionIds = backoffMockSessionIds
            .Where(id => !dueMockSessionIds.Contains(id))
            .ToHashSet();

        var interruptedMockSessions = await context.MockExamSessions
            .AsNoTracking()
            .Where(s =>
                !excludedSessionIds.Contains(s.Id) &&
                ((s.Status == "in_progress" && now > s.ExpiresAt.AddSeconds(15)) ||
                 (s.Status == "submitted" && s.TotalScore == null)))
            .OrderBy(s => s.ExpiresAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var mockSession in interruptedMockSessions)
        {
            try
            {
                var submitHandler = new SubmitMockExamSessionCommandHandler(context, aiGradingService);
                await submitHandler.Handle(
                    new SubmitMockExamSessionCommand(mockSession.Id, mockSession.StudentId),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Deferred grading for interrupted MockExamSession {SessionId} failed transiently; will retry on next cycle.",
                    mockSession.Id);
            }
        }
    }
}
