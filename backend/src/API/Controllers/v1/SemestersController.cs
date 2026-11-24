using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.AcademicCatalog.Commands.SyncSemesterCourseOfferings;
using OralExamination.Application.Features.AcademicCatalog.Queries.GetSemesterCourseCatalog;
using OralExamination.Application.Features.Semesters.Commands.CreateSemester;
using OralExamination.Application.Features.Semesters.Commands.UpdateSemester;
using OralExamination.Application.Features.Semesters.Queries.GetAcademicClasses;
using OralExamination.Application.Features.Semesters.Queries.GetSemesters;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản lý Học kỳ và Lớp học phần (BE-V06 / FE-08).
/// Bất biến FE-08: Cho phép Xem, Tạo mới và Cập nhật học kỳ; TUYỆT ĐỐI CẤM xóa học kỳ.
/// </summary>
[ApiController]
[Route("api/v1/semesters")]
[Route("api/v1/academic/semesters")]
[Authorize]
public class SemestersController(ISender sender) : ControllerBase
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

    [HttpGet]
    public async Task<IActionResult> GetSemesters(
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetSemestersQuery(isActive), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    [HttpPost("/api/v1/admin/semesters")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateSemester(
        [FromBody] SaveSemesterRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetCurrentUserId();
        var result = await _sender.Send(
            new CreateSemesterCommand(actorId, request.Code, request.Name, request.StartDate, request.EndDate, request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SemesterProblem(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{id:guid}")]
    [HttpPut("/api/v1/admin/semesters/{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateSemester(
        [FromRoute] Guid id,
        [FromBody] SaveSemesterRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetCurrentUserId();
        var result = await _sender.Send(
            new UpdateSemesterCommand(actorId, id, request.Code, request.Name, request.StartDate, request.EndDate, request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SemesterProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/course-offerings")]
    [Authorize(Roles = "admin,department_head")]
    public async Task<IActionResult> GetCourseOfferings(
        [FromRoute] Guid id,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetSemesterCourseCatalogQuery(id, includeInactive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SemesterProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/course-offerings")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SyncCourseOfferings(
        [FromRoute] Guid id,
        [FromBody] SyncSemesterCourseOfferingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new SyncSemesterCourseOfferingsCommand(GetCurrentUserId(), id, request.SelectedCourseIds),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SemesterProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("/api/v1/academic/classes")]
    [HttpGet("classes")]
    [Authorize(Roles = "admin,department_head,lecturer,examination_officer")]
    public async Task<IActionResult> GetAcademicClasses(
        [FromQuery] Guid? courseId = null,
        [FromQuery] Guid? semesterId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? lecturerId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetAcademicClassesQuery(
                courseId,
                semesterId,
                isActive,
                lecturerId,
                GetCurrentUserId(),
                GetActorRole()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return SemesterProblem(result.Error);
        }

        return Ok(result.Value);
    }

    private IActionResult SemesterProblem(string error)
    {
        var statusCode = error switch
        {
            var err when err.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var err when err.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var err when err.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Không có quyền truy cập.",
                StatusCodes.Status404NotFound => "Không tìm thấy học kỳ.",
                StatusCodes.Status409Conflict => "Trùng mã học kỳ.",
                _ => "Dữ liệu học kỳ không hợp lệ."
            },
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/semester-{statusCode}"
        });
    }
}

public sealed record SaveSemesterRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record SyncSemesterCourseOfferingsRequest
{
    public IReadOnlyCollection<Guid> SelectedCourseIds { get; init; } = Array.Empty<Guid>();
}
