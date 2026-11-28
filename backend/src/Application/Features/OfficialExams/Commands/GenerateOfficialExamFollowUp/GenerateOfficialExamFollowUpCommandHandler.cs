using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.GenerateOfficialExamFollowUp;

public sealed class GenerateOfficialExamFollowUpCommandHandler(
    IApplicationDbContext context,
    IAiGradingService? aiGradingService = null,
    IStorageService? storageService = null,
    ISpeechToTextService? speechToTextService = null,
    ILogger<GenerateOfficialExamFollowUpCommandHandler>? logger = null)
    : IRequestHandler<GenerateOfficialExamFollowUpCommand, Result<OfficialExamNextFollowUpResponse>>
{
    public async Task<Result<OfficialExamNextFollowUpResponse>> Handle(
        GenerateOfficialExamFollowUpCommand request,
        CancellationToken cancellationToken)
    {
        var ticket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Student)
            .Include(t => t.Shift)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (ticket.StudentId != request.ActorUserId || ticket.Student.Role != UserRole.Student || !ticket.Student.IsActive)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            ticket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true, requireBoundDevice: true);
        if (deviceError is not null)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(deviceError);
        }

        if (ticket.Status == ExamTicketStatus.Locked || ticket.IsLocked)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketLocked);
        }

        if (!string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
        }

        if (ticket.Shift is null)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure("CONFLICT: Ca thi không tồn tại hoặc chưa mở phát đề.");
        }

        var shiftWindowError = ExamShiftWindow.ValidateActiveSubmissionWindow(ticket.Shift, DateTime.UtcNow);
        if (shiftWindowError is not null)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(shiftWindowError);
        }

        var submission = await context.ExamQuestionSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == request.SubmissionId && s.TicketId == request.TicketId,
                cancellationToken);

        if (submission is null)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure("NOT_FOUND: Không tìm thấy bài nộp câu hỏi cho vé thi này.");
        }

        var session = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(s => s.Id == ticket.Shift.SessionId)
            .Select(s => new { s.HasFollowUp, s.MaxFollowUpQuestions, s.ExamInputMode })
            .FirstOrDefaultAsync(cancellationToken);

        var hasFollowUp = session?.HasFollowUp ?? false;
        var maxFollowUp = session?.MaxFollowUpQuestions ?? OfficialExamRules.DefaultFollowUpQuestions;

        if (!hasFollowUp || maxFollowUp <= 0 || submission.FollowUpIndex >= maxFollowUp)
        {
            return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                HasMoreFollowUp: false,
                FollowUpIndex: submission.FollowUpIndex,
                MaxFollowUpQuestions: maxFollowUp,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.ExamQuestionId,
                FollowUpPrompt: null));
        }

        var nextFollowUpIndex = submission.FollowUpIndex + 1;

        // Idempotent replay: return previously generated prompt stored in GeneratedFollowUpPrompt
        if (!string.IsNullOrWhiteSpace(submission.GeneratedFollowUpPrompt))
        {
            return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                HasMoreFollowUp: true,
                FollowUpIndex: nextFollowUpIndex,
                MaxFollowUpQuestions: maxFollowUp,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.ExamQuestionId,
                FollowUpPrompt: submission.GeneratedFollowUpPrompt.Trim()));
        }

        var isVoiceOnly = string.Equals(session?.ExamInputMode, ExamInputMode.VoiceOnly, StringComparison.OrdinalIgnoreCase);

        // Pha phối hợp chống race condition với OfficialExamGradingWorker (ZERO external I/O trong lock):
        // - Nếu bài đã có AiEvaluation (worker đã chấm xong trước), tái sử dụng FollowUpPrompt có sẵn không gọi lại STT/LLM.
        // - Nếu GradingJob đang pending, gia hạn NextAttemptAt để Dispatcher không claim song song khi đang sinh follow-up.
        // - Nếu GradingJob đã đang processing bởi worker, chờ ngắn để dùng kết quả worker thay vì chạy STT/LLM song song.
        var preClaim = await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            async ct =>
            {
                var currentTicket = await context.StudentExamTickets
                    .AsNoTracking()
                    .Where(t => t.Id == request.TicketId)
                    .Select(t => new { t.Status, t.IsLocked })
                    .FirstOrDefaultAsync(ct);
                if (currentTicket is null)
                {
                    return (
                        Found: false,
                        TicketNotFound: true,
                        TicketLocked: false,
                        ExistingPrompt: (string?)null,
                        ExistingEvalPrompt: (string?)null,
                        ExistingEvalSuspicious: false,
                        HasExistingEval: false,
                        Transcript: (string?)null,
                        JobInFlight: false);
                }

                if (currentTicket.Status == ExamTicketStatus.Locked || currentTicket.IsLocked)
                {
                    return (
                        Found: true,
                        TicketNotFound: false,
                        TicketLocked: true,
                        ExistingPrompt: (string?)null,
                        ExistingEvalPrompt: (string?)null,
                        ExistingEvalSuspicious: false,
                        HasExistingEval: false,
                        Transcript: (string?)null,
                        JobInFlight: false);
                }

                var currentSub = await context.ExamQuestionSubmissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && s.TicketId == request.TicketId, ct);
                if (currentSub is null)
                {
                    return (
                        Found: false,
                        TicketNotFound: false,
                        TicketLocked: false,
                        ExistingPrompt: (string?)null,
                        ExistingEvalPrompt: (string?)null,
                        ExistingEvalSuspicious: false,
                        HasExistingEval: false,
                        Transcript: (string?)null,
                        JobInFlight: false);
                }

                if (!string.IsNullOrWhiteSpace(currentSub.GeneratedFollowUpPrompt))
                {
                    return (true, false, false, currentSub.GeneratedFollowUpPrompt.Trim(), null, false, false, currentSub.TranscriptWhisper, false);
                }

                var existingEval = await context.AiEvaluations
                    .AsNoTracking()
                    .Where(e => e.ExamSubmissionId == currentSub.Id)
                    .Select(e => new { e.FollowUpPrompt })
                    .FirstOrDefaultAsync(ct);
                if (existingEval is not null)
                {
                    return (true, false, false, null, existingEval.FollowUpPrompt, currentSub.IsSuspicious, true, currentSub.TranscriptWhisper, false);
                }

                var nowLockUtc = DateTime.UtcNow;
                var job = await context.GradingJobs
                    .FirstOrDefaultAsync(j => j.SubmissionId == currentSub.Id, ct);

                var jobInFlight = false;
                if (job is not null)
                {
                    if (string.Equals(job.Status, "pending", StringComparison.OrdinalIgnoreCase))
                    {
                        var extendedUntil = nowLockUtc.AddSeconds(OfficialExamRules.FollowUpInFlightLeaseExtensionSeconds);
                        if (!job.NextAttemptAt.HasValue || job.NextAttemptAt.Value < extendedUntil)
                        {
                            job.NextAttemptAt = extendedUntil;
                            job.UpdatedAt = nowLockUtc;
                            await context.SaveChangesAsync(ct);
                        }
                    }
                    else if (string.Equals(job.Status, "processing", StringComparison.OrdinalIgnoreCase)
                        && (!job.LockedUntil.HasValue || job.LockedUntil.Value > nowLockUtc))
                    {
                        jobInFlight = true;
                    }
                }

                return (true, false, false, null, null, currentSub.IsSuspicious, false, currentSub.TranscriptWhisper, jobInFlight);
            },
            cancellationToken);

        if (preClaim.TicketNotFound)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (preClaim.TicketLocked)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketLocked);
        }

        if (!preClaim.Found)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure("NOT_FOUND: Không tìm thấy bài nộp câu hỏi cho vé thi này.");
        }

        if (!string.IsNullOrWhiteSpace(preClaim.ExistingPrompt))
        {
            return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                HasMoreFollowUp: true,
                FollowUpIndex: nextFollowUpIndex,
                MaxFollowUpQuestions: maxFollowUp,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.ExamQuestionId,
                FollowUpPrompt: preClaim.ExistingPrompt));
        }

        var hasExistingEval = preClaim.HasExistingEval;
        var existingEvalPrompt = preClaim.ExistingEvalPrompt;
        var existingEvalSuspicious = preClaim.ExistingEvalSuspicious;
        var transcript = (preClaim.Transcript ?? submission.TranscriptWhisper)?.Trim() ?? string.Empty;

        if (preClaim.JobInFlight && !hasExistingEval)
        {
            // Worker đang xử lý submission này: chờ ngắn có giới hạn để tái sử dụng kết quả worker, tuyệt đối không gọi STT/LLM song song.
            var deadlineUtc = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadlineUtc)
            {
                await Task.Delay(250, cancellationToken);

                var ticketSnap = await context.StudentExamTickets
                    .AsNoTracking()
                    .Where(t => t.Id == request.TicketId)
                    .Select(t => new { t.Status, t.IsLocked })
                    .FirstOrDefaultAsync(cancellationToken);

                if (ticketSnap is null)
                {
                    return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketNotFound);
                }

                if (ticketSnap.Status == ExamTicketStatus.Locked || ticketSnap.IsLocked)
                {
                    return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketLocked);
                }

                if (!string.Equals(ticketSnap.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
                {
                    return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
                }

                var evalSnap = await context.AiEvaluations
                    .AsNoTracking()
                    .Where(e => e.ExamSubmissionId == submission.Id)
                    .Select(e => new { e.FollowUpPrompt })
                    .FirstOrDefaultAsync(cancellationToken);

                var subSnap = await context.ExamQuestionSubmissions
                    .AsNoTracking()
                    .Where(s => s.Id == submission.Id)
                    .Select(s => new { s.TranscriptWhisper, s.IsSuspicious, s.GeneratedFollowUpPrompt })
                    .FirstOrDefaultAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(subSnap?.GeneratedFollowUpPrompt))
                {
                    return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                        HasMoreFollowUp: true,
                        FollowUpIndex: nextFollowUpIndex,
                        MaxFollowUpQuestions: maxFollowUp,
                        ParentSubmissionId: submission.Id,
                        QuestionId: submission.ExamQuestionId,
                        FollowUpPrompt: subSnap.GeneratedFollowUpPrompt.Trim()));
                }

                if (!string.IsNullOrWhiteSpace(subSnap?.TranscriptWhisper))
                {
                    transcript = subSnap.TranscriptWhisper.Trim();
                }

                if (evalSnap is not null)
                {
                    hasExistingEval = true;
                    existingEvalPrompt = evalSnap.FollowUpPrompt;
                    existingEvalSuspicious = subSnap?.IsSuspicious ?? false;
                    break;
                }

                var jobSnap = await context.GradingJobs
                    .AsNoTracking()
                    .Where(j => j.SubmissionId == submission.Id)
                    .Select(j => new { j.Status, j.LockedUntil })
                    .FirstOrDefaultAsync(cancellationToken);

                if (jobSnap is null
                    || !string.Equals(jobSnap.Status, "processing", StringComparison.OrdinalIgnoreCase)
                    || (jobSnap.LockedUntil.HasValue && jobSnap.LockedUntil.Value <= DateTime.UtcNow))
                {
                    break;
                }
            }

            if (!hasExistingEval)
            {
                var stillProcessing = await context.GradingJobs
                    .AsNoTracking()
                    .AnyAsync(
                        j => j.SubmissionId == submission.Id
                            && j.Status == "processing"
                            && (j.LockedUntil == null || j.LockedUntil > DateTime.UtcNow),
                        cancellationToken);
                if (stillProcessing)
                {
                    var postPollTicket = await context.StudentExamTickets
                        .AsNoTracking()
                        .Where(t => t.Id == request.TicketId)
                        .Select(t => new { t.Status, t.IsLocked })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (postPollTicket is null)
                    {
                        return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketNotFound);
                    }

                    if (postPollTicket.Status == ExamTicketStatus.Locked || postPollTicket.IsLocked)
                    {
                        return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketLocked);
                    }

                    if (!string.Equals(postPollTicket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
                    {
                        return Result<OfficialExamNextFollowUpResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
                    }

                    logger?.LogInformation(
                        "Skipping parallel follow-up STT/LLM because OfficialExamGradingWorker is actively grading submission {SubmissionId}.",
                        submission.Id);
                    return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                        HasMoreFollowUp: false,
                        FollowUpIndex: submission.FollowUpIndex,
                        MaxFollowUpQuestions: maxFollowUp,
                        ParentSubmissionId: submission.Id,
                        QuestionId: submission.ExamQuestionId,
                        FollowUpPrompt: null));
                }
            }
        }

        var question = await context.ExamQuestions
            .AsNoTracking()
            .Include(q => q.Course)
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(q => q.Id == submission.ExamQuestionId, cancellationToken);

        var sttRanFromR2 = false;
        var audioShaVerified = false;
        string? sttTimestampsJson = null;

        // VoiceOnly / empty transcript fallback: run Whisper STT from R2 audio stream (chỉ khi worker chưa chấm xong)
        if (!hasExistingEval && string.IsNullOrWhiteSpace(transcript) && storageService is not null && speechToTextService is not null)
        {
            var audioKey = !string.IsNullOrWhiteSpace(submission.AudioObjectKey)
                ? submission.AudioObjectKey
                : !string.IsNullOrWhiteSpace(submission.AudioR2Url)
                    ? submission.AudioR2Url
                    : ticket.AudioObjectKey;
            var expectedHash = !string.IsNullOrWhiteSpace(submission.AudioHashSha256)
                ? submission.AudioHashSha256
                : ticket.AudioHashSha256;

            if (!string.IsNullOrWhiteSpace(audioKey) && !Uri.TryCreate(audioKey, UriKind.Absolute, out _))
            {
                try
                {
                    var canTranscribeAudio = true;
                    if (!string.IsNullOrWhiteSpace(expectedHash))
                    {
                        audioShaVerified = await storageService.VerifySha256Async(audioKey, expectedHash, cancellationToken);
                        if (!audioShaVerified)
                        {
                            canTranscribeAudio = false;
                            logger?.LogWarning(
                                "Audio SHA-256 verification failed during follow-up STT fallback for submission {SubmissionId}; releasing job for worker integrity handling.",
                                submission.Id);
                        }
                    }

                    if (canTranscribeAudio)
                    {
                        await using var stream = await storageService.OpenReadStreamAsync(audioKey, cancellationToken);
                        var stt = await speechToTextService.TranscribeDetailedAsync(
                            stream,
                            Path.GetFileName(audioKey),
                            question?.Course?.Code,
                            cancellationToken);
                        transcript = (stt.Text ?? string.Empty).Trim();
                        sttTimestampsJson = stt.TimestampsJson;
                        sttRanFromR2 = true;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger?.LogWarning(
                        ex,
                        "Whisper STT fallback failed while generating follow-up for submission {SubmissionId}.",
                        submission.Id);
                    transcript = string.Empty;
                }
            }
        }

        if (FollowUpPromptBuilder.IsStudentRefusalOrBlank(transcript))
        {
            var releaseError = await ReleaseDeferredGradingJobAsync(
                context,
                request,
                submission.Id,
                transcript,
                sttTimestampsJson,
                markTranscribed: sttRanFromR2 && audioShaVerified && isVoiceOnly && !string.IsNullOrWhiteSpace(transcript),
                cancellationToken);
            if (releaseError is not null)
            {
                return Result<OfficialExamNextFollowUpResponse>.Failure(releaseError);
            }

            return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                HasMoreFollowUp: false,
                FollowUpIndex: submission.FollowUpIndex,
                MaxFollowUpQuestions: maxFollowUp,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.ExamQuestionId,
                FollowUpPrompt: null));
        }

        var priorSubmissions = await context.ExamQuestionSubmissions
            .AsNoTracking()
            .Where(s => s.TicketId == request.TicketId && s.ExamQuestionId == submission.ExamQuestionId)
            .OrderBy(s => s.FollowUpIndex)
            .ToListAsync(cancellationToken);

        var previousFollowUpPrompts = priorSubmissions
            .SelectMany(s => new[] { s.FollowUpPrompt, s.GeneratedFollowUpPrompt })
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orderedCriteria = question?.Rubric?.Criteria?
            .OrderBy(c => c.OrderIndex)
            .ToList() ?? new List<RubricCriterion>();

        var originalTranscript = priorSubmissions
            .FirstOrDefault(s => !s.IsFollowUp)?.TranscriptWhisper ?? transcript;

        string? aiFollowUpPrompt = null;
        if (hasExistingEval)
        {
            // Worker đã chấm xong trước: tái sử dụng FollowUpPrompt từ AiEvaluation, không gọi LLM trùng.
            if (!existingEvalSuspicious
                && !string.IsNullOrWhiteSpace(existingEvalPrompt)
                && question is not null
                && orderedCriteria.Count > 0)
            {
                var targetCriterion = orderedCriteria[(nextFollowUpIndex - 1) % orderedCriteria.Count];
                var validated = FollowUpPromptBuilder.ResolveFollowUpPrompt(
                    question.Course?.Code,
                    null,
                    existingEvalPrompt,
                    nextFollowUpIndex,
                    targetCriterion.CriterionName,
                    targetCriterion.Description,
                    orderedCriteria.Select(c => c.CriterionName),
                    question.Content,
                    question.SampleAnswer,
                    transcript,
                    originalTranscript,
                    previousFollowUpPrompts);

                if (!string.IsNullOrWhiteSpace(validated))
                {
                    aiFollowUpPrompt = validated.Trim();
                }
            }
        }
        else if (aiGradingService is not null && question is not null && orderedCriteria.Count > 0)
        {
            try
            {
                var criteriaContext = orderedCriteria.Select(c => new AiGradingCriterion
                {
                    Id = c.Id.ToString(),
                    Name = c.CriterionName,
                    Description = c.Description ?? string.Empty,
                    MaxScore = c.MaxScore,
                    Weight = c.Weight
                }).ToList();

                var aiRequest = new AiGradingRequest
                {
                    QuestionContent = question.Content,
                    SampleAnswer = question.SampleAnswer ?? string.Empty,
                    CourseCode = question.Course?.Code,
                    Transcript = transcript,
                    Criteria = criteriaContext,
                    QuestionImageUrl = question.ImageUrl,
                    QuestionImageDescription = question.ImageDescription,
                    IsFollowUp = submission.IsFollowUp,
                    FollowUpPrompt = submission.FollowUpPrompt,
                    FollowUpIndex = submission.FollowUpIndex,
                    OriginalAnswerTranscript = originalTranscript,
                    PreviousFollowUpPrompts = previousFollowUpPrompts,
                    ThrowOnTransientFailure = false
                };

                var gradingResult = await aiGradingService.GradeAnswerAsync(aiRequest, cancellationToken);
                if (!gradingResult.IsSuspicious && !string.IsNullOrWhiteSpace(gradingResult.FollowUpPrompt))
                {
                    var targetCriterion = orderedCriteria[(nextFollowUpIndex - 1) % orderedCriteria.Count];
                    var validated = FollowUpPromptBuilder.ResolveFollowUpPrompt(
                        question.Course?.Code,
                        null,
                        gradingResult.FollowUpPrompt,
                        nextFollowUpIndex,
                        targetCriterion.CriterionName,
                        targetCriterion.Description,
                        orderedCriteria.Select(c => c.CriterionName),
                        question.Content,
                        question.SampleAnswer,
                        transcript,
                        originalTranscript,
                        previousFollowUpPrompts);

                    if (!string.IsNullOrWhiteSpace(validated))
                    {
                        aiFollowUpPrompt = validated.Trim();
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogWarning(
                    ex,
                    "AI follow-up generation degraded gracefully for submission {SubmissionId}.",
                    submission.Id);
            }
        }

        if (string.IsNullOrWhiteSpace(aiFollowUpPrompt))
        {
            var releaseError = await ReleaseDeferredGradingJobAsync(
                context,
                request,
                submission.Id,
                transcript,
                sttTimestampsJson,
                markTranscribed: sttRanFromR2 && audioShaVerified && isVoiceOnly && !string.IsNullOrWhiteSpace(transcript),
                cancellationToken);
            if (releaseError is not null)
            {
                return Result<OfficialExamNextFollowUpResponse>.Failure(releaseError);
            }

            return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                HasMoreFollowUp: false,
                FollowUpIndex: submission.FollowUpIndex,
                MaxFollowUpQuestions: maxFollowUp,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.ExamQuestionId,
                FollowUpPrompt: null));
        }

        return await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            async ct =>
            {
                var nowUtc = DateTime.UtcNow;
                var revalidationError = await ValidateTrackedTicketForFollowUpAsync(context, request, nowUtc, ct);
                if (revalidationError is not null)
                {
                    return Result<OfficialExamNextFollowUpResponse>.Failure(revalidationError);
                }

                var tracked = await context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && s.TicketId == request.TicketId, ct);

                if (tracked is null)
                {
                    return Result<OfficialExamNextFollowUpResponse>.Failure("NOT_FOUND: Không tìm thấy bài nộp câu hỏi cho vé thi này.");
                }

                var dirty = false;
                // Store strictly in GeneratedFollowUpPrompt (NEVER FollowUpPrompt -> preserves CHECK 23514 on main rows)
                if (string.IsNullOrWhiteSpace(tracked.GeneratedFollowUpPrompt))
                {
                    tracked.GeneratedFollowUpPrompt = aiFollowUpPrompt;
                    if (string.Equals(tracked.GradingStatus, "pending", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(tracked.TranscriptWhisper)
                        && !string.IsNullOrWhiteSpace(transcript))
                    {
                        tracked.TranscriptWhisper = transcript;
                        tracked.InitialVoiceTranscript ??= transcript;
                        if (!string.IsNullOrWhiteSpace(sttTimestampsJson) && string.IsNullOrWhiteSpace(tracked.TimestampsWhisper))
                        {
                            tracked.TimestampsWhisper = sttTimestampsJson;
                        }

                        if (sttRanFromR2 && audioShaVerified && isVoiceOnly)
                        {
                            tracked.TranscriptEditRatio = 0m;
                            tracked.AudioVerifiedAt ??= nowUtc;
                            tracked.GradingStatus = "transcribed";
                        }
                    }

                    dirty = true;
                }

                var pendingJob = await context.GradingJobs
                    .FirstOrDefaultAsync(j => j.SubmissionId == tracked.Id, ct);
                if (pendingJob is not null
                    && string.Equals(pendingJob.Status, "pending", StringComparison.OrdinalIgnoreCase)
                    && pendingJob.NextAttemptAt.HasValue
                    && pendingJob.NextAttemptAt.Value > nowUtc)
                {
                    pendingJob.NextAttemptAt = nowUtc;
                    pendingJob.UpdatedAt = nowUtc;
                    dirty = true;
                }

                if (dirty)
                {
                    await context.SaveChangesAsync(ct);
                }

                return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                    HasMoreFollowUp: true,
                    FollowUpIndex: nextFollowUpIndex,
                    MaxFollowUpQuestions: maxFollowUp,
                    ParentSubmissionId: tracked.Id,
                    QuestionId: tracked.ExamQuestionId,
                    FollowUpPrompt: tracked.GeneratedFollowUpPrompt!.Trim()));
            },
            cancellationToken);
    }

    private static async Task<string?> ReleaseDeferredGradingJobAsync(
        IApplicationDbContext context,
        GenerateOfficialExamFollowUpCommand request,
        Guid submissionId,
        string? transcript,
        string? sttTimestampsJson,
        bool markTranscribed,
        CancellationToken cancellationToken)
    {
        return await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            async ct =>
            {
                var nowUtc = DateTime.UtcNow;
                var revalidationError = await ValidateTrackedTicketForFollowUpAsync(context, request, nowUtc, ct);
                if (revalidationError is not null)
                {
                    return revalidationError;
                }

                var dirty = false;

                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    var tracked = await context.ExamQuestionSubmissions
                        .FirstOrDefaultAsync(s => s.Id == submissionId && s.TicketId == request.TicketId, ct);
                    if (tracked is not null
                        && string.Equals(tracked.GradingStatus, "pending", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(tracked.TranscriptWhisper))
                    {
                        tracked.TranscriptWhisper = transcript;
                        tracked.InitialVoiceTranscript ??= transcript;
                        if (!string.IsNullOrWhiteSpace(sttTimestampsJson) && string.IsNullOrWhiteSpace(tracked.TimestampsWhisper))
                        {
                            tracked.TimestampsWhisper = sttTimestampsJson;
                        }

                        if (markTranscribed)
                        {
                            tracked.TranscriptEditRatio = 0m;
                            tracked.AudioVerifiedAt ??= nowUtc;
                            tracked.GradingStatus = "transcribed";
                        }

                        dirty = true;
                    }
                }

                var job = await context.GradingJobs
                    .FirstOrDefaultAsync(j => j.SubmissionId == submissionId, ct);
                if (job is not null
                    && string.Equals(job.Status, "pending", StringComparison.OrdinalIgnoreCase)
                    && job.NextAttemptAt.HasValue
                    && job.NextAttemptAt.Value > nowUtc)
                {
                    job.NextAttemptAt = nowUtc;
                    job.UpdatedAt = nowUtc;
                    dirty = true;
                }

                if (dirty)
                {
                    await context.SaveChangesAsync(ct);
                }

                return (string?)null;
            },
            cancellationToken);
    }

    private static async Task<string?> ValidateTrackedTicketForFollowUpAsync(
        IApplicationDbContext context,
        GenerateOfficialExamFollowUpCommand request,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var trackedTicket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Student)
            .Include(t => t.Shift)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, ct);

        if (trackedTicket is null)
        {
            return OfficialExamErrors.TicketNotFound;
        }

        if (trackedTicket.StudentId != request.ActorUserId
            || trackedTicket.Student.Role != UserRole.Student
            || !trackedTicket.Student.IsActive)
        {
            return OfficialExamErrors.Forbidden;
        }

        var revalidatedDeviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            trackedTicket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true, requireBoundDevice: true);
        if (revalidatedDeviceError is not null)
        {
            return revalidatedDeviceError;
        }

        if (trackedTicket.Status == ExamTicketStatus.Locked || trackedTicket.IsLocked)
        {
            return OfficialExamErrors.TicketLocked;
        }

        if (!string.Equals(trackedTicket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return OfficialExamErrors.TicketNotInProgress;
        }

        if (trackedTicket.Shift is null)
        {
            return "CONFLICT: Ca thi không tồn tại hoặc chưa mở phát đề.";
        }

        return ExamShiftWindow.ValidateActiveSubmissionWindow(trackedTicket.Shift, nowUtc);
    }
}
