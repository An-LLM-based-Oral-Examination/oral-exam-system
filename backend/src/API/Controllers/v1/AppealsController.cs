using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;
using OralExamination.Application.Features.Appeals.Commands.RegradeAppeal;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.Appeals.Queries.GetAppealById;
using OralExamination.Application.Features.Appeals.Queries.GetAppeals;

using Microsoft.AspNetCore.Authorization;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý quy trình Phúc khảo Nội bộ (Internal Appeals) — MF-04.
/// Chuẩn RESTful RFC 7807 Problem Details.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AppealsController : ControllerBase
{
    private readonly ISender _sender;

    public AppealsController(ISender sender)
    {
        _sender = sender;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        if (Request?.Headers != null && Request.Headers.TryGetValue("X-User-Id", out var headerVal) && Guid.TryParse(headerVal, out var headerId))
        {
            return headerId;
        }

        return Guid.Empty;
    }

    private Guid GetAuthenticatedUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Sinh viên nộp đơn phúc khảo bài thi chính thức (MF-04).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "student")]
    public async Task<IActionResult> CreateAppeal([FromBody] CreateAppealRequestDto request)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new CreateAppealCommand(
            studentId,
            request.TicketId,
            request.SubmissionId,
            request.Reason
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/appeal-failed",
                title = "Unprocessable Entity",
                status = 422,
                detail = result.Error
            });
        }

        return CreatedAtAction(nameof(GetAppealById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Tra cứu danh sách đơn phúc khảo có hỗ trợ lọc theo trạng thái, ca thi, sinh viên, người thụ lý.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "student,lecturer,department_head")]
    public async Task<IActionResult> GetAppeals(
        [FromQuery] string? status,
        [FromQuery] Guid? sessionId,
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? assignedTo)
    {
        var requesterId = GetAuthenticatedUserId();
        if (requesterId == Guid.Empty)
        {
            return Unauthorized();
        }
        var requesterRole = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var query = new GetAppealsQuery(status, sessionId, studentId, assignedTo, requesterId, requesterRole);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/query-failed",
                title = "Bad Request",
                status = 400,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tra cứu chi tiết một đơn phúc khảo theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "student,lecturer,department_head")]
    public async Task<IActionResult> GetAppealById(Guid id)
    {
        var requesterId = GetAuthenticatedUserId();
        if (requesterId == Guid.Empty)
        {
            return Unauthorized();
        }
        var requesterRole = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var query = new GetAppealByIdQuery(id, requesterId, requesterRole);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            return NotFound(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/not-found",
                title = "Not Found",
                status = 404,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Trưởng Bộ Môn (department_head) hoặc Admin thẩm định và ra quyết định phúc khảo (APPROVED / REJECTED).
    /// </summary>
    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> ReviewAppeal(Guid id, [FromBody] ReviewAppealRequestDto request)
    {
        var reviewerId = GetAuthenticatedUserId();
        if (reviewerId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new ReviewAppealDecisionCommand(
            id,
            reviewerId,
            request.Decision,
            request.ProposedScore,
            request.ReviewNotes
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/review-failed",
                title = "Unprocessable Entity",
                status = 422,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Trưởng bộ môn tiếp nhận và giao một giảng viên đang hoạt động chấm lại đơn phúc khảo.
    /// </summary>
    [HttpPut("{id:guid}/assign-lecturer")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> AssignLecturer(
        Guid id,
        [FromBody] AssignAppealLecturerRequestDto request,
        CancellationToken cancellationToken)
    {
        var departmentHeadId = GetAuthenticatedUserId();
        if (departmentHeadId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new AssignAppealLecturerCommand(id, departmentHeadId, request.LecturerId),
            cancellationToken);
        return AppealMutationResult(result, "assign-lecturer");
    }

    /// <summary>
    /// Giảng viên được phân công gửi điểm chấm lại và giải trình; One-Way Lock chỉ mở cho transaction này.
    /// </summary>
    [HttpPut("{id:guid}/regrade")]
    [Authorize(Roles = "lecturer")]
    public async Task<IActionResult> Regrade(
        Guid id,
        [FromBody] RegradeAppealRequestDto request,
        CancellationToken cancellationToken)
    {
        var lecturerId = GetAuthenticatedUserId();
        if (lecturerId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new RegradeAppealCommand(id, lecturerId, request.NewScore, request.ReviewNotes),
            cancellationToken);
        return AppealMutationResult(result, "regrade");
    }

    private IActionResult AppealMutationResult(Result<AppealResponseDto> result, string operation)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var statusCode = result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : result.Error.Contains("Chỉ ", StringComparison.OrdinalIgnoreCase)
                || result.Error.Contains("không phải người được phân công", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status409Conflict;

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status404NotFound => "Không tìm thấy.",
                StatusCodes.Status403Forbidden => "Không được phép.",
                _ => "Xung đột trạng thái tài nguyên."
            },
            Detail = result.Error,
            Type = $"https://oralexam.fpt.edu.vn/errors/appeal-{operation}-{statusCode}"
        });
    }
}
