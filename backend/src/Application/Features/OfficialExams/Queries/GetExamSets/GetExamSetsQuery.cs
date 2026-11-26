using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;

public sealed record GetExamSetsQuery(
    Guid? CourseId = null,
    Guid? SessionId = null,
    string? Status = null,
    Guid ActorUserId = default,
    string? ActorRole = null,
    Guid? StructureId = null,
    bool IncludeInactive = false) : IRequest<Result<List<ExamSetDto>>>;
