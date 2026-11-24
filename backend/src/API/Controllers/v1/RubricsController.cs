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
[Authorize(Roles = "lecturer,department_head")]
public class RubricsController : ControllerBase
{
    private readonly ISender _sender;

    public RubricsController(ISender sender)
    {
        _sender = sender;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User?.FindFirstValue("sub");

        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        return Guid.Empty;
    }

    private string? GetActorRole()
    {
        if (User?.IsInRole("department_head") == true) return "department_head";
        if (User?.IsInRole("lecturer") == true) return "lecturer";
        if (User?.IsInRole("admin") == true) return "admin";
        if (User?.IsInRole("examination_officer") == true) return "examination_officer";
        if (User?.IsInRole("proctor") == true) return "proctor";
        if (User?.IsInRole("student") == true) return "student";
        return null;
    }

    /// <summary>
    /// Tra cứu danh sách Barem Rubric theo học phần
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRubrics(
        [FromQuery] Guid? courseId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetRubricsQuery(courseId, null, GetCurrentUserId(), GetActorRole()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return RubricProblem(result.Error);
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
        var result = await _sender.Send(
            new GetRubricsQuery(null, id, GetCurrentUserId(), GetActorRole()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return RubricProblem(result.Error);
        }

        if (result.Value == null || result.Value.Count == 0)
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
            return RubricProblem(result.Error);
        }

        return CreatedAtAction(nameof(GetRubricById), new { id = result.Value!.Id }, result.Value);
    }

    private IActionResult RubricProblem(string error)
    {
        if (error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                type = "https://oralexam.fpt.edu.vn/errors/rubric-forbidden",
                title = "Không có quyền truy cập Barem Rubric",
                status = StatusCodes.Status403Forbidden,
                detail = error
            });
        }

        if (error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/course-not-found",
                title = "Không tìm thấy môn học",
                status = StatusCodes.Status404NotFound,
                detail = error
            });
        }

        if (error.StartsWith("INVARIANT:", StringComparison.OrdinalIgnoreCase) ||
            error.StartsWith("VALIDATION:", StringComparison.OrdinalIgnoreCase))
        {
            return UnprocessableEntity(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/rubric-invariant-violation",
                title = "Vi phạm quy tắc Barem Rubric 10.00 điểm",
                status = StatusCodes.Status422UnprocessableEntity,
                detail = error
            });
        }

        return BadRequest(new
        {
            type = "https://oralexam.fpt.edu.vn/errors/rubric-request-failed",
            title = "Không thể xử lý yêu cầu Barem Rubric",
            status = StatusCodes.Status400BadRequest,
            detail = error
        });
    }
}
