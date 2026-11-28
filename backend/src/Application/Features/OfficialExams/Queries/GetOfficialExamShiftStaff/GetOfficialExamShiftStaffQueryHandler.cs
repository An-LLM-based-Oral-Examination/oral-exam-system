using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamShiftStaff;

public sealed class GetOfficialExamShiftStaffQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOfficialExamShiftStaffQuery, Result<OfficialExamShiftStaffResponse>>
{
    public async Task<Result<OfficialExamShiftStaffResponse>> Handle(
        GetOfficialExamShiftStaffQuery request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<OfficialExamShiftStaffResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        var proctors = await context.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.Role == UserRole.Proctor)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .Select(user => new OfficialExamShiftStaffMemberDto(
                user.Id,
                user.FullName,
                user.Email,
                user.Username,
                user.Role,
                string.IsNullOrWhiteSpace(user.Email) ? "Giám thị coi thi" : user.Email,
                false))
            .ToListAsync(cancellationToken);

        Guid? courseId = request.CourseId.HasValue && request.CourseId.Value != Guid.Empty
            ? request.CourseId.Value
            : null;
        Guid? semesterId = request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty
            ? request.SemesterId.Value
            : null;

        var examiners = await ResolveExaminersAsync(courseId, semesterId, cancellationToken);
        if (!examiners.IsSuccess)
        {
            return Result<OfficialExamShiftStaffResponse>.Failure(examiners.Error);
        }

        return Result<OfficialExamShiftStaffResponse>.Success(
            new OfficialExamShiftStaffResponse(proctors, examiners.Value));
    }

    private async Task<Result<IReadOnlyList<OfficialExamShiftStaffMemberDto>>> ResolveExaminersAsync(
        Guid? courseId,
        Guid? semesterId,
        CancellationToken cancellationToken)
    {
        if (!courseId.HasValue)
        {
            var allLecturers = await context.Users
                .AsNoTracking()
                .Where(user => user.IsActive && user.Role == UserRole.Lecturer)
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Email)
                .Select(user => new OfficialExamShiftStaffMemberDto(
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Username,
                    user.Role,
                    string.IsNullOrWhiteSpace(user.Email) ? "Giảng viên" : user.Email,
                    false))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<OfficialExamShiftStaffMemberDto>>.Success(allLecturers);
        }

        var course = await context.Courses
            .AsNoTracking()
            .Where(item => item.Id == courseId.Value)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.AcademicProgramId,
                item.DepartmentHeadUserId,
                ProgramCode = item.AcademicProgram != null ? item.AcademicProgram.Code : null,
                ProgramHeadUserId = item.AcademicProgram != null ? item.AcademicProgram.DepartmentHeadUserId : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            return Result<IReadOnlyList<OfficialExamShiftStaffMemberDto>>.Failure(
                $"NOT_FOUND: {OfficialExamErrors.CourseNotFound}");
        }

        var assignedLecturerIds = await context.CourseLecturers
            .AsNoTracking()
            .Where(cl =>
                cl.CourseId == course.Id
                && cl.IsActive
                && (!semesterId.HasValue || cl.SemesterId == null || cl.SemesterId == semesterId.Value))
            .Select(cl => cl.LecturerUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var classLecturerIds = await context.Classes
            .AsNoTracking()
            .Where(cls =>
                cls.CourseId == course.Id
                && cls.IsActive
                && cls.LecturerId.HasValue
                && (!semesterId.HasValue || cls.SemesterId == semesterId.Value))
            .Select(cls => cls.LecturerId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var courseAssignedIds = assignedLecturerIds
            .Concat(classLecturerIds)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        var courseHeadIds = new HashSet<Guid>();
        if (course.DepartmentHeadUserId.HasValue && course.DepartmentHeadUserId.Value != Guid.Empty)
        {
            courseHeadIds.Add(course.DepartmentHeadUserId.Value);
        }
        if (course.ProgramHeadUserId.HasValue && course.ProgramHeadUserId.Value != Guid.Empty)
        {
            courseHeadIds.Add(course.ProgramHeadUserId.Value);
        }

        var sameProgramLecturerIds = new HashSet<Guid>();
        if (course.AcademicProgramId.HasValue)
        {
            var programCourseIds = await context.Courses
                .AsNoTracking()
                .Where(c => c.IsActive && c.AcademicProgramId == course.AcademicProgramId.Value && c.Id != course.Id)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            if (programCourseIds.Count > 0)
            {
                var fromAssignments = await context.CourseLecturers
                    .AsNoTracking()
                    .Where(cl =>
                        cl.IsActive
                        && programCourseIds.Contains(cl.CourseId)
                        && (!semesterId.HasValue || cl.SemesterId == null || cl.SemesterId == semesterId.Value))
                    .Select(cl => cl.LecturerUserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var fromClasses = await context.Classes
                    .AsNoTracking()
                    .Where(cls =>
                        cls.IsActive
                        && cls.LecturerId.HasValue
                        && programCourseIds.Contains(cls.CourseId)
                        && (!semesterId.HasValue || cls.SemesterId == semesterId.Value))
                    .Select(cls => cls.LecturerId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var id in fromAssignments.Concat(fromClasses))
                {
                    if (id != Guid.Empty && !courseAssignedIds.Contains(id) && !courseHeadIds.Contains(id))
                    {
                        sameProgramLecturerIds.Add(id);
                    }
                }
            }
        }

        var scopedIds = courseAssignedIds
            .Concat(courseHeadIds)
            .Concat(sameProgramLecturerIds)
            .Distinct()
            .ToList();

        var scopedUsers = scopedIds.Count == 0
            ? []
            : await context.Users
                .AsNoTracking()
                .Where(user =>
                    scopedIds.Contains(user.Id)
                    && user.IsActive
                    && (user.Role == UserRole.Lecturer || user.Role == UserRole.DepartmentHead))
                .Select(user => new { user.Id, user.FullName, user.Email, user.Username, user.Role })
                .ToListAsync(cancellationToken);

        var result = new List<OfficialExamShiftStaffMemberDto>();
        var addedIds = new HashSet<Guid>();

        void AddGroup(
            IEnumerable<Guid> ids,
            bool requireLecturerRole,
            bool isCourseAssigned,
            string fallbackLabel)
        {
            var groupUsers = scopedUsers
                .Where(u =>
                    ids.Contains(u.Id)
                    && !addedIds.Contains(u.Id)
                    && (!requireLecturerRole || u.Role == UserRole.Lecturer))
                .OrderBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.Email, StringComparer.OrdinalIgnoreCase);

            foreach (var user in groupUsers)
            {
                addedIds.Add(user.Id);
                var detail = string.IsNullOrWhiteSpace(user.Email)
                    ? fallbackLabel
                    : $"{fallbackLabel} · {user.Email}";
                result.Add(new OfficialExamShiftStaffMemberDto(
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Username,
                    user.Role,
                    detail,
                    isCourseAssigned));
            }
        }

        // Nhóm 1: Giảng viên được phân công hoặc đang đứng lớp đúng môn học.
        AddGroup(courseAssignedIds, requireLecturerRole: true, isCourseAssigned: true, $"GV môn {course.Code}");

        // Nhóm 2: Trưởng bộ môn của đúng môn học / chương trình đào tạo đó (tuyệt đối không trộn TBM ngành khác).
        var headLabel = !string.IsNullOrWhiteSpace(course.ProgramCode)
            ? $"Trưởng BM {course.ProgramCode}"
            : $"Trưởng BM {course.Code}";
        AddGroup(courseHeadIds, requireLecturerRole: false, isCourseAssigned: true, headLabel);

        // Nhóm 3: Giảng viên cùng chương trình đào tạo (dự phòng khi cần chấm song song nhiều phòng).
        var programLabel = !string.IsNullOrWhiteSpace(course.ProgramCode)
            ? $"GV ngành {course.ProgramCode}"
            : "GV cùng ngành";
        AddGroup(sameProgramLecturerIds, requireLecturerRole: true, isCourseAssigned: false, programLabel);

        // Nhóm 4 (Fallback cuối): Chỉ bổ sung giảng viên đang hoạt động khác nếu môn hiện chưa có >= 2 giám khảo,
        // giúp Khảo thí vẫn phân công được khi môn mới tạo chưa gán đủ giảng viên; KHÔNG kéo Trưởng BM ngành khác.
        if (result.Count < 2)
        {
            var fallbackLecturers = await context.Users
                .AsNoTracking()
                .Where(user =>
                    user.IsActive
                    && user.Role == UserRole.Lecturer
                    && !addedIds.Contains(user.Id))
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Email)
                .Select(user => new { user.Id, user.FullName, user.Email, user.Username, user.Role })
                .ToListAsync(cancellationToken);

            foreach (var user in fallbackLecturers)
            {
                addedIds.Add(user.Id);
                var detail = string.IsNullOrWhiteSpace(user.Email)
                    ? "GV dự phòng"
                    : $"GV dự phòng · {user.Email}";
                result.Add(new OfficialExamShiftStaffMemberDto(
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Username,
                    user.Role,
                    detail,
                    false));
            }
        }

        return Result<IReadOnlyList<OfficialExamShiftStaffMemberDto>>.Success(result);
    }
}
