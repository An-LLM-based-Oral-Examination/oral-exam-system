using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;

public sealed record CreateAudioUploadAuthorizationCommand(
    Guid ActorUserId,
    Guid TicketId,
    string? RemoteIpAddress,
    DateTime AtUtc)
    : IRequest<Result<OfficialExamAudioUploadAuthorizationResponse>>;
