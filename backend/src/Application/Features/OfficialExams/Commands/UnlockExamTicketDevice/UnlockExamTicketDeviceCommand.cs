using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.UnlockExamTicketDevice;

public sealed record UnlockExamTicketDeviceCommand(
    Guid ActorUserId,
    Guid TicketId,
    string Reason) : IRequest<Result<UnlockExamTicketDeviceResponse>>;
