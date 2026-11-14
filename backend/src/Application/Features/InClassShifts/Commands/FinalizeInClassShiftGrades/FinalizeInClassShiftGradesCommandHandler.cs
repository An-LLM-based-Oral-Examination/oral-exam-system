using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;

public sealed class FinalizeInClassShiftGradesCommandHandler(
    IApplicationDbContext context)
    : IRequestHandler<FinalizeInClassShiftGradesCommand, Result<FinalizeInClassGradesResponse>>
{
    public async Task<Result<FinalizeInClassGradesResponse>> Handle(
        FinalizeInClassShiftGradesCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role != UserRole.Lecturer)
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
                .ThenInclude(c => c.Course)
            .Include(s => s.Participants)
            .Include(s => s.Submissions)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (shift.IsLocked || shift.Status == "FINALIZED")
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.ShiftLocked);
        }

        if (shift.Status != "CLOSED")
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.ShiftMustBeClosedBeforeFinalizing);
        }

        bool isAssigned = shift.LecturerId == actor.Id ||
            shift.Class.LecturerId == actor.Id ||
            await context.CourseLecturers.AnyAsync(
                cl => cl.CourseId == shift.Class.CourseId && cl.LecturerUserId == actor.Id,
                cancellationToken);

        if (!isAssigned)
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var hasPendingSubmission = shift.Submissions.Any(submission => !submission.FinalScore.HasValue);
        var hasIncompleteParticipant = shift.Participants
            .Where(participant => !string.Equals(participant.Status, "ABSENT", StringComparison.OrdinalIgnoreCase))
            .Any(participant =>
            {
                var assignedIds = EnterInClassShiftCommandHandler.ParseGuidList(participant.AssignedQuestionIdsJson);
                if (assignedIds.Count == 0 && participant.AssignedQuestionId.HasValue)
                {
                    assignedIds.Add(participant.AssignedQuestionId.Value);
                }

                if (assignedIds.Count == 0)
                {
                    return !shift.Submissions.Any(submission =>
                        submission.StudentId == participant.StudentId && !submission.IsFollowUp);
                }

                var submittedMainIds = shift.Submissions
                    .Where(submission => submission.StudentId == participant.StudentId && !submission.IsFollowUp)
                    .Select(submission => submission.QuestionId)
                    .Distinct()
                    .ToHashSet();

                return assignedIds.Any(questionId => !submittedMainIds.Contains(questionId));
            });

        if (hasPendingSubmission || hasIncompleteParticipant)
        {
            return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.PendingGradingBeforeFinalizing);
        }

        foreach (var participant in shift.Participants)
        {
            if (string.Equals(participant.Status, "ABSENT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var effectiveQuestionScores = shift.Submissions
                .Where(submission => submission.StudentId == participant.StudentId)
                .GroupBy(submission => submission.QuestionId)
                .Select(group => group
                    .Select(submission => submission.FinalScore ?? submission.AiScore)
                    .Where(score => score.HasValue)
                    .Select(score => score!.Value)
                    .DefaultIfEmpty(0m)
                    .Max())
                .ToList();

            participant.TotalFinalScore = effectiveQuestionScores.Count == 0
                ? 0m
                : Math.Round(effectiveQuestionScores.Average(), 2, MidpointRounding.AwayFromZero);
            participant.Status = "FINALIZED";
        }

        shift.Status = "FINALIZED";
        shift.IsLocked = true;
        shift.FinalizedAt = DateTime.UtcNow;
        shift.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        var response = new FinalizeInClassGradesResponse(
            shift.Id,
            shift.ShiftName,
            shift.Status,
            shift.IsLocked,
            shift.FinalizedAt
        );

        return Result<FinalizeInClassGradesResponse>.Success(response);
    }
}
