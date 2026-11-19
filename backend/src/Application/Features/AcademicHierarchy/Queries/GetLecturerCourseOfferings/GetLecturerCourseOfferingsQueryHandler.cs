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
            .Select(u => new { u.Id, u.Role, u.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return Result<IReadOnlyList<LecturerCourseOfferingDto>>.Failure(
                "NOT_FOUND: Không tìm thấy giảng viên.");
        }

        if (!user.IsActive || user.Role is not (UserRole.DepartmentHead or UserRole.Lecturer))
            return Result<IReadOnlyList<LecturerCourseOfferingDto>>.Failure("FORBIDDEN: Không có quyền xem phân công môn học.");

        var query = context.CourseLecturers
            .AsNoTracking()
            .Where(a => a.LecturerUserId == request.LecturerUserId && a.CourseOfferingId != null);
        if (!request.IncludeInactive)
        {
            query = query.Where(a => a.IsActive && a.CourseOffering!.IsEnabled);
        }
        if (request.SemesterId.HasValue)
            query = query.Where(a => a.SemesterId == request.SemesterId.Value);

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

        if (user.Role == UserRole.DepartmentHead)
        {
            var offeringQuery = context.CourseOfferings
                .AsNoTracking()
                .AsQueryable();

            if (!request.IncludeInactive)
            {
                offeringQuery = offeringQuery.Where(o => o.IsEnabled && o.Course.IsActive);
            }
            if (request.SemesterId.HasValue)
                offeringQuery = offeringQuery.Where(o => o.SemesterId == request.SemesterId.Value);

            offeringQuery = offeringQuery.Where(o =>
                (o.Course.AcademicProgram != null && o.Course.AcademicProgram.DepartmentHeadUserId != null && o.Course.AcademicProgram.DepartmentHeadUserId != Guid.Empty
                    ? o.Course.AcademicProgram.DepartmentHeadUserId : o.Course.DepartmentHeadUserId) == user.Id);

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
