using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;

public sealed record CreateAudioUploadAuthorizationCommand(
    Guid ActorUserId,
    Guid TicketId,
    string? RemoteIpAddress,
    DateTime AtUtc,
    string? DeviceId = null,
    string? KioskSessionToken = null,
    string? ExpectedSha256Base64 = null)
    : IRequest<Result<OfficialExamAudioUploadAuthorizationResponse>>;
