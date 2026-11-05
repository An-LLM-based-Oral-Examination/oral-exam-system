using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;

namespace OralExamination.Application.Features.AcademicCatalog.Queries.GetAcademicPrograms;

public sealed class GetAcademicProgramsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAcademicProgramsQuery, Result<IReadOnlyList<AcademicProgramDto>>>
{
    public async Task<Result<IReadOnlyList<AcademicProgramDto>>> Handle(
        GetAcademicProgramsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.AcademicPrograms.AsNoTracking();
        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        var programs = await query
            .OrderBy(p => p.Code)
            .Select(p => new AcademicProgramDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                DepartmentHeadUserId = p.DepartmentHeadUserId,
                DepartmentHeadName = p.DepartmentHeadUser == null ? null : p.DepartmentHeadUser.FullName,
                IsActive = p.IsActive,
                CoursesCount = p.Courses.Count
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AcademicProgramDto>>.Success(programs);
    }
}
