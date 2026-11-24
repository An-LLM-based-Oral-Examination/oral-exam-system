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
    public Task<Result<FinalizeInClassGradesResponse>> Handle(
        FinalizeInClassShiftGradesCommand request,
        CancellationToken cancellationToken) =>
        context.ExecuteShiftMutationAsync(
            request.ShiftId,
            isOfficial: false,
            async ct =>
            {
                var actor = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, ct);

                if (actor is null || !actor.IsActive || actor.Role != UserRole.Lecturer)
                {
                    return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
                }

                var shift = await context.InClassExamShifts
                    .Include(s => s.Class)
                        .ThenInclude(c => c.Course)
                    .Include(s => s.Participants)
                    .Include(s => s.Submissions)
                    .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

                if (shift is null)
                {
                    return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.ShiftNotFound);
                }

                bool isAssigned = shift.Class.LecturerId == actor.Id;

                if (!isAssigned)
                {
                    return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
                }

                if (shift.IsLocked || shift.Status == "FINALIZED")
                {
                    return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.ShiftLocked);
                }

                if (shift.Status != "CLOSED")
                {
                    return Result<FinalizeInClassGradesResponse>.Failure(InClassShiftErrors.ShiftMustBeClosedBeforeFinalizing);
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

                var nowUtc = DateTime.UtcNow;
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
                    participant.FinishedAt ??= nowUtc;
                }

                shift.Status = "FINALIZED";
                shift.IsLocked = true;
                shift.FinalizedAt = nowUtc;
                shift.UpdatedAt = nowUtc;

                await context.SaveChangesAsync(ct);

                var response = new FinalizeInClassGradesResponse(
                    shift.Id,
                    shift.ShiftName,
                    shift.Status,
                    shift.IsLocked,
                    shift.FinalizedAt
                );

                return Result<FinalizeInClassGradesResponse>.Success(response);
            },
            cancellationToken);
}
