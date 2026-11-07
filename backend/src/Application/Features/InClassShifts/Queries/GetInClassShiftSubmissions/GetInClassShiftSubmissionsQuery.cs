using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftSubmissions;

public sealed record GetInClassShiftSubmissionsQuery(
    Guid ActorUserId,
    Guid ShiftId
) : IRequest<Result<IReadOnlyList<InClassExamSubmissionResponse>>>;
