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
[Authorize(Roles = "admin,department_head,lecturer,examination_officer")]
public class ExamStructuresController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    private Guid GetCurrentUserId()
    {
        var claim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User?.FindFirstValue("sub");

        if (Guid.TryParse(claim, out var id) && id != Guid.Empty)
        {
            return id;
        }

        return Guid.Empty;
    }

    private string? GetActorRole()
    {
        if (User?.IsInRole("admin") == true) return "admin";
        if (User?.IsInRole("department_head") == true) return "department_head";
        if (User?.IsInRole("examination_officer") == true) return "examination_officer";
        if (User?.IsInRole("lecturer") == true) return "lecturer";
        if (User?.IsInRole("proctor") == true) return "proctor";
        if (User?.IsInRole("student") == true) return "student";
        return null;
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
        var result = await _sender.Send(
            new GetExamStructuresQuery(courseId, null, isActive, GetCurrentUserId(), GetActorRole()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return StructureProblem(result.Error);
        }

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
        var result = await _sender.Send(
            new GetExamStructuresQuery(null, id, null, GetCurrentUserId(), GetActorRole()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return StructureProblem(result.Error);
        }

        if (result.Value.Count == 0)
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
    /// Trưởng Bộ Môn phụ trách môn tạo mới cấu trúc đề thi.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "department_head")]
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
            request.IsActive,
            request.RequiredSetCount,
            request.SlotDefinitions);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return StructureProblem(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Trưởng Bộ Môn phụ trách môn cập nhật cấu trúc đề thi.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "department_head")]
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
            request.IsActive,
            request.RequiredSetCount,
            request.SlotDefinitions);

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
