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
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure(validation.Errors[0].ErrorMessage);
        }

        var course = await context.Courses
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course is null)
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure("Không tìm thấy môn học.");
        }

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.DepartmentHeadUserId, cancellationToken);

        if (user is null)
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure("Không tìm thấy người dùng.");
        }

        if (!user.IsActive)
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure(
                "VALIDATION: Tài khoản Trưởng bộ môn đang bị khóa hoặc vô hiệu hóa.");
        }

        if (!string.Equals(user.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(user.Role, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AssignDepartmentHeadResponseDto>.Failure(
                $"Người dùng '{user.FullName}' không có vai trò Trưởng bộ môn ({UserRole.DepartmentHead}).");
        }

        course.DepartmentHeadUserId = request.DepartmentHeadUserId;
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
