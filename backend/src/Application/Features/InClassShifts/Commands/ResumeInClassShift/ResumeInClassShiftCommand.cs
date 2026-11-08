using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Commands.ResumeInClassShift;

public sealed record ResumeInClassShiftCommand(
    Guid StudentId,
    Guid ShiftId
) : IRequest<Result<JoinInClassShiftResponse>>;
