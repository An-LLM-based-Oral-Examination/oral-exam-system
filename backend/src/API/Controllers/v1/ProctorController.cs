using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Proctor.Commands.SuspendExamTicket;
using OralExamination.Application.Features.Proctor.Commands.UpdateTicketAttendance;
using OralExamination.Application.Features.Proctor.Queries.GetShiftAttendance;

namespace API.Controllers.v1;

/// <summary>
/// Controller phục vụ Giám thị Coi thi Phòng Lab Kiosk 40 máy trạm (BE-V10 / FE-P01):
/// - Giám sát sơ đồ 40 ghế ca thi thời gian thực
/// - Điểm danh xác nhận thẻ SV/CCCD mở vé thi
/// - Đình chỉ vé thi vi phạm quy chế Kiosk Lockdown (blur > 3 lần, mở DevTools, gian lận)
/// </summary>
[ApiController]
[Route("api/v1/proctor")]
[Authorize(Roles = "proctor,lecturer,department_head,admin")]
public class ProctorController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    private Guid GetCurrentUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var id) && id != Guid.Empty)
        {
            return id;
        }

        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-User-Id", out var headerVal) &&
            Guid.TryParse(headerVal, out var headerId) &&
            headerId != Guid.Empty)
        {
            return headerId;
        }

        return Guid.Empty;
    }

    [HttpGet("shifts/{shiftId:guid}/attendance")]
    [HttpGet("shifts/{shiftId:guid}/seats")]
    public async Task<IActionResult> GetShiftAttendance(
        [FromRoute] Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetShiftAttendanceQuery(shiftId), cancellationToken);
        if (!result.IsSuccess)
        {
            return ProctorProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPut("tickets/{ticketId:guid}/attendance")]
    [HttpPost("tickets/{ticketId:guid}/check-in")]
    public async Task<IActionResult> UpdateAttendance(
        [FromRoute] Guid ticketId,
        [FromBody] UpdateTicketAttendanceRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetCurrentUserId();
        if (actorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new UpdateTicketAttendanceCommand(
                actorId,
                ticketId,
                request?.IsPresent ?? true,
                request?.IpAddress,
                request?.Note),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ProctorProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("tickets/{ticketId:guid}/suspend")]
    [Authorize(Roles = "proctor,lecturer,department_head,admin,student")]
    public async Task<IActionResult> SuspendTicket(
        [FromRoute] Guid ticketId,
        [FromBody] SuspendExamTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetCurrentUserId();
        if (actorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new SuspendExamTicketCommand(actorId, ticketId, request.Reason, request.BlurViolationCount),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ProctorProblem(result.Error);
        }

        return Ok(result.Value);
    }

    private IActionResult ProctorProblem(string error)
    {
        var statusCode = error switch
        {
            var err when err.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var err when err.StartsWith("LOCKED:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var err when err.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Thao tác bị khóa một chiều.",
                StatusCodes.Status404NotFound => "Không tìm thấy dữ liệu ca thi hoặc vé thi.",
                StatusCodes.Status409Conflict => "Xung đột trạng thái vé thi.",
                _ => "Dữ liệu coi thi không hợp lệ."
            },
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/proctor-{statusCode}"
        });
    }
}

public sealed record UpdateTicketAttendanceRequest
{
    public bool IsPresent { get; init; } = true;
    public string? IpAddress { get; init; }
    public string? Note { get; init; }
}

public sealed record SuspendExamTicketRequest
{
    public string Reason { get; init; } = string.Empty;
    public int? BlurViolationCount { get; init; }
}
