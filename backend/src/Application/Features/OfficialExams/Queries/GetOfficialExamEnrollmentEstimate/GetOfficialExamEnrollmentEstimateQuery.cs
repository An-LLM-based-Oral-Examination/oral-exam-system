using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamEnrollmentEstimate;

public sealed record GetOfficialExamEnrollmentEstimateQuery(
    Guid ActorUserId,
    Guid SessionId) : IRequest<Result<OfficialExamEnrollmentEstimateDto>>;
