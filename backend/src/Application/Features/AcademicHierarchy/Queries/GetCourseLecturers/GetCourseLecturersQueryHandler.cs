using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
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
        var courseExists = await context.Courses
            .AnyAsync(c => c.Id == request.CourseId, cancellationToken);

        if (!courseExists)
        {
            return Result<List<CourseLecturerDto>>.Failure("Không tìm thấy môn học.");
        }

        var lecturers = await context.CourseLecturers
            .AsNoTracking()
            .Where(cl => cl.CourseId == request.CourseId && cl.IsActive)
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
                cl.Semester != null ? cl.Semester.Code : null))
            .ToListAsync(cancellationToken);

        return Result<List<CourseLecturerDto>>.Success(lecturers);
    }
}
