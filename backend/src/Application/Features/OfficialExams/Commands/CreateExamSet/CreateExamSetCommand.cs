using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;

public sealed record CreateExamSetCommand(
    Guid StructureId,
    Guid CourseId,
    Guid? SessionId,
    string SetCode,
    List<Guid> QuestionIds,
    Guid ActorUserId,
    string? ActorRole = null) : IRequest<Result<ExamSetDto>>;
