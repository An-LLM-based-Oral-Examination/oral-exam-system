using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;

public sealed class LogAntiCheatEventCommandHandler(
    IApplicationDbContext context,
    IAntiCheatNotifier? notifier = null,
    ILogger<LogAntiCheatEventCommandHandler>? logger = null)
    : IRequestHandler<LogAntiCheatEventCommand, Result<ExamAntiCheatLogDto>>
{
    public async Task<Result<ExamAntiCheatLogDto>> Handle(LogAntiCheatEventCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                "FORBIDDEN: Không xác định được danh tính người gửi sự kiện an ninh phòng thi.");
        }

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null || actor.Role is not (UserRole.Student or UserRole.Proctor or UserRole.DepartmentHead or UserRole.ExaminationOfficer))
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                "FORBIDDEN: Bạn không có quyền ghi nhận sự kiện an ninh phòng thi.");
        }

        if (actor.Role == UserRole.Student && request.StudentId != actor.Id)
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                "FORBIDDEN: Sinh viên chỉ được gửi sự kiện an ninh cho chính vé thi của mình.");
        }

        var normalizedEventType = request.EventType?.Trim() ?? string.Empty;
        if (!AntiCheatEventType.All.Contains(normalizedEventType, StringComparer.Ordinal))
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                $"BAD_REQUEST: EventType không hợp lệ. Cho phép: {string.Join(", ", AntiCheatEventType.All)}");
        }

        var normalizedSeverity = string.IsNullOrWhiteSpace(request.Severity)
            ? string.Empty
            : request.Severity.Trim();
        if (!AntiCheatSeverity.All.Contains(normalizedSeverity, StringComparer.Ordinal))
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                $"BAD_REQUEST: Severity không hợp lệ. Cho phép: {string.Join(", ", AntiCheatSeverity.All)}");
        }

        if (request.Details is not null && request.Details.Length > 2000)
        {
            return Result<ExamAntiCheatLogDto>.Failure(
                "VALIDATION: Chi tiết sự kiện an ninh (Details) không được vượt quá 2000 ký tự.");
        }

        var normalizedClientEventId = string.IsNullOrWhiteSpace(request.ClientEventId)
            ? null
            : request.ClientEventId.Trim();

        var (outcome, shouldNotify) = await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            async ct =>
            {
                var ticket = await context.StudentExamTickets
                    .Include(t => t.Student)
                    .FirstOrDefaultAsync(
                        t => t.Id == request.TicketId && t.ShiftId == request.ShiftId && t.StudentId == request.StudentId,
                        ct);

                if (ticket is null)
                {
                    return (Result<ExamAntiCheatLogDto>.Failure(
                        "NOT_FOUND: Vé thi không hợp lệ hoặc thông tin thí sinh không khớp ca thi."), false);
                }

                var shift = await context.RealExamSessionShifts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == ticket.ShiftId, ct);

                var isAuthorized = actor.Role switch
                {
                    UserRole.Student => ticket.StudentId == actor.Id,
                    UserRole.ExaminationOfficer => true,
                    UserRole.Proctor => shift is not null && shift.ProctorUserId == actor.Id,
                    UserRole.DepartmentHead => shift is not null
                        && await OfficialExamAuthorization.OwnsShiftCourseAsync(context, shift, actor.Id, ct),
                    _ => false
                };

                if (!isAuthorized)
                {
                    return (Result<ExamAntiCheatLogDto>.Failure(
                        "FORBIDDEN: Bạn không có quyền ghi nhận sự kiện an ninh cho vé thi hoặc ca thi này."), false);
                }

                if (actor.Role == UserRole.Student
                    && ticket.Status is ExamTicketStatus.Submitted
                        or ExamTicketStatus.AiGraded
                        or ExamTicketStatus.Audited
                        or ExamTicketStatus.Published)
                {
                    return (Result<ExamAntiCheatLogDto>.Failure(
                        "CONFLICT: Vé thi đã kết thúc, không thể ghi thêm sự kiện giám sát."), false);
                }

                if (actor.Role == UserRole.Student
                    && (!string.IsNullOrWhiteSpace(ticket.DeviceId)
                        || !string.IsNullOrWhiteSpace(ticket.DeviceSessionTokenHash)
                        || !string.IsNullOrWhiteSpace(request.DeviceId)))
                {
                    var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
                        ticket,
                        request.DeviceId,
                        request.KioskSessionToken,
                        requireSessionToken: !string.IsNullOrWhiteSpace(ticket.DeviceSessionTokenHash));
                    if (deviceError is not null)
                    {
                        return (Result<ExamAntiCheatLogDto>.Failure(deviceError), false);
                    }
                }

                // Idempotency cho offline queue drain từ Kiosk
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
                    .CountAsync(l => l.TicketId == request.TicketId && l.EventType == normalizedEventType, ct);

                var violationCount = previousCount + 1;

                var effectiveSeverity = normalizedSeverity;
                if (violationCount >= OfficialExamRules.MaxAllowedWindowBlurs
                    || IsCriticalByodEvent(normalizedEventType))
                {
                    effectiveSeverity = AntiCheatSeverity.Critical;
                }

                var now = DateTime.UtcNow;
                var logId = Guid.NewGuid();
                var log = new ExamAntiCheatLog
                {
                    Id = logId,
                    TicketId = request.TicketId,
                    ShiftId = request.ShiftId,
                    StudentId = request.StudentId,
                    EventType = normalizedEventType,
                    Severity = effectiveSeverity,
                    Details = request.Details,
                    ClientEventId = normalizedClientEventId ?? logId.ToString("D"),
                    ViolationCount = violationCount,
                    OccurredAt = now
                };

                context.ExamAntiCheatLogs.Add(log);

                // Server-authoritative blur > 3 (lần thứ 4) -> tự động khóa vé LOCKED
                if (string.Equals(normalizedEventType, AntiCheatEventType.WindowBlur, StringComparison.Ordinal)
                    && violationCount > OfficialExamRules.MaxAllowedWindowBlurs
                    && string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
                {
                    ticket.Status = ExamTicketStatus.Locked;
                    context.AuditLogs.Add(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = actor.Id,
                        Action = "TICKET_AUTO_LOCKED_BLUR_VIOLATION",
                        EntityName = "student_exam_tickets",
                        EntityId = ticket.Id,
                        OldValues = ExamTicketStatus.InProgress,
                        NewValues = $"status={ExamTicketStatus.Locked};blurCount={violationCount}",
                        CreatedAt = now
                    });
                }

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
            catch (Exception ex)
            {
                // Thông báo realtime thất bại không làm hỏng transaction ghi log kiểm toán
                logger?.LogWarning(
                    ex,
                    "Không thể phát thông báo realtime anti-cheat cho TicketId={TicketId}, ShiftId={ShiftId}, EventType={EventType}.",
                    request.TicketId,
                    request.ShiftId,
                    normalizedEventType);
            }
        }

        return outcome;
    }

    private static bool IsCriticalByodEvent(string eventType) =>
        eventType is AntiCheatEventType.VmDetected
            or AntiCheatEventType.RemoteControl
            or AntiCheatEventType.AudioTamper;

    private static ExamAntiCheatLogDto ToDto(ExamAntiCheatLog log, StudentExamTicket ticket, int blurCount)
    {
        var isLocked = ticket.IsLocked || string.Equals(ticket.Status, ExamTicketStatus.Locked, StringComparison.Ordinal);
        return new ExamAntiCheatLogDto
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
            IsLocked = isLocked,
            MaxViolations = OfficialExamRules.MaxAllowedWindowBlurs,
            LockThreshold = OfficialExamRules.WindowBlurLockThreshold,
            RemainingStrikes = Math.Max(0, OfficialExamRules.WindowBlurLockThreshold - blurCount),
            OccurredAt = log.OccurredAt
        };
    }
}
