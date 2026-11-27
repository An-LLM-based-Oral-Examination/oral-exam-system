using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Authorization;
using OralExamination.Domain.Enums;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseLecturers;

public class GetCourseLecturersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCourseLecturersQuery, Result<List<CourseLecturerDto>>>
{
    public async Task<Result<List<CourseLecturerDto>>> Handle(
        GetCourseLecturersQuery request,
        CancellationToken cancellationToken)
    {
        var course = await context.Courses.AsNoTracking().Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course is null)
        {
            return Result<List<CourseLecturerDto>>.Failure("NOT_FOUND: Không tìm thấy môn học.");
        }

        var actor = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive ||
            (!(actor.Role == UserRole.DepartmentHead && AcademicCourseOwnership.IsOwnedBy(course, actor.Id)) &&
             !(actor.Role == UserRole.Lecturer && await context.CourseLecturers.AnyAsync(a =>
                 a.CourseId == course.Id && a.LecturerUserId == actor.Id && a.IsActive &&
                 (!request.SemesterId.HasValue || a.SemesterId == request.SemesterId.Value), cancellationToken))))
            return Result<List<CourseLecturerDto>>.Failure("FORBIDDEN: Không có quyền xem phân công của môn học này.");

        var lecturers = await context.CourseLecturers
            .AsNoTracking()
            .Where(cl => cl.CourseId == request.CourseId && cl.IsActive)
            .Where(cl => !request.SemesterId.HasValue || cl.SemesterId == request.SemesterId.Value)
            .Include(cl => cl.LecturerUser)
            .Include(cl => cl.Semester)
            .OrderByDescending(cl => cl.AssignedAt)
            .Select(cl => new CourseLecturerDto(
                cl.LecturerUserId,
                cl.LecturerUser.FullName,
                cl.LecturerUser.Email,
                cl.LecturerUser.StudentCode,
                cl.AssignedAt,
                cl.IsActive,
                cl.SemesterId,
                cl.Semester != null ? cl.Semester.Code : null,
                cl.CourseOfferingId))
            .ToListAsync(cancellationToken);

        return Result<List<CourseLecturerDto>>.Success(lecturers);
    }
}
