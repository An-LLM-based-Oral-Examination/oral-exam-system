using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
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
        if (!await ProctorAuthorization.CanAccessShiftAsync(context, request.ActorUserId, request.ShiftId, false, cancellationToken))
            return Result<ExamAntiCheatLogDto>.Failure(ProctorAuthorization.Forbidden);

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            return Result<ExamAntiCheatLogDto>.Failure("VALIDATION: Loại vi phạm (EventType) không được để trống.");
        }

        var normalizedEventType = request.EventType.Trim().ToUpperInvariant();
        if (!AntiCheatEventType.ProctorAllowed.Contains(normalizedEventType, StringComparer.Ordinal))
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                $"VALIDATION: Loại vi phạm '{request.EventType}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AntiCheatEventType.ProctorAllowed)}.");
        }

        var normalizedSeverity = string.IsNullOrWhiteSpace(request.Severity)
            ? AntiCheatSeverity.Medium
            : request.Severity.Trim().ToLowerInvariant();
        if (!AntiCheatSeverity.All.Contains(normalizedSeverity, StringComparer.Ordinal))
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                $"VALIDATION: Mức độ vi phạm '{request.Severity}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AntiCheatSeverity.All)}.");
        }

        if (request.Details is not null && request.Details.Length > 2000)
        {
            return Result<ExamAntiCheatLogDto>.Failure("VALIDATION: Chi tiết vi phạm (Details) tối đa 2000 ký tự.");
        }

        var shiftExists = await context.RealExamSessionShifts
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (!shiftExists)
        {
            return Result<ExamAntiCheatLogDto>.Failure("NOT_FOUND: Không tìm thấy ca thi trong hệ thống.");
        }

        Guid? resolvedTicketId = null;

        if (request.TicketId.HasValue && request.TicketId.Value != Guid.Empty)
        {
            resolvedTicketId = await context.StudentExamTickets
                .AsNoTracking()
                .Where(t => t.Id == request.TicketId.Value && t.ShiftId == request.ShiftId)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty)
        {
            resolvedTicketId = await context.StudentExamTickets
                .AsNoTracking()
                .Where(t => t.StudentId == request.StudentId.Value && t.ShiftId == request.ShiftId)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!resolvedTicketId.HasValue)
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                "NOT_FOUND: Không tìm thấy vé thi của thí sinh trong ca thi này.");
        }

        var normalizedClientEventId = string.IsNullOrWhiteSpace(request.ClientEventId)
            ? null
            : request.ClientEventId.Trim();

        var (outcome, shouldNotify) = await context.ExecuteTicketDecisionAsync(
            resolvedTicketId.Value,
            async ct =>
            {
                var ticket = await context.StudentExamTickets
                    .Include(t => t.Student)
                    .FirstOrDefaultAsync(t => t.Id == resolvedTicketId.Value && t.ShiftId == request.ShiftId, ct);

                if (ticket is null)
                {
                    return (Result<ExamAntiCheatLogDto>.Failure(
                        "NOT_FOUND: Không tìm thấy vé thi của thí sinh trong ca thi này."), false);
                }

                // Idempotency theo (ticket_id, client_event_id) khớp unique index uq_exam_anti_cheat_logs_ticket_client_event
                if (normalizedClientEventId is not null)
                {
                    var existing = await context.ExamAntiCheatLogs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            l => l.TicketId == ticket.Id && l.ClientEventId == normalizedClientEventId,
                            ct);

                    if (existing is not null)
                    {
                        var existingBlurCount = await context.ExamAntiCheatLogs
                            .AsNoTracking()
                            .CountAsync(
                                l => l.TicketId == ticket.Id && l.EventType == AntiCheatEventType.WindowBlur,
                                ct);

                        return (Result<ExamAntiCheatLogDto>.Success(ToDto(existing, ticket, existingBlurCount)), false);
                    }
                }

                var previousCount = await context.ExamAntiCheatLogs
                    .CountAsync(l => l.TicketId == ticket.Id && l.EventType == normalizedEventType, ct);

                var violationCount = previousCount + 1;

                var effectiveSeverity = normalizedSeverity;

                if (violationCount >= 3)
                {
                    effectiveSeverity = AntiCheatSeverity.Critical;
                }

                var logId = Guid.NewGuid();
                var resolvedClientEventId = normalizedClientEventId ?? logId.ToString("D");

                var log = new ExamAntiCheatLog
                {
                    Id = logId,
                    TicketId = ticket.Id,
                    ShiftId = request.ShiftId,
                    StudentId = ticket.StudentId,
                    EventType = normalizedEventType,
                    Severity = effectiveSeverity,
                    Details = request.Details?.Trim() ?? string.Empty,
                    ClientEventId = resolvedClientEventId,
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

                await context.SaveChangesAsync(ct);

                var blurCount = string.Equals(normalizedEventType, AntiCheatEventType.WindowBlur, StringComparison.Ordinal)
                    ? violationCount
                    : await context.ExamAntiCheatLogs
                        .AsNoTracking()
                        .CountAsync(
                            l => l.TicketId == ticket.Id && l.EventType == AntiCheatEventType.WindowBlur,
                            ct);

                return (Result<ExamAntiCheatLogDto>.Success(ToDto(log, ticket, blurCount)), true);
            },
            cancellationToken);

        if (shouldNotify && outcome.IsSuccess && notifier is not null)
        {
            try
            {
                await notifier.NotifyAntiCheatEventAsync(outcome.Value, cancellationToken);
            }
            catch
            {
                // Thông báo realtime thất bại không làm hỏng lưu vết DB
            }
        }

        return outcome;
    }

    private static ExamAntiCheatLogDto ToDto(ExamAntiCheatLog log, StudentExamTicket ticket, int blurCount) => new()
    {
        Id = log.Id,
        ClientEventId = log.ClientEventId ?? log.Id.ToString("D"),
        TicketId = log.TicketId,
        ShiftId = log.ShiftId,
        StudentId = log.StudentId,
        StudentCode = ticket.Student?.StudentCode ?? string.Empty,
        StudentName = ticket.Student?.FullName ?? string.Empty,
        EventType = log.EventType,
        Severity = log.Severity,
        Details = log.Details,
        ViolationCount = log.ViolationCount,
        IsLocked = ticket.IsLocked || string.Equals(ticket.Status, ExamTicketStatus.Locked, StringComparison.Ordinal),
        MaxViolations = OfficialExamRules.MaxAllowedWindowBlurs,
        LockThreshold = OfficialExamRules.WindowBlurLockThreshold,
        RemainingStrikes = Math.Max(0, OfficialExamRules.WindowBlurLockThreshold - blurCount),
        OccurredAt = log.OccurredAt
    };
}
