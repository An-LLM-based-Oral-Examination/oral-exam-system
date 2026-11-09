using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.AcademicHierarchy.Commands.SyncCourseOfferingLecturers;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseOfferingLecturers;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerCourseOfferings;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/course-offerings")]
[Authorize]
public sealed class CourseOfferingsController(ISender sender) : ControllerBase
{
    [HttpPut("{id:guid}/lecturers")]
    [Authorize(Roles = "department_head,admin")]
    public async Task<IActionResult> SyncLecturers(
        [FromRoute] Guid id,
        [FromBody] SyncCourseOfferingLecturersRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new SyncCourseOfferingLecturersCommand(GetCurrentUserId(), id, request.LecturerUserIds),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfferingProblem(result.Error);
    }

    [HttpGet("{id:guid}/lecturers")]
    [Authorize(Roles = "department_head,admin,lecturer")]
    public async Task<IActionResult> GetLecturers(
        [FromRoute] Guid id,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetCourseOfferingLecturersQuery(id, includeInactive),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfferingProblem(result.Error);
    }

    [HttpGet("/api/v1/lecturer/course-offerings")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> GetMyOfferings(
        [FromQuery] Guid? lecturerId = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var targetLecturerId = User.IsInRole("admin") && lecturerId is { } requestedId && requestedId != Guid.Empty
            ? requestedId
            : GetCurrentUserId();

        if (targetLecturerId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Không xác định được giảng viên hiện tại.",
                Type = "https://oralexam.fpt.edu.vn/errors/unauthorized"
            });
        }

        var result = await sender.Send(
            new GetLecturerCourseOfferingsQuery(targetLecturerId, includeInactive),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfferingProblem(result.Error);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private IActionResult OfferingProblem(string error)
    {
        var statusCode = error switch
        {
            var value when value.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) =>
                StatusCodes.Status403Forbidden,
            var value when value.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) =>
                StatusCodes.Status404NotFound,
            var value when value.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not Found",
                StatusCodes.Status409Conflict => "Conflict",
                _ => "Unprocessable Entity"
            },
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/course-offering-{statusCode}"
        });
    }
}
