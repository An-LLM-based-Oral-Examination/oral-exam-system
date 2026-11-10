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
    private static readonly string[] TerminalStatuses =
    [
        "SUBMITTED",
        "GRADED",
        "FINALIZED",
        "COMPLETED"
    ];

    public static async Task<int> SubmitMissingAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        CancellationToken cancellationToken,
        Guid? shiftId = null)
    {
        var participants = await context.InClassShiftParticipants
            .Where(participant => !TerminalStatuses.Contains(participant.Status))
            .Where(participant => context.InClassExamShifts.Any(shift =>
                shift.Id == participant.ShiftId &&
                shift.Status == "CLOSED" &&
                !shift.IsLocked &&
                (shiftId == null || shift.Id == shiftId)))
            .ToListAsync(cancellationToken);

        return await FinalizeParticipantsAsync(context, gradingQueue, participants, cancellationToken);
    }

    /// <summary>
    /// Hết thời gian làm bài cá nhân (JoinedAt + ExamDurationMinutes) khi ca vẫn IN_PROGRESS —
    /// tự nộp câu trống còn thiếu, khóa participant, enqueue AI.
    /// </summary>
    public static async Task<int> SubmitExpiredPersonalExamsAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        CancellationToken cancellationToken,
        Guid? shiftId = null,
        Guid? studentId = null)
    {
        var utcNow = DateTime.UtcNow;

        var openShiftsQuery = context.InClassExamShifts
            .Where(shift => shift.Status == "IN_PROGRESS" && !shift.IsLocked);
        if (shiftId.HasValue)
        {
            openShiftsQuery = openShiftsQuery.Where(shift => shift.Id == shiftId.Value);
        }

        var openShifts = await openShiftsQuery
            .Select(shift => new { shift.Id, shift.ExamDurationMinutes })
            .ToListAsync(cancellationToken);

        if (openShifts.Count == 0)
        {
            return 0;
        }

        var openShiftIds = openShifts.Select(shift => shift.Id).ToList();
        var durationByShift = openShifts.ToDictionary(shift => shift.Id, shift => shift.ExamDurationMinutes);

        var participantsQuery = context.InClassShiftParticipants
            .Where(participant => openShiftIds.Contains(participant.ShiftId))
            .Where(participant => !TerminalStatuses.Contains(participant.Status));
        if (studentId.HasValue)
        {
            participantsQuery = participantsQuery.Where(participant => participant.StudentId == studentId.Value);
        }

        var participants = await participantsQuery.ToListAsync(cancellationToken);
        var expired = participants
            .Where(participant =>
            {
                durationByShift.TryGetValue(participant.ShiftId, out var minutes);
                var endsAt = InClassShiftLifecycle.GetPersonalExamEndsAt(participant.JoinedAt, minutes);
                return utcNow >= endsAt;
            })
            .ToList();

        return await FinalizeParticipantsAsync(context, gradingQueue, expired, cancellationToken);
    }

    /// <summary>Khóa một participant cụ thể (hết giờ khi đang submit / resume).</summary>
    public static async Task<int> FinalizeParticipantAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        InClassShiftParticipant participant,
        CancellationToken cancellationToken)
    {
        if (IsTerminalStatus(participant.Status))
        {
            return 0;
        }

        return await FinalizeParticipantsAsync(
            context,
            gradingQueue,
            new List<InClassShiftParticipant> { participant },
            cancellationToken);
    }

    private static async Task<int> FinalizeParticipantsAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        IReadOnlyList<InClassShiftParticipant> participants,
        CancellationToken cancellationToken)
    {
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
            if (IsTerminalStatus(participant.Status))
            {
                continue;
            }

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

            participant.Status = "SUBMITTED";
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

    private static bool IsTerminalStatus(string? status) =>
        EnterInClassShiftCommandHandler.IsCompletedParticipation(status);
}
