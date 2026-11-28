using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AssignDepartmentHead;

public class AssignDepartmentHeadCommandHandler(
    IApplicationDbContext context,
    IValidator<AssignDepartmentHeadCommand> validator)
    : IRequestHandler<AssignDepartmentHeadCommand, Result<AssignDepartmentHeadResponseDto>>
{
    public async Task<Result<AssignDepartmentHeadResponseDto>> Handle(
        AssignDepartmentHeadCommand request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ActorRole, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure(
                "FORBIDDEN: Chỉ có Quản trị viên (Admin) mới có quyền gán Trưởng bộ môn cho môn học.");
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errMsg = validation.Errors[0].ErrorMessage;
            return Result<AssignDepartmentHeadResponseDto>.Failure(
                errMsg.StartsWith("VALIDATION:", StringComparison.OrdinalIgnoreCase) ? errMsg : $"VALIDATION: {errMsg}");
        }

        var course = await context.Courses
            .Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course is null)
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure("NOT_FOUND: Không tìm thấy môn học.");
        }

        if (course.AcademicProgram?.DepartmentHeadUserId is Guid programHeadId &&
            programHeadId != Guid.Empty &&
            programHeadId != request.DepartmentHeadUserId)
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure(
                "CONFLICT: Môn học thuộc ngành đào tạo đã có Trưởng bộ môn ở cấp ngành. Vui lòng cập nhật Trưởng bộ môn tại ngành đào tạo tương ứng.");
        }

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.DepartmentHeadUserId, cancellationToken);

        if (user is null)
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure("NOT_FOUND: Không tìm thấy người dùng.");
        }

        if (!user.IsActive)
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure(
                "VALIDATION: Tài khoản Trưởng bộ môn đang bị khóa hoặc vô hiệu hóa.");
        }

        if (!string.Equals(user.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure(
                $"VALIDATION: Người dùng '{user.FullName}' không có vai trò Trưởng bộ môn ({UserRole.DepartmentHead}).");
        }

        var previousHeadId = course.DepartmentHeadUserId;
        course.DepartmentHeadUserId = request.DepartmentHeadUserId;

        context.AuditLogs.Add(new OralExamination.Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId.HasValue && request.ActorUserId.Value != Guid.Empty ? request.ActorUserId.Value : null,
            Action = "COURSE_DEPARTMENT_HEAD_ASSIGNED",
            EntityName = "courses",
            EntityId = course.Id,
            OldValues = $"department_head_user_id={previousHeadId?.ToString() ?? "null"}",
            NewValues = $"department_head_user_id={user.Id};email={user.Email}",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        var response = new AssignDepartmentHeadResponseDto(
            course.Id,
            course.Code,
            course.Name,
            user.Id,
            user.FullName,
            user.Email);

        return Result<AssignDepartmentHeadResponseDto>.Success(response);
    }
}
