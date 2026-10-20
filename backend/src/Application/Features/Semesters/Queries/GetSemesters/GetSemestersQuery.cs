using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Semesters.DTOs;

namespace OralExamination.Application.Features.Semesters.Queries.GetSemesters;

public sealed record GetSemestersQuery(bool? IsActive = null) : IRequest<Result<List<SemesterItemDto>>>;

public sealed class GetSemestersQueryHandler
    : IRequestHandler<GetSemestersQuery, Result<List<SemesterItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetSemestersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SemesterItemDto>>> Handle(
        GetSemestersQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Semesters.AsNoTracking().AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActive.Value);
        }

        var items = await query
            .OrderByDescending(s => s.StartDate)
            .ThenBy(s => s.Code)
            .Select(s => new SemesterItemDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                IsActive = s.IsActive,
                CoursesCount = s.Courses.Count,
                ClassesCount = s.Classes.Count,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<List<SemesterItemDto>>.Success(items);
    }
}
