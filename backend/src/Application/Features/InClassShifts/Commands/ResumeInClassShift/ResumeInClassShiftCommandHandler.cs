using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
namespace OralExamination.Application.Features.InClassShifts.Commands.ResumeInClassShift;

public sealed class ResumeInClassShiftCommandHandler(
    IApplicationDbContext context,
    IGradingQueueChannel gradingQueue)
    : IRequestHandler<ResumeInClassShiftCommand, Result<JoinInClassShiftResponse>>
{
    public async Task<Result<JoinInClassShiftResponse>> Handle(
        ResumeInClassShiftCommand request,
        CancellationToken cancellationToken)
    {
        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
                .ThenInclude(c => c.Course)
            .Include(s => s.Class)
                .ThenInclude(c => c.CourseOffering)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (!InClassShiftAcademicContext.IsOperational(shift.Class))
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ClassNotFound);
        }

        if (InClassShiftLifecycle.TryCloseIfDue(shift, DateTime.UtcNow))
        {
            await context.SaveChangesAsync(cancellationToken);
            await InClassShiftAutoSubmit.SubmitMissingAsync(
                context,
                gradingQueue,
                cancellationToken,
                shift.Id);
        }

        if (shift.Status is "CLOSED" or "ENDED")
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ShiftClosed);
        }

        if (shift.IsLocked || shift.Status == "FINALIZED")
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ShiftLocked);
        }

        if (shift.Status != "IN_PROGRESS")
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotOpen);
        }

        bool isEnrolled = await context.ClassEnrollments
            .AsNoTracking()
            .AnyAsync(
                e => e.ClassId == shift.ClassId && e.StudentId == request.StudentId && e.Status == "enrolled",
                cancellationToken);

        if (!isEnrolled)
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.StudentNotEnrolled);
        }

        var participant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(
                p => p.ShiftId == shift.Id && p.StudentId == request.StudentId,
                cancellationToken);

        if (participant is null)
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ParticipantNotFound);
        }

        var utcNow = DateTime.UtcNow;
        var assignedIds = EnterInClassShiftCommandHandler.ParseGuidList(participant.AssignedQuestionIdsJson);
        if (assignedIds.Count == 0 && participant.AssignedQuestionId.HasValue)
        {
            assignedIds.Add(participant.AssignedQuestionId.Value);
        }

        var questionDtos = await LoadQuestionDtosAsync(assignedIds, cancellationToken);
        var examEndsAt = InClassShiftLifecycle.GetPersonalExamEndsAt(participant, shift);

        if (EnterInClassShiftCommandHandler.IsCompletedParticipation(participant.Status))
        {
            return Result<JoinInClassShiftResponse>.Success(new JoinInClassShiftResponse(
                shift.Id,
                participant.StudentId,
                shift.ShiftName,
                participant.Status,
                participant.JoinedAt,
                participant.AssignedQuestionId,
                questionDtos.FirstOrDefault(),
                assignedIds,
                questionDtos,
                Math.Clamp(shift.TotalQuestionsPerStudent <= 0 ? 1 : shift.TotalQuestionsPerStudent, 1, 10),
                shift.HasFollowUp,
                shift.HasFollowUp ? shift.MaxFollowUpQuestions : 0,
                examEndsAt,
                0,
                TimedOut: false));
        }

        if (InClassShiftLifecycle.IsPersonalExamTimeExpired(participant, shift, utcNow))
        {
            await InClassShiftAutoSubmit.FinalizeParticipantAsync(
                context,
                gradingQueue,
                participant,
                cancellationToken);

            return Result<JoinInClassShiftResponse>.Success(new JoinInClassShiftResponse(
                shift.Id,
                participant.StudentId,
                shift.ShiftName,
                participant.Status,
                participant.JoinedAt,
                participant.AssignedQuestionId,
                questionDtos.FirstOrDefault(),
                assignedIds,
                questionDtos,
                Math.Clamp(shift.TotalQuestionsPerStudent <= 0 ? 1 : shift.TotalQuestionsPerStudent, 1, 10),
                shift.HasFollowUp,
                shift.HasFollowUp ? shift.MaxFollowUpQuestions : 0,
                examEndsAt,
                0,
                TimedOut: true));
        }

        return Result<JoinInClassShiftResponse>.Success(new JoinInClassShiftResponse(
            shift.Id,
            participant.StudentId,
            shift.ShiftName,
            participant.Status,
            participant.JoinedAt,
            participant.AssignedQuestionId,
            questionDtos.FirstOrDefault(),
            assignedIds,
            questionDtos,
            Math.Clamp(shift.TotalQuestionsPerStudent <= 0 ? 1 : shift.TotalQuestionsPerStudent, 1, 10),
            shift.HasFollowUp,
            shift.HasFollowUp ? shift.MaxFollowUpQuestions : 0,
            examEndsAt,
            InClassShiftLifecycle.GetRemainingExamSeconds(examEndsAt, utcNow),
            TimedOut: false));
    }

    private async Task<List<InClassAssignedQuestionDto>> LoadQuestionDtosAsync(
        IReadOnlyList<Guid> questionIds,
        CancellationToken cancellationToken)
    {
        if (questionIds.Count == 0)
        {
            return new List<InClassAssignedQuestionDto>();
        }

        var rawQuestions = await context.PracticeQuestions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.Id))
            .Select(q => new InClassAssignedQuestionDto(
                q.Id,
                q.Title,
                q.Content,
                q.Difficulty.ToString().ToLowerInvariant(),
                q.BloomLevel.ToString().ToLowerInvariant()))
            .ToListAsync(cancellationToken);

        var dict = rawQuestions.ToDictionary(q => q.QuestionId);
        var ordered = new List<InClassAssignedQuestionDto>(questionIds.Count);
        foreach (var id in questionIds)
        {
            if (dict.TryGetValue(id, out var dto))
            {
                ordered.Add(dto);
            }
        }

        return ordered;
    }
}
