using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;

public sealed record SealOfficialExamAudioCommand(
    Guid ActorUserId,
    Guid TicketId,
    string? RemoteIpAddress,
    string? ObjectKey,
    string? Sha256)
    : IRequest<Result<OfficialExamAudioSealResponse>>;
