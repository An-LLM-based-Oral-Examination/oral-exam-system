using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateQuestionAudioUploadAuthorization;

public sealed record CreateQuestionAudioUploadAuthorizationCommand(
    Guid ActorUserId,
    Guid TicketId,
    Guid QuestionId,
    int FollowUpIndex,
    string? ExpectedSha256Base64,
    string? DeviceId,
    string? KioskSessionToken,
    DateTime AtUtc) : IRequest<Result<OfficialExamAudioUploadAuthorizationResponse>>;
