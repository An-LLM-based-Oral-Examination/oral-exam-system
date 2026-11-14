using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;

public sealed class AdjustInClassSubmissionScoreCommandHandler(
    IApplicationDbContext context,
    IValidator<AdjustInClassSubmissionScoreCommand> validator)
    : IRequestHandler<AdjustInClassSubmissionScoreCommand, Result<InClassExamSubmissionResponse>>
{
    public async Task<Result<InClassExamSubmissionResponse>> Handle(
        AdjustInClassSubmissionScoreCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<InClassExamSubmissionResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role != UserRole.Lecturer)
        {
            return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var submission = await context.InClassExamSubmissions
            .Include(s => s.Shift)
                .ThenInclude(sh => sh.Class)
            .Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission is null)
        {
            return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
        }

        if (submission.Shift.IsLocked || submission.Shift.Status == "FINALIZED")
        {
            return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.ShiftLocked);
        }

        bool isAssigned = submission.Shift.LecturerId == actor.Id ||
            submission.Shift.Class.LecturerId == actor.Id ||
            await context.CourseLecturers.AnyAsync(
                cl => cl.CourseId == submission.Shift.Class.CourseId && cl.LecturerUserId == actor.Id,
                cancellationToken);

        if (!isAssigned)
        {
            return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        submission.FinalScore = Math.Round(request.FinalScore, 2, MidpointRounding.AwayFromZero);
        submission.LecturerNotes = request.LecturerNotes;
        submission.Status = "AUDITED";
        submission.GradedAt = DateTime.UtcNow;

        var participant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(p => p.ShiftId == submission.ShiftId && p.StudentId == submission.StudentId, cancellationToken);

        if (participant is not null)
        {
            var studentSubmissions = await context.InClassExamSubmissions
                .AsNoTracking()
                .Where(s => s.ShiftId == submission.ShiftId && s.StudentId == submission.StudentId && s.Id != submission.Id)
                .ToListAsync(cancellationToken);

            var allSubmissions = studentSubmissions.Append(submission).ToList();

            var effectiveQuestionScores = allSubmissions
                .GroupBy(s => s.QuestionId)
                .Select(group => group
                    .Select(s => s.FinalScore ?? s.AiScore)
                    .Where(score => score.HasValue)
                    .Select(score => score!.Value)
                    .DefaultIfEmpty(0m)
                    .Max())
                .ToList();

            if (effectiveQuestionScores.Count > 0)
            {
                participant.TotalFinalScore = Math.Round(effectiveQuestionScores.Average(), 2, MidpointRounding.AwayFromZero);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var response = new InClassExamSubmissionResponse(
            submission.Id,
            submission.ShiftId,
            submission.StudentId,
            submission.Student?.FullName,
            submission.Student?.Email,
            submission.QuestionId,
            submission.Transcript,
            submission.AiScore,
            submission.AiFeedback,
            submission.FinalScore,
            submission.LecturerNotes,
            submission.Status,
            submission.SubmittedAt,
            submission.GradedAt,
            IsFollowUp: submission.IsFollowUp,
            ParentSubmissionId: submission.ParentSubmissionId,
            FollowUpIndex: submission.FollowUpIndex,
            FollowUpPrompt: submission.FollowUpPrompt,
            AiConfidenceScore: submission.AiConfidenceScore,
            StudentCode: submission.Student?.StudentCode
        );

        return Result<InClassExamSubmissionResponse>.Success(response);
    }
}
