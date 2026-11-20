using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Lecturer.Queries.GetLecturerCourses;

public sealed record LecturerCourseDto(
    Guid Id,
    string Code,
    string Name,
    int Credits,
    bool IsActive);

public sealed record GetLecturerCoursesQuery(Guid LecturerUserId)
    : IRequest<Result<IReadOnlyList<LecturerCourseDto>>>;

public sealed class GetLecturerCoursesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLecturerCoursesQuery, Result<IReadOnlyList<LecturerCourseDto>>>
{
    public async Task<Result<IReadOnlyList<LecturerCourseDto>>> Handle(
        GetLecturerCoursesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.LecturerUserId == Guid.Empty)
        {
            return Result<IReadOnlyList<LecturerCourseDto>>.Failure(
                "UNAUTHORIZED: Không xác định được giảng viên.");
        }

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.LecturerUserId, cancellationToken);

        if (user is null)
        {
            return Result<IReadOnlyList<LecturerCourseDto>>.Failure(
                "NOT_FOUND: Không tìm thấy giảng viên.");
        }

        var isElevated = user.Role == UserRole.DepartmentHead;

        if (!user.IsActive || (!isElevated && user.Role != UserRole.Lecturer))
            return Result<IReadOnlyList<LecturerCourseDto>>.Failure("FORBIDDEN: Không có quyền xem môn được phân công.");

        if (isElevated)
        {
            var allCourses = await context.Courses
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Where(c =>
                    (c.AcademicProgram != null && c.AcademicProgram.DepartmentHeadUserId != null && c.AcademicProgram.DepartmentHeadUserId != Guid.Empty
                        ? c.AcademicProgram.DepartmentHeadUserId : c.DepartmentHeadUserId) == user.Id)
                .OrderBy(c => c.Code)
                .Select(c => new LecturerCourseDto(c.Id, c.Code, c.Name, c.Credits, c.IsActive))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<LecturerCourseDto>>.Success(allCourses);
        }

        var assigned = await (
            from cl in context.CourseLecturers.AsNoTracking()
            join c in context.Courses.AsNoTracking() on cl.CourseId equals c.Id
            where cl.LecturerUserId == request.LecturerUserId
                  && cl.IsActive
                  && (cl.CourseOffering == null || cl.CourseOffering.IsEnabled)
                  && c.IsActive
            orderby c.Code
            select new LecturerCourseDto(c.Id, c.Code, c.Name, c.Credits, c.IsActive)
        )
            .Distinct()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<LecturerCourseDto>>.Success(assigned);
    }
}
