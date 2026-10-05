using MediatR;
using OralExamination.Application.Common.Models;
using System;

namespace OralExamination.Application.Features.Practice.Commands.StartPracticeSession;

public sealed record StartPracticeSessionCommand(
    Guid StudentId,
    Guid CourseId,
    bool IsFullSession
) : IRequest<Result<Guid>>;
