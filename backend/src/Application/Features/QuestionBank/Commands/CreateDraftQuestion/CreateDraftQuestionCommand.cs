using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;

public sealed record CreateDraftQuestionCommand(
    Guid CourseId,
    Guid LecturerId,
    string Title,
    string Content,
    string? SampleAnswer,
    List<string> KeyPoints,
    string Difficulty,
    string BloomLevel,
    string Source,
    string UsageScope,
    bool HasFollowUp,
    string? FollowUpPrompt,
    RubricDraftDto Rubric
) : IRequest<Result<Guid>>;
