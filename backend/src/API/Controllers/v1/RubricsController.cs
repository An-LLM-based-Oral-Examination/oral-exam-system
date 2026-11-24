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
using OralExamination.Application.Features.QuestionBank.Commands.DeleteRubric;
using OralExamination.Application.Features.QuestionBank.Commands.UpdateRubric;
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
            return NotFound(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/rubric-not-found",
                Title = "Không tìm thấy Barem Rubric",
                Status = StatusCodes.Status404NotFound,
                Detail = "Không tìm thấy Barem Rubric với mã tương ứng."
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

    /// <summary>
    /// Cập nhật Barem Rubric 10.00 điểm chuẩn hóa (chặn 409 nếu đã gắn bài thi thật đã nộp/khóa).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateRubric(
        [FromRoute] Guid id,
        [FromBody] UpdateRubricRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateRubricCommand(
            GetCurrentUserId(),
            id,
            request.Name,
            request.Description,
            request.Criteria
        );

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return RubricProblem(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Vô hiệu hóa mềm Barem Rubric (chặn 409 nếu đang được tham chiếu bởi câu hỏi chưa xóa).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRubric(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteRubricCommand(GetCurrentUserId(), id);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return RubricProblem(result.Error);
        }

        return Ok(new
        {
            success = true,
            id,
            isActive = false,
            message = "Đã vô hiệu hóa Barem Rubric thành công."
        });
    }

    private IActionResult RubricProblem(string error)
    {
        if (error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/rubric-forbidden",
                Title = "Không có quyền truy cập Barem Rubric",
                Status = StatusCodes.Status403Forbidden,
                Detail = error
            });
        }

        if (error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/course-not-found",
                Title = "Không tìm thấy tài nguyên",
                Status = StatusCodes.Status404NotFound,
                Detail = error
            });
        }

        if (error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/rubric-conflict",
                Title = "Xung đột tham chiếu Barem Rubric",
                Status = StatusCodes.Status409Conflict,
                Detail = error
            });
        }

        if (error.StartsWith("INVARIANT:", StringComparison.OrdinalIgnoreCase) ||
            error.StartsWith("VALIDATION:", StringComparison.OrdinalIgnoreCase))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/rubric-invariant-violation",
                Title = "Vi phạm quy tắc Barem Rubric 10.00 điểm",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = error
            });
        }

        return BadRequest(new ProblemDetails
        {
            Type = "https://oralexam.fpt.edu.vn/errors/rubric-request-failed",
            Title = "Không thể xử lý yêu cầu Barem Rubric",
            Status = StatusCodes.Status400BadRequest,
            Detail = error
        });
    }
}
