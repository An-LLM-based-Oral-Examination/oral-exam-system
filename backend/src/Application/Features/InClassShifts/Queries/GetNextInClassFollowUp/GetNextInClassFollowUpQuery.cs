using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetNextInClassFollowUp;

public sealed record GetNextInClassFollowUpQuery(
    Guid StudentUserId,
    Guid ShiftId,
    Guid SubmissionId
) : IRequest<Result<InClassNextFollowUpResponse>>;
