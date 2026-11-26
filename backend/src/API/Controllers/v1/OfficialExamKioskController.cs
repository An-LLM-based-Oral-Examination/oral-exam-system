using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.KioskHeartbeat;
using OralExamination.Application.Features.OfficialExams.Common;

namespace API.Controllers.v1;

public sealed class KioskHeartbeatRequest
{
    public int? AudioLevelDb { get; set; }
    public bool? IsFullscreen { get; set; }
    public int? DisplayCount { get; set; }
    public string? DeviceId { get; set; }
    public string? KioskVersion { get; set; }
}

/// <summary>
/// Endpoint Heartbeat dành riêng cho Desktop/Web Kiosk BYOD của thí sinh (MF-04 / PR-6).
/// Xác thực chặt ràng buộc thiết bị + phiên hoạt động và trả trạng thái khóa/thời gian còn lại từ server.
/// </summary>
[ApiController]
[Route("api/v1/official-exams")]
[Authorize(Roles = "student")]
public sealed class OfficialExamKioskController(ISender sender) : ControllerBase
{
    [HttpPost("tickets/{ticketId:guid}/heartbeat")]
    [ProducesResponseType(typeof(KioskHeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Heartbeat(
        Guid ticketId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] KioskHeartbeatRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var deviceId = ResolveHeader(OfficialExamDeviceGuard.DeviceIdHeaderName)
            ?? TrimOrNull(request?.DeviceId);
        var kioskSessionToken = ResolveHeader(OfficialExamDeviceGuard.SessionTokenHeaderName);
        var kioskVersion = ResolveHeader(OfficialExamDeviceGuard.KioskVersionHeaderName)
            ?? TrimOrNull(request?.KioskVersion);

        var result = await sender.Send(
            new KioskHeartbeatCommand(
                actorId.Value,
                ticketId,
                deviceId,
                kioskSessionToken,
                kioskVersion,
                ResolveRemoteIpAddress(),
                request?.AudioLevelDb,
                request?.IsFullscreen,
                request?.DisplayCount),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : KioskProblem(result.Error);
    }

    private string? ResolveRemoteIpAddress()
    {
        return HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private string? ResolveHeader(string headerName)
    {
        if (Request?.Headers != null
            && Request.Headers.TryGetValue(headerName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        return null;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Guid? GetActorId()
    {
        var claim = User?.FindFirst("sub")?.Value
            ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var actorId) && actorId != Guid.Empty
            ? actorId
            : null;
    }

    private IActionResult KioskProblem(string error)
    {
        var (statusCode, detail) = error switch
        {
            _ when error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status403Forbidden, error["FORBIDDEN:".Length..].Trim()),
            OfficialExamErrors.Forbidden or OfficialExamErrors.WrongWorkstation =>
                (StatusCodes.Status403Forbidden, error),
            _ when error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status404NotFound, error["NOT_FOUND:".Length..].Trim()),
            OfficialExamErrors.TicketNotFound or OfficialExamErrors.ShiftNotFound =>
                (StatusCodes.Status404NotFound, error),
            _ when error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status409Conflict, error["CONFLICT:".Length..].Trim()),
            OfficialExamErrors.DeviceMismatch
                or OfficialExamErrors.ActiveSessionConflict
                or OfficialExamErrors.DeviceInUseOtherTicket =>
                (StatusCodes.Status409Conflict, error),
            _ when error.StartsWith("BAD_REQUEST:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status400BadRequest, error["BAD_REQUEST:".Length..].Trim()),
            OfficialExamErrors.DeviceIdInvalid =>
                (StatusCodes.Status400BadRequest, error),
            _ => (StatusCodes.Status400BadRequest, error)
        };

        var errorType = error switch
        {
            _ when error.Contains("DEVICE_MISMATCH", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/device-mismatch",
            _ when error.Contains("ACTIVE_SESSION_CONFLICT", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/active-session-conflict",
            _ when error.Contains("DEVICE_IN_USE_OTHER_TICKET", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/device-in-use-other-ticket",
            _ => $"https://oralexam.fpt.edu.vn/errors/official-exam-kiosk-{statusCode}"
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Không được phép.",
                StatusCodes.Status404NotFound => "Không tìm thấy.",
                StatusCodes.Status409Conflict => "Xung đột trạng thái phiên thi Kiosk.",
                _ => "Dữ liệu đầu vào không hợp lệ."
            },
            Detail = string.IsNullOrWhiteSpace(detail) ? error : detail,
            Type = errorType
        });
    }
}
