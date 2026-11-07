using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AssignLecturersToCourse;

public class AssignLecturersToCourseCommandHandler(
    IApplicationDbContext context,
    IValidator<AssignLecturersToCourseCommand> validator)
    : IRequestHandler<AssignLecturersToCourseCommand, Result<AssignLecturersToCourseResponseDto>>
{
    public async Task<Result<AssignLecturersToCourseResponseDto>> Handle(
        AssignLecturersToCourseCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AssignLecturersToCourseResponseDto>.Failure(validation.Errors[0].ErrorMessage);
        }

        var course = await context.Courses
            .Include(c => c.Semester)
            .Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course is null)
        {
            return Result<AssignLecturersToCourseResponseDto>.Failure("Không tìm thấy môn học.");
        }

        var targetSemesterId = request.SemesterId ?? course.SemesterId;
        var semester = await context.Semesters
            .FirstOrDefaultAsync(s => s.Id == targetSemesterId, cancellationToken);

        if (semester is null)
        {
            return Result<AssignLecturersToCourseResponseDto>.Failure("Không tìm thấy học kỳ được chỉ định.");
        }

        var courseOffering = await context.CourseOfferings
            .FirstOrDefaultAsync(o => o.CourseId == course.Id && o.SemesterId == targetSemesterId, cancellationToken);

        if (courseOffering is not null && !courseOffering.IsEnabled)
        {
            return Result<AssignLecturersToCourseResponseDto>.Failure(
                "VALIDATION: Course offering đang bị vô hiệu hóa.");
        }

        var assigningUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.AssignedByUserId, cancellationToken);

        if (assigningUser is null)
        {
            return Result<AssignLecturersToCourseResponseDto>.Failure("Không tìm thấy người thực hiện phân công.");
        }

        if (!assigningUser.IsActive)
        {
            return Result<AssignLecturersToCourseResponseDto>.Failure(
                "VALIDATION: Tài khoản người thực hiện phân công đang bị khóa hoặc vô hiệu hóa.");
        }

        var isCallerAdmin = string.Equals(assigningUser.Role, UserRole.Admin, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(request.ActorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase);

        if (!isCallerAdmin)
        {
            if (!string.Equals(assigningUser.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
            {
                return Result<AssignLecturersToCourseResponseDto>.Failure(
                    "FORBIDDEN: Chỉ có Trưởng bộ môn hoặc Admin mới có quyền phân công giảng viên.");
            }

            var owningDepartmentHeadId = course.AcademicProgram?.DepartmentHeadUserId ?? course.DepartmentHeadUserId;
            if (owningDepartmentHeadId.HasValue && owningDepartmentHeadId.Value != request.AssignedByUserId)
            {
                return Result<AssignLecturersToCourseResponseDto>.Failure(
                    "FORBIDDEN: Trưởng bộ môn chỉ được phép phân công giảng viên cho môn học do mình quản lý.");
            }
        }

        var uniqueLecturerIds = request.LecturerUserIds.Distinct().ToList();

        var users = await context.Users
            .Where(u => uniqueLecturerIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        if (users.Count != uniqueLecturerIds.Count)
        {
            return Result<AssignLecturersToCourseResponseDto>.Failure("Một số giảng viên không tồn tại trong hệ thống.");
        }

        // Kiểm tra role và trạng thái hoạt động của từng giảng viên
        foreach (var user in users)
        {
            if (!string.Equals(user.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
            {
                return Result<AssignLecturersToCourseResponseDto>.Failure(
                    $"Người dùng '{user.FullName}' không có vai trò Giảng viên ({UserRole.Lecturer}).");
            }

            if (!user.IsActive)
            {
                return Result<AssignLecturersToCourseResponseDto>.Failure(
                    $"VALIDATION: Tài khoản giảng viên '{user.FullName}' đang bị khóa hoặc vô hiệu hóa.");
            }
        }

        var existingAssignments = await context.CourseLecturers
            .Where(cl => cl.CourseId == request.CourseId &&
                         uniqueLecturerIds.Contains(cl.LecturerUserId) &&
                         cl.SemesterId == targetSemesterId &&
                         (courseOffering == null || cl.CourseOfferingId == null || cl.CourseOfferingId == courseOffering.Id))
            .ToListAsync(cancellationToken);

        var resultDtos = new List<CourseLecturerDto>();
        var now = DateTime.UtcNow;

        foreach (var user in users)
        {
            var existing = existingAssignments.FirstOrDefault(cl => cl.LecturerUserId == user.Id);
            if (existing is not null)
            {
                existing.IsActive = true;
                existing.CourseOfferingId = courseOffering?.Id;
                existing.UpdatedAt = now;
                existing.AssignedByUserId = request.AssignedByUserId;

                resultDtos.Add(new CourseLecturerDto(
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.StudentCode,
                    existing.AssignedAt,
                    existing.IsActive,
                    existing.SemesterId,
                    semester.Code,
                    courseOffering?.Id));
            }
            else
            {
                var newCl = new CourseLecturer
                {
                    Id = Guid.NewGuid(),
                    CourseId = request.CourseId,
                    CourseOfferingId = courseOffering?.Id,
                    LecturerUserId = user.Id,
                    AssignedByUserId = request.AssignedByUserId,
                    SemesterId = targetSemesterId,
                    AssignedAt = now,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                context.CourseLecturers.Add(newCl);

                resultDtos.Add(new CourseLecturerDto(
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.StudentCode,
                    now,
                    true,
                    targetSemesterId,
                    semester.Code,
                    courseOffering?.Id));
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var response = new AssignLecturersToCourseResponseDto(
            course.Id,
            course.Code,
            course.Name,
            resultDtos.Count,
            resultDtos);

        return Result<AssignLecturersToCourseResponseDto>.Success(response);
    }
}
