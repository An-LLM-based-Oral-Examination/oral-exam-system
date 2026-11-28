using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetNextInClassFollowUp;

public sealed class GetNextInClassFollowUpQueryHandler(
    IApplicationDbContext context,
    IAiGradingService? aiGradingService = null,
    ILogger<GetNextInClassFollowUpQueryHandler>? logger = null)
    : IRequestHandler<GetNextInClassFollowUpQuery, Result<InClassNextFollowUpResponse>>
{
    public async Task<Result<InClassNextFollowUpResponse>> Handle(
        GetNextInClassFollowUpQuery request,
        CancellationToken cancellationToken)
    {
        var submission = await context.InClassExamSubmissions
            .AsNoTracking()
            .Include(s => s.Shift)
                .ThenInclude(sh => sh.Class)
                    .ThenInclude(cl => cl.Course)
            .Include(s => s.Shift)
                .ThenInclude(sh => sh.Class)
                    .ThenInclude(cl => cl.CourseOffering)
            .Include(s => s.ParentSubmission)
            .FirstOrDefaultAsync(
                s => s.Id == request.SubmissionId && s.ShiftId == request.ShiftId && s.StudentId == request.StudentUserId,
                cancellationToken);

        if (submission?.Shift is null)
        {
            return Result<InClassNextFollowUpResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
        }

        var shift = submission.Shift;
        if (!InClassShiftAcademicContext.IsOperational(shift.Class))
        {
            return Result<InClassNextFollowUpResponse>.Failure(InClassShiftErrors.ClassNotFound);
        }

        if (!shift.HasFollowUp || shift.MaxFollowUpQuestions <= 0 || submission.FollowUpIndex >= shift.MaxFollowUpQuestions)
        {
            return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                HasMoreFollowUp: false,
                FollowUpIndex: submission.FollowUpIndex,
                MaxFollowUpQuestions: shift.MaxFollowUpQuestions,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.QuestionId,
                FollowUpPrompt: null));
        }

        int nextFollowUpIndex = submission.FollowUpIndex + 1;

        var utcNow = DateTime.UtcNow;
        var shiftClosed = shift.Status is "CLOSED" or "ENDED" ||
            (InClassShiftLifecycle.IsClosable(shift.Status) && shift.EndTime <= utcNow);
        if (shift.IsLocked ||
            shift.Status == "FINALIZED" ||
            (shiftClosed && !InClassShiftLifecycle.CanAcceptLateAnswer(shift, utcNow)) ||
            (!shiftClosed && shift.Status != "IN_PROGRESS"))
        {
            return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                HasMoreFollowUp: false,
                FollowUpIndex: submission.FollowUpIndex,
                MaxFollowUpQuestions: shift.MaxFollowUpQuestions,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.QuestionId,
                FollowUpPrompt: null));
        }

        var participant = await context.InClassShiftParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.ShiftId == shift.Id && p.StudentId == request.StudentUserId,
                cancellationToken);

        if (participant is null)
        {
            return Result<InClassNextFollowUpResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
        }

        if (participant.FinishedAt.HasValue ||
            string.Equals(participant.Status, "GRADED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(participant.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(participant.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(participant.Status, "ABSENT", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(participant.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrWhiteSpace(submission.Transcript)))
        {
            return Result<InClassNextFollowUpResponse>.Failure($"CONFLICT: {InClassShiftErrors.AlreadyParticipated}");
        }

        if (InClassShiftLifecycle.IsPersonalExamAnswerWindowExpired(participant, shift, utcNow))
        {
            return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                HasMoreFollowUp: false,
                FollowUpIndex: submission.FollowUpIndex,
                MaxFollowUpQuestions: shift.MaxFollowUpQuestions,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.QuestionId,
                FollowUpPrompt: null));
        }

        if (string.IsNullOrWhiteSpace(submission.FollowUpPrompt))
        {
            // Nếu GradingQueueWorker đã chấm xong (AI_GRADED/AUDITED) mà không sinh câu phụ -> không gọi LLM lại lần 2.
            if (string.Equals(submission.Status, "AI_GRADED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(submission.Status, "AUDITED", StringComparison.OrdinalIgnoreCase))
            {
                return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                    HasMoreFollowUp: false,
                    FollowUpIndex: submission.FollowUpIndex,
                    MaxFollowUpQuestions: shift.MaxFollowUpQuestions,
                    ParentSubmissionId: submission.Id,
                    QuestionId: submission.QuestionId,
                    FollowUpPrompt: null));
            }
            var question = await context.PracticeQuestions
                .AsNoTracking()
                .Include(q => q.Course)
                .Include(q => q.Rubric)
                    .ThenInclude(r => r!.Criteria)
                .FirstOrDefaultAsync(q => q.Id == submission.QuestionId, cancellationToken);

            var priorSubmissions = await context.InClassExamSubmissions
                .AsNoTracking()
                .Where(s => s.ShiftId == request.ShiftId && s.StudentId == request.StudentUserId && s.QuestionId == submission.QuestionId)
                .OrderBy(s => s.FollowUpIndex)
                .ToListAsync(cancellationToken);

            var previousFollowUpPrompts = priorSubmissions
                .Select(s => s.FollowUpPrompt)
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
                    var criteriaContext = orderedCriteria
                        .Select(c => new AiGradingCriterion
                        {
                            Id = c.Id.ToString(),
                            Name = c.CriterionName,
                            Description = c.Description ?? string.Empty,
                            MaxScore = c.MaxScore,
                            Weight = c.Weight
                        })
                        .ToList();

                    string? answeredFollowUpPrompt = submission.IsFollowUp
                        ? (!string.IsNullOrWhiteSpace(submission.ParentSubmission?.FollowUpPrompt)
                            ? submission.ParentSubmission!.FollowUpPrompt!.Trim()
                            : previousFollowUpPrompts.LastOrDefault())
                        : null;

                    var aiRequest = new AiGradingRequest
                    {
                        QuestionContent = question.Content,
                        SampleAnswer = question.SampleAnswer ?? string.Empty,
                        CourseCode = question.Course?.Code,
                        Transcript = submission.Transcript,
                        Criteria = criteriaContext,
                        QuestionImageUrl = question.ImageUrl,
                        QuestionImageDescription = question.ImageDescription,
                        IsFollowUp = submission.IsFollowUp,
                        FollowUpPrompt = answeredFollowUpPrompt,
                        FollowUpIndex = submission.FollowUpIndex,
                        OriginalAnswerTranscript = priorSubmissions.FirstOrDefault(s => !s.IsFollowUp)?.Transcript ?? submission.Transcript,
                        PreviousFollowUpPrompts = previousFollowUpPrompts,
                        ThrowOnTransientFailure = false
                    };

                    var gradingResult = await aiGradingService.GradeAnswerAsync(aiRequest, cancellationToken);
                    if (!gradingResult.IsSuspicious && !string.IsNullOrWhiteSpace(gradingResult.FollowUpPrompt))
                    {
                        var targetCriterion = orderedCriteria.Count > 0
                            ? orderedCriteria[(nextFollowUpIndex - 1) % orderedCriteria.Count]
                            : null;

                        var validatedPrompt = FollowUpPromptBuilder.ResolveFollowUpPrompt(
                            question.Course?.Code,
                            null,
                            gradingResult.FollowUpPrompt,
                            nextFollowUpIndex,
                            targetCriterion?.CriterionName,
                            targetCriterion?.Description,
                            orderedCriteria.Select(c => c.CriterionName),
                            question.Content,
                            question.SampleAnswer,
                            submission.Transcript,
                            priorSubmissions.FirstOrDefault(s => !s.IsFollowUp)?.Transcript ?? submission.Transcript,
                            previousFollowUpPrompts);

                        if (!string.IsNullOrWhiteSpace(validatedPrompt))
                        {
                            aiFollowUpPrompt = validatedPrompt;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Khi LLM gián đoạn tạm thời, không sinh câu mẫu viết sẵn; trả HasMoreFollowUp = false để SV chuyển câu kế tiếp
                    logger?.LogWarning(
                        ex,
                        "AI follow-up generation degraded gracefully for in-class submission {SubmissionId}.",
                        submission.Id);
                }
            }

            if (string.IsNullOrWhiteSpace(aiFollowUpPrompt))
            {
                return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                    HasMoreFollowUp: false,
                    FollowUpIndex: submission.FollowUpIndex,
                    MaxFollowUpQuestions: shift.MaxFollowUpQuestions,
                    ParentSubmissionId: submission.Id,
                    QuestionId: submission.QuestionId,
                    FollowUpPrompt: null));
            }

            var resolvedFollowUpPrompt = aiFollowUpPrompt;

            var persistResult = await context.ExecuteInClassSubmissionAsync(
                request.ShiftId,
                request.StudentUserId,
                async ct =>
                {
                    var latestSubmission = await context.InClassExamSubmissions
                        .Include(s => s.Shift)
                            .ThenInclude(sh => sh.Class)
                                .ThenInclude(cl => cl.Course)
                        .Include(s => s.Shift)
                            .ThenInclude(sh => sh.Class)
                                .ThenInclude(cl => cl.CourseOffering)
                        .FirstOrDefaultAsync(
                            s => s.Id == request.SubmissionId && s.ShiftId == request.ShiftId && s.StudentId == request.StudentUserId,
                            ct);

                    if (latestSubmission?.Shift is null)
                    {
                        return Result<InClassNextFollowUpResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
                    }

                    if (!InClassShiftAcademicContext.IsOperational(latestSubmission.Shift.Class))
                    {
                        return Result<InClassNextFollowUpResponse>.Failure(InClassShiftErrors.ClassNotFound);
                    }

                    var lockNow = DateTime.UtcNow;
                    var lockShiftClosed = latestSubmission.Shift.Status is "CLOSED" or "ENDED" ||
                        (InClassShiftLifecycle.IsClosable(latestSubmission.Shift.Status) && latestSubmission.Shift.EndTime <= lockNow);
                    if (latestSubmission.Shift.IsLocked ||
                        latestSubmission.Shift.Status == "FINALIZED" ||
                        (lockShiftClosed && !InClassShiftLifecycle.CanAcceptLateAnswer(latestSubmission.Shift, lockNow)) ||
                        (!lockShiftClosed && latestSubmission.Shift.Status != "IN_PROGRESS"))
                    {
                        return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                            HasMoreFollowUp: false,
                            FollowUpIndex: latestSubmission.FollowUpIndex,
                            MaxFollowUpQuestions: latestSubmission.Shift.MaxFollowUpQuestions,
                            ParentSubmissionId: latestSubmission.Id,
                            QuestionId: latestSubmission.QuestionId,
                            FollowUpPrompt: null));
                    }

                    var latestParticipant = await context.InClassShiftParticipants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            p => p.ShiftId == request.ShiftId && p.StudentId == request.StudentUserId,
                            ct);

                    if (latestParticipant is null)
                    {
                        return Result<InClassNextFollowUpResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
                    }

                    if (latestParticipant.FinishedAt.HasValue ||
                        string.Equals(latestParticipant.Status, "GRADED", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(latestParticipant.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(latestParticipant.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(latestParticipant.Status, "ABSENT", StringComparison.OrdinalIgnoreCase) ||
                        (string.Equals(latestParticipant.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) &&
                         string.IsNullOrWhiteSpace(latestSubmission.Transcript)))
                    {
                        return Result<InClassNextFollowUpResponse>.Failure($"CONFLICT: {InClassShiftErrors.AlreadyParticipated}");
                    }

                    if (InClassShiftLifecycle.IsPersonalExamAnswerWindowExpired(latestParticipant, latestSubmission.Shift, lockNow))
                    {
                        return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                            HasMoreFollowUp: false,
                            FollowUpIndex: latestSubmission.FollowUpIndex,
                            MaxFollowUpQuestions: latestSubmission.Shift.MaxFollowUpQuestions,
                            ParentSubmissionId: latestSubmission.Id,
                            QuestionId: latestSubmission.QuestionId,
                            FollowUpPrompt: null));
                    }

                    if (string.IsNullOrWhiteSpace(latestSubmission.FollowUpPrompt))
                    {
                        if (string.Equals(latestSubmission.Status, "AI_GRADED", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(latestSubmission.Status, "AUDITED", StringComparison.OrdinalIgnoreCase))
                        {
                            return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                                HasMoreFollowUp: false,
                                FollowUpIndex: latestSubmission.FollowUpIndex,
                                MaxFollowUpQuestions: latestSubmission.Shift.MaxFollowUpQuestions,
                                ParentSubmissionId: latestSubmission.Id,
                                QuestionId: latestSubmission.QuestionId,
                                FollowUpPrompt: null));
                        }

                        latestSubmission.FollowUpPrompt = resolvedFollowUpPrompt;
                        await context.SaveChangesAsync(ct);
                    }

                    return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                        HasMoreFollowUp: true,
                        FollowUpIndex: nextFollowUpIndex,
                        MaxFollowUpQuestions: latestSubmission.Shift.MaxFollowUpQuestions,
                        ParentSubmissionId: latestSubmission.Id,
                        QuestionId: latestSubmission.QuestionId,
                        FollowUpPrompt: latestSubmission.FollowUpPrompt));
                },
                cancellationToken);

            return persistResult;
        }

        return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
            HasMoreFollowUp: true,
            FollowUpIndex: nextFollowUpIndex,
            MaxFollowUpQuestions: shift.MaxFollowUpQuestions,
            ParentSubmissionId: submission.Id,
            QuestionId: submission.QuestionId,
            FollowUpPrompt: submission.FollowUpPrompt));
    }
}
