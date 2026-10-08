using MediatR;
using OralExamination.Application.Common.Models;
using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;

public sealed record SubmitPracticeBatchCommand(
    Guid SessionId,
    Guid StudentId,
    List<BatchAnswerDto> Answers
) : IRequest<Result<Unit>>;

public record BatchAnswerDto(
    Guid QuestionId,
    string AnswerText,
    bool IsFollowUp,
    Guid? ParentAnswerId
);
