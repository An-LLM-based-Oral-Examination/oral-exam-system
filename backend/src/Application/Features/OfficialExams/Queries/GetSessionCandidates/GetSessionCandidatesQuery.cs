using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetSessionCandidates;

public sealed record GetSessionCandidatesQuery(
    Guid ActorUserId,
    Guid SessionId,
    string? Search = null,
    bool? AllocatedOnly = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<SessionCandidatesPageResponse>>;
