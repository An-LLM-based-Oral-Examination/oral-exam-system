using MediatR;
using OralExamination.Application.Common.Models;
using System;

namespace OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;

public sealed record SubmitPracticeAnswerCommand(
    Guid SessionId,
    Guid QuestionId,
    Guid StudentId,
    string AnswerText,
    bool IsFollowUp,
    Guid? ParentAnswerId
) : IRequest<Result<Guid>>;
