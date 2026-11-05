using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.AcademicCatalog.Commands.AssignProgramDepartmentHead;
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
        var result = await sender.Send(new GetAcademicProgramsQuery(isActive), cancellationToken);
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
        var statusCode = error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status404NotFound ? "Not Found" : "Unprocessable Entity",
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/academic-program-{statusCode}"
        });
    }
}

public sealed record AssignProgramDepartmentHeadRequest(Guid DepartmentHeadUserId);
