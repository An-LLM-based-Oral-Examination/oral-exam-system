using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.AcademicCatalog.Commands.AssignProgramDepartmentHead;
using OralExamination.Application.Features.AcademicCatalog.Commands.CreateAcademicProgram;
using OralExamination.Application.Features.AcademicCatalog.Commands.UpdateAcademicProgram;
using OralExamination.Application.Features.AcademicCatalog.Queries.GetAcademicPrograms;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/academic-programs")]
[Authorize]
public sealed class AcademicProgramsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPrograms(
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveIsActive = User?.IsInRole("admin") == true ? isActive : true;
        var result = await sender.Send(new GetAcademicProgramsQuery(effectiveIsActive), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateProgram(
        [FromBody] CreateAcademicProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new CreateAcademicProgramCommand(
                GetCurrentUserId(),
                request.Code,
                request.Name,
                request.DepartmentHeadUserId,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return CatalogProblem(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateProgram(
        [FromRoute] Guid id,
        [FromBody] UpdateAcademicProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new UpdateAcademicProgramCommand(
                GetCurrentUserId(),
                id,
                request.Code,
                request.Name,
                request.IsActive,
                request.DepartmentHeadUserId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return CatalogProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/department-head")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignDepartmentHead(
        [FromRoute] Guid id,
        [FromBody] AssignProgramDepartmentHeadRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new AssignProgramDepartmentHeadCommand(GetCurrentUserId(), id, request.DepartmentHeadUserId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return CatalogProblem(result.Error);
        }

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private IActionResult CatalogProblem(string error)
    {
        var statusCode = error switch
        {
            var err when err.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var err when err.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        var title = statusCode switch
        {
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            _ => "Unprocessable Entity"
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/academic-program-{statusCode}"
        });
    }
}

public sealed record CreateAcademicProgramRequest(
    string Code,
    string Name,
    Guid? DepartmentHeadUserId = null,
    bool IsActive = true);

public sealed record UpdateAcademicProgramRequest(
    string Code,
    string Name,
    bool IsActive = true,
    Guid? DepartmentHeadUserId = null);

public sealed record AssignProgramDepartmentHeadRequest(Guid DepartmentHeadUserId);
