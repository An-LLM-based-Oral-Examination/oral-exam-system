using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.API.Workers;

/// <summary>
/// Converts due official-exam and practice DLQ entries back into pending jobs/tasks.
/// </summary>
public sealed class OfficialExamDlqReplayWorker : BackgroundService
{
    private const int DefaultPollIntervalSeconds = 300;
    private const int DefaultRecoveryPollIntervalSeconds = 30;
    private const int DefaultStuckRecoveryMinutes = 3;
    private const int RecoveryBatchSize = 100;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _recoveryPollInterval;
    private readonly TimeSpan _stuckRecoveryWindow;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfficialExamDlqReplayWorker> _logger;
    private readonly IGradingQueueChannel? _gradingQueueChannel;

    public TimeSpan PollInterval => _pollInterval;
    public TimeSpan RecoveryPollInterval => _recoveryPollInterval;
    public TimeSpan StuckRecoveryWindow => _stuckRecoveryWindow;

    public OfficialExamDlqReplayWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OfficialExamDlqReplayWorker> logger,
        IGradingQueueChannel? gradingQueueChannel = null,
        IConfiguration? configuration = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _gradingQueueChannel = gradingQueueChannel;

        var configuredSeconds = configuration?["DlqReplay:PollIntervalSeconds"]
            ?? configuration?["DlqReplay__PollIntervalSeconds"]
            ?? Environment.GetEnvironmentVariable("DLQ_REPLAY_POLL_INTERVAL_SECONDS");
        var pollSeconds = int.TryParse(configuredSeconds, out var parsedSeconds)
            ? Math.Clamp(parsedSeconds, 5, 3600)
            : DefaultPollIntervalSeconds;
        _pollInterval = TimeSpan.FromSeconds(pollSeconds);

        var configuredRecoverySeconds = configuration?["DlqReplay:RecoveryPollIntervalSeconds"]
            ?? configuration?["DlqReplay__RecoveryPollIntervalSeconds"]
            ?? Environment.GetEnvironmentVariable("DLQ_REPLAY_RECOVERY_POLL_INTERVAL_SECONDS");
        var recoverySeconds = int.TryParse(configuredRecoverySeconds, out var parsedRecoverySeconds)
            ? Math.Clamp(parsedRecoverySeconds, 5, 300)
            : DefaultRecoveryPollIntervalSeconds;
        _recoveryPollInterval = TimeSpan.FromSeconds(Math.Min(recoverySeconds, pollSeconds));

        var configuredStuckMinutes = configuration?["DlqReplay:StuckRecoveryMinutes"]
            ?? configuration?["DlqReplay__StuckRecoveryMinutes"]
            ?? Environment.GetEnvironmentVariable("DLQ_REPLAY_STUCK_RECOVERY_MINUTES");
        var stuckMinutes = int.TryParse(configuredStuckMinutes, out var parsedStuckMinutes)
            ? Math.Clamp(parsedStuckMinutes, 1, 30)
            : DefaultStuckRecoveryMinutes;
        _stuckRecoveryWindow = TimeSpan.FromMinutes(stuckMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Quét cứu hộ nhanh các bài MF-01/MF-05 bị gián đoạn ngay sau khi API khởi động lại (không phải chờ đủ 5 phút).
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            try
            {
                await RecoverInterruptedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to run initial interrupted-queue recovery sweep.");
            }

            var nextDlqReplayUtc = DateTime.UtcNow.Add(_pollInterval);
            using var timer = new PeriodicTimer(_recoveryPollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    if (DateTime.UtcNow >= nextDlqReplayUtc)
                    {
                        await ReplayDueAsync(stoppingToken);
                        nextDlqReplayUtc = DateTime.UtcNow.Add(_pollInterval);
                    }
                    else
                    {
                        await RecoverInterruptedAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to replay DLQ or recover interrupted grading items.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown khi Host dừng
        }
    }

    public async Task RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var recoveryScope = _scopeFactory.CreateAsyncScope();
        var recoveryContext = recoveryScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        await RecoverInterruptedPracticeAndMockSessionsAsync(
            recoveryScope.ServiceProvider,
            recoveryContext,
            now,
            [],
            cancellationToken);
    }

    public async Task ReplayDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        List<Guid> entryIds;
        await using (var listScope = _scopeFactory.CreateAsyncScope())
        {
            var listContext = listScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            entryIds = await listContext.DeadLetterQueues
                .AsNoTracking()
                .Where(entry => (entry.TaskType == "OfficialExamGrading" ||
                                 entry.TaskType == "PracticeGrading" ||
                                 entry.TaskType == "MockExamGrading" ||
                                 entry.TaskType == "InClassGrading") &&
                    entry.Status == "pending" && entry.NextRetryAt != null && entry.NextRetryAt <= now)
                .OrderBy(entry => entry.NextRetryAt)
                .Select(entry => entry.Id)
                .Take(50)
                .ToListAsync(cancellationToken);
        }

        var dueMockSessionIds = new HashSet<Guid>();

        foreach (var entryId in entryIds)
        {
            await using var itemScope = _scopeFactory.CreateAsyncScope();
            try
            {
                await ReplaySingleEntryAsync(itemScope.ServiceProvider, entryId, now, dueMockSessionIds, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi cô lập khi replay bản ghi DLQ {DlqEntryId}; tiếp tục xử lý các bản ghi DLQ còn lại.",
                    entryId);
            }
        }

        await using var recoveryScope = _scopeFactory.CreateAsyncScope();
        var recoveryContext = recoveryScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        await RecoverInterruptedPracticeAndMockSessionsAsync(
            recoveryScope.ServiceProvider,
            recoveryContext,
            now,
            dueMockSessionIds,
            cancellationToken);
    }

    private async Task ReplaySingleEntryAsync(
        IServiceProvider serviceProvider,
        Guid entryId,
        DateTime now,
        HashSet<Guid> dueMockSessionIds,
        CancellationToken cancellationToken)
    {
        var context = serviceProvider.GetRequiredService<OralExamDbContext>();
        var entry = await context.DeadLetterQueues
            .FirstOrDefaultAsync(item => item.Id == entryId && item.Status == "pending", cancellationToken);
        if (entry is null)
        {
            return;
        }

        if (string.Equals(entry.TaskType, "InClassGrading", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.RetryCount >= 9)
            {
                entry.Status = "abandoned";
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                await context.SaveChangesAsync(cancellationToken);
                return;
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
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            var submissionId = inClassTask?.InClassSubmissionId ?? entry.CorrelationId ?? Guid.Empty;
            if (inClassTask is null || submissionId == Guid.Empty)
            {
                entry.Status = "abandoned";
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                entry.ErrorMessage = "Invalid DLQ payload: Missing InClassSubmissionId.";
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            var inClassSubmission = await context.InClassExamSubmissions
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);
            var isShiftLocked = inClassSubmission?.Shift?.IsLocked == true ||
                string.Equals(inClassSubmission?.Shift?.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase);
            var isAlreadyGraded = inClassSubmission != null &&
                (inClassSubmission.AiScore.HasValue ||
                 isShiftLocked ||
                 string.Equals(inClassSubmission.Status, "AI_GRADED", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(inClassSubmission.Status, "AUDITED", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(inClassSubmission.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase));

            if (inClassSubmission == null || isAlreadyGraded)
            {
                entry.Status = isAlreadyGraded ? "resolved" : "abandoned";
                entry.ResolvedAt = now;
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            var inClassChannel = _gradingQueueChannel ?? serviceProvider.GetService<IGradingQueueChannel>();
            if (inClassChannel is null)
            {
                return;
            }

            // Giữ bản ghi pending và dời lần retry trước khi giao cho queue trong RAM.
            // GradingQueueWorker sẽ tự đánh dấu resolved ngay khi chấm điểm thành công để tránh mất job nếu tiến trình dừng đột ngột.
            entry.Status = "pending";
            entry.ResolvedAt = null;
            entry.NextRetryAt = now.AddMinutes(15);
            entry.LastAttemptAt = now;
            entry.CorrelationId ??= submissionId;
            inClassSubmission.ProcessingStartedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            inClassTask.AnswerId = inClassSubmission.Id;
            inClassTask.SessionId = inClassSubmission.ShiftId;
            inClassTask.QuestionId = inClassSubmission.QuestionId;
            inClassTask.StudentId = inClassSubmission.StudentId;
            inClassTask.AnswerText = inClassSubmission.Transcript ?? string.Empty;
            inClassTask.IsFollowUp = inClassSubmission.IsFollowUp;
            inClassTask.ParentAnswerId = inClassSubmission.ParentSubmissionId;
            inClassTask.InClassSubmissionId = submissionId;
            await inClassChannel.EnqueueAsync(inClassTask, cancellationToken);
            return;
        }

        if (string.Equals(entry.TaskType, "PracticeGrading", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.RetryCount >= 9)
            {
                entry.Status = "abandoned";
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                await context.SaveChangesAsync(cancellationToken);
                return;
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
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            if (practiceTask is null)
            {
                entry.Status = "abandoned";
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                entry.ErrorMessage = "Invalid DLQ payload: Deserialized task is null.";
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            var answer = await context.PracticeAnswers
                .FirstOrDefaultAsync(a => a.Id == practiceTask.AnswerId, cancellationToken);
            if (answer == null || answer.Status == "graded")
            {
                entry.Status = answer?.Status == "graded" ? "resolved" : "abandoned";
                entry.ResolvedAt = now;
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            var channel = _gradingQueueChannel ?? serviceProvider.GetService<IGradingQueueChannel>();
            if (channel is null)
            {
                return;
            }

            // Giữ bản ghi pending và dời NextRetryAt trước khi đẩy vào RAM channel.
            // GradingQueueWorker sẽ tự đánh dấu resolved ngay khi chấm điểm thành công.
            entry.Status = "pending";
            entry.ResolvedAt = null;
            entry.NextRetryAt = now.AddMinutes(15);
            entry.LastAttemptAt = now;
            if (!entry.CorrelationId.HasValue && practiceTask.AnswerId != Guid.Empty)
            {
                entry.CorrelationId = practiceTask.AnswerId;
            }
            answer.Status = "pending";
            answer.ProcessingStartedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            await channel.EnqueueAsync(practiceTask, cancellationToken);
            return;
        }

        if (string.Equals(entry.TaskType, "MockExamGrading", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.RetryCount >= 9)
            {
                entry.Status = "abandoned";
                entry.NextRetryAt = null;
                entry.LastAttemptAt = now;
                await context.SaveChangesAsync(cancellationToken);
                return;
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
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            session.Status = "submitted";
            entry.LastAttemptAt = now;
            entry.NextRetryAt = now.AddMinutes(5);
            await context.SaveChangesAsync(cancellationToken);
            dueMockSessionIds.Add(session.Id);
            return;
        }

        if (entry.RetryCount >= 9)
        {
            entry.Status = "abandoned";
            entry.NextRetryAt = null;
            entry.LastAttemptAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        OfficialExamGradingTask? task;
        try
        {
            task = JsonSerializer.Deserialize<OfficialExamGradingTask>(
                entry.PayloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            entry.Status = "abandoned";
            entry.NextRetryAt = null;
            entry.LastAttemptAt = now;
            entry.ErrorMessage = $"Invalid DLQ payload: {exception.Message}";
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var job = task is null
            ? null
            : await context.GradingJobs.FirstOrDefaultAsync(
                item => item.Id == task.JobId || (task.SubmissionId != Guid.Empty && item.SubmissionId == task.SubmissionId),
                cancellationToken);
        if (job is null || job.Status == "completed")
        {
            entry.Status = job?.Status == "completed" ? "resolved" : "abandoned";
            entry.ResolvedAt = now;
            entry.NextRetryAt = null;
            entry.LastAttemptAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var submission = await context.ExamQuestionSubmissions
            .Include(item => item.Ticket)
            .FirstOrDefaultAsync(item => item.Id == job.SubmissionId, cancellationToken)
            ?? await context.ExamQuestionSubmissions
                .FirstOrDefaultAsync(item => item.Id == job.SubmissionId, cancellationToken);

        var isTicketLockedOrPublished = submission?.Ticket?.IsLocked == true ||
            string.Equals(submission?.Ticket?.Status, ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(submission?.Ticket?.Status, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase);

        if (isTicketLockedOrPublished)
        {
            entry.Status = "resolved";
            entry.ResolvedAt = now;
            entry.NextRetryAt = null;
            entry.LastAttemptAt = now;
            entry.ResolutionNotes = "Bỏ qua replay do vé thi đã khóa một chiều hoặc đã công bố điểm.";
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var isOfficialSubmissionAlreadyGraded = submission is not null &&
            (string.Equals(submission.GradingStatus, "ai_graded", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(submission.GradingStatus, "audited", StringComparison.OrdinalIgnoreCase));

        if (isOfficialSubmissionAlreadyGraded)
        {
            entry.Status = "resolved";
            entry.ResolvedAt = now;
            entry.NextRetryAt = null;
            entry.LastAttemptAt = now;
            entry.ResolutionNotes ??= "Auto-resolved because official exam submission was already graded or audited.";
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        job.Status = "pending";
        job.NextAttemptAt = now;
        job.LockedUntil = null;
        job.UpdatedAt = now;
        if (submission is not null && submission.GradingStatus == "failed")
        {
            var canResumeFromTranscribed = task?.ResumeFromTranscribed == true
                && !submission.IsSuspicious
                && !string.IsNullOrWhiteSpace(submission.TranscriptWhisper);
            submission.GradingStatus = canResumeFromTranscribed ? "transcribed" : "pending";
        }
        entry.Status = "resolved";
        entry.ResolvedAt = now;
        entry.NextRetryAt = null;
        entry.LastAttemptAt = now;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task RecoverInterruptedPracticeAndMockSessionsAsync(
        IServiceProvider serviceProvider,
        OralExamDbContext context,
        DateTime now,
        HashSet<Guid> dueMockSessionIds,
        CancellationToken cancellationToken)
    {
        // Cửa sổ cứu hộ mặc định 3 phút (cấu hình được qua DlqReplay:StuckRecoveryMinutes) kết hợp lô 100 bài/nhịp 30s
        // giúp phục hồi nhanh toàn bộ 120 SV MF-01/MF-05 sau sự cố restart mà không chấm trùng các bài đang xử lý.
        var stuckCutoff = now - _stuckRecoveryWindow;
        var channel = _gradingQueueChannel ?? serviceProvider.GetService<IGradingQueueChannel>();
        if (channel != null)
        {
            var backoffPracticeIds = await context.DeadLetterQueues
                .AsNoTracking()
                .Where(q => q.TaskType == "PracticeGrading" &&
                            q.CorrelationId != null &&
                            (q.Status == "abandoned" ||
                             (q.Status == "pending" && q.NextRetryAt != null && q.NextRetryAt > now)))
                .Select(q => q.CorrelationId!.Value)
                .ToListAsync(cancellationToken);
            var excludedPracticeSet = backoffPracticeIds.ToHashSet();

            var stuckPracticeAnswers = await context.PracticeAnswers
                .Include(a => a.Session)
                .Include(a => a.AiEvaluations)
                .Where(a => a.Status == "pending" &&
                            a.SubmittedAt <= stuckCutoff &&
                            (a.ProcessingStartedAt == null || a.ProcessingStartedAt <= stuckCutoff) &&
                            !excludedPracticeSet.Contains(a.Id))
                .OrderBy(a => a.SubmittedAt)
                .Take(RecoveryBatchSize)
                .ToListAsync(cancellationToken);

            var practiceToEnqueue = stuckPracticeAnswers
                .Where(stuckAnswer => stuckAnswer.AiEvaluations == null || !stuckAnswer.AiEvaluations.Any())
                .ToList();

            if (practiceToEnqueue.Count > 0)
            {
                foreach (var stuckAnswer in practiceToEnqueue)
                {
                    stuckAnswer.ProcessingStartedAt = now;
                }

                await context.SaveChangesAsync(cancellationToken);

                foreach (var stuckAnswer in practiceToEnqueue)
                {
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
            }

            // 1b. Khôi phục bài nộp MF-05 quá hạn ngưỡng stuckCutoff để tránh treo chấm điểm sau restart.
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
                .Where(s => s.Status == "SUBMITTED" &&
                            s.AiScore == null &&
                            s.SubmittedAt <= stuckCutoff &&
                            (s.ProcessingStartedAt == null || s.ProcessingStartedAt <= stuckCutoff) &&
                            !excludedInClassSet.Contains(s.Id) &&
                            !context.InClassExamShifts.Any(sh => sh.Id == s.ShiftId && (sh.IsLocked || sh.Status == "FINALIZED")))
                .OrderBy(s => s.SubmittedAt)
                .Take(RecoveryBatchSize)
                .ToListAsync(cancellationToken);

            if (stuckInClassSubmissions.Count > 0)
            {
                foreach (var stuckInClass in stuckInClassSubmissions)
                {
                    stuckInClass.ProcessingStartedAt = now;
                }

                await context.SaveChangesAsync(cancellationToken);

                foreach (var stuckInClass in stuckInClassSubmissions)
                {
                    await channel.EnqueueAsync(new GradingTask
                    {
                        AnswerId = stuckInClass.Id,
                        SessionId = stuckInClass.ShiftId,
                        QuestionId = stuckInClass.QuestionId,
                        StudentId = stuckInClass.StudentId,
                        AnswerText = stuckInClass.Transcript ?? string.Empty,
                        IsFollowUp = stuckInClass.IsFollowUp,
                        ParentAnswerId = stuckInClass.ParentSubmissionId,
                        IsFullSession = false,
                        InClassSubmissionId = stuckInClass.Id
                    }, cancellationToken);
                }
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
            .Where(q => q.TaskType == "MockExamGrading" &&
                        q.CorrelationId != null &&
                        (q.Status == "abandoned" ||
                         (q.Status == "pending" && q.NextRetryAt != null && q.NextRetryAt > now)))
            .Select(q => q.CorrelationId!.Value)
            .ToListAsync(cancellationToken);

        var excludedSessionIds = backoffMockSessionIds
            .Where(id => !dueMockSessionIds.Contains(id))
            .ToHashSet();

        var interruptedMockSessions = await context.MockExamSessions
            .AsNoTracking()
            .Where(s =>
                !excludedSessionIds.Contains(s.Id) &&
                ((s.Status == "in_progress" && s.PausedAt == null && now > s.ExpiresAt.AddSeconds(15)) ||
                 (s.Status == "submitted" && s.TotalScore == null)))
            .OrderBy(s => s.ExpiresAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var mockSession in interruptedMockSessions)
        {
            try
            {
                var submitHandler = new SubmitMockExamSessionCommandHandler(context, aiGradingService);
                var submitRes = await submitHandler.Handle(
                    new SubmitMockExamSessionCommand(mockSession.Id, mockSession.StudentId),
                    cancellationToken);
                if (!submitRes.IsSuccess)
                {
                    _logger.LogWarning(
                        "Deferred grading for interrupted MockExamSession {SessionId} not yet completed: {Error}",
                        mockSession.Id,
                        submitRes.Error);
                }
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
