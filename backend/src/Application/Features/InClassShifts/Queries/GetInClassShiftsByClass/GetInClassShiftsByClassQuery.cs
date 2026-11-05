using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftsByClass;

public sealed record GetInClassShiftsByClassQuery(
    Guid ActorUserId,
    Guid ClassId
) : IRequest<Result<IReadOnlyList<InClassExamShiftResponse>>>;
