using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.DeleteExamTicket;

public sealed class DeleteExamTicketCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteExamTicketCommand, Result>
{
    public async Task<Result> Handle(DeleteExamTicketCommand request, CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context,
                request.ActorUserId,
                request.ActorRole,
                UserRole.ExaminationOfficer,
                cancellationToken))
        {
            return Result.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.TicketId == Guid.Empty)
        {
            return Result.Failure($"NOT_FOUND: {OfficialExamErrors.TicketNotFound}");
        }

        var shiftId = await context.StudentExamTickets
            .AsNoTracking()
            .Where(t => t.Id == request.TicketId)
            .Select(t => (Guid?)t.ShiftId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!shiftId.HasValue)
        {
            return Result.Failure($"NOT_FOUND: {OfficialExamErrors.TicketNotFound}");
        }

        return await context.ExecuteShiftMutationAsync(
            shiftId.Value,
            isOfficial: true,
            ct => HandleLockedAsync(request, ct),
            cancellationToken);
    }

    private async Task<Result> HandleLockedAsync(
        DeleteExamTicketCommand request,
        CancellationToken cancellationToken)
    {
        var ticket = await context.StudentExamTickets
            .Include(t => t.Shift)
            .Include(t => t.LecturerAudit)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure($"NOT_FOUND: {OfficialExamErrors.TicketNotFound}");
        }

        if (ticket.IsLocked
            || ticket.GradesPublishedAt.HasValue
            || ticket.Status is ExamTicketStatus.Published or ExamTicketStatus.Locked)
        {
            return Result.Failure("LOCKED: Vé thi đã bị khóa hoặc đã công bố điểm, không thể xóa.");
        }

        if (ticket.Shift.IsExamPublished
            || ticket.Shift.PublishedAt.HasValue
            || !string.Equals(ticket.Shift.Status, "scheduled", StringComparison.OrdinalIgnoreCase)
            || ticket.Shift.StartTime <= DateTime.UtcNow)
        {
            return Result.Failure("CONFLICT: Ca thi đã công bố đề, đã bắt đầu hoặc không ở trạng thái scheduled, không thể xóa vé.");
        }

        if (!string.Equals(ticket.Status, ExamTicketStatus.Scheduled, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(ticket.DeviceId)
            || ticket.DeviceLockedAt.HasValue
            || !string.IsNullOrWhiteSpace(ticket.DeviceSessionTokenHash)
            || ticket.DeviceUnlockCount > 0
            || ticket.SubmittedAt.HasValue
            || ticket.AudioObjectKey != null
            || ticket.AudioR2Url != null
            || ticket.AudioHashSha256 != null
            || ticket.AudioVerifiedAt.HasValue
            || ticket.LecturerAudit != null)
        {
            return Result.Failure("CONFLICT: Vé thi đã được điểm danh, gắn thiết bị, nộp bài hoặc hậu kiểm, không thể xóa.");
        }

        if (await context.ExamQuestionSubmissions.AsNoTracking().AnyAsync(s => s.TicketId == ticket.Id, cancellationToken))
        {
            return Result.Failure("CONFLICT: Vé thi đã có dữ liệu bài nộp câu hỏi, không thể xóa.");
        }

        if (await context.ExamAntiCheatLogs.AsNoTracking().AnyAsync(l => l.TicketId == ticket.Id, cancellationToken))
        {
            return Result.Failure("CONFLICT: Vé thi đã có nhật ký giám sát an ninh, không thể xóa.");
        }

        if (await context.AppealRequests.AsNoTracking().AnyAsync(a => a.TicketId == ticket.Id, cancellationToken))
        {
            return Result.Failure("CONFLICT: Vé thi đã có đơn phúc khảo liên quan, không thể xóa.");
        }

        var inProgressStatus = "status=" + ExamTicketStatus.InProgress;
        var attendanceHistory = await context.AuditLogs
            .AsNoTracking()
            .Where(log => log.Action == "PROCTOR_ATTENDANCE_UPDATED"
                && log.EntityName == "student_exam_tickets"
                && log.EntityId == ticket.Id)
            .Select(log => new { log.OldValues, log.NewValues })
            .ToListAsync(cancellationToken);

        if (attendanceHistory.Any(log => log.OldValues == inProgressStatus
                || (log.NewValues?.StartsWith(inProgressStatus + ";", StringComparison.Ordinal) ?? false)))
        {
            return Result.Failure("CONFLICT: Vé thi đã từng được điểm danh vào thi, không thể xóa.");
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            ticket.Id,
            ticket.ShiftId,
            ticket.StudentId,
            ticket.SeatNumber,
            ticket.ExamSetId,
            ticket.Status
        });

        context.StudentExamTickets.Remove(ticket);
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "OFFICIAL_EXAM_TICKET_DELETED",
            EntityName = "student_exam_tickets",
            EntityId = ticket.Id,
            OldValues = oldValues,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
