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
            .AsNoTracking()
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
        if (existingEvaluation || IsSubmissionTerminal(submission.GradingStatus))
        {
            await PromoteCompletedSubmissionUnderTicketLockAsync(
                context,
                submission.TicketId,
                submission.Id,
                job.Id,
                DateTime.UtcNow,
                cancellationToken);
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

        var audioVerifiedAt = submission.AudioVerifiedAt;
        var initialVoiceTranscript = submission.InitialVoiceTranscript;
        var transcriptWhisper = submission.TranscriptWhisper;
        var transcriptEditRatio = submission.TranscriptEditRatio;
        var timestampsWhisper = submission.TimestampsWhisper;

        var audioKey = ResolveR2AudioObjectKey(submission);
        var expectedHash = !string.IsNullOrWhiteSpace(submission.AudioHashSha256)
            ? submission.AudioHashSha256
            : submission.Ticket?.AudioHashSha256;

        if (storageService is not null && sttService is not null && IsInternalR2ObjectKey(audioKey))
        {
            var canTranscribe = await TryAdvanceSubmissionStageUnderTicketLockAsync(
                context,
                submission.TicketId,
                submission.Id,
                job.Id,
                targetStage: "transcribing",
                nowUtc: DateTime.UtcNow,
                audioVerifiedAt: null,
                initialVoiceTranscript: null,
                transcriptWhisper: null,
                transcriptEditRatio: null,
                timestampsWhisper: null,
                cancellationToken: cancellationToken);
            if (!canTranscribe)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(expectedHash))
            {
                var hashValid = await storageService.VerifySha256Async(audioKey!, expectedHash, cancellationToken);
                if (!hashValid)
                {
                    await FlagAudioIntegrityMismatchUnderTicketLockAsync(
                        context,
                        submission.TicketId,
                        submission.Id,
                        "Phát hiện sai lệch mã băm SHA-256 của tệp âm thanh trên Cloudflare R2 (nghi ngờ giả mạo bằng chứng).",
                        cancellationToken);
                    throw new InvalidOperationException(
                        $"AUDIO_INTEGRITY_MISMATCH: Audio SHA-256 verification failed for submission {submission.Id} (objectKey={audioKey}).");
                }

                audioVerifiedAt ??= DateTime.UtcNow;
            }

            var sttResult = await TranscribeWithTransientRetryAsync(
                storageService,
                sttService,
                audioKey!,
                Path.GetFileName(audioKey!),
                submission.ExamQuestion?.Course?.Code,
                cancellationToken);

            var whisperText = sttResult.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sttResult.TimestampsJson) && string.IsNullOrWhiteSpace(timestampsWhisper))
            {
                timestampsWhisper = sttResult.TimestampsJson;
            }

            if (isVoiceOnly)
            {
                initialVoiceTranscript = whisperText;
                transcriptWhisper = whisperText;
                transcriptEditRatio = 0m;
            }
            else if (string.IsNullOrWhiteSpace(transcriptWhisper))
            {
                if (string.IsNullOrWhiteSpace(initialVoiceTranscript))
                {
                    initialVoiceTranscript = whisperText;
                }

                transcriptWhisper = whisperText;
                transcriptEditRatio ??= 0m;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(initialVoiceTranscript))
                {
                    initialVoiceTranscript = whisperText;
                }

                var serverEditRatio = TranscriptEditRatioCalculator.CalculateRatio(whisperText, transcriptWhisper);
                transcriptEditRatio = transcriptEditRatio.HasValue
                    ? Math.Max(transcriptEditRatio.Value, serverEditRatio)
                    : serverEditRatio;

                if (serverEditRatio > OfficialExamRules.MaxTranscriptEditRatio)
                {
                    transcriptTamperSuspected = true;
                    transcriptTamperReason =
                        $"Phát hiện chênh lệch giữa bản bóc băng Whisper từ R2 và transcript thí sinh nộp ({serverEditRatio:P1} > {OfficialExamRules.MaxTranscriptEditRatio:P0}), yêu cầu Giảng viên hậu kiểm.";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(transcriptWhisper))
        {
            var emptyNow = DateTime.UtcNow;
            var emptyDetailSpecs = orderedCriteria
                .Select(criterion => (
                    criterion.Id,
                    Score: 0m,
                    Comment: $"Sinh viên không trả lời câu hỏi này (0.00/{criterion.MaxScore:0.00} cho tiêu chí '{criterion.CriterionName}')."))
                .ToList();

            await PersistGradedSubmissionUnderTicketLockAsync(
                context,
                ticketId: submission.TicketId,
                studentId: submission.Ticket?.StudentId,
                submissionId: submission.Id,
                jobId: job.Id,
                audioVerifiedAt: audioVerifiedAt,
                initialVoiceTranscript: initialVoiceTranscript,
                transcriptWhisper: transcriptWhisper,
                transcriptEditRatio: transcriptEditRatio,
                timestampsWhisper: timestampsWhisper,
                score: 0m,
                confidenceScore: 1.00m,
                feedback: "Sinh viên bỏ trống không trả lời câu hỏi này (0.0/10.0 điểm).",
                cotTrace: "[NON_ANSWER_GUARD]: Bản nộp câu hỏi trống (TranscriptWhisper rỗng) -> Chấm 0.0 điểm để bảo toàn mẫu số trung bình của toàn bộ đề thi.",
                isSuspicious: true,
                suspiciousReason: "Sinh viên bỏ trống không trả lời câu hỏi này (0.0/10.0 điểm).",
                detailSpecs: emptyDetailSpecs,
                nowUtc: emptyNow,
                cancellationToken: cancellationToken);
            return;
        }

        var canGrade = await TryAdvanceSubmissionStageUnderTicketLockAsync(
            context,
            submission.TicketId,
            submission.Id,
            job.Id,
            targetStage: "transcribed",
            nowUtc: DateTime.UtcNow,
            audioVerifiedAt: audioVerifiedAt,
            initialVoiceTranscript: initialVoiceTranscript,
            transcriptWhisper: transcriptWhisper,
            transcriptEditRatio: transcriptEditRatio,
            timestampsWhisper: timestampsWhisper,
            cancellationToken: cancellationToken);
        if (!canGrade)
        {
            return;
        }

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
            Transcript = transcriptWhisper,
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

        var suspiciousReason = suspicious
            ? (!string.IsNullOrWhiteSpace(transcriptTamperReason)
                ? transcriptTamperReason
                : !string.IsNullOrWhiteSpace(result.SuspiciousReason)
                    ? result.SuspiciousReason
                    : isSystemFallback
                        ? "Bài thi được chấm bằng cơ chế dự phòng [SYSTEM_FALLBACK], bắt buộc Giảng viên hậu kiểm."
                        : $"AI confidence {result.ConfidenceScore:0.00} is below {SuspiciousConfidenceThreshold:0.00}.")
            : null;
        var detailSpecs = details
            .Select(item => (item.CriterionId, item.Score, item.Comment ?? string.Empty))
            .ToList();

        await PersistGradedSubmissionUnderTicketLockAsync(
            context,
            ticketId: submission.TicketId,
            studentId: submission.Ticket?.StudentId,
            submissionId: submission.Id,
            jobId: job.Id,
            audioVerifiedAt: audioVerifiedAt,
            initialVoiceTranscript: initialVoiceTranscript,
            transcriptWhisper: transcriptWhisper,
            transcriptEditRatio: transcriptEditRatio,
            timestampsWhisper: timestampsWhisper,
            score: score,
            confidenceScore: result.ConfidenceScore,
            feedback: result.Feedback,
            cotTrace: result.CotTrace,
            isSuspicious: suspicious,
            suspiciousReason: suspiciousReason,
            detailSpecs: detailSpecs,
            nowUtc: now,
            cancellationToken: cancellationToken);
    }

    private static bool IsSubmissionTerminal(string? gradingStatus) =>
        string.Equals(gradingStatus, "ai_graded", StringComparison.OrdinalIgnoreCase)
        || string.Equals(gradingStatus, "audited", StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> TryAdvanceSubmissionStageUnderTicketLockAsync(
        OralExamDbContext context,
        Guid ticketId,
        Guid submissionId,
        Guid jobId,
        string targetStage,
        DateTime nowUtc,
        DateTime? audioVerifiedAt,
        string? initialVoiceTranscript,
        string? transcriptWhisper,
        decimal? transcriptEditRatio,
        string? timestampsWhisper,
        CancellationToken cancellationToken)
    {
        return await context.ExecuteTicketDecisionAsync(
            ticketId,
            async ct =>
            {
                context.ChangeTracker.Clear();

                var trackedJob = await context.GradingJobs
                    .FirstOrDefaultAsync(item => item.Id == jobId, ct);
                var trackedSubmission = await context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(item => item.Id == submissionId, ct);
                if (trackedSubmission is null)
                {
                    if (trackedJob is not null)
                    {
                        CompleteJob(trackedJob, nowUtc);
                        await context.SaveChangesAsync(ct);
                    }

                    return false;
                }

                var alreadyEvaluated = await context.AiEvaluations
                    .AsNoTracking()
                    .AnyAsync(item => item.ExamSubmissionId == submissionId, ct);

                if (alreadyEvaluated || IsSubmissionTerminal(trackedSubmission.GradingStatus))
                {
                    if (alreadyEvaluated && !IsSubmissionTerminal(trackedSubmission.GradingStatus))
                    {
                        trackedSubmission.GradingStatus = "ai_graded";
                    }

                    if (trackedJob is not null)
                    {
                        CompleteJob(trackedJob, nowUtc);
                    }

                    await PromoteTicketIfAllSubmissionsTerminalAsync(context, ticketId, submissionId, ct);
                    await context.SaveChangesAsync(ct);
                    return false;
                }

                trackedSubmission.GradingStatus = targetStage;
                trackedSubmission.GradingStartedAt ??= nowUtc;

                if (audioVerifiedAt.HasValue)
                {
                    trackedSubmission.AudioVerifiedAt ??= audioVerifiedAt;
                }

                if (!string.IsNullOrWhiteSpace(initialVoiceTranscript))
                {
                    trackedSubmission.InitialVoiceTranscript = initialVoiceTranscript;
                }

                if (transcriptWhisper is not null)
                {
                    trackedSubmission.TranscriptWhisper = transcriptWhisper;
                }

                if (transcriptEditRatio.HasValue)
                {
                    trackedSubmission.TranscriptEditRatio = transcriptEditRatio;
                }

                if (!string.IsNullOrWhiteSpace(timestampsWhisper))
                {
                    trackedSubmission.TimestampsWhisper = timestampsWhisper;
                }

                await context.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
    }

    private static async Task FlagAudioIntegrityMismatchUnderTicketLockAsync(
        OralExamDbContext context,
        Guid ticketId,
        Guid submissionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await context.ExecuteTicketDecisionAsync(
            ticketId,
            async ct =>
            {
                context.ChangeTracker.Clear();

                var trackedSubmission = await context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(item => item.Id == submissionId, ct);
                if (trackedSubmission is null || IsSubmissionTerminal(trackedSubmission.GradingStatus))
                {
                    return true;
                }

                trackedSubmission.IsSuspicious = true;
                trackedSubmission.SuspiciousReason = reason;
                await context.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
    }

    private static async Task PromoteTicketIfAllSubmissionsTerminalAsync(
        OralExamDbContext context,
        Guid ticketId,
        Guid completedSubmissionId,
        CancellationToken cancellationToken)
    {
        var hasOtherIncompleteSubmissions = await context.ExamQuestionSubmissions
            .AsNoTracking()
            .AnyAsync(item =>
                item.TicketId == ticketId
                && item.Id != completedSubmissionId
                && item.GradingStatus != "ai_graded"
                && item.GradingStatus != "audited",
                cancellationToken);

        if (!hasOtherIncompleteSubmissions)
        {
            var ticket = await context.StudentExamTickets
                .FirstOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
            if (ticket is not null &&
                string.Equals(ticket.Status, ExamTicketStatus.Submitted, StringComparison.OrdinalIgnoreCase))
            {
                ticket.Status = ExamTicketStatus.AiGraded;
            }
        }
    }

    private static async Task PersistGradedSubmissionUnderTicketLockAsync(
        OralExamDbContext context,
        Guid ticketId,
        Guid? studentId,
        Guid submissionId,
        Guid jobId,
        DateTime? audioVerifiedAt,
        string? initialVoiceTranscript,
        string? transcriptWhisper,
        decimal? transcriptEditRatio,
        string? timestampsWhisper,
        decimal score,
        decimal confidenceScore,
        string? feedback,
        string? cotTrace,
        bool isSuspicious,
        string? suspiciousReason,
        IReadOnlyList<(Guid CriterionId, decimal Score, string Comment)> detailSpecs,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await context.ExecuteTicketDecisionAsync(
            ticketId,
            async ct =>
            {
                context.ChangeTracker.Clear();

                var trackedJob = await context.GradingJobs
                    .FirstOrDefaultAsync(item => item.Id == jobId, ct);
                var trackedSubmission = await context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(item => item.Id == submissionId, ct);
                if (trackedSubmission is null)
                {
                    if (trackedJob is not null)
                    {
                        CompleteJob(trackedJob, nowUtc);
                        await context.SaveChangesAsync(ct);
                    }

                    return true;
                }

                var alreadyEvaluated = await context.AiEvaluations
                    .AsNoTracking()
                    .AnyAsync(item => item.ExamSubmissionId == submissionId, ct);

                if (!alreadyEvaluated && !IsSubmissionTerminal(trackedSubmission.GradingStatus))
                {
                    if (audioVerifiedAt.HasValue)
                    {
                        trackedSubmission.AudioVerifiedAt ??= audioVerifiedAt;
                    }

                    if (!string.IsNullOrWhiteSpace(initialVoiceTranscript))
                    {
                        trackedSubmission.InitialVoiceTranscript = initialVoiceTranscript;
                    }

                    if (transcriptWhisper is not null)
                    {
                        trackedSubmission.TranscriptWhisper = transcriptWhisper;
                    }

                    if (transcriptEditRatio.HasValue)
                    {
                        trackedSubmission.TranscriptEditRatio = transcriptEditRatio;
                    }

                    if (!string.IsNullOrWhiteSpace(timestampsWhisper))
                    {
                        trackedSubmission.TimestampsWhisper = timestampsWhisper;
                    }

                    context.AiEvaluations.Add(new AiEvaluation
                    {
                        AnswerType = "exam",
                        StudentId = studentId,
                        ExamSubmissionId = submissionId,
                        TotalScore = score,
                        ConfidenceScore = confidenceScore,
                        Feedback = feedback,
                        CotTrace = cotTrace,
                        EvaluatedAt = nowUtc,
                        Details = detailSpecs.Select(spec => new AiEvaluationDetail
                        {
                            CriterionId = spec.CriterionId,
                            Score = spec.Score,
                            Comment = spec.Comment
                        }).ToList()
                    });

                    trackedSubmission.AiScore = score;
                    trackedSubmission.GradingStatus = "ai_graded";
                    trackedSubmission.IsSuspicious = isSuspicious;
                    trackedSubmission.SuspiciousReason = suspiciousReason;
                }
                else if (alreadyEvaluated && !IsSubmissionTerminal(trackedSubmission.GradingStatus))
                {
                    trackedSubmission.GradingStatus = "ai_graded";
                }

                if (trackedJob is not null)
                {
                    CompleteJob(trackedJob, nowUtc);
                }

                await PromoteTicketIfAllSubmissionsTerminalAsync(context, ticketId, submissionId, ct);
                await context.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
    }

    private static async Task PromoteCompletedSubmissionUnderTicketLockAsync(
        OralExamDbContext context,
        Guid ticketId,
        Guid submissionId,
        Guid jobId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await context.ExecuteTicketDecisionAsync(
            ticketId,
            async ct =>
            {
                context.ChangeTracker.Clear();

                var trackedJob = await context.GradingJobs
                    .FirstOrDefaultAsync(item => item.Id == jobId, ct);
                var trackedSubmission = await context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(item => item.Id == submissionId, ct);
                if (trackedSubmission is not null && !IsSubmissionTerminal(trackedSubmission.GradingStatus))
                {
                    var alreadyEvaluated = await context.AiEvaluations
                        .AsNoTracking()
                        .AnyAsync(item => item.ExamSubmissionId == submissionId, ct);
                    if (alreadyEvaluated)
                    {
                        trackedSubmission.GradingStatus = "ai_graded";
                    }
                }

                if (trackedJob is not null)
                {
                    CompleteJob(trackedJob, nowUtc);
                }

                await PromoteTicketIfAllSubmissionsTerminalAsync(context, ticketId, submissionId, ct);
                await context.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
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

    internal async Task MarkFailedAsync(
        OfficialExamGradingTask task,
        Exception exception,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();

        var ticketId = await context.ExamQuestionSubmissions
            .AsNoTracking()
            .Where(item => item.Id == task.SubmissionId)
            .Select(item => (Guid?)item.TicketId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ticketId.HasValue)
        {
            await context.ExecuteTicketDecisionAsync(
                ticketId.Value,
                async ct =>
                {
                    context.ChangeTracker.Clear();
                    await MarkFailedCoreAsync(context, task, ticketId.Value, exception, ct);
                    return true;
                },
                cancellationToken);
            return;
        }

        await MarkFailedCoreAsync(context, task, null, exception, cancellationToken);
    }

    private static async Task MarkFailedCoreAsync(
        OralExamDbContext context,
        OfficialExamGradingTask task,
        Guid? ticketId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var job = await context.GradingJobs.FirstOrDefaultAsync(item => item.Id == task.JobId, cancellationToken);
        if (job is null || job.Status == "completed")
        {
            return;
        }

        var now = DateTime.UtcNow;
        var submission = await context.ExamQuestionSubmissions
            .AsSingleQuery()
            .FirstOrDefaultAsync(item => item.Id == task.SubmissionId, cancellationToken);

        if (submission is not null)
        {
            var alreadyEvaluated = await context.AiEvaluations
                .AsNoTracking()
                .AnyAsync(item => item.ExamSubmissionId == submission.Id, cancellationToken);

            if (alreadyEvaluated || IsSubmissionTerminal(submission.GradingStatus))
            {
                if (alreadyEvaluated && !IsSubmissionTerminal(submission.GradingStatus))
                {
                    submission.GradingStatus = "ai_graded";
                }

                CompleteJob(job, now);
                if (ticketId.HasValue)
                {
                    await PromoteTicketIfAllSubmissionsTerminalAsync(context, ticketId.Value, submission.Id, cancellationToken);
                }

                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            submission.GradingStatus = "failed";
        }

        job.Status = "failed";
        job.LockedUntil = null;
        job.NextAttemptAt = now.Add(DlqReplayDelay);
        job.LastError = exception.Message;
        job.UpdatedAt = now;

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
