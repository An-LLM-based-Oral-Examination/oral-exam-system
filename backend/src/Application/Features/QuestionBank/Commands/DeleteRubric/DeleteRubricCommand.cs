using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.QuestionBank.Commands.DeleteRubric;

public sealed record DeleteRubricCommand(
    Guid ActorUserId,
    Guid RubricId
) : IRequest<Result>;
