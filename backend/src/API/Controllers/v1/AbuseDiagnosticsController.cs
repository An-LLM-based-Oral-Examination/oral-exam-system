using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using OralExamination.Application.Features.Admin.Queries.GetAbuseMetrics;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/diagnostics/abuse")]
[Authorize(Roles = "admin")]
public sealed class AbuseDiagnosticsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await sender.Send(new GetAbuseMetricsQuery(), cancellationToken));
}
