using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.CreateQuestionAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.GenerateOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;
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
        CancellationToken cancellationToken = default,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateAudioUploadAuthorizationRequest? request = null)
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
            DateTime.UtcNow,
            ResolveDeviceId(),
            ResolveKioskSessionToken(),
            request?.ExpectedSha256Base64), cancellationToken);

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
        CancellationToken cancellationToken = default)
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
            request.Sha256,
            ResolveDeviceId(),
            ResolveKioskSessionToken()), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    [HttpPost("questions/{questionId:guid}/audio-upload-authorizations")]
    [ProducesResponseType(typeof(OfficialExamAudioUploadAuthorizationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateQuestionAudioUploadAuthorization(
        Guid ticketId,
        Guid questionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateQuestionAudioUploadAuthorizationRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var effectiveRequest = request ?? new CreateQuestionAudioUploadAuthorizationRequest();
        var result = await sender.Send(new CreateQuestionAudioUploadAuthorizationCommand(
            actorId.Value,
            ticketId,
            questionId,
            effectiveRequest.FollowUpIndex,
            effectiveRequest.ExpectedSha256Base64,
            ResolveDeviceId(),
            ResolveKioskSessionToken(),
            DateTime.UtcNow), cancellationToken);

        if (!result.IsSuccess)
        {
            return OfficialExamProblem(result.Error);
        }

        return Created(
            $"/api/v1/official-exams/tickets/{ticketId}/questions/{questionId}/answers",
            result.Value);
    }

    [HttpPut("questions/{questionId:guid}/answers")]
    [ProducesResponseType(typeof(SubmitOfficialQuestionAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitQuestionAnswer(
        Guid ticketId,
        Guid questionId,
        [FromBody] SubmitOfficialQuestionAnswerRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return OfficialExamProblem("BAD_REQUEST: Nội dung câu trả lời từng lượt không được để trống.");
        }

        var result = await sender.Send(new SubmitOfficialQuestionAnswerCommand(
            actorId.Value,
            ticketId,
            questionId,
            request.FollowUpIndex,
            request.ParentSubmissionId,
            request.AudioObjectKey ?? string.Empty,
            request.AudioHashSha256 ?? string.Empty,
            request.InitialVoiceTranscript,
            request.TranscriptWhisper,
            request.TimestampsWhisper,
            request.TimeSpentSeconds,
            request.IsManuallyEdited,
            ResolveDeviceId(),
            ResolveKioskSessionToken()), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    [HttpPost("submissions/{submissionId:guid}/next-followup")]
    [ProducesResponseType(typeof(OfficialExamNextFollowUpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RequestNextFollowUp(
        Guid ticketId,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new GenerateOfficialExamFollowUpCommand(
            actorId.Value,
            ticketId,
            submissionId,
            ResolveDeviceId(),
            ResolveKioskSessionToken()), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    [HttpPut("submission")]
    public async Task<IActionResult> Submit(
        Guid ticketId,
        [FromBody] SubmitOfficialExamRequest request,
        CancellationToken cancellationToken = default)
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
            request.Answers,
            ResolveDeviceId(),
            ResolveKioskSessionToken()), cancellationToken);

        if (!result.IsSuccess)
        {
            return OfficialExamProblem(result.Error);
        }

        return AcceptedAtAction(nameof(GetSubmissionStatus), new { ticketId }, result.Value);
    }

    [HttpGet("submission")]
    public async Task<IActionResult> GetSubmissionStatus(Guid ticketId, CancellationToken cancellationToken = default)
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
        // Chỉ đọc RemoteIpAddress đã được chuẩn hóa bởi ForwardedHeadersMiddleware từ KnownProxies/KnownNetworks.
        return HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private string? ResolveDeviceId()
    {
        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-Device-Id", out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        return null;
    }

    private string? ResolveKioskSessionToken()
    {
        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-Kiosk-Session-Token", out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        return null;
    }

    private Guid? GetActorId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var actorId) ? actorId : null;
    }

    private IActionResult OfficialExamProblem(string error)
    {
        var (statusCode, detail) = error switch
        {
            _ when error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status403Forbidden, error["FORBIDDEN:".Length..].Trim()),
            OfficialExamErrors.Forbidden or OfficialExamErrors.WrongWorkstation =>
                (StatusCodes.Status403Forbidden, error),
            _ when error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status404NotFound, error["NOT_FOUND:".Length..].Trim()),
            OfficialExamErrors.TicketNotFound or OfficialExamErrors.AudioObjectNotFound =>
                (StatusCodes.Status404NotFound, error),
            _ when error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status409Conflict, error["CONFLICT:".Length..].Trim()),
            OfficialExamErrors.TicketNotInProgress
                or OfficialExamErrors.AudioAlreadySealed
                or OfficialExamErrors.AudioHashMismatch
                or OfficialExamErrors.AudioObjectKeyMismatch
                or OfficialExamErrors.AudioNotSealed
                or OfficialExamErrors.SubmissionAlreadyExists
                or OfficialExamErrors.SubmissionQuestionsMismatch
                or OfficialExamErrors.DeviceMismatch
                or OfficialExamErrors.ActiveSessionConflict
                or OfficialExamErrors.DeviceInUseOtherTicket
                or OfficialExamErrors.FollowUpNotEnabled
                or OfficialExamErrors.FollowUpParentPromptMissing =>
                (StatusCodes.Status409Conflict, error),
            _ when error.StartsWith("BAD_REQUEST:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status400BadRequest, error["BAD_REQUEST:".Length..].Trim()),
            OfficialExamErrors.DeviceIdInvalid =>
                (StatusCodes.Status400BadRequest, error),
            _ when error.StartsWith("UNPROCESSABLE:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status422UnprocessableEntity, error["UNPROCESSABLE:".Length..].Trim()),
            _ => (StatusCodes.Status422UnprocessableEntity, error)
        };

        var errorType = error switch
        {
            _ when error.Contains("DEVICE_MISMATCH", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/device-mismatch",
            _ when error.Contains("ACTIVE_SESSION_CONFLICT", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/active-session-conflict",
            _ when error.Contains("DEVICE_IN_USE_OTHER_TICKET", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/device-in-use-other-ticket",
            _ => $"https://oralexam.fpt.edu.vn/errors/official-exam-{statusCode}"
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status400BadRequest => "Dữ liệu đầu vào không hợp lệ.",
                StatusCodes.Status403Forbidden => "Không được phép.",
                StatusCodes.Status404NotFound => "Không tìm thấy.",
                StatusCodes.Status409Conflict => "Xung đột trạng thái tài nguyên.",
                _ => "Dữ liệu không thể xử lý."
            },
            Detail = string.IsNullOrWhiteSpace(detail) ? error : detail,
            Type = errorType
        });
    }
}
