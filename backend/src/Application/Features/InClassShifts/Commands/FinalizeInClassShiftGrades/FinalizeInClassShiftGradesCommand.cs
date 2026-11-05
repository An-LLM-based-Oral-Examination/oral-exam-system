using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;

public sealed record FinalizeInClassShiftGradesCommand(
    Guid ActorUserId,
    Guid ShiftId
) : IRequest<Result<FinalizeInClassGradesResponse>>;
