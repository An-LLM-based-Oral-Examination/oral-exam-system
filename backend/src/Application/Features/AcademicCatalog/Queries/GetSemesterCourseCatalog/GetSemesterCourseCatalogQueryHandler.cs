using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;

namespace OralExamination.Application.Features.AcademicCatalog.Queries.GetSemesterCourseCatalog;

public sealed class GetSemesterCourseCatalogQueryHandler(
    IApplicationDbContext context)
    : IRequestHandler<GetSemesterCourseCatalogQuery, Result<IReadOnlyList<CourseCatalogItemDto>>>
{
    public async Task<Result<IReadOnlyList<CourseCatalogItemDto>>> Handle(
        GetSemesterCourseCatalogQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SemesterId == Guid.Empty)
        {
            return Result<IReadOnlyList<CourseCatalogItemDto>>.Failure(
                "VALIDATION: SemesterId không được để trống.");
        }

        var semesterExists = await context.Semesters
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.SemesterId, cancellationToken);

        if (!semesterExists)
        {
            return Result<IReadOnlyList<CourseCatalogItemDto>>.Failure(
                "NOT_FOUND: Không tìm thấy học kỳ.");
        }

        var query = context.Courses.AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var courses = await query
            .OrderBy(c => c.Code)
            .Select(c => new CourseCatalogItemDto
            {
                CourseId = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                Credits = c.Credits,
                AcademicProgramId = c.AcademicProgramId,
                AcademicProgramCode = c.AcademicProgram == null ? null : c.AcademicProgram.Code,
                IsCourseActive = c.IsActive,
                CourseOfferingId = c.CourseOfferings
                    .Where(o => o.SemesterId == request.SemesterId)
                    .Select(o => (Guid?)o.Id)
                    .FirstOrDefault(),
                IsSelected = c.CourseOfferings
                    .Any(o => o.SemesterId == request.SemesterId && o.IsEnabled)
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CourseCatalogItemDto>>.Success(courses);
    }
}
