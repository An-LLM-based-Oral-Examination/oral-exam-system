using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.KioskHeartbeat;

public sealed class KioskHeartbeatCommandHandler(
    IApplicationDbContext context,
    TimeProvider? clock = null,
    ILogger<KioskHeartbeatCommandHandler>? logger = null)
    : IRequestHandler<KioskHeartbeatCommand, Result<KioskHeartbeatResponse>>
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<KioskHeartbeatResponse>> Handle(
        KioskHeartbeatCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<KioskHeartbeatResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (request.AudioLevelDb is < 0 or > 150)
        {
            return Result<KioskHeartbeatResponse>.Failure(
                "BAD_REQUEST: Chỉ số mức âm thanh micro (AudioLevelDb) phải nằm trong khoảng 0 đến 150 dB.");
        }

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null || !string.Equals(actor.Role, UserRole.Student, StringComparison.Ordinal))
        {
            return Result<KioskHeartbeatResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (request.TicketId == Guid.Empty)
        {
            return Result<KioskHeartbeatResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        var ticket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Shift)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<KioskHeartbeatResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (ticket.StudentId != actor.Id)
        {
            return Result<KioskHeartbeatResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (ticket.Shift is null)
        {
            return Result<KioskHeartbeatResponse>.Failure(OfficialExamErrors.ShiftNotFound);
        }

        var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            ticket,
            request.DeviceId,
            request.KioskSessionToken,
            requireSessionToken: true,
            requireBoundDevice: true);

        if (deviceError is not null)
        {
            return Result<KioskHeartbeatResponse>.Failure(deviceError);
        }

        logger?.LogDebug(
            "Kiosk heartbeat accepted for TicketId={TicketId}, ShiftId={ShiftId}, KioskVersion={KioskVersion}, RemoteIpAddress={RemoteIpAddress}, AudioLevelDb={AudioLevelDb}.",
            ticket.Id,
            ticket.ShiftId,
            request.KioskVersion ?? "unknown",
            request.RemoteIpAddress ?? "unknown",
            request.AudioLevelDb);

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var shiftEndUtc = AsUtc(ticket.Shift.EndTime);
        var rawRemainingSeconds = Math.Floor((shiftEndUtc - nowUtc).TotalSeconds);
        var remainingShiftSeconds = rawRemainingSeconds <= 0
            ? 0
            : (rawRemainingSeconds >= int.MaxValue ? int.MaxValue : (int)rawRemainingSeconds);

        var isLocked = ticket.IsLocked
            || string.Equals(ticket.Status, ExamTicketStatus.Locked, StringComparison.Ordinal);
        // ValidateBoundDeviceAndSession(requireBoundDevice: true) đã xác nhận thiết bị đang khớp nên không yêu cầu mở khóa tại nhánh thành công.
        const bool deviceUnlockRequired = false;
        var shouldTerminateExam = isLocked
            || ticket.Status is ExamTicketStatus.Locked
                or ExamTicketStatus.Submitted
                or ExamTicketStatus.AiGraded
                or ExamTicketStatus.Audited
                or ExamTicketStatus.Published
            || remainingShiftSeconds == 0
            || !ExamShiftWindow.IsOpenStatus(ticket.Shift.Status);

        var notice = BuildNotice(isLocked, shouldTerminateExam, request.DisplayCount, request.IsFullscreen, request.AudioLevelDb);

        return Result<KioskHeartbeatResponse>.Success(new KioskHeartbeatResponse(
            TicketId: ticket.Id,
            ShiftId: ticket.ShiftId,
            TicketStatus: ticket.Status,
            ShiftStatus: ticket.Shift.Status,
            ServerUtcNow: nowUtc,
            RemainingShiftSeconds: remainingShiftSeconds,
            IsLocked: isLocked,
            DeviceUnlockRequired: deviceUnlockRequired,
            ShouldTerminateExam: shouldTerminateExam,
            Notice: notice));
    }

    private static string? BuildNotice(
        bool isLocked,
        bool shouldTerminateExam,
        int? displayCount,
        bool? isFullscreen,
        int? audioLevelDb)
    {
        var notices = new List<string>();

        if (isLocked)
        {
            notices.Add("Vé thi đã bị khóa. Vui lòng ngừng làm bài và liên hệ Giám thị hoặc Cán bộ Khảo thí.");
        }
        else if (shouldTerminateExam)
        {
            notices.Add("Ca thi đã kết thúc hoặc bài thi đã hoàn tất. Kiosk cần dừng phiên làm bài.");
        }

        if (displayCount is > 1)
        {
            notices.Add(
                $"Phát hiện {displayCount.Value} màn hình đang kết nối (MULTI_MONITOR). Vui lòng ngắt màn hình phụ và gửi sự kiện MULTI_MONITOR idempotent.");
        }

        if (isFullscreen == false)
        {
            notices.Add("Ứng dụng Kiosk không ở chế độ toàn màn hình (FULLSCREEN_EXIT).");
        }

        if (audioLevelDb is >= 0 and < 10)
        {
            notices.Add("Tín hiệu micro quá nhỏ hoặc đang bị tắt tiếng (MICROPHONE_LOW_SIGNAL). Vui lòng kiểm tra lại thiết bị thu âm.");
        }

        return notices.Count > 0 ? string.Join(" ", notices) : null;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
