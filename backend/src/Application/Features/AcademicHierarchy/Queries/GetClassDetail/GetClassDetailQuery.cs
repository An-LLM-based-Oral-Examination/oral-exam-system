using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetClassDetail;

public sealed record GetClassDetailQuery(
    Guid ClassId,
    Guid RequesterUserId,
    string? RequesterRole) : IRequest<Result<ClassDetailDto>>;

public sealed class GetClassDetailQueryHandler(
    IApplicationDbContext context,
    IClassJoinPasswordProtector? passwordProtector = null)
    : IRequestHandler<GetClassDetailQuery, Result<ClassDetailDto>>
{
    public async Task<Result<ClassDetailDto>> Handle(
        GetClassDetailQuery request,
        CancellationToken cancellationToken)
    {
        var targetClass = await context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
                .ThenInclude(c => c.AcademicProgram)
            .Include(c => c.Semester)
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (targetClass is null)
        {
            return Result<ClassDetailDto>.Failure("NOT_FOUND: Không tìm thấy lớp học phần.");
        }

        var role = (request.RequesterRole ?? string.Empty).Trim().ToLowerInvariant();
        if (role is not ("lecturer" or "department_head" or "student"))
        {
            return Result<ClassDetailDto>.Failure("FORBIDDEN: Không có quyền xem chi tiết lớp học phần.");
        }

        var isDeptHead = role == "department_head";
        var isOwningDeptHead = isDeptHead
            && request.RequesterUserId != Guid.Empty
            && targetClass.Course is not null
            && AcademicCourseOwnership.IsOwnedBy(targetClass.Course, request.RequesterUserId);
        var isOwnerLecturer = role == "lecturer" &&
            request.RequesterUserId != Guid.Empty &&
            targetClass.LecturerId == request.RequesterUserId;

        if (role == "lecturer" && !isOwnerLecturer)
        {
            return Result<ClassDetailDto>.Failure("FORBIDDEN: Giảng viên chỉ được phép xem chi tiết lớp học phần do chính mình phụ trách.");
        }

        var enrolledCount = await context.ClassEnrollments
            .AsNoTracking()
            .CountAsync(e => e.ClassId == targetClass.Id && e.Status == "enrolled", cancellationToken);

        var isAlreadyEnrolled = request.RequesterUserId != Guid.Empty &&
            await context.ClassEnrollments
                .AsNoTracking()
                .AnyAsync(e => e.ClassId == targetClass.Id && e.StudentId == request.RequesterUserId && e.Status == "enrolled", cancellationToken);

        var canViewShifts = isOwningDeptHead || isOwnerLecturer || isAlreadyEnrolled;
        var shifts = canViewShifts
            ? await context.InClassExamShifts
                .AsNoTracking()
                .Where(s => s.ClassId == targetClass.Id)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new ClassInClassShiftSummaryDto(
                    s.Id,
                    s.ShiftName,
                    s.StartTime,
                    s.EndTime,
                    s.Status,
                    s.IsLocked,
                    s.Participants.Count,
                    s.Participants.Count(p => p.Status == "SUBMITTED"),
                    s.CreatedAt))
                .ToListAsync(cancellationToken)
            : new List<ClassInClassShiftSummaryDto>();

        var canViewRoster = isOwningDeptHead || isOwnerLecturer;
        var enrolledStudents = canViewRoster
            ? await context.ClassEnrollments
                .AsNoTracking()
                .Where(e => e.ClassId == targetClass.Id && e.Status == "enrolled")
                .Include(e => e.Student)
                .OrderBy(e => e.Student.FullName)
                .Select(e => new ClassEnrolledStudentDto(
                    e.StudentId,
                    e.Student.FullName,
                    e.Student.Email,
                    e.Student.StudentCode,
                    e.EnrolledAt,
                    e.Status,
                    e.JoinMethod))
                .ToListAsync(cancellationToken)
            : new List<ClassEnrolledStudentDto>();

        var canRevealJoinPassword = request.RequesterUserId != Guid.Empty &&
            (isOwnerLecturer || isOwningDeptHead);
        var joinPassword = canRevealJoinPassword
            ? passwordProtector?.Unprotect(targetClass.JoinPasswordProtected)
            : null;

        var detail = new ClassDetailDto(
            ClassId: targetClass.Id,
            Code: targetClass.Code,
            Name: targetClass.Name,
            JoinCode: targetClass.JoinCode,
            CourseId: targetClass.CourseId,
            CourseCode: targetClass.Course!.Code,
            CourseName: targetClass.Course.Name,
            SemesterId: targetClass.SemesterId,
            SemesterCode: targetClass.Semester.Code,
            CourseOfferingId: targetClass.CourseOfferingId,
            LecturerId: targetClass.LecturerId,
            LecturerName: targetClass.Lecturer?.FullName,
            EnrolledCount: enrolledCount,
            MaxStudents: targetClass.MaxStudents,
            HasPassword: !string.IsNullOrWhiteSpace(targetClass.JoinPasswordHash),
            IsJoinEnabled: targetClass.IsJoinEnabled,
            AllowSelfEnrollment: targetClass.AllowSelfEnrollment,
            IsActive: targetClass.IsActive,
            IsAlreadyEnrolled: isAlreadyEnrolled,
            CreatedAt: targetClass.CreatedAt,
            Shifts: shifts,
            EnrolledStudents: enrolledStudents,
            JoinPassword: joinPassword);

        return Result<ClassDetailDto>.Success(detail);
    }
}
