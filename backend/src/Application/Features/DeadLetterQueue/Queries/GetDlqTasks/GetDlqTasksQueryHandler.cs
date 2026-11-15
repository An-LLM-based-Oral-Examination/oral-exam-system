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
        var actor = await DlqAuthorization.ActorAsync(_context, request.ActorUserId, null, false, cancellationToken);
        if (actor is null) return Result<List<DeadLetterQueueDto>>.Failure(DlqAuthorization.Forbidden);
        if ((!string.IsNullOrWhiteSpace(request.FlowType) && !DlqAuthorization.CanManageFlow(actor.Role, request.FlowType))
            || (request.FlowTypes is not null && request.FlowTypes.Any(f => !DlqAuthorization.CanManageFlow(actor.Role, f))))
            return Result<List<DeadLetterQueueDto>>.Failure(DlqAuthorization.Forbidden);

        var query = _context.DeadLetterQueues
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToLowerInvariant();
            query = query.Where(q => q.Status.ToLower() == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.FlowType))
        {
            query = query.Where(q => q.FlowType == request.FlowType);
        }

        if (request.FlowTypes != null && request.FlowTypes.Count > 0)
        {
            query = query.Where(q => request.FlowTypes.Contains(q.FlowType));
        }

        if (!string.IsNullOrWhiteSpace(request.ManagedByRole))
        {
            if (request.ManagedByRole == "admin")
            {
                query = query.Where(q => q.ManagedByRole == "admin" || q.FlowType == "IN_CLASS_EXAM");
            }
            else
            {
                query = query.Where(q => q.ManagedByRole == request.ManagedByRole);
            }
        }

        var candidates = await query.OrderByDescending(q => q.CreatedAt).ToListAsync(cancellationToken);
        var tasks = new List<DeadLetterQueueDto>();
        foreach (var q in candidates)
        {
            if (!await DlqAuthorization.CanAccessAsync(_context, actor, q, cancellationToken)) continue;
            tasks.Add(new DeadLetterQueueDto
            {
                Id = q.Id,
                TaskType = q.TaskType,
                PayloadJson = q.PayloadJson,
                ErrorMessage = q.ErrorMessage,
                RetryCount = q.RetryCount,
                Status = q.Status,
                FlowType = q.FlowType,
                ManagedByRole = q.ManagedByRole,
                ResolvedByUserId = q.ResolvedByUserId,
                ResolutionNotes = q.ResolutionNotes,
                CreatedAt = q.CreatedAt,
                ResolvedAt = q.ResolvedAt
            });
        }

        return Result<List<DeadLetterQueueDto>>.Success(tasks);
    }
}
