using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetStudentMyClasses;

public class GetStudentMyClassesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetStudentMyClassesQuery, Result<List<StudentClassDto>>>
{
    public async Task<Result<List<StudentClassDto>>> Handle(
        GetStudentMyClassesQuery request,
        CancellationToken cancellationToken)
    {
        var studentExists = await context.Users
            .AnyAsync(u => u.Id == request.StudentId, cancellationToken);

        if (!studentExists)
        {
            return Result<List<StudentClassDto>>.Failure("Không tìm thấy sinh viên.");
        }

        var classes = await context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.StudentId == request.StudentId && e.Status == "enrolled")
            .Include(e => e.Class)
                .ThenInclude(c => c.Course)
            .Include(e => e.Class)
                .ThenInclude(c => c.Semester)
            .Include(e => e.Class)
                .ThenInclude(c => c.Lecturer)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new StudentClassDto(
                e.ClassId,
                e.Class.Code,
                e.Class.Name,
                e.Class.CourseId,
                e.Class.Course.Code,
                e.Class.Course.Name,
                e.Class.SemesterId,
                e.Class.Semester.Code,
                e.Class.LecturerId,
                e.Class.Lecturer != null ? e.Class.Lecturer.FullName : null,
                e.EnrolledAt,
                e.Status,
                e.JoinMethod))
            .ToListAsync(cancellationToken);

        return Result<List<StudentClassDto>>.Success(classes);
    }
}
