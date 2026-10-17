using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.DeadLetterQueue.DTOs;

namespace OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;

public class GetDlqTasksQueryHandler : IRequestHandler<GetDlqTasksQuery, Result<List<DeadLetterQueueDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetDlqTasksQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DeadLetterQueueDto>>> Handle(GetDlqTasksQuery request, CancellationToken cancellationToken)
    {
        var query = _context.DeadLetterQueues
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(q => q.Status == request.Status);
        }

        var tasks = await query
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new DeadLetterQueueDto
            {
                Id = q.Id,
                TaskType = q.TaskType,
                PayloadJson = q.PayloadJson,
                ErrorMessage = q.ErrorMessage,
                RetryCount = q.RetryCount,
                Status = q.Status,
                CreatedAt = q.CreatedAt,
                ResolvedAt = q.ResolvedAt
            })
            .ToListAsync(cancellationToken);

        return Result<List<DeadLetterQueueDto>>.Success(tasks);
    }
}
