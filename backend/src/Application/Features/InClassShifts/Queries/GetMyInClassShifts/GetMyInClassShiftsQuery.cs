using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetMyInClassShifts;

public sealed record GetMyInClassShiftsQuery(Guid StudentUserId)
    : IRequest<Result<IReadOnlyList<InClassExamShiftResponse>>>;
