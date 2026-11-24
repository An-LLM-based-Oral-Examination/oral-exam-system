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
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerClasses;

public sealed record GetLecturerClassesQuery(
    Guid? LecturerId = null,
    Guid? CourseId = null,
    Guid? SemesterId = null,
    Guid? CourseOfferingId = null,
    string? Keyword = null,
    bool IncludeInactive = false,
    Guid? ActorUserId = null,
    string? ActorRole = null) : IRequest<Result<List<LecturerClassItemDto>>>;

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
            .Include(c => c.CourseOffering)
            .Include(c => c.Semester)
            .Include(c => c.Lecturer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ActorRole))
        {
            if (!request.ActorUserId.HasValue || request.ActorUserId.Value == Guid.Empty)
            {
                return Result<List<LecturerClassItemDto>>.Failure(
                    "FORBIDDEN: Thiếu định danh người dùng khi tra cứu danh sách lớp học phần.");
            }

            var actorId = request.ActorUserId.Value;
            if (string.Equals(request.ActorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
            {
                var ownedCourseIds = await context.Courses
                    .AsNoTracking()
                    .Where(c =>
                        (c.AcademicProgram != null && c.AcademicProgram.DepartmentHeadUserId == actorId)
                        || ((c.AcademicProgram == null
                            || c.AcademicProgram.DepartmentHeadUserId == null
                            || c.AcademicProgram.DepartmentHeadUserId == Guid.Empty)
                            && c.DepartmentHeadUserId == actorId))
                    .Select(c => c.Id)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                if (request.CourseId.HasValue &&
                    request.CourseId.Value != Guid.Empty &&
                    !ownedCourseIds.Contains(request.CourseId.Value))
                {
                    return Result<List<LecturerClassItemDto>>.Failure(
                        "FORBIDDEN: Trưởng Bộ Môn không có quyền xem danh sách lớp học phần của môn học này.");
                }

                query = query.Where(c => ownedCourseIds.Contains(c.CourseId));
            }
            else if (string.Equals(request.ActorRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
            {
                if (request.LecturerId.HasValue &&
                    request.LecturerId.Value != Guid.Empty &&
                    request.LecturerId.Value != actorId)
                {
                    return Result<List<LecturerClassItemDto>>.Failure(
                        "FORBIDDEN: Giảng viên không được xem danh sách lớp học phần của giảng viên khác.");
                }

                query = query.Where(c => c.LecturerId == actorId);
            }
            else
            {
                return Result<List<LecturerClassItemDto>>.Failure(
                    "FORBIDDEN: Vai trò của bạn không có quyền xem danh sách lớp học phần.");
            }
        }

        if (!request.IncludeInactive)
        {
            query = query.Where(c =>
                c.IsActive &&
                c.Course.IsActive &&
                (c.CourseOffering == null || c.CourseOffering.IsEnabled));
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
                (!string.IsNullOrEmpty(c.JoinCode) && c.JoinCode.ToLower().Contains(kw)) ||
                (c.Course != null && c.Course.Code.ToLower().Contains(kw)) ||
                (c.Course != null && c.Course.Name.ToLower().Contains(kw)));
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
                c.Course != null ? c.Course.Code : string.Empty,
                c.Course != null ? c.Course.Name : string.Empty,
                c.SemesterId,
                c.Semester != null ? c.Semester.Code : string.Empty,
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
                c.CreatedAt,
                c.InClassExamShifts.Count(s => s.Status == "ACTIVE" || s.Status == "IN_PROGRESS")))
            .ToListAsync(cancellationToken);

        return Result<List<LecturerClassItemDto>>.Success(items);
    }
}
