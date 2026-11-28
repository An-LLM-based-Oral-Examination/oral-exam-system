using MediatR;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.API.Configuration;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;

namespace OralExamination.API.Controllers.v1;

[ApiController]
[Route("api/v1/lecturer/question-review-requests")]
[Authorize(Roles = "lecturer")]
public sealed class LecturerQuestionReviewRequestsController(ISender sender) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? courseId, [FromQuery] string? status,
        [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (ActorId == Guid.Empty) return Unauthorized();
        return Ok(await sender.Send(new ListMyQuestionReviewRequests(ActorId, courseId, status, page), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken = default)
    {
        if (ActorId == Guid.Empty) return Unauthorized();
        var result = await sender.Send(new GetMyQuestionReviewRequest(ActorId, id), cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);
        var status = result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status404NotFound;
        return StatusCode(status, ApiProblems.CreateForController(HttpContext, status, result.Error));
    }
}
