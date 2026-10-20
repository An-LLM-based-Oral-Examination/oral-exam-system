using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSubmissionStatus;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/official-exams/tickets/{ticketId:guid}")]
[Authorize(Roles = "student")]
public sealed class OfficialExamSubmissionsController(ISender sender) : ControllerBase
{
    [HttpPost("audio-upload-authorizations")]
    public async Task<IActionResult> CreateAudioUploadAuthorization(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new CreateAudioUploadAuthorizationCommand(
            actorId.Value,
            ticketId,
            ResolveRemoteIpAddress(),
            DateTime.UtcNow), cancellationToken);

        if (!result.IsSuccess)
        {
            return OfficialExamProblem(result.Error);
        }

        return Created(
            $"/api/v1/official-exams/tickets/{ticketId}/audio",
            result.Value);
    }

    [HttpPut("audio")]
    public async Task<IActionResult> SealAudio(
        Guid ticketId,
        [FromBody] SealOfficialExamAudioRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new SealOfficialExamAudioCommand(
            actorId.Value,
            ticketId,
            ResolveRemoteIpAddress(),
            request.ObjectKey,
            request.Sha256), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    [HttpPut("submission")]
    public async Task<IActionResult> Submit(
        Guid ticketId,
        [FromBody] SubmitOfficialExamRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new SubmitOfficialExamCommand(
            actorId.Value,
            ticketId,
            ResolveRemoteIpAddress(),
            request.Answers), cancellationToken);

        if (!result.IsSuccess)
        {
            return OfficialExamProblem(result.Error);
        }

        return AcceptedAtAction(nameof(GetSubmissionStatus), new { ticketId }, result.Value);
    }

    [HttpGet("submission")]
    public async Task<IActionResult> GetSubmissionStatus(Guid ticketId, CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await sender.Send(
            new GetOfficialExamSubmissionStatusQuery(actorId.Value, ticketId),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    private string? ResolveRemoteIpAddress()
    {
        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) &&
            !string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.ToString().Split(',')[0].Trim();
        }

        return HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private Guid? GetActorId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var actorId) ? actorId : null;
    }

    private IActionResult OfficialExamProblem(string error)
    {
        var statusCode = error switch
        {
            OfficialExamErrors.Forbidden or OfficialExamErrors.WrongWorkstation => StatusCodes.Status403Forbidden,
            OfficialExamErrors.TicketNotFound or OfficialExamErrors.AudioObjectNotFound => StatusCodes.Status404NotFound,
            OfficialExamErrors.TicketNotInProgress
                or OfficialExamErrors.AudioAlreadySealed
                or OfficialExamErrors.AudioHashMismatch
                or OfficialExamErrors.AudioObjectKeyMismatch
                or OfficialExamErrors.AudioNotSealed
                or OfficialExamErrors.SubmissionAlreadyExists
                or OfficialExamErrors.SubmissionQuestionsMismatch => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Không được phép.",
                StatusCodes.Status404NotFound => "Không tìm thấy.",
                StatusCodes.Status409Conflict => "Xung đột trạng thái tài nguyên.",
                _ => "Dữ liệu không thể xử lý."
            },
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/official-exam-{statusCode}"
        });
    }
}
