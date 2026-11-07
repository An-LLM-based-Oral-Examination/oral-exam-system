using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerCourseOfferings;

public sealed class GetLecturerCourseOfferingsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLecturerCourseOfferingsQuery, Result<IReadOnlyList<LecturerCourseOfferingDto>>>
{
    public async Task<Result<IReadOnlyList<LecturerCourseOfferingDto>>> Handle(
        GetLecturerCourseOfferingsQuery request,
        CancellationToken cancellationToken)
    {
        var lecturerExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.LecturerUserId, cancellationToken);
        if (!lecturerExists)
        {
            return Result<IReadOnlyList<LecturerCourseOfferingDto>>.Failure(
                "NOT_FOUND: Không tìm thấy giảng viên.");
        }

        var query = context.CourseLecturers
            .AsNoTracking()
            .Where(a => a.LecturerUserId == request.LecturerUserId && a.CourseOfferingId != null);
        if (!request.IncludeInactive)
        {
            query = query.Where(a => a.IsActive && a.CourseOffering!.IsEnabled);
        }

        var offerings = await query
            .OrderByDescending(a => a.CourseOffering!.Semester.StartDate)
            .ThenBy(a => a.Course.Code)
            .Select(a => new LecturerCourseOfferingDto(
                a.CourseOfferingId!.Value,
                a.CourseId,
                a.Course.Code,
                a.Course.Name,
                a.CourseOffering!.SemesterId,
                a.CourseOffering.Semester.Code,
                a.AssignedAt,
                a.IsActive,
                a.CourseOffering.IsEnabled))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<LecturerCourseOfferingDto>>.Success(offerings);
    }
}
