using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Lecturer.Queries.GetLecturerDashboard;

namespace API.Controllers.v1;

/// <summary>
/// Controller phục vụ Bảng điều khiển Giảng viên & Trưởng bộ môn (Lecturer Dashboard — BE-V11 / FE-10).
/// </summary>
[ApiController]
[Route("api/v1/lecturer")]
[Authorize(Roles = "lecturer,department_head,admin")]
public class LecturerController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    private Guid GetCurrentUserId(Guid? fallbackId = null)
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

        return fallbackId ?? Guid.Empty;
    }

    [HttpGet("dashboard")]
    [HttpGet("/api/v1/dashboard/lecturer")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? courseId = null,
        [FromQuery] Guid? lecturerId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedUserId = GetCurrentUserId(lecturerId);
        if (resolvedUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new GetLecturerDashboardQuery(resolvedUserId, courseId), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy thông tin giảng viên.",
                Detail = result.Error,
                Type = "https://oralexam.fpt.edu.vn/errors/lecturer-404"
            });
        }

        return Ok(result.Value);
    }
}
