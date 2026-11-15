using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands;

internal static class ShiftMutationGuard
{
    public static async Task<string?> Conflict(IApplicationDbContext context, RealExamSessionShift shift,
        bool deleting, CancellationToken cancellationToken)
    {
        if (shift.IsExamPublished || shift.PublishedAt.HasValue) return ShiftMutationErrors.Published;
        if (!string.Equals(shift.Status, "scheduled", StringComparison.OrdinalIgnoreCase)) return ShiftMutationErrors.NotScheduled;
        if (shift.StartTime <= DateTime.UtcNow) return ShiftMutationErrors.AlreadyStarted;
        var tickets = context.StudentExamTickets.Where(ticket => ticket.ShiftId == shift.Id);
        if (await context.ExamQuestionSubmissions.AnyAsync(submission => tickets.Any(ticket => ticket.Id == submission.TicketId), cancellationToken))
            return ShiftMutationErrors.HasSubmissions;
        if (await context.ExamAntiCheatLogs.AnyAsync(log => log.ShiftId == shift.Id || tickets.Any(ticket => ticket.Id == log.TicketId), cancellationToken))
            return ShiftMutationErrors.HasAntiCheatLogs;
        if (deleting && await tickets.AnyAsync(cancellationToken)) return ShiftMutationErrors.HasTickets;
        // Attendance may be reverted to Scheduled; retain the evidence that the ticket was used.
        var inProgressStatus = "status=" + ExamTicketStatus.InProgress;
        // Old/NewValues use a JSONB value converter. Filter by this shift's tickets in SQL,
        // then inspect decoded strings; SQL LIKE on the JSONB column is not supported.
        var attendanceHistory = await context.AuditLogs.AsNoTracking()
            .Where(log => log.Action == "PROCTOR_ATTENDANCE_UPDATED"
                && log.EntityName == "student_exam_tickets" && tickets.Any(ticket => ticket.Id == log.EntityId))
            .Select(log => new { log.OldValues, log.NewValues }).ToListAsync(cancellationToken);
        if (attendanceHistory.Any(log => log.OldValues == inProgressStatus
                || (log.NewValues?.StartsWith(inProgressStatus + ";", StringComparison.Ordinal) ?? false)))
            return ShiftMutationErrors.HasUsedTickets;
        if (await tickets.AnyAsync(ticket => ticket.Status != ExamTicketStatus.Scheduled || ticket.IsLocked
                || ticket.SubmittedAt.HasValue || ticket.GradesPublishedAt.HasValue
                || ticket.AudioObjectKey != null || ticket.AudioR2Url != null || ticket.AudioHashSha256 != null
                || ticket.AudioVerifiedAt.HasValue || ticket.LecturerAudit != null, cancellationToken))
            return ShiftMutationErrors.HasUsedTickets;
        return null;
    }
}
