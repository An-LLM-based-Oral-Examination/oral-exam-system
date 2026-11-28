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
    // SUBMITTED vẫn phải được đối soát vì dữ liệu cũ hoặc request finish lỗi có thể
    // đã đổi trạng thái participant trước khi đủ submission. Chỉ các trạng thái đã
    // chấm/chốt hoặc vắng mặt (ABSENT) mới thực sự bất biến.
    private static readonly string[] ImmutableStatuses =
    [
        "GRADED",
        "FINALIZED",
        "COMPLETED",
        "ABSENT"
    ];

    public static async Task<int> SubmitMissingAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        CancellationToken cancellationToken,
        Guid? shiftId = null,
        bool respectClosedGrace = false)
    {
        var closedGraceCutoff = DateTime.UtcNow - InClassShiftLifecycle.ClosedAnswerGrace;

        var candidateKeys = await context.InClassShiftParticipants
            .AsNoTracking()
            .Where(participant => !ImmutableStatuses.Contains(participant.Status))
            .Where(participant => participant.Status != "SUBMITTED" ||
                context.InClassExamSubmissions.Count(submission =>
                    submission.ShiftId == participant.ShiftId &&
                    submission.StudentId == participant.StudentId &&
                    !submission.IsFollowUp) <
                context.InClassExamShifts
                    .Where(shift => shift.Id == participant.ShiftId)
                    .Select(shift => shift.TotalQuestionsPerStudent)
                    .First())
            .Where(participant => context.InClassExamShifts.Any(shift =>
                shift.Id == participant.ShiftId &&
                shift.Status == "CLOSED" &&
                !shift.IsLocked &&
                (shiftId == null || shift.Id == shiftId) &&
                (!respectClosedGrace || (shift.UpdatedAt ?? shift.EndTime) <= closedGraceCutoff)))
            .Select(participant => new ParticipantKey(participant.ShiftId, participant.StudentId))
            .ToListAsync(cancellationToken);

        return await FinalizeParticipantKeysAsync(context, gradingQueue, candidateKeys, cancellationToken);
    }

    /// <summary>
    /// Hết thời gian làm bài cá nhân (kẹp theo shift.EndTime + tùy chọn grace 2 phút) khi ca vẫn IN_PROGRESS —
    /// tự nộp câu trống còn thiếu, khóa participant, enqueue AI.
    /// </summary>
    public static async Task<int> SubmitExpiredPersonalExamsAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        CancellationToken cancellationToken,
        Guid? shiftId = null,
        Guid? studentId = null,
        bool respectGrace = true)
    {
        var utcNow = DateTime.UtcNow;

        var openShiftsQuery = context.InClassExamShifts
            .AsNoTracking()
            .Where(shift => shift.Status == "IN_PROGRESS" && !shift.IsLocked);
        if (shiftId.HasValue)
        {
            openShiftsQuery = openShiftsQuery.Where(shift => shift.Id == shiftId.Value);
        }

        var openShifts = await openShiftsQuery
            .Select(shift => new { shift.Id, shift.ExamDurationMinutes, shift.EndTime })
            .ToListAsync(cancellationToken);

        if (openShifts.Count == 0)
        {
            return 0;
        }

        var openShiftIds = openShifts.Select(shift => shift.Id).ToList();
        var shiftTimingById = openShifts.ToDictionary(shift => shift.Id);

        var participantsQuery = context.InClassShiftParticipants
            .AsNoTracking()
            .Where(participant => openShiftIds.Contains(participant.ShiftId))
            .Where(participant => !ImmutableStatuses.Contains(participant.Status))
            .Where(participant => participant.Status != "SUBMITTED" ||
                context.InClassExamSubmissions.Count(submission =>
                    submission.ShiftId == participant.ShiftId &&
                    submission.StudentId == participant.StudentId &&
                    !submission.IsFollowUp) <
                context.InClassExamShifts
                    .Where(shift => shift.Id == participant.ShiftId)
                    .Select(shift => shift.TotalQuestionsPerStudent)
                    .First());
        if (studentId.HasValue)
        {
            participantsQuery = participantsQuery.Where(participant => participant.StudentId == studentId.Value);
        }

        var participants = await participantsQuery
            .Select(participant => new { participant.ShiftId, participant.StudentId, participant.JoinedAt })
            .ToListAsync(cancellationToken);

        var expiredKeys = participants
            .Where(participant =>
            {
                if (!shiftTimingById.TryGetValue(participant.ShiftId, out var timing))
                {
                    return false;
                }

                var endsAt = InClassShiftLifecycle.GetPersonalExamEndsAt(
                    participant.JoinedAt,
                    timing.ExamDurationMinutes,
                    timing.EndTime);
                var cutoff = respectGrace ? endsAt.Add(InClassShiftLifecycle.ClosedAnswerGrace) : endsAt;
                return utcNow >= cutoff;
            })
            .Select(participant => new ParticipantKey(participant.ShiftId, participant.StudentId))
            .ToList();

        return await FinalizeParticipantKeysAsync(context, gradingQueue, expiredKeys, cancellationToken);
    }

    /// <summary>Khóa một participant cụ thể (hết giờ khi đang submit / resume) dưới advisory lock độc lập.</summary>
    public static async Task<int> FinalizeParticipantAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        InClassShiftParticipant participant,
        CancellationToken cancellationToken)
    {
        if (IsImmutableStatus(participant.Status))
        {
            return 0;
        }

        var created = await context.ExecuteInClassSubmissionAsync(
            participant.ShiftId,
            participant.StudentId,
            async ct =>
            {
                // Xóa stale entity khỏi EF Core Identity Map để đảm bảo đọc bản ghi tươi nhất từ PostgreSQL
                // dưới advisory lock (tránh hạ ngược GRADED/FINALIZED về SUBMITTED khi worker khác vừa chấm xong).
                if (context is DbContext efContext)
                {
                    efContext.ChangeTracker.Clear();
                }

                var freshSnapshot = await context.InClassShiftParticipants
                    .AsNoTracking()
                    .Where(p => p.ShiftId == participant.ShiftId && p.StudentId == participant.StudentId)
                    .Select(p => new { p.Status, p.FinishedAt })
                    .FirstOrDefaultAsync(ct);

                if (freshSnapshot is null)
                {
                    return new List<InClassExamSubmission>();
                }

                if (IsImmutableStatus(freshSnapshot.Status))
                {
                    participant.Status = freshSnapshot.Status;
                    participant.FinishedAt = freshSnapshot.FinishedAt;
                    return new List<InClassExamSubmission>();
                }

                var tracked = await context.InClassShiftParticipants
                    .FirstOrDefaultAsync(
                        p => p.ShiftId == participant.ShiftId && p.StudentId == participant.StudentId,
                        ct);

                if (tracked is null)
                {
                    return new List<InClassExamSubmission>();
                }

                if (IsImmutableStatus(tracked.Status))
                {
                    participant.Status = tracked.Status;
                    participant.FinishedAt = tracked.FinishedAt;
                    return new List<InClassExamSubmission>();
                }

                var batch = await FinalizeParticipantInTransactionAsync(context, tracked, ct);

                if (!ReferenceEquals(tracked, participant))
                {
                    participant.Status = tracked.Status;
                    participant.FinishedAt = tracked.FinishedAt;
                }

                return batch;
            },
            cancellationToken);

        await EnqueueSubmissionsAsync(gradingQueue, created, cancellationToken);
        return created.Count;
    }

    /// <summary>
    /// Chốt các câu chính còn thiếu và đánh dấu FinishedAt/SUBMITTED cho 1 participant bên trong giao dịch/lock có sẵn.
    /// Caller chịu trách nhiệm gọi EnqueueSubmissionsAsync NGOÀI transaction.
    /// </summary>
    public static async Task<List<InClassExamSubmission>> FinalizeParticipantInTransactionAsync(
        IApplicationDbContext context,
        InClassShiftParticipant participant,
        CancellationToken cancellationToken)
    {
        var created = new List<InClassExamSubmission>();
        if (IsImmutableStatus(participant.Status))
        {
            return created;
        }

        var existingQuestionIds = await context.InClassExamSubmissions
            .AsNoTracking()
            .Where(submission =>
                submission.ShiftId == participant.ShiftId &&
                submission.StudentId == participant.StudentId &&
                !submission.IsFollowUp)
            .Select(submission => submission.QuestionId)
            .ToListAsync(cancellationToken);

        var answered = existingQuestionIds.ToHashSet();
        var utcNow = DateTime.UtcNow;

        var assigned = EnterInClassShiftCommandHandler.ParseGuidList(participant.AssignedQuestionIdsJson);
        if (assigned.Count == 0 && participant.AssignedQuestionId.HasValue)
        {
            assigned.Add(participant.AssignedQuestionId.Value);
        }

        foreach (var questionId in assigned)
        {
            if (!answered.Add(questionId))
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
        participant.FinishedAt ??= utcNow;

        await context.SaveChangesAsync(cancellationToken);
        return created;
    }

    public static async Task EnqueueSubmissionsAsync(
        IGradingQueueChannel gradingQueue,
        IReadOnlyList<InClassExamSubmission> created,
        CancellationToken cancellationToken)
    {
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
    }

    private static async Task<int> FinalizeParticipantKeysAsync(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueue,
        IReadOnlyList<ParticipantKey> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return 0;
        }

        var totalCreated = 0;

        foreach (var key in keys)
        {
            List<InClassExamSubmission> created;
            try
            {
                created = await context.ExecuteInClassSubmissionAsync(
                    key.ShiftId,
                    key.StudentId,
                    async ct =>
                    {
                        if (context is DbContext efContext)
                        {
                            efContext.ChangeTracker.Clear();
                        }

                        var tracked = await context.InClassShiftParticipants
                            .FirstOrDefaultAsync(
                                p => p.ShiftId == key.ShiftId && p.StudentId == key.StudentId,
                                ct);

                        if (tracked is null || IsImmutableStatus(tracked.Status))
                        {
                            return new List<InClassExamSubmission>();
                        }

                        return await FinalizeParticipantInTransactionAsync(context, tracked, ct);
                    },
                    cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Cô lập xung đột ghi/unique 23505 theo từng sinh viên để không rollback cả lớp 120 SV.
                if (context is DbContext efContext)
                {
                    efContext.ChangeTracker.Clear();
                }

                continue;
            }

            if (created.Count > 0)
            {
                await EnqueueSubmissionsAsync(gradingQueue, created, cancellationToken);
                totalCreated += created.Count;
            }
        }

        return totalCreated;
    }

    private readonly record struct ParticipantKey(Guid ShiftId, Guid StudentId);

    private static bool IsImmutableStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        ImmutableStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
}
