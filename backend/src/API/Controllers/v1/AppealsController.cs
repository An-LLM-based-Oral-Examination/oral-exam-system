using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.Appeals.Queries.GetAppealById;
using OralExamination.Application.Features.Appeals.Queries.GetAppeals;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý quy trình Phúc khảo Nội bộ (Internal Appeals) — MF-04.
/// Chuẩn RESTful RFC 7807 Problem Details.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AppealsController : ControllerBase
{
    private readonly ISender _sender;

    public AppealsController(ISender sender)
    {
        _sender = sender;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        if (Request.Headers.TryGetValue("X-User-Id", out var headerVal) && Guid.TryParse(headerVal, out var headerId))
        {
            return headerId;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Sinh viên nộp đơn phúc khảo bài thi chính thức (MF-04).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAppeal([FromBody] CreateAppealRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var studentId = currentUserId != Guid.Empty ? currentUserId : (request.StudentId ?? Guid.Empty);

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
    public async Task<IActionResult> GetAppeals(
        [FromQuery] string? status,
        [FromQuery] Guid? sessionId,
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? assignedTo)
    {
        var query = new GetAppealsQuery(status, sessionId, studentId, assignedTo);
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
    public async Task<IActionResult> GetAppealById(Guid id)
    {
        var query = new GetAppealByIdQuery(id);
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
    public async Task<IActionResult> ReviewAppeal(Guid id, [FromBody] ReviewAppealRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var reviewerId = currentUserId != Guid.Empty ? currentUserId : (request.ReviewerId ?? Guid.Empty);

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
}
