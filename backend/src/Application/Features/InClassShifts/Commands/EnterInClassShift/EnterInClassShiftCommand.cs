using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;

public sealed record EnterInClassShiftCommand(
    Guid StudentId,
    Guid ShiftId,
    string OtpCode
) : IRequest<Result<JoinInClassShiftResponse>>;
