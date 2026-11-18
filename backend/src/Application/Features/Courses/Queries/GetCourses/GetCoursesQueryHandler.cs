using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Queries.GetCourses;

public sealed class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, Result<List<CourseDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetCoursesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CourseDto>>> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Courses.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var courses = await query
            .OrderBy(c => c.Code)
            .Select(c => new CourseDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                Credits = c.Credits,
                AcademicProgramId = c.AcademicProgramId,
                SemesterId = c.SemesterId,
                SemesterCode = c.Semester != null ? c.Semester.Code : null,
                DepartmentHeadUserId = c.DepartmentHeadUserId,
                DepartmentHeadName = c.DepartmentHeadUser != null ? c.DepartmentHeadUser.FullName : null,
                TranscriptBufferSeconds = c.TranscriptBufferSeconds,
                MaxFollowUpQuestions = c.MaxFollowUpQuestions,
                MaxMockExamsPerDay = c.MaxMockExamsPerDay,
                MockExamDurationMinutes = c.MockExamDurationMinutes,
                MockExamTotalQuestions = c.MockExamTotalQuestions,
                MockExamBloomDistribution = c.MockExamBloomDistribution,
                HasFollowUp = c.HasFollowUp,
                ExamInputMode = c.ExamInputMode,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);

        return Result<List<CourseDto>>.Success(courses);
    }
}
