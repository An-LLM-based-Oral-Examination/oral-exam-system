using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.InClassShifts.Commands.ActivateInClassShift;

public sealed record ActivateInClassShiftCommand(
    Guid ActorUserId,
    Guid ShiftId
) : IRequest<Result<ActivateInClassShiftResponse>>;

public sealed record ActivateInClassShiftResponse(
    Guid ShiftId,
    string ShiftName,
    string Status
);
