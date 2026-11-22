using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.GenerateOfficialExamFollowUp;

public sealed record GenerateOfficialExamFollowUpCommand(
    Guid ActorUserId,
    Guid TicketId,
    Guid SubmissionId,
    string? DeviceId,
    string? KioskSessionToken) : IRequest<Result<OfficialExamNextFollowUpResponse>>;
