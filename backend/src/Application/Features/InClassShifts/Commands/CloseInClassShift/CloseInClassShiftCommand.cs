using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.InClassShifts.Commands.CloseInClassShift;

public sealed record CloseInClassShiftCommand(
    Guid ActorUserId,
    Guid ShiftId
) : IRequest<Result<CloseInClassShiftResponse>>;

public sealed record CloseInClassShiftResponse(
    Guid ShiftId,
    string ShiftName,
    string Status
);
