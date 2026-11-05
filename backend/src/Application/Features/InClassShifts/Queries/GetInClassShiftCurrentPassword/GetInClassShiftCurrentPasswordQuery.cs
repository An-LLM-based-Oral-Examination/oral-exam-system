using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftCurrentPassword;

public sealed record GetInClassShiftCurrentPasswordQuery(
    Guid ActorUserId,
    Guid ShiftId
) : IRequest<Result<ActiveShiftPasswordResponse>>;
