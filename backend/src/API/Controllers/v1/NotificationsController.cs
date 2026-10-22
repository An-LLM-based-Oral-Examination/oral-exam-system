using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using OralExamination.Application.Features.Notifications.Commands.MarkNotificationRead;
using OralExamination.Application.Features.Notifications.Queries.GetNotifications;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản lý thông báo trong ứng dụng (In-App Notifications — BE-V07 / FE-11).
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(ISender sender) : ControllerBase
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

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int limit = 50,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 0,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new GetNotificationsQuery(userId, unreadOnly, limit, page, pageSize), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy người dùng.",
                Detail = result.Error,
                Type = "https://oralexam.fpt.edu.vn/errors/notifications-404"
            });
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/read")]
    [HttpPost("{id:guid}/read")]
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new MarkNotificationReadCommand(userId, id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Không thể đánh dấu đã đọc.",
                Detail = result.Error,
                Type = "https://oralexam.fpt.edu.vn/errors/notifications-400"
            });
        }

        return Ok(new { id, notificationId = id, isRead = true, readAt = DateTime.UtcNow });
    }

    [HttpPut("read-all")]
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var summary = await _sender.Send(new GetNotificationsQuery(userId, UnreadOnly: true), cancellationToken);
        var unreadCount = summary.IsSuccess && summary.Value != null ? summary.Value.UnreadCount : 0;

        var result = await _sender.Send(new MarkAllNotificationsReadCommand(userId), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Không thể đánh dấu tất cả thông báo.",
                Detail = result.Error,
                Type = "https://oralexam.fpt.edu.vn/errors/notifications-400"
            });
        }

        return Ok(new { isReadAll = true, updatedCount = unreadCount, message = "Đã đánh dấu tất cả thông báo là đã đọc." });
    }
}
