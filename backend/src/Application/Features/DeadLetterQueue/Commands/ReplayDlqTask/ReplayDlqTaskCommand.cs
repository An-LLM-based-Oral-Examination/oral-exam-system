using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;

public record ReplayDlqTaskCommand(
    Guid Id,
    Guid? ActorUserId = null,
    string? ActorRole = null) : IRequest<Result<Unit>>;
