using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;

public record ReplayDlqTaskCommand(Guid Id) : IRequest<Result<Unit>>;
