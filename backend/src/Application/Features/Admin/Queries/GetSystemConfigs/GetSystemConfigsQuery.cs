using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Features.Admin.Queries.GetSystemConfigs;

public sealed record GetSystemConfigsQuery : IRequest<Result<List<SystemConfigItemDto>>>;

public sealed class GetSystemConfigsQueryHandler
    : IRequestHandler<GetSystemConfigsQuery, Result<List<SystemConfigItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetSystemConfigsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SystemConfigItemDto>>> Handle(
        GetSystemConfigsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _context.SystemConfigs
            .AsNoTracking()
            .OrderBy(c => c.Key)
            .Select(c => new SystemConfigItemDto
            {
                Id = c.Id,
                Key = c.Key,
                Value = c.Value,
                Description = c.Description,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<List<SystemConfigItemDto>>.Success(items);
    }
}
