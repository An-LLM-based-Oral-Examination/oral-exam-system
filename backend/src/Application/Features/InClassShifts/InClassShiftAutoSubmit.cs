using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.InClassShifts;

public static class InClassShiftAutoSubmit
{
    public static async Task<int> SubmitMissingAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        CancellationToken cancellationToken,
        Guid? shiftId = null)
    {
        var participants = await context.InClassShiftParticipants
            .Where(participant => participant.Status != "SUBMITTED")
            .Where(participant => context.InClassExamShifts.Any(shift =>
                shift.Id == participant.ShiftId &&
                shift.Status == "CLOSED" &&
                !shift.IsLocked &&
                (shiftId == null || shift.Id == shiftId)))
            .ToListAsync(cancellationToken);

        if (participants.Count == 0)
        {
            return 0;
        }

        var shiftIds = participants.Select(participant => participant.ShiftId).Distinct().ToList();
        var existing = await context.InClassExamSubmissions
            .Where(submission => shiftIds.Contains(submission.ShiftId) && !submission.IsFollowUp)
            .Select(submission => new { submission.ShiftId, submission.StudentId, submission.QuestionId })
            .ToListAsync(cancellationToken);
        var answered = existing
            .Select(submission => (submission.ShiftId, submission.StudentId, submission.QuestionId))
            .ToHashSet();

        var created = new List<InClassExamSubmission>();
        var utcNow = DateTime.UtcNow;

        foreach (var participant in participants)
        {
            var assigned = EnterInClassShiftCommandHandler.ParseGuidList(participant.AssignedQuestionIdsJson);
            if (assigned.Count == 0 && participant.AssignedQuestionId.HasValue)
            {
                assigned.Add(participant.AssignedQuestionId.Value);
            }

            foreach (var questionId in assigned)
            {
                var key = (participant.ShiftId, participant.StudentId, questionId);
                if (!answered.Add(key))
                {
                    continue;
                }

                var submission = new InClassExamSubmission
                {
                    Id = Guid.NewGuid(),
                    ShiftId = participant.ShiftId,
                    StudentId = participant.StudentId,
                    QuestionId = questionId,
                    IsFollowUp = false,
                    FollowUpIndex = 0,
                    Transcript = string.Empty,
                    Status = "SUBMITTED",
                    SubmittedAt = utcNow
                };
                context.InClassExamSubmissions.Add(submission);
                created.Add(submission);
            }

            // Đóng ca → khóa tham gia: mọi participant còn mở đều chuyển SUBMITTED.
            participant.Status = "SUBMITTED";
        }

        if (created.Count == 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            return 0;
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var submission in created)
        {
            await gradingQueue.EnqueueAsync(new GradingTask
            {
                AnswerId = submission.Id,
                SessionId = submission.ShiftId,
                StudentId = submission.StudentId,
                QuestionId = submission.QuestionId,
                AnswerText = submission.Transcript,
                IsFollowUp = false,
                TaskType = "InClass",
                InClassSubmissionId = submission.Id
            }, cancellationToken);
        }

        return created.Count;
    }
}
