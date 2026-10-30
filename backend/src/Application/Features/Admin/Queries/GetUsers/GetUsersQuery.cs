using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Features.Admin.Queries.GetUsers;

public sealed record GetUsersQuery(
    string? Search = null,
    string? Role = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedAdminUsersResponseDto>>;

public sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<PagedAdminUsersResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedAdminUsersResponseDto>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term) ||
                (u.StudentCode != null && u.StudentCode.ToLower().Contains(term)) ||
                (u.Username != null && u.Username.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var normalizedRole = request.Role.Trim().ToLowerInvariant();
            query = query.Where(u => u.Role == normalizedRole);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserItemDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                StudentCode = u.StudentCode,
                Username = u.Username,
                Role = u.Role,
                IsActive = u.IsActive,
                MustChangePassword = u.MustChangePassword,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<PagedAdminUsersResponseDto>.Success(new PagedAdminUsersResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}
