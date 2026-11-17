using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;

namespace OralExamination.API.Controllers.v1;

[ApiController]
[Route("api/v1/department-head/question-review-requests")]
[Authorize(Roles = "department_head,admin")]
public sealed class QuestionReviewRequestsController(QuestionReviewRequestService service) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? courseId, [FromQuery] string? status,
        [FromQuery] string? keyword, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (ActorId == Guid.Empty) return Unauthorized();
        return Ok(await service.List(ActorId, courseId, status, keyword, page, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        if (ActorId == Guid.Empty) return Unauthorized();
        var result = await service.Detail(ActorId, id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new ProblemDetails { Detail = result.Error, Status = 404 });
    }

    [HttpPost("{id:guid}/decisions")]
    public async Task<IActionResult> Decide(Guid id, [FromBody] ReviewRequestDecision input, CancellationToken cancellationToken)
    {
        if (ActorId == Guid.Empty) return Unauthorized();
        var result = await service.Decide(ActorId, id, input, cancellationToken);
        if (result.IsSuccess) return Ok(new { message = "Đã lưu quyết định thẩm định." });
        var status = result.Error.StartsWith("CONFLICT:") ? 409 : result.Error.StartsWith("NOT_FOUND:") ? 404 : 400;
        return StatusCode(status, new ProblemDetails { Detail = result.Error, Status = status });
    }
}
