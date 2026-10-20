using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Features.Admin.Queries.GetAuditLogs;

public sealed record GetAuditLogsQuery(
    string? EntityName = null,
    string? Action = null,
    Guid? UserId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedAuditLogsResponseDto>>;

public sealed class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, Result<PagedAuditLogsResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedAuditLogsResponseDto>> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            var entity = request.EntityName.Trim().ToLowerInvariant();
            query = query.Where(a => a.EntityName.ToLower() == entity);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action.ToUpper() == action);
        }

        if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            query = query.Where(a => a.UserId == request.UserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogItemDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserEmail = a.User != null ? a.User.Email : null,
                UserFullName = a.User != null ? a.User.FullName : null,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<PagedAuditLogsResponseDto>.Success(new PagedAuditLogsResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}
