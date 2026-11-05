using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Semesters.DTOs;

namespace OralExamination.Application.Features.Semesters.Queries.GetAcademicClasses;

public sealed record GetAcademicClassesQuery(
    Guid? CourseId = null,
    Guid? SemesterId = null,
    bool? IsActive = null,
    Guid? LecturerId = null) : IRequest<Result<List<AcademicClassItemDto>>>;

public sealed class GetAcademicClassesQueryHandler
    : IRequestHandler<GetAcademicClassesQuery, Result<List<AcademicClassItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAcademicClassesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AcademicClassItemDto>>> Handle(
        GetAcademicClassesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.Lecturer)
            .AsQueryable();

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            query = query.Where(c => c.CourseId == request.CourseId.Value);
        }

        if (request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty)
        {
            query = query.Where(c => c.SemesterId == request.SemesterId.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == request.IsActive.Value);
        }

        if (request.LecturerId.HasValue && request.LecturerId.Value != Guid.Empty)
        {
            query = query.Where(c => c.LecturerId == request.LecturerId.Value);
        }

        var items = await query
            .OrderBy(c => c.Code)
            .Select(c => new AcademicClassItemDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                CourseId = c.CourseId,
                CourseCode = c.Course != null ? c.Course.Code : string.Empty,
                CourseName = c.Course != null ? c.Course.Name : string.Empty,
                SemesterId = c.SemesterId,
                CourseOfferingId = c.CourseOfferingId,
                SemesterCode = c.Semester != null ? c.Semester.Code : string.Empty,
                LecturerId = c.LecturerId,
                LecturerName = c.Lecturer != null ? c.Lecturer.FullName : null,
                MaxStudents = c.MaxStudents,
                EnrolledStudentsCount = c.Enrollments.Count,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                JoinCode = c.JoinCode,
                IsJoinEnabled = c.IsJoinEnabled,
                HasPassword = c.JoinPasswordHash != null && c.JoinPasswordHash != string.Empty
            })
            .ToListAsync(cancellationToken);

        return Result<List<AcademicClassItemDto>>.Success(items);
    }
}
