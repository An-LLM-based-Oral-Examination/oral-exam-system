using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamSetById;

public sealed record GetExamSetByIdQuery(
    Guid ExamSetId,
    Guid ActorUserId = default,
    string? ActorRole = null,
    bool IncludeInactive = false) : IRequest<Result<ExamSetDto>>;
