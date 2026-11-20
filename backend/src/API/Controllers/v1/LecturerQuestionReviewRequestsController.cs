using MediatR;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;

namespace OralExamination.API.Controllers.v1;

[ApiController]
[Route("api/v1/lecturer/question-review-requests")]
[Authorize(Roles = "lecturer")]
public sealed class LecturerQuestionReviewRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? courseId, [FromQuery] string? status,
        [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var actorId)
            || actorId == Guid.Empty) return Unauthorized();
        return Ok(await sender.Send(new ListMyQuestionReviewRequests(actorId, courseId, status, page), cancellationToken));
    }
}
