using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.InClassShifts;

public static class InClassShiftLifecycle
{
    public static readonly TimeSpan ClosedAnswerGrace = TimeSpan.FromMinutes(2);
    public const int DefaultExamDurationMinutes = 20;

    public static bool IsClosable(string status) =>
        status is "SCHEDULED" or "IN_PROGRESS";

    /// <summary>Thời lượng làm bài cá nhân (phút), mặc định 20 nếu ca chưa cấu hình.</summary>
    public static int EffectiveExamDurationMinutes(InClassExamShift shift)
    {
        var minutes = shift.ExamDurationMinutes ?? DefaultExamDurationMinutes;
        return Math.Clamp(minutes <= 0 ? DefaultExamDurationMinutes : minutes, 1, 60);
    }

    public static DateTime GetPersonalExamEndsAt(InClassShiftParticipant participant, InClassExamShift shift) =>
        participant.JoinedAt.AddMinutes(EffectiveExamDurationMinutes(shift));

    public static DateTime GetPersonalExamEndsAt(DateTime joinedAt, int? examDurationMinutes)
    {
        var minutes = examDurationMinutes ?? DefaultExamDurationMinutes;
        minutes = Math.Clamp(minutes <= 0 ? DefaultExamDurationMinutes : minutes, 1, 60);
        return joinedAt.AddMinutes(minutes);
    }

    public static bool IsPersonalExamTimeExpired(
        InClassShiftParticipant participant,
        InClassExamShift shift,
        DateTime utcNow) =>
        utcNow >= GetPersonalExamEndsAt(participant, shift);

    public static int GetRemainingExamSeconds(DateTime examEndsAt, DateTime utcNow) =>
        Math.Max(0, (int)Math.Floor((examEndsAt - utcNow).TotalSeconds));

    public static bool CanAcceptLateAnswer(InClassExamShift shift, DateTime utcNow)
    {
        if (shift.IsLocked || shift.Status is not ("CLOSED" or "ENDED"))
        {
            return false;
        }

        var closedAt = shift.UpdatedAt ?? shift.EndTime;
        return utcNow - closedAt <= ClosedAnswerGrace;
    }

    public static bool TryCloseIfDue(InClassExamShift shift, DateTime utcNow)
    {
        if (shift.IsLocked || !IsClosable(shift.Status) || utcNow < shift.EndTime)
        {
            return false;
        }

        shift.Status = "CLOSED";
        shift.UpdatedAt = utcNow;
        return true;
    }

    public static async Task<int> CloseDueAsync(
        IApplicationDbContext context,
        DateTime utcNow,
        CancellationToken cancellationToken,
        Guid? classId = null,
        Guid? shiftId = null)
    {
        var query = context.InClassExamShifts.Where(shift =>
            (shift.Status == "SCHEDULED" || shift.Status == "IN_PROGRESS") &&
            !shift.IsLocked &&
            shift.EndTime <= utcNow);

        if (classId.HasValue)
        {
            query = query.Where(shift => shift.ClassId == classId.Value);
        }

        if (shiftId.HasValue)
        {
            query = query.Where(shift => shift.Id == shiftId.Value);
        }

        var due = await query.ToListAsync(cancellationToken);
        foreach (var shift in due)
        {
            shift.Status = "CLOSED";
            shift.UpdatedAt = utcNow;
        }

        if (due.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }
}
