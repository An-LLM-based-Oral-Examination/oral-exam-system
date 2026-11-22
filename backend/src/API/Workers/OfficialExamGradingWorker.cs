using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
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
    private readonly int _maxDegreeOfParallelism;

    public OfficialExamGradingWorker(
        IOfficialExamGradingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<OfficialExamGradingWorker> logger,
        IConfiguration? configuration = null)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;

        var configuredConcurrency = configuration?["OfficialExamGradingQueue:MaxDegreeOfParallelism"]
            ?? configuration?["GradingQueue:MaxDegreeOfParallelism"]
            ?? Environment.GetEnvironmentVariable("OFFICIAL_EXAM_GRADING_MAX_DEGREE_OF_PARALLELISM")
            ?? Environment.GetEnvironmentVariable("GRADING_QUEUE_MAX_DEGREE_OF_PARALLELISM");
        _maxDegreeOfParallelism = int.TryParse(configuredConcurrency, out var parsedConcurrency)
            ? Math.Clamp(parsedConcurrency, 1, 32)
            : 12;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OfficialExamGradingWorker đã khởi động ở chế độ song song đa luồng (MaxDegreeOfParallelism = {MaxDegree}).",
            _maxDegreeOfParallelism);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = stoppingToken
        };

        try
        {
            await Parallel.ForEachAsync(
                _queue.ReadAllAsync(stoppingToken),
                parallelOptions,
                async (task, ct) =>
                {
                    try
                    {
                        await ProcessAsync(task, ct);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Graceful shutdown
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception,
                            "Official grading failed for submission {SubmissionId}; scheduling DLQ replay.",
                            task.SubmissionId);
                        await MarkFailedAsync(task, exception, CancellationToken.None);
                    }
                });
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown khi Host dừng
        }
    }

    internal async Task ProcessAsync(OfficialExamGradingTask task, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var gradingService = scope.ServiceProvider.GetRequiredService<IAiGradingService>();
        var storageService = scope.ServiceProvider.GetService<IStorageService>();
        var sttService = scope.ServiceProvider.GetService<ISpeechToTextService>();

        var job = await context.GradingJobs
            .FirstOrDefaultAsync(item => item.Id == task.JobId && item.SubmissionId == task.SubmissionId, cancellationToken)
            ?? throw new InvalidOperationException("The official grading job no longer exists.");

        if (job.Status == "completed")
        {
            return;
        }

        var submission = await context.ExamQuestionSubmissions
            .Include(item => item.Ticket)
            .Include(item => item.ExamQuestion)
                .ThenInclude(question => question.Course)
            .Include(item => item.ExamQuestion)
                .ThenInclude(question => question.Rubric)
                    .ThenInclude(rubric => rubric.Criteria)
            .AsSingleQuery()
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

        var rubric = submission.ExamQuestion?.Rubric;
        var orderedCriteria = rubric?.Criteria?.OrderBy(item => item.OrderIndex).ToList() ?? [];
        if (orderedCriteria.Count == 0)
        {
            throw new InvalidOperationException("The exam question has no rubric criteria.");
        }

        var examInputMode = await ResolveExamInputModeAsync(context, submission.Ticket, cancellationToken);
        var isVoiceOnly = string.Equals(examInputMode, ExamInputMode.VoiceOnly, StringComparison.OrdinalIgnoreCase);

        var transcriptTamperSuspected = false;
        string? transcriptTamperReason = null;

        var audioKey = ResolveR2AudioObjectKey(submission);
        var expectedHash = !string.IsNullOrWhiteSpace(submission.AudioHashSha256)
            ? submission.AudioHashSha256
            : submission.Ticket?.AudioHashSha256;

        if (storageService is not null && sttService is not null && IsInternalR2ObjectKey(audioKey))
        {
            submission.GradingStatus = "transcribing";
            submission.GradingStartedAt ??= DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(expectedHash))
            {
                var hashValid = await storageService.VerifySha256Async(audioKey!, expectedHash, cancellationToken);
                if (!hashValid)
                {
                    submission.IsSuspicious = true;
                    submission.SuspiciousReason = "Phát hiện sai lệch mã băm SHA-256 của tệp âm thanh trên Cloudflare R2 (nghi ngờ giả mạo bằng chứng).";
                    await context.SaveChangesAsync(cancellationToken);
                    throw new InvalidOperationException(
                        $"AUDIO_INTEGRITY_MISMATCH: Audio SHA-256 verification failed for submission {submission.Id} (objectKey={audioKey}).");
                }

                submission.AudioVerifiedAt ??= DateTime.UtcNow;
            }

            var sttResult = await TranscribeWithTransientRetryAsync(
                storageService,
                sttService,
                audioKey!,
                Path.GetFileName(audioKey!),
                submission.ExamQuestion?.Course?.Code,
                cancellationToken);

            var whisperText = sttResult.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sttResult.TimestampsJson) && string.IsNullOrWhiteSpace(submission.TimestampsWhisper))
            {
                submission.TimestampsWhisper = sttResult.TimestampsJson;
            }

            if (isVoiceOnly)
            {
                submission.InitialVoiceTranscript = whisperText;
                submission.TranscriptWhisper = whisperText;
                submission.TranscriptEditRatio = 0m;
            }
            else if (string.IsNullOrWhiteSpace(submission.TranscriptWhisper))
            {
                if (string.IsNullOrWhiteSpace(submission.InitialVoiceTranscript))
                {
                    submission.InitialVoiceTranscript = whisperText;
                }

                submission.TranscriptWhisper = whisperText;
                submission.TranscriptEditRatio ??= 0m;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(submission.InitialVoiceTranscript))
                {
                    submission.InitialVoiceTranscript = whisperText;
                }

                var serverEditRatio = TranscriptEditRatioCalculator.CalculateRatio(whisperText, submission.TranscriptWhisper);
                submission.TranscriptEditRatio = submission.TranscriptEditRatio.HasValue
                    ? Math.Max(submission.TranscriptEditRatio.Value, serverEditRatio)
                    : serverEditRatio;

                if (serverEditRatio > OfficialExamRules.MaxTranscriptEditRatio)
                {
                    transcriptTamperSuspected = true;
                    transcriptTamperReason =
                        $"Phát hiện chênh lệch giữa bản bóc băng Whisper từ R2 và transcript thí sinh nộp ({serverEditRatio:P1} > {OfficialExamRules.MaxTranscriptEditRatio:P0}), yêu cầu Giảng viên hậu kiểm.";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(submission.TranscriptWhisper))
        {
            var emptyNow = DateTime.UtcNow;
            var emptyDetails = orderedCriteria.Select(criterion => new AiEvaluationDetail
            {
                CriterionId = criterion.Id,
                Score = 0m,
                Comment = $"Sinh viên không trả lời câu hỏi này (0.00/{criterion.MaxScore:0.00} cho tiêu chí '{criterion.CriterionName}')."
            }).ToList();

            context.AiEvaluations.Add(new AiEvaluation
            {
                AnswerType = "exam",
                StudentId = submission.Ticket?.StudentId,
                ExamSubmissionId = submission.Id,
                TotalScore = 0m,
                ConfidenceScore = 1.00m,
                Feedback = "Sinh viên bỏ trống không trả lời câu hỏi này (0.0/10.0 điểm).",
                CotTrace = "[NON_ANSWER_GUARD]: Bản nộp câu hỏi trống (TranscriptWhisper rỗng) -> Chấm 0.0 điểm để bảo toàn mẫu số trung bình của toàn bộ đề thi.",
                EvaluatedAt = emptyNow,
                Details = emptyDetails
            });

            submission.AiScore = 0m;
            submission.GradingStatus = "ai_graded";
            submission.IsSuspicious = true;
            submission.SuspiciousReason = "Sinh viên bỏ trống không trả lời câu hỏi này (0.0/10.0 điểm).";
            CompleteJob(job, emptyNow);

            var hasOtherPending = await context.ExamQuestionSubmissions
                .AsNoTracking()
                .AnyAsync(item => item.TicketId == submission.TicketId &&
                    item.Id != submission.Id && item.GradingStatus != "ai_graded", cancellationToken);
            if (!hasOtherPending)
            {
                var currentTicket = await context.StudentExamTickets
                    .FirstOrDefaultAsync(item => item.Id == submission.TicketId, cancellationToken);
                if (currentTicket?.Status == "SUBMITTED")
                {
                    currentTicket.Status = "AI_GRADED";
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        submission.GradingStatus = "transcribed";
        submission.GradingStartedAt ??= DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        string? originalAnswerTranscript = null;
        var previousFollowUpPrompts = new List<string>();
        if (submission.IsFollowUp)
        {
            var priorTurns = await context.ExamQuestionSubmissions
                .AsNoTracking()
                .Where(item => item.TicketId == submission.TicketId
                    && item.ExamQuestionId == submission.ExamQuestionId
                    && item.FollowUpIndex < submission.FollowUpIndex)
                .OrderBy(item => item.FollowUpIndex)
                .Select(item => new
                {
                    item.IsFollowUp,
                    item.FollowUpIndex,
                    item.TranscriptWhisper,
                    item.FollowUpPrompt
                })
                .ToListAsync(cancellationToken);

            originalAnswerTranscript = priorTurns
                .FirstOrDefault(item => !item.IsFollowUp || item.FollowUpIndex == 0)
                ?.TranscriptWhisper;

            previousFollowUpPrompts = priorTurns
                .Where(item => item.IsFollowUp && !string.IsNullOrWhiteSpace(item.FollowUpPrompt))
                .Select(item => item.FollowUpPrompt!.Trim())
                .ToList();
        }

        var request = new AiGradingRequest
        {
            QuestionContent = submission.ExamQuestion!.Content,
            SampleAnswer = submission.ExamQuestion.SampleAnswer ?? string.Empty,
            CourseCode = submission.ExamQuestion.Course?.Code,
            Transcript = submission.TranscriptWhisper,
            QuestionImageUrl = submission.ExamQuestion.ImageUrl,
            QuestionImageDescription = submission.ExamQuestion.ImageDescription,
            IsFollowUp = submission.IsFollowUp,
            FollowUpPrompt = submission.IsFollowUp ? submission.FollowUpPrompt : null,
            FollowUpIndex = submission.FollowUpIndex,
            OriginalAnswerTranscript = originalAnswerTranscript,
            PreviousFollowUpPrompts = previousFollowUpPrompts,
            ThrowOnTransientFailure = true,
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
        var clampedOverallScore = Math.Clamp(Math.Round(result.Score, 2, MidpointRounding.AwayFromZero), 0m, 10m);
        var details = BuildDetails(orderedCriteria, rawDetails, clampedOverallScore);
        var score = details.Count > 0
            ? Math.Clamp(Math.Round(details.Sum(item => item.Score), 2, MidpointRounding.AwayFromZero), 0m, 10m)
            : clampedOverallScore;
        var isSystemFallback = result.CotTrace?.StartsWith("[SYSTEM_FALLBACK]", StringComparison.OrdinalIgnoreCase) == true;
        var suspicious = result.IsSuspicious
            || isSystemFallback
            || transcriptTamperSuspected
            || result.ConfidenceScore < SuspiciousConfidenceThreshold;
        var now = DateTime.UtcNow;

        context.AiEvaluations.Add(new AiEvaluation
        {
            AnswerType = "exam",
            StudentId = submission.Ticket?.StudentId,
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
            ? (!string.IsNullOrWhiteSpace(transcriptTamperReason)
                ? transcriptTamperReason
                : !string.IsNullOrWhiteSpace(result.SuspiciousReason)
                    ? result.SuspiciousReason
                    : isSystemFallback
                        ? "Bài thi được chấm bằng cơ chế dự phòng [SYSTEM_FALLBACK], bắt buộc Giảng viên hậu kiểm."
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

    private static async Task<string?> ResolveExamInputModeAsync(
        OralExamDbContext context,
        StudentExamTicket? ticket,
        CancellationToken cancellationToken)
    {
        if (ticket is null)
        {
            return null;
        }

        var sessionId = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(s => s.Id == ticket.ShiftId)
            .Select(s => (Guid?)s.SessionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!sessionId.HasValue)
        {
            return null;
        }

        return await context.OfficialExamSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId.Value)
            .Select(s => s.ExamInputMode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? ResolveR2AudioObjectKey(ExamQuestionSubmission submission)
    {
        if (!string.IsNullOrWhiteSpace(submission.AudioObjectKey))
        {
            return submission.AudioObjectKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(submission.AudioR2Url))
        {
            return submission.AudioR2Url.Trim();
        }

        if (!string.IsNullOrWhiteSpace(submission.Ticket?.AudioObjectKey))
        {
            return submission.Ticket.AudioObjectKey.Trim();
        }

        return null;
    }

    private static bool IsInternalR2ObjectKey(string? audioKey)
    {
        if (string.IsNullOrWhiteSpace(audioKey))
        {
            return false;
        }

        return !audioKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !audioKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<SpeechTranscriptionResult> TranscribeWithTransientRetryAsync(
        IStorageService storageService,
        ISpeechToTextService sttService,
        string audioObjectKey,
        string fileName,
        string? vocabularyHint,
        CancellationToken cancellationToken)
    {
        for (var retry = 0; ; retry++)
        {
            try
            {
                await using var audioStream = await storageService.OpenReadStreamAsync(audioObjectKey, cancellationToken);
                return await sttService.TranscribeDetailedAsync(
                    audioStream,
                    string.IsNullOrWhiteSpace(fileName) ? "official-answer.webm" : fileName,
                    vocabularyHint,
                    cancellationToken);
            }
            catch (Exception exception) when (IsTransient(exception) && retry < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[retry], cancellationToken);
            }
        }
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
            .AsSingleQuery()
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
        IReadOnlyList<GradingDetailDto> rawDetails,
        decimal fallbackOverallScore = 0m)
    {
        var details = new List<AiEvaluationDetail>(criteria.Count);
        var matchedByCriterionIndex = new GradingDetailDto?[criteria.Count];
        var usedRawIndices = new HashSet<int>();

        for (var index = 0; index < criteria.Count; index++)
        {
            var criterion = criteria[index];
            var matchedIdx = -1;

            for (var rIdx = 0; rIdx < rawDetails.Count; rIdx++)
            {
                if (usedRawIndices.Contains(rIdx))
                {
                    continue;
                }

                var candidateId = rawDetails[rIdx].CriterionId?.Trim();
                if (string.Equals(candidateId, criterion.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    matchedIdx = rIdx;
                    break;
                }
            }

            if (matchedIdx < 0 && !string.IsNullOrWhiteSpace(criterion.CriterionName))
            {
                for (var rIdx = 0; rIdx < rawDetails.Count; rIdx++)
                {
                    if (usedRawIndices.Contains(rIdx))
                    {
                        continue;
                    }

                    var candidateId = rawDetails[rIdx].CriterionId?.Trim();
                    if (string.Equals(candidateId, criterion.CriterionName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        matchedIdx = rIdx;
                        break;
                    }
                }
            }

            if (matchedIdx >= 0)
            {
                usedRawIndices.Add(matchedIdx);
                matchedByCriterionIndex[index] = rawDetails[matchedIdx];
            }
        }

        var totalCriteriaMax = criteria.Sum(c => c.MaxScore);
        decimal runningAllocatedScore = 0m;

        for (var index = 0; index < criteria.Count; index++)
        {
            var criterion = criteria[index];
            var raw = matchedByCriterionIndex[index];
            if (raw is null && index < rawDetails.Count && !usedRawIndices.Contains(index))
            {
                usedRawIndices.Add(index);
                raw = rawDetails[index];
            }

            decimal score;
            if (rawDetails.Count > 0)
            {
                score = raw is null
                    ? 0m
                    : Math.Clamp(Math.Round(raw.Score, 2, MidpointRounding.AwayFromZero), 0m, criterion.MaxScore);
            }
            else if (totalCriteriaMax > 0m && fallbackOverallScore > 0m)
            {
                if (index == criteria.Count - 1)
                {
                    score = Math.Clamp(
                        Math.Round(fallbackOverallScore - runningAllocatedScore, 2, MidpointRounding.AwayFromZero),
                        0m,
                        criterion.MaxScore);
                }
                else
                {
                    score = Math.Clamp(
                        Math.Round(fallbackOverallScore * (criterion.MaxScore / totalCriteriaMax), 2, MidpointRounding.AwayFromZero),
                        0m,
                        criterion.MaxScore);
                    runningAllocatedScore += score;
                }
            }
            else
            {
                score = 0m;
            }

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
