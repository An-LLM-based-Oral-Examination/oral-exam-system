using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetRubrics;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/rubrics")]
[Authorize(Roles = "lecturer,department_head,admin")]
public class RubricsController : ControllerBase
{
    private readonly ISender _sender;

    public RubricsController(ISender sender)
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

        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-User-Id", out var headerVal) &&
            Guid.TryParse(headerVal, out var headerId))
        {
            return headerId;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Tra cứu danh sách Barem Rubric theo học phần
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRubrics(
        [FromQuery] Guid? courseId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetRubricsQuery(courseId, null), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { detail = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Xem chi tiết một Barem Rubric theo Id
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRubricById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetRubricsQuery(null, id), cancellationToken);
        if (!result.IsSuccess || result.Value == null || result.Value.Count == 0)
        {
            return NotFound(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/rubric-not-found",
                title = "Không tìm thấy Barem Rubric",
                status = StatusCodes.Status404NotFound,
                detail = "Không tìm thấy Barem Rubric với mã tương ứng."
            });
        }

        return Ok(result.Value.First());
    }

    /// <summary>
    /// Khởi tạo Barem Rubric 10.00 điểm chuẩn hóa (MF-03 Rubric Studio)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRubric(
        [FromBody] CreateRubricRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateRubricCommand(
            GetCurrentUserId(),
            request.CourseId,
            request.Name,
            request.Description,
            request.Criteria
        );

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/course-not-found",
                    title = "Không tìm thấy môn học",
                    status = StatusCodes.Status404NotFound,
                    detail = result.Error
                });
            }

            if (result.Error.StartsWith("INVARIANT:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.StartsWith("VALIDATION:", StringComparison.OrdinalIgnoreCase))
            {
                return UnprocessableEntity(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/rubric-invariant-violation",
                    title = "Vi phạm quy tắc Barem Rubric 10.00 điểm",
                    status = StatusCodes.Status422UnprocessableEntity,
                    detail = result.Error
                });
            }

            return BadRequest(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/rubric-creation-failed",
                title = "Không thể tạo Barem Rubric",
                status = StatusCodes.Status400BadRequest,
                detail = result.Error
            });
        }

        return CreatedAtAction(nameof(GetRubricById), new { id = result.Value!.Id }, result.Value);
    }
}
