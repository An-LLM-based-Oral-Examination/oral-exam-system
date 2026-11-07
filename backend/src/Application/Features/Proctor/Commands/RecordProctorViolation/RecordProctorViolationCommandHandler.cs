using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Proctor.Commands.RecordProctorViolation;

public sealed class RecordProctorViolationCommandHandler(
    IApplicationDbContext context,
    IAntiCheatNotifier? notifier = null)
    : IRequestHandler<RecordProctorViolationCommand, Result<ExamAntiCheatLogDto>>
{
    public async Task<Result<ExamAntiCheatLogDto>> Handle(
        RecordProctorViolationCommand request,
        CancellationToken cancellationToken)
    {
        var shiftExists = await context.RealExamSessionShifts
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (!shiftExists)
        {
            return Result<ExamAntiCheatLogDto>.Failure("NOT_FOUND: Không tìm thấy ca thi trong hệ thống.");
        }

        StudentExamTicket? ticket = null;

        if (request.TicketId.HasValue && request.TicketId.Value != Guid.Empty)
        {
            ticket = await context.StudentExamTickets
                .Include(t => t.Student)
                .FirstOrDefaultAsync(t => t.Id == request.TicketId.Value && t.ShiftId == request.ShiftId, cancellationToken);
        }
        else if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty)
        {
            ticket = await context.StudentExamTickets
                .Include(t => t.Student)
                .FirstOrDefaultAsync(t => t.StudentId == request.StudentId.Value && t.ShiftId == request.ShiftId, cancellationToken);
        }

        if (ticket is null)
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                "NOT_FOUND: Không tìm thấy vé thi của thí sinh trong ca thi này.");
        }

        var previousCount = await context.ExamAntiCheatLogs
            .CountAsync(l => l.TicketId == ticket.Id && l.EventType == request.EventType, cancellationToken);

        var violationCount = previousCount + 1;

        var effectiveSeverity = string.IsNullOrWhiteSpace(request.Severity)
            ? AntiCheatSeverity.Medium
            : request.Severity.Trim();

        if (violationCount >= 3)
        {
            effectiveSeverity = AntiCheatSeverity.Critical;
        }

        var log = new ExamAntiCheatLog
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ShiftId = request.ShiftId,
            StudentId = ticket.StudentId,
            EventType = request.EventType.Trim(),
            Severity = effectiveSeverity,
            Details = request.Details?.Trim() ?? string.Empty,
            ViolationCount = violationCount,
            OccurredAt = DateTime.UtcNow
        };

        context.ExamAntiCheatLogs.Add(log);

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "PROCTOR_VIOLATION_RECORDED",
            EntityName = "exam_anti_cheat_logs",
            EntityId = log.Id,
            NewValues = $"shiftId={request.ShiftId};ticketId={ticket.Id};eventType={log.EventType};severity={log.Severity};details={log.Details}",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        var dto = new ExamAntiCheatLogDto
        {
            Id = log.Id,
            TicketId = log.TicketId,
            ShiftId = log.ShiftId,
            StudentId = log.StudentId,
            StudentCode = ticket.Student?.StudentCode ?? string.Empty,
            StudentName = ticket.Student?.FullName ?? string.Empty,
            EventType = log.EventType,
            Severity = log.Severity,
            Details = log.Details,
            ViolationCount = log.ViolationCount,
            OccurredAt = log.OccurredAt
        };

        if (notifier is not null)
        {
            try
            {
                await notifier.NotifyAntiCheatEventAsync(dto, cancellationToken);
            }
            catch
            {
                // Thông báo realtime thất bại không làm hỏng lưu vết DB
            }
        }

        return Result<ExamAntiCheatLogDto>.Success(dto);
    }
}
