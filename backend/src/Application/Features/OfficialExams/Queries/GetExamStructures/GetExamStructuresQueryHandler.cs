using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures;

/// <summary>
/// Handler truy vấn danh sách cấu trúc đề thi (ExamStructure).
/// </summary>
public sealed class GetExamStructuresQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetExamStructuresQuery, Result<List<ExamStructureDto>>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<List<ExamStructureDto>>> Handle(
        GetExamStructuresQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ExamStructures
            .AsNoTracking()
            .Include(e => e.Course)
            .AsQueryable();

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            query = query.Where(e => e.CourseId == request.CourseId.Value);
        }

        if (request.StructureId.HasValue && request.StructureId.Value != Guid.Empty)
        {
            query = query.Where(e => e.Id == request.StructureId.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(e => e.IsActive == request.IsActive.Value);
        }

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(e => new ExamStructureDto(
            e.Id,
            e.CourseId,
            e.Course?.Code ?? string.Empty,
            e.CreatedBy,
            e.Name,
            e.TotalQuestions,
            e.DurationMinutes,
            e.BloomDistribution,
            e.IsActive,
            e.CreatedAt)).ToList();

        return Result<List<ExamStructureDto>>.Success(dtos);
    }
}
