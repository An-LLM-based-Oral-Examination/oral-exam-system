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

namespace OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;

public sealed class LogAntiCheatEventCommandHandler(
    IApplicationDbContext context,
    IAntiCheatNotifier? notifier = null)
    : IRequestHandler<LogAntiCheatEventCommand, Result<ExamAntiCheatLogDto>>
{
    public async Task<Result<ExamAntiCheatLogDto>> Handle(LogAntiCheatEventCommand request, CancellationToken cancellationToken)
    {
        var ticket = await context.StudentExamTickets
            .Include(t => t.Student)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId && t.ShiftId == request.ShiftId && t.StudentId == request.StudentId, cancellationToken);

        if (ticket is null)
        {
            return Result<ExamAntiCheatLogDto>.Failure("Vé thi không hợp lệ hoặc thông tin thí sinh không khớp ca thi.");
        }

        var previousCount = await context.ExamAntiCheatLogs
            .CountAsync(l => l.TicketId == request.TicketId && l.EventType == request.EventType, cancellationToken);

        var violationCount = previousCount + 1;

        // Tự động nâng cấp mức độ vi phạm nếu lặp lại nhiều lần
        var effectiveSeverity = request.Severity;
        if (violationCount >= 3)
        {
            effectiveSeverity = AntiCheatSeverity.Critical;
        }

        var log = new ExamAntiCheatLog
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            ShiftId = request.ShiftId,
            StudentId = request.StudentId,
            EventType = request.EventType,
            Severity = effectiveSeverity,
            Details = request.Details,
            ViolationCount = violationCount,
            OccurredAt = DateTime.UtcNow
        };

        context.ExamAntiCheatLogs.Add(log);
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
                // Thông báo realtime thất bại không làm hỏng transaction ghi log kiểm toán
            }
        }

        return Result<ExamAntiCheatLogDto>.Success(dto);
    }
}
