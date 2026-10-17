using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.DeadLetterQueue.DTOs;

namespace OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;

public record GetDlqTasksQuery(string? Status = null) : IRequest<Result<List<DeadLetterQueueDto>>>;
