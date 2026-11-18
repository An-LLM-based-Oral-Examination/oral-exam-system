using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.DistributeShiftTickets;

public sealed record DistributeShiftTicketsCommand(
    Guid ActorUserId,
    string? ActorRole,
    Guid ShiftId,
    bool AllowRandomShuffle = true) : IRequest<Result<DistributeShiftTicketsResponse>>;
