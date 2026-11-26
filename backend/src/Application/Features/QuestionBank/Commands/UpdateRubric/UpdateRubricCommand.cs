using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.UpdateRubric;

public sealed record UpdateRubricCommand(
    Guid ActorUserId,
    Guid RubricId,
    string Name,
    string? Description,
    List<RubricCriterionDraftDto> Criteria
) : IRequest<Result<CreateRubricResponseDto>>;
