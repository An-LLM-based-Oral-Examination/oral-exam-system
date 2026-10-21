using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;

public sealed record CreateRubricCommand(
    Guid ActorUserId,
    Guid CourseId,
    string Name,
    string? Description,
    List<RubricCriterionDraftDto> Criteria
) : IRequest<Result<CreateRubricResponseDto>>;
