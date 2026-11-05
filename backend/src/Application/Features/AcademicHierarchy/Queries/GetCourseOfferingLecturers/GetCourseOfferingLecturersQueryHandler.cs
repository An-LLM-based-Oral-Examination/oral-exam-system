using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseOfferingLecturers;

public sealed class GetCourseOfferingLecturersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCourseOfferingLecturersQuery, Result<IReadOnlyList<CourseLecturerDto>>>
{
    public async Task<Result<IReadOnlyList<CourseLecturerDto>>> Handle(
        GetCourseOfferingLecturersQuery request,
        CancellationToken cancellationToken)
    {
        var offeringExists = await context.CourseOfferings
            .AsNoTracking()
            .AnyAsync(o => o.Id == request.CourseOfferingId, cancellationToken);
        if (!offeringExists)
        {
            return Result<IReadOnlyList<CourseLecturerDto>>.Failure(
                "NOT_FOUND: Không tìm thấy course offering.");
        }

        var query = context.CourseLecturers
            .AsNoTracking()
            .Where(a => a.CourseOfferingId == request.CourseOfferingId);
        if (!request.IncludeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        var lecturers = await query
            .OrderBy(a => a.LecturerUser.FullName)
            .Select(a => new CourseLecturerDto(
                a.LecturerUserId,
                a.LecturerUser.FullName,
                a.LecturerUser.Email,
                a.LecturerUser.StudentCode,
                a.AssignedAt,
                a.IsActive,
                a.SemesterId,
                a.CourseOffering == null ? null : a.CourseOffering.Semester.Code,
                a.CourseOfferingId))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CourseLecturerDto>>.Success(lecturers);
    }
}
