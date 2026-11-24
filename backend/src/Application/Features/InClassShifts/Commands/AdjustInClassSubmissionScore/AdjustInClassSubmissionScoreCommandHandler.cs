using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
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

        var submissionLookup = await context.InClassExamSubmissions
            .AsNoTracking()
            .Where(s => s.Id == request.SubmissionId)
            .Select(s => new { s.ShiftId, s.StudentId })
            .FirstOrDefaultAsync(cancellationToken);

        if (submissionLookup is null)
        {
            return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
        }

        if (request.ShiftId.HasValue && submissionLookup.ShiftId != request.ShiftId.Value)
        {
            return Result<InClassExamSubmissionResponse>.Failure($"NOT_FOUND: {InClassShiftErrors.SubmissionNotFound}");
        }

        return await context.ExecuteInClassSubmissionAsync(
            submissionLookup.ShiftId,
            submissionLookup.StudentId,
            async ct =>
            {
                if (context is DbContext efContext)
                {
                    efContext.ChangeTracker.Clear();
                }

                var submission = await context.InClassExamSubmissions
                    .Include(s => s.Shift)
                        .ThenInclude(sh => sh.Class)
                    .Include(s => s.Student)
                    .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, ct);

                if (submission is null)
                {
                    return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
                }

                if (request.ShiftId.HasValue && submission.ShiftId != request.ShiftId.Value)
                {
                    return Result<InClassExamSubmissionResponse>.Failure($"NOT_FOUND: {InClassShiftErrors.SubmissionNotFound}");
                }

                if (submission.Shift.IsLocked || submission.Shift.Status == "FINALIZED")
                {
                    return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.ShiftLocked);
                }

                bool isAssigned = submission.Shift.Class.LecturerId == actor.Id;

                if (!isAssigned)
                {
                    return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
                }

                submission.FinalScore = Math.Round(request.FinalScore, 2, MidpointRounding.AwayFromZero);
                submission.LecturerNotes = request.LecturerNotes?.Trim();
                submission.Status = "AUDITED";
                submission.GradedAt = DateTime.UtcNow;

                var participant = await context.InClassShiftParticipants
                    .FirstOrDefaultAsync(p => p.ShiftId == submission.ShiftId && p.StudentId == submission.StudentId, ct);

                if (participant is not null)
                {
                    var studentSubmissions = await context.InClassExamSubmissions
                        .AsNoTracking()
                        .Where(s => s.ShiftId == submission.ShiftId && s.StudentId == submission.StudentId && s.Id != submission.Id)
                        .ToListAsync(ct);

                    var allSubmissions = studentSubmissions.Append(submission).ToList();
                    var questionGroups = allSubmissions.GroupBy(s => s.QuestionId).ToList();

                    var effectiveAiScores = new List<decimal>();
                    var effectiveFinalScores = new List<decimal>();

                    foreach (var group in questionGroups)
                    {
                        var mainSub = group.FirstOrDefault(s => !s.IsFollowUp) ?? group.First();
                        var followUps = group.Where(s => s.IsFollowUp && s.Id != mainSub.Id).ToList();

                        // Đồng bộ công thức với GradingQueueWorker: chỉ tổng hợp câu đã có điểm,
                        // tuyệt đối không ép 0m giả cho các câu đang chờ AI chấm song song.
                        if (mainSub.AiScore.HasValue)
                        {
                            decimal bestAi = mainSub.AiScore.Value;
                            foreach (var fu in followUps)
                            {
                                if (fu.AiScore.HasValue && fu.AiScore.Value > bestAi)
                                {
                                    bestAi = fu.AiScore.Value;
                                }
                            }
                            effectiveAiScores.Add(bestAi);
                        }
                        else
                        {
                            var anyFuAi = followUps
                                .Where(f => f.AiScore.HasValue)
                                .Select(f => f.AiScore!.Value)
                                .ToList();
                            if (anyFuAi.Count > 0)
                            {
                                effectiveAiScores.Add(anyFuAi.Max());
                            }
                        }

                        decimal? mainFinal = mainSub.FinalScore ?? mainSub.AiScore;
                        if (mainFinal.HasValue)
                        {
                            decimal bestFinal = mainFinal.Value;
                            foreach (var fu in followUps)
                            {
                                var fuFinal = fu.FinalScore ?? fu.AiScore;
                                if (fuFinal.HasValue && fuFinal.Value > bestFinal)
                                {
                                    bestFinal = fuFinal.Value;
                                }
                            }
                            effectiveFinalScores.Add(bestFinal);
                        }
                        else
                        {
                            var anyFuFinal = followUps
                                .Select(f => f.FinalScore ?? f.AiScore)
                                .Where(sc => sc.HasValue)
                                .Select(sc => sc!.Value)
                                .ToList();
                            if (anyFuFinal.Count > 0)
                            {
                                effectiveFinalScores.Add(anyFuFinal.Max());
                            }
                        }
                    }

                    if (effectiveAiScores.Count > 0)
                    {
                        participant.TotalAiScore = Math.Round(effectiveAiScores.Average(), 2, MidpointRounding.AwayFromZero);
                    }

                    if (effectiveFinalScores.Count > 0)
                    {
                        participant.TotalFinalScore = Math.Round(effectiveFinalScores.Average(), 2, MidpointRounding.AwayFromZero);
                    }

                    if (string.Equals(participant.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase))
                    {
                        var assignedIds = InClassShiftPoolConfig.ParseGuidList(participant.AssignedQuestionIdsJson);
                        if (assignedIds.Count == 0 && participant.AssignedQuestionId.HasValue)
                        {
                            assignedIds.Add(participant.AssignedQuestionId.Value);
                        }

                        var mainSubmittedIds = allSubmissions
                            .Where(s => !s.IsFollowUp)
                            .Select(s => s.QuestionId)
                            .ToHashSet();
                        var allMainAnswered = assignedIds.Count == 0 || assignedIds.All(mainSubmittedIds.Contains);
                        var allScored = allSubmissions.Count > 0 && allSubmissions.All(s =>
                            !string.Equals(s.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) &&
                            (s.FinalScore.HasValue || s.AiScore.HasValue));
                        if (allMainAnswered && allScored)
                        {
                            participant.Status = "GRADED";
                        }
                    }
                }

                await context.SaveChangesAsync(ct);

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
            },
            cancellationToken);
    }
}
