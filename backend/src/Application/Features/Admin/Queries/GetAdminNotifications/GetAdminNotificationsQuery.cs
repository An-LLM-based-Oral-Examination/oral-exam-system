using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Features.Admin.Queries.GetAdminNotifications;

public sealed record GetAdminNotificationsQuery(
    string? Search = null,
    string? Type = null,
    string? Role = null,
    bool? IsRead = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedAdminNotificationsResponseDto>>;

public sealed class GetAdminNotificationsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAdminNotificationsQuery, Result<PagedAdminNotificationsResponseDto>>
{
    public async Task<Result<PagedAdminNotificationsResponseDto>> Handle(
        GetAdminNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var baseQuery = context.Notifications
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(n =>
                n.Title.ToLower().Contains(term) ||
                n.Message.ToLower().Contains(term) ||
                n.User.Email.ToLower().Contains(term) ||
                n.User.FullName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var normalizedType = request.Type.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(n => n.Type.ToUpper() == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var normalizedRole = request.Role.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(n => n.User.Role == normalizedRole);
        }

        if (request.IsRead.HasValue)
        {
            baseQuery = baseQuery.Where(n => n.IsRead == request.IsRead.Value);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var unreadTotalCount = await baseQuery.CountAsync(n => !n.IsRead, cancellationToken);

        var items = await baseQuery
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new AdminNotificationItemDto
            {
                Id = n.Id,
                UserId = n.UserId,
                UserEmail = n.User.Email,
                UserFullName = n.User.FullName,
                UserRole = n.User.Role,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<PagedAdminNotificationsResponseDto>.Success(new PagedAdminNotificationsResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            UnreadTotalCount = unreadTotalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}
