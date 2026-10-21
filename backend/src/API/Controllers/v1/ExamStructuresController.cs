using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản lý Cấu trúc Đề thi (ExamStructure) theo môn học và phân bổ Bloom (MF-02 & MF-04).
/// Chuẩn RESTful RFC 7807 Problem Details.
/// </summary>
[ApiController]
[Route("api/v1/exam-structures")]
[Authorize]
public class ExamStructuresController(ISender sender) : ControllerBase
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

    /// <summary>
    /// Lấy danh sách cấu trúc đề thi theo môn học hoặc trạng thái hoạt động.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetExamStructures(
        [FromQuery] Guid? courseId = null,
        [FromQuery] bool? isActive = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetExamStructuresQuery(courseId, null, isActive), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy chi tiết một cấu trúc đề thi theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExamStructureById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetExamStructuresQuery(null, id, null), cancellationToken);
        if (!result.IsSuccess || result.Value.Count == 0)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy cấu trúc đề thi.",
                Detail = "Cấu trúc đề thi không tồn tại trong hệ thống.",
                Type = "https://oralexam.fpt.edu.vn/errors/exam-structure-not-found"
            });
        }

        return Ok(result.Value[0]);
    }

    /// <summary>
    /// Trưởng Bộ Môn hoặc Quản trị viên tạo mới cấu trúc đề thi cho môn học.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "department_head,admin")]
    public async Task<IActionResult> CreateExamStructure(
        [FromBody] CreateExamStructureRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetCurrentUserId();
        var command = new CreateExamStructureCommand(
            actorId,
            request.CourseId,
            request.Name,
            request.TotalQuestions,
            request.DurationMinutes,
            request.BloomDistribution,
            request.IsActive);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return StructureProblem(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Trưởng Bộ Môn hoặc Quản trị viên cập nhật cấu trúc đề thi.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "department_head,admin")]
    public async Task<IActionResult> UpdateExamStructure(
        [FromRoute] Guid id,
        [FromBody] UpdateExamStructureRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetCurrentUserId();
        var command = new UpdateExamStructureCommand(
            actorId,
            id,
            request.Name,
            request.TotalQuestions,
            request.DurationMinutes,
            request.BloomDistribution,
            request.IsActive);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return StructureProblem(result.Error);
        }

        return Ok(result.Value);
    }

    private IActionResult StructureProblem(string error)
    {
        var statusCode = error switch
        {
            var err when err.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var err when err.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Không được phép.",
                StatusCodes.Status404NotFound => "Không tìm thấy tài nguyên.",
                _ => "Dữ liệu cấu trúc đề thi không hợp lệ."
            },
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/exam-structure-{statusCode}"
        });
    }
}
