using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftById;

public sealed record GetInClassShiftByIdQuery(
    Guid ActorUserId,
    Guid ShiftId
) : IRequest<Result<InClassShiftDetailResponse>>;
