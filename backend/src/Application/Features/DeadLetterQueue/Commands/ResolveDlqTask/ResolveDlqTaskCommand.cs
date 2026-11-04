using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.DeadLetterQueue.Commands.ResolveDlqTask;

public record ResolveDlqTaskCommand(
    Guid Id,
    string? ResolutionNotes = null,
    Guid? ResolvedByUserId = null,
    string? ActorRole = null) : IRequest<Result<Unit>>;
