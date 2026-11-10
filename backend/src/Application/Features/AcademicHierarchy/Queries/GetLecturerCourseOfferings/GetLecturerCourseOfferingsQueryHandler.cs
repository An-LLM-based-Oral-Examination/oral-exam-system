using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerCourseOfferings;

public sealed class GetLecturerCourseOfferingsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLecturerCourseOfferingsQuery, Result<IReadOnlyList<LecturerCourseOfferingDto>>>
{
    public async Task<Result<IReadOnlyList<LecturerCourseOfferingDto>>> Handle(
        GetLecturerCourseOfferingsQuery request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.LecturerUserId)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
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
            .OrderByDescending(a => a.CourseOffering != null && a.CourseOffering.Semester != null ? a.CourseOffering.Semester.StartDate : DateOnly.MinValue)
            .ThenBy(a => a.Course != null ? a.Course.Code : string.Empty)
            .Select(a => new LecturerCourseOfferingDto(
                a.CourseOfferingId!.Value,
                a.CourseId,
                a.Course != null ? a.Course.Code : string.Empty,
                a.Course != null ? a.Course.Name : string.Empty,
                a.CourseOffering != null ? a.CourseOffering.SemesterId : Guid.Empty,
                a.CourseOffering != null && a.CourseOffering.Semester != null ? a.CourseOffering.Semester.Code : string.Empty,
                a.AssignedAt,
                a.IsActive,
                a.CourseOffering != null && a.CourseOffering.IsEnabled))
            .ToListAsync(cancellationToken);

        if (offerings.Count == 0 &&
            (user.Role == UserRole.DepartmentHead || user.Role == UserRole.Admin))
        {
            var offeringQuery = context.CourseOfferings
                .AsNoTracking()
                .AsQueryable();

            if (!request.IncludeInactive)
            {
                offeringQuery = offeringQuery.Where(o => o.IsEnabled && o.Course.IsActive);
            }

            if (user.Role == UserRole.DepartmentHead)
            {
                offeringQuery = offeringQuery.Where(o =>
                    o.Course.DepartmentHeadUserId == user.Id ||
                    (o.Course.AcademicProgram != null && o.Course.AcademicProgram.DepartmentHeadUserId == user.Id));
            }

            offerings = await offeringQuery
                .OrderByDescending(o => o.Semester != null ? o.Semester.StartDate : DateOnly.MinValue)
                .ThenBy(o => o.Course != null ? o.Course.Code : string.Empty)
                .Select(o => new LecturerCourseOfferingDto(
                    o.Id,
                    o.CourseId,
                    o.Course != null ? o.Course.Code : string.Empty,
                    o.Course != null ? o.Course.Name : string.Empty,
                    o.SemesterId,
                    o.Semester != null ? o.Semester.Code : string.Empty,
                    o.CreatedAt,
                    true,
                    o.IsEnabled))
                .ToListAsync(cancellationToken);
        }

        return Result<IReadOnlyList<LecturerCourseOfferingDto>>.Success(offerings);
    }
}
