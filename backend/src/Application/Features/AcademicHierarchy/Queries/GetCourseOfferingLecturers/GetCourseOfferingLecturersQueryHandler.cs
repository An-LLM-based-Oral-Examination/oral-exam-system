using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseOfferingLecturers;

public sealed class GetCourseOfferingLecturersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCourseOfferingLecturersQuery, Result<IReadOnlyList<CourseLecturerDto>>>
{
    public async Task<Result<IReadOnlyList<CourseLecturerDto>>> Handle(
        GetCourseOfferingLecturersQuery request,
        CancellationToken cancellationToken)
    {
        var offering = await context.CourseOfferings
            .AsNoTracking()
            .Include(o => o.Course).ThenInclude(c => c.AcademicProgram)
            .FirstOrDefaultAsync(o => o.Id == request.CourseOfferingId, cancellationToken);
        if (offering is null)
        {
            return Result<IReadOnlyList<CourseLecturerDto>>.Failure(
                "NOT_FOUND: Không tìm thấy course offering.");
        }

        var actor = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive ||
            (!(actor.Role == UserRole.DepartmentHead && AcademicCourseOwnership.IsOwnedBy(offering.Course, actor.Id)) &&
             !(actor.Role == UserRole.Lecturer && await context.CourseLecturers.AnyAsync(a =>
                 a.CourseOfferingId == offering.Id && a.LecturerUserId == actor.Id && a.IsActive, cancellationToken))))
            return Result<IReadOnlyList<CourseLecturerDto>>.Failure("FORBIDDEN: Không có quyền xem phân công của môn học này.");

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
