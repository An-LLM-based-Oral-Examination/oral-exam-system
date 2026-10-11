using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;
using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Practice.Commands.StartPracticeSession;

public sealed record StartPracticeSessionCommand(
    Guid StudentId,
    Guid CourseId,
    string? Difficulty = null,
    int? QuestionCount = null,
    bool IsFullSession = false,
    string? Topic = null,
    List<string>? Difficulties = null
) : IRequest<Result<StartPracticeSessionResponse>>;
