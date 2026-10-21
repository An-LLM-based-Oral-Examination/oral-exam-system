using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetRubrics;

public sealed class GetRubricsQueryHandler
    : IRequestHandler<GetRubricsQuery, Result<List<RubricDetailDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetRubricsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<RubricDetailDto>>> Handle(
        GetRubricsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Rubrics
            .AsNoTracking()
            .Include(r => r.Criteria)
            .Where(r => r.IsActive);

        if (request.CourseId.HasValue && request.CourseId.Value != System.Guid.Empty)
        {
            query = query.Where(r => r.CourseId == request.CourseId.Value);
        }

        if (request.RubricId.HasValue && request.RubricId.Value != System.Guid.Empty)
        {
            query = query.Where(r => r.Id == request.RubricId.Value);
        }

        var rubrics = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = rubrics.Select(r => new RubricDetailDto(
            r.Id,
            r.Name,
            r.Description,
            r.TotalMaxScore,
            r.Criteria
                .OrderBy(c => c.OrderIndex)
                .Select(c => new RubricCriterionDetailDto(
                    c.Id,
                    c.CriterionName,
                    c.Description,
                    c.MaxScore,
                    c.Weight,
                    c.BloomLevel,
                    c.OrderIndex
                ))
                .ToList()
        )).ToList();

        return Result<List<RubricDetailDto>>.Success(items);
    }
}
