using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;
using System;

namespace OralExamination.Application.Features.Practice.Commands.StartPracticeSession;

public sealed record StartPracticeSessionCommand(
    Guid StudentId,
    Guid CourseId,
    string Difficulty,
    int QuestionCount,
    bool IsFullSession = false,
    string? Topic = null
) : IRequest<Result<StartPracticeSessionResponse>>;
