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
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Semesters.Queries.GetAcademicClasses;

public sealed record GetAcademicClassesQuery(
    Guid? CourseId = null,
    Guid? SemesterId = null,
    bool? IsActive = null,
    Guid? LecturerId = null,
    Guid ActorUserId = default,
    string? ActorRole = null) : IRequest<Result<List<AcademicClassItemDto>>>;

public sealed class GetAcademicClassesQueryHandler
    : IRequestHandler<GetAcademicClassesQuery, Result<List<AcademicClassItemDto>>>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Admin,
        UserRole.DepartmentHead,
        UserRole.Lecturer,
        UserRole.ExaminationOfficer
    ];

    private readonly IApplicationDbContext _context;

    public GetAcademicClassesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AcademicClassItemDto>>> Handle(
        GetAcademicClassesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result<List<AcademicClassItemDto>>.Failure(
                "FORBIDDEN: Bạn không có quyền truy cập danh sách lớp học.");
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<List<AcademicClassItemDto>>.Failure(
                "FORBIDDEN: Vai trò của bạn không có quyền xem danh sách lớp học phần.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null ||
            !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<List<AcademicClassItemDto>>.Failure(
                "FORBIDDEN: Vai trò của bạn không có quyền xem danh sách lớp học phần.");
        }

        var query = _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.Lecturer)
            .AsQueryable();

        if (string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
        {
            var ownedCourseIds = _context.Courses
                .AsNoTracking()
                .Where(c =>
                    (c.AcademicProgram != null && c.AcademicProgram.DepartmentHeadUserId == actor.Id)
                    || ((c.AcademicProgram == null
                        || c.AcademicProgram.DepartmentHeadUserId == null
                        || c.AcademicProgram.DepartmentHeadUserId == Guid.Empty)
                        && c.DepartmentHeadUserId == actor.Id))
                .Select(c => c.Id);

            query = query.Where(c => ownedCourseIds.Contains(c.CourseId));
        }
        else if (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            var assignedCourseIds = _context.CourseLecturers
                .AsNoTracking()
                .Where(cl =>
                    cl.LecturerUserId == actor.Id
                    && cl.IsActive
                    && cl.Course != null
                    && cl.Course.IsActive
                    && (cl.CourseOffering == null || cl.CourseOffering.IsEnabled))
                .Select(cl => cl.CourseId);

            query = query.Where(c => c.LecturerId == actor.Id || assignedCourseIds.Contains(c.CourseId));
        }

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

        var canExposeAllJoinCodes =
            string.Equals(actor.Role, UserRole.Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase);
        var isLecturerActor = string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase);

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
                EnrolledStudentsCount = c.Enrollments.Count(e => e.Status == "enrolled"),
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                JoinCode = c.IsJoinEnabled && (canExposeAllJoinCodes || (isLecturerActor && c.LecturerId == actor.Id))
                    ? c.JoinCode
                    : string.Empty,
                IsJoinEnabled = c.IsJoinEnabled,
                HasPassword = c.JoinPasswordHash != null && c.JoinPasswordHash != string.Empty
            })
            .ToListAsync(cancellationToken);

        return Result<List<AcademicClassItemDto>>.Success(items);
    }
}
