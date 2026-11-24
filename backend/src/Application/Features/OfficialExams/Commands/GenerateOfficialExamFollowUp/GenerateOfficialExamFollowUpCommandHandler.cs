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
            ticket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true);
        if (deviceError is not null)
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure(deviceError);
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

        var question = await context.ExamQuestions
            .AsNoTracking()
            .Include(q => q.Course)
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(q => q.Id == submission.ExamQuestionId, cancellationToken);

        var transcript = submission.TranscriptWhisper?.Trim() ?? string.Empty;

        // VoiceOnly / empty transcript fallback: run Whisper STT from R2 audio stream
        if (string.IsNullOrWhiteSpace(transcript) && storageService is not null && speechToTextService is not null)
        {
            var audioKey = !string.IsNullOrWhiteSpace(submission.AudioObjectKey)
                ? submission.AudioObjectKey
                : !string.IsNullOrWhiteSpace(submission.AudioR2Url)
                    ? submission.AudioR2Url
                    : ticket.AudioObjectKey;

            if (!string.IsNullOrWhiteSpace(audioKey) && !Uri.TryCreate(audioKey, UriKind.Absolute, out _))
            {
                try
                {
                    await using var stream = await storageService.OpenReadStreamAsync(audioKey, cancellationToken);
                    var stt = await speechToTextService.TranscribeDetailedAsync(
                        stream,
                        Path.GetFileName(audioKey),
                        question?.Course?.Code,
                        cancellationToken);
                    transcript = (stt.Text ?? string.Empty).Trim();
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

        string? aiFollowUpPrompt = null;
        if (aiGradingService is not null && question is not null && orderedCriteria.Count > 0)
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

                var originalTranscript = priorSubmissions
                    .FirstOrDefault(s => !s.IsFollowUp)?.TranscriptWhisper ?? transcript;

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
            return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
                HasMoreFollowUp: false,
                FollowUpIndex: submission.FollowUpIndex,
                MaxFollowUpQuestions: maxFollowUp,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.ExamQuestionId,
                FollowUpPrompt: null));
        }

        var savedPrompt = await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            async ct =>
            {
                var tracked = await context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && s.TicketId == request.TicketId, ct);

                if (tracked is null)
                {
                    return (string?)null;
                }

                // Store strictly in GeneratedFollowUpPrompt (NEVER FollowUpPrompt -> preserves CHECK 23514 on main rows)
                if (string.IsNullOrWhiteSpace(tracked.GeneratedFollowUpPrompt))
                {
                    tracked.GeneratedFollowUpPrompt = aiFollowUpPrompt;
                    if (string.IsNullOrWhiteSpace(tracked.TranscriptWhisper) && !string.IsNullOrWhiteSpace(transcript))
                    {
                        tracked.TranscriptWhisper = transcript;
                        tracked.InitialVoiceTranscript ??= transcript;
                    }
                    await context.SaveChangesAsync(ct);
                }

                return tracked.GeneratedFollowUpPrompt!.Trim();
            },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(savedPrompt))
        {
            return Result<OfficialExamNextFollowUpResponse>.Failure("NOT_FOUND: Không tìm thấy bài nộp câu hỏi cho vé thi này.");
        }

        return Result<OfficialExamNextFollowUpResponse>.Success(new OfficialExamNextFollowUpResponse(
            HasMoreFollowUp: true,
            FollowUpIndex: nextFollowUpIndex,
            MaxFollowUpQuestions: maxFollowUp,
            ParentSubmissionId: submission.Id,
            QuestionId: submission.ExamQuestionId,
            FollowUpPrompt: savedPrompt));
    }
}
