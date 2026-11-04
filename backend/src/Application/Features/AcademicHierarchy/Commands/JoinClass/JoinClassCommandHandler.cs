using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;

public class JoinClassCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IValidator<JoinClassCommand> validator)
    : IRequestHandler<JoinClassCommand, Result<JoinClassResponseDto>>
{
    public async Task<Result<JoinClassResponseDto>> Handle(
        JoinClassCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<JoinClassResponseDto>.Failure(validation.Errors[0].ErrorMessage);
        }

        var student = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.StudentId, cancellationToken);

        if (student is not null)
        {
            if (!student.IsActive)
            {
                return Result<JoinClassResponseDto>.Failure("FORBIDDEN: Tài khoản sinh viên đang bị khóa hoặc vô hiệu hóa.");
            }

            if (!string.Equals(student.Role, "student", StringComparison.OrdinalIgnoreCase))
            {
                return Result<JoinClassResponseDto>.Failure("FORBIDDEN: Chỉ tài khoản có vai trò Sinh viên mới được phép tham gia lớp học phần.");
            }
        }

        var normalizedJoinCode = request.JoinCode.Trim().ToUpperInvariant();

        var targetClass = await context.Classes
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync(c => c.JoinCode.ToUpper() == normalizedJoinCode, cancellationToken);

        if (targetClass is null)
        {
            return Result<JoinClassResponseDto>.Failure("NOT_FOUND: Không tìm thấy lớp học với mã tham gia này.");
        }

        if (!targetClass.IsActive)
        {
            return Result<JoinClassResponseDto>.Failure("Lớp học đã bị vô hiệu hóa.");
        }

        // Kiểm tra is_join_enabled
        if (!targetClass.IsJoinEnabled)
        {
            return Result<JoinClassResponseDto>.Failure("JOIN_DISABLED: Lớp học hiện đang khóa tính năng tham gia.");
        }

        if (!targetClass.AllowSelfEnrollment)
        {
            return Result<JoinClassResponseDto>.Failure("Lớp học không cho phép sinh viên tự ghi danh.");
        }

        // Kiểm tra sĩ số tối đa
        var currentEnrollmentCount = await context.ClassEnrollments
            .CountAsync(e => e.ClassId == targetClass.Id && e.Status == "enrolled", cancellationToken);

        if (currentEnrollmentCount >= targetClass.MaxStudents)
        {
            return Result<JoinClassResponseDto>.Failure("Lớp học đã đạt số lượng sinh viên tối đa.");
        }

        // Xác thực mật khẩu nếu lớp học được đặt mật khẩu
        if (!string.IsNullOrWhiteSpace(targetClass.JoinPasswordHash))
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return Result<JoinClassResponseDto>.Failure("INVALID_PASSWORD: Lớp học yêu cầu mật khẩu tham gia.");
            }

            if (!passwordHasher.Verify(request.Password.Trim(), targetClass.JoinPasswordHash))
            {
                return Result<JoinClassResponseDto>.Failure("INVALID_PASSWORD: Mật khẩu tham gia lớp học không chính xác.");
            }
        }

        // Kiểm tra đã enroll chưa
        var existingEnrollment = await context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.ClassId == targetClass.Id && e.StudentId == request.StudentId, cancellationToken);

        DateTime enrolledAt;
        var joinMethod = string.IsNullOrWhiteSpace(targetClass.JoinPasswordHash) ? "INVITATION" : "PASSWORD";

        if (existingEnrollment is not null)
        {
            if (string.Equals(existingEnrollment.Status, "enrolled", StringComparison.OrdinalIgnoreCase))
            {
                return Result<JoinClassResponseDto>.Failure("ALREADY_ENROLLED: Sinh viên đã ghi danh vào lớp học này rồi.");
            }

            existingEnrollment.Status = "enrolled";
            existingEnrollment.JoinedViaCode = true;
            existingEnrollment.JoinMethod = joinMethod;
            existingEnrollment.EnrolledAt = DateTime.UtcNow;
            enrolledAt = existingEnrollment.EnrolledAt;
        }
        else
        {
            enrolledAt = DateTime.UtcNow;
            var newEnrollment = new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                StudentId = request.StudentId,
                JoinedViaCode = true,
                JoinMethod = joinMethod,
                Status = "enrolled",
                EnrolledAt = enrolledAt
            };

            context.ClassEnrollments.Add(newEnrollment);
        }

        await context.SaveChangesAsync(cancellationToken);

        var response = new JoinClassResponseDto(
            targetClass.Id,
            targetClass.Code,
            targetClass.Name,
            targetClass.CourseId,
            targetClass.Course.Code,
            targetClass.Course.Name,
            targetClass.SemesterId,
            targetClass.Semester.Code,
            targetClass.LecturerId,
            targetClass.Lecturer?.FullName,
            enrolledAt,
            "enrolled",
            joinMethod);

        return Result<JoinClassResponseDto>.Success(response);
    }
}
