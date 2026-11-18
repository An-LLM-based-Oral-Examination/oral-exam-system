using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftPoolStats;

public sealed record GetInClassShiftPoolStatsQuery(
    Guid ActorUserId,
    Guid ClassId
) : IRequest<Result<InClassShiftPoolStatsResponse>>;
