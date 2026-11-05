using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerClasses;

public sealed record GetLecturerClassesQuery(
    Guid? LecturerId = null,
    Guid? CourseId = null,
    Guid? SemesterId = null,
    Guid? CourseOfferingId = null,
    string? Keyword = null,
    bool IncludeInactive = false) : IRequest<Result<List<LecturerClassItemDto>>>;

public sealed class GetLecturerClassesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLecturerClassesQuery, Result<List<LecturerClassItemDto>>>
{
    public async Task<Result<List<LecturerClassItemDto>>> Handle(
        GetLecturerClassesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.Lecturer)
            .AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (request.LecturerId.HasValue && request.LecturerId.Value != Guid.Empty)
        {
            query = query.Where(c => c.LecturerId == request.LecturerId.Value);
        }

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            query = query.Where(c => c.CourseId == request.CourseId.Value);
        }

        if (request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty)
        {
            query = query.Where(c => c.SemesterId == request.SemesterId.Value);
        }

        if (request.CourseOfferingId.HasValue && request.CourseOfferingId.Value != Guid.Empty)
        {
            query = query.Where(c => c.CourseOfferingId == request.CourseOfferingId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Code.ToLower().Contains(kw) ||
                c.Name.ToLower().Contains(kw) ||
                c.JoinCode.ToLower().Contains(kw) ||
                c.Course.Code.ToLower().Contains(kw) ||
                c.Course.Name.ToLower().Contains(kw));
        }

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Code)
            .Select(c => new LecturerClassItemDto(
                c.Id,
                c.Code,
                c.Name,
                c.JoinCode,
                c.CourseId,
                c.Course.Code,
                c.Course.Name,
                c.SemesterId,
                c.Semester.Code,
                c.CourseOfferingId,
                c.LecturerId,
                c.Lecturer != null ? c.Lecturer.FullName : null,
                c.Enrollments.Count(e => e.Status == "enrolled"),
                c.MaxStudents,
                c.InClassExamShifts.Count,
                c.JoinPasswordHash != null && c.JoinPasswordHash != string.Empty,
                c.IsJoinEnabled,
                c.AllowSelfEnrollment,
                c.IsActive,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<LecturerClassItemDto>>.Success(items);
    }
}
