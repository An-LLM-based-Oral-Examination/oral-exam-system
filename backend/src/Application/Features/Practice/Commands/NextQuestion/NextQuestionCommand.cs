using MediatR;
using OralExamination.Application.Common.Models;
using System;

namespace OralExamination.Application.Features.Practice.Commands.NextQuestion;

public sealed record NextQuestionCommand(
    Guid SessionId,
    Guid StudentId
) : IRequest<Result<NextQuestionResponse>>;
