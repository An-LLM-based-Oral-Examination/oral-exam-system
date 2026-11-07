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

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.SearchAvailableClasses;

public sealed record SearchAvailableClassesQuery(
    Guid? CurrentStudentId = null,
    string? Keyword = null,
    Guid? CourseId = null,
    Guid? SemesterId = null,
    Guid? CourseOfferingId = null,
    bool OnlyAvailableToJoin = false,
    Guid? LecturerId = null) : IRequest<Result<List<ClassDirectoryItemDto>>>;

public sealed class SearchAvailableClassesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchAvailableClassesQuery, Result<List<ClassDirectoryItemDto>>>
{
    public async Task<Result<List<ClassDirectoryItemDto>>> Handle(
        SearchAvailableClassesQuery request,
        CancellationToken cancellationToken)
    {
        var enrolledClassIds = new HashSet<Guid>();
        if (request.CurrentStudentId.HasValue && request.CurrentStudentId.Value != Guid.Empty)
        {
            var myEnrolledIds = await context.ClassEnrollments
                .AsNoTracking()
                .Where(e => e.StudentId == request.CurrentStudentId.Value && e.Status == "enrolled")
                .Select(e => e.ClassId)
                .ToListAsync(cancellationToken);

            enrolledClassIds = [.. myEnrolledIds];
        }

        var query = context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.Lecturer)
            .Include(c => c.CourseOffering)
            .Where(c => c.IsActive && c.Course.IsActive && (c.CourseOffering == null || c.CourseOffering.IsEnabled))
            .AsQueryable();

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

        if (request.LecturerId.HasValue && request.LecturerId.Value != Guid.Empty)
        {
            query = query.Where(c => c.LecturerId == request.LecturerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Code.ToLower().Contains(kw) ||
                c.Name.ToLower().Contains(kw) ||
                c.JoinCode.ToLower().Contains(kw) ||
                c.Course.Code.ToLower().Contains(kw) ||
                c.Course.Name.ToLower().Contains(kw) ||
                (c.Lecturer != null && c.Lecturer.FullName.ToLower().Contains(kw)));
        }

        var rawItems = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Code)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Name,
                c.JoinCode,
                c.CourseId,
                CourseCode = c.Course.Code,
                CourseName = c.Course.Name,
                c.SemesterId,
                SemesterCode = c.Semester.Code,
                c.CourseOfferingId,
                c.LecturerId,
                LecturerName = c.Lecturer != null ? c.Lecturer.FullName : null,
                EnrolledCount = c.Enrollments.Count(e => e.Status == "enrolled"),
                c.MaxStudents,
                RequiresPassword = c.JoinPasswordHash != null && c.JoinPasswordHash != string.Empty,
                c.IsJoinEnabled,
                c.AllowSelfEnrollment,
                c.IsActive,
                c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = new List<ClassDirectoryItemDto>(rawItems.Count);
        foreach (var item in rawItems)
        {
            var isAlreadyEnrolled = enrolledClassIds.Contains(item.Id);
            var canJoin = item.IsActive &&
                          item.IsJoinEnabled &&
                          item.AllowSelfEnrollment &&
                          item.EnrolledCount < item.MaxStudents &&
                          !isAlreadyEnrolled;

            if (request.OnlyAvailableToJoin && !canJoin)
            {
                continue;
            }

            items.Add(new ClassDirectoryItemDto(
                ClassId: item.Id,
                ClassCode: item.Code,
                ClassName: item.Name,
                JoinCode: item.JoinCode,
                CourseId: item.CourseId,
                CourseCode: item.CourseCode,
                CourseName: item.CourseName,
                SemesterId: item.SemesterId,
                SemesterCode: item.SemesterCode,
                CourseOfferingId: item.CourseOfferingId,
                LecturerId: item.LecturerId,
                LecturerName: item.LecturerName,
                EnrolledCount: item.EnrolledCount,
                MaxStudents: item.MaxStudents,
                RequiresPassword: item.RequiresPassword,
                IsJoinEnabled: item.IsJoinEnabled,
                AllowSelfEnrollment: item.AllowSelfEnrollment,
                IsActive: item.IsActive,
                IsAlreadyEnrolled: isAlreadyEnrolled,
                CanJoin: canJoin,
                CreatedAt: item.CreatedAt));
        }

        return Result<List<ClassDirectoryItemDto>>.Success(items);
    }
}
