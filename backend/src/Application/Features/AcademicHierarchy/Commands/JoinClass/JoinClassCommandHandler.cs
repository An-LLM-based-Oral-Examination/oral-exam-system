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

        if (student is null)
        {
            return Result<JoinClassResponseDto>.Failure("NOT_FOUND: Không tìm thấy sinh viên.");
        }

        if (!student.IsActive)
        {
            return Result<JoinClassResponseDto>.Failure("FORBIDDEN: Tài khoản sinh viên đang bị khóa hoặc vô hiệu hóa.");
        }

        if (!string.Equals(student.Role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<JoinClassResponseDto>.Failure("FORBIDDEN: Chỉ tài khoản có vai trò Sinh viên mới được phép tham gia lớp học phần.");
        }

        Class? targetClass;
        if (request.ClassId.HasValue && request.ClassId.Value != Guid.Empty)
        {
            targetClass = await context.Classes
                .AsNoTracking()
                .Include(c => c.Course)
                .Include(c => c.Semester)
                .Include(c => c.Lecturer)
                .Include(c => c.CourseOffering)
                .FirstOrDefaultAsync(c => c.Id == request.ClassId.Value, cancellationToken);

            if (targetClass is not null && !string.IsNullOrWhiteSpace(request.JoinCode))
            {
                var trimmedCode = request.JoinCode.Trim();
                if (!string.Equals(targetClass.JoinCode, trimmedCode, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(targetClass.Code, trimmedCode, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<JoinClassResponseDto>.Failure("NOT_FOUND: Mã tham gia lớp không khớp với lớp học đã chọn.");
                }
            }
        }
        else
        {
            var normalizedJoinCode = (request.JoinCode ?? string.Empty).Trim().ToUpperInvariant();
            targetClass = await context.Classes
                .AsNoTracking()
                .Include(c => c.Course)
                .Include(c => c.Semester)
                .Include(c => c.Lecturer)
                .Include(c => c.CourseOffering)
                .FirstOrDefaultAsync(
                    c => c.JoinCode.ToUpper() == normalizedJoinCode || c.Code.ToUpper() == normalizedJoinCode,
                    cancellationToken);
        }

        if (targetClass is null)
        {
            return Result<JoinClassResponseDto>.Failure("NOT_FOUND: Không tìm thấy lớp học với mã tham gia này.");
        }

        if (!targetClass.IsActive)
        {
            return Result<JoinClassResponseDto>.Failure("JOIN_DISABLED: Lớp học đã bị vô hiệu hóa.");
        }

        if (targetClass.CourseOfferingId.HasValue &&
            (targetClass.CourseOffering is null || !targetClass.CourseOffering.IsEnabled || targetClass.Course is null || !targetClass.Course.IsActive))
        {
            return Result<JoinClassResponseDto>.Failure(
                "JOIN_DISABLED: Course offering của lớp hiện đang bị vô hiệu hóa.");
        }

        if (targetClass.Course is null || !targetClass.Course.IsActive)
        {
            return Result<JoinClassResponseDto>.Failure(
                "JOIN_DISABLED: Môn học của lớp hiện đang bị vô hiệu hóa.");
        }

        // Kiểm tra is_join_enabled
        if (!targetClass.IsJoinEnabled)
        {
            return Result<JoinClassResponseDto>.Failure("JOIN_DISABLED: Lớp học hiện đang khóa tính năng tham gia.");
        }

        if (!targetClass.AllowSelfEnrollment)
        {
            return Result<JoinClassResponseDto>.Failure("FORBIDDEN: Lớp học không cho phép sinh viên tự ghi danh.");
        }

        // Xác thực mật khẩu PBKDF2 ngoài khóa để không giữ PostgreSQL advisory lock trong lúc hash,
        // sau đó đối chiếu lại JoinPasswordHash & trạng thái lớp bên trong khóa chống TOCTOU.
        var verifiedPasswordHash = targetClass.JoinPasswordHash;
        if (!string.IsNullOrWhiteSpace(verifiedPasswordHash))
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return Result<JoinClassResponseDto>.Failure("INVALID_PASSWORD: Lớp học yêu cầu mật khẩu tham gia.");
            }

            if (!passwordHasher.Verify(request.Password.Trim(), verifiedPasswordHash))
            {
                return Result<JoinClassResponseDto>.Failure("INVALID_PASSWORD: Mật khẩu tham gia lớp học không chính xác.");
            }
        }

        return await context.ExecuteClassEnrollmentAsync(targetClass.Id, async lockToken =>
        {
            // Đọc lại bản ghi lớp tươi từ CSDL bên trong khóa để chặn race condition
            // (lớp vừa tắt IsActive / khóa IsJoinEnabled / đổi mật khẩu / giảm MaxStudents).
            var trackedClass = await context.Classes
                .AsTracking()
                .Include(c => c.Course)
                .Include(c => c.Semester)
                .Include(c => c.Lecturer)
                .Include(c => c.CourseOffering)
                .FirstOrDefaultAsync(c => c.Id == targetClass.Id, lockToken);

            if (trackedClass is null)
            {
                return Result<JoinClassResponseDto>.Failure("NOT_FOUND: Không tìm thấy lớp học với mã tham gia này.");
            }

            if (!trackedClass.IsActive)
            {
                return Result<JoinClassResponseDto>.Failure("JOIN_DISABLED: Lớp học đã bị vô hiệu hóa.");
            }

            if (trackedClass.CourseOfferingId.HasValue &&
                (trackedClass.CourseOffering is null || !trackedClass.CourseOffering.IsEnabled || trackedClass.Course is null || !trackedClass.Course.IsActive))
            {
                return Result<JoinClassResponseDto>.Failure(
                    "JOIN_DISABLED: Course offering của lớp hiện đang bị vô hiệu hóa.");
            }

            if (trackedClass.Course is null || !trackedClass.Course.IsActive)
            {
                return Result<JoinClassResponseDto>.Failure(
                    "JOIN_DISABLED: Môn học của lớp hiện đang bị vô hiệu hóa.");
            }

            if (!trackedClass.IsJoinEnabled)
            {
                return Result<JoinClassResponseDto>.Failure("JOIN_DISABLED: Lớp học hiện đang khóa tính năng tham gia.");
            }

            if (!trackedClass.AllowSelfEnrollment)
            {
                return Result<JoinClassResponseDto>.Failure("FORBIDDEN: Lớp học không cho phép sinh viên tự ghi danh.");
            }

            if (!string.IsNullOrWhiteSpace(request.JoinCode))
            {
                var trimmedCode = request.JoinCode.Trim();
                if (!string.Equals(trackedClass.JoinCode, trimmedCode, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(trackedClass.Code, trimmedCode, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<JoinClassResponseDto>.Failure(
                        "NOT_FOUND: Mã tham gia lớp vừa được thay đổi. Vui lòng thử lại.");
                }
            }

            if (!string.Equals(
                    trackedClass.JoinPasswordHash ?? string.Empty,
                    verifiedPasswordHash ?? string.Empty,
                    StringComparison.Ordinal))
            {
                return Result<JoinClassResponseDto>.Failure(
                    "INVALID_PASSWORD: Mật khẩu tham gia lớp học vừa được thay đổi. Vui lòng thử lại.");
            }

            // Kiểm tra đã enroll chưa trước khi kiểm tra đầy sĩ số
            var existingEnrollment = await context.ClassEnrollments
                .FirstOrDefaultAsync(e => e.ClassId == trackedClass.Id && e.StudentId == request.StudentId, lockToken);

            if (existingEnrollment is not null &&
                string.Equals(existingEnrollment.Status, "enrolled", StringComparison.OrdinalIgnoreCase))
            {
                return Result<JoinClassResponseDto>.Failure("ALREADY_ENROLLED: Sinh viên đã ghi danh vào lớp học này rồi.");
            }

            // Kiểm tra sĩ số và ghi danh trong cùng transaction có khóa theo lớp.
            var currentEnrollmentCount = await context.ClassEnrollments
                .CountAsync(e => e.ClassId == trackedClass.Id && e.Status == "enrolled", lockToken);

            if (currentEnrollmentCount >= trackedClass.MaxStudents)
            {
                return Result<JoinClassResponseDto>.Failure("CONFLICT: Lớp học đã đạt số lượng sinh viên tối đa.");
            }

            DateTime enrolledAt;
            var joinMethod = string.IsNullOrWhiteSpace(trackedClass.JoinPasswordHash) ? "INVITATION" : "PASSWORD";

            if (existingEnrollment is not null)
            {
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
                    ClassId = trackedClass.Id,
                    StudentId = request.StudentId,
                    JoinedViaCode = true,
                    JoinMethod = joinMethod,
                    Status = "enrolled",
                    EnrolledAt = enrolledAt
                };

                context.ClassEnrollments.Add(newEnrollment);
            }

            context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.StudentId,
                Action = "CLASS_JOINED",
                EntityName = "classes",
                EntityId = trackedClass.Id,
                NewValues = $"JoinMethod={joinMethod}",
                CreatedAt = enrolledAt
            });

            await context.SaveChangesAsync(lockToken);

            var response = new JoinClassResponseDto(
                trackedClass.Id,
                trackedClass.Code,
                trackedClass.Name,
                trackedClass.CourseId,
                trackedClass.Course.Code,
                trackedClass.Course.Name,
                trackedClass.SemesterId,
                trackedClass.Semester?.Code ?? string.Empty,
                trackedClass.LecturerId,
                trackedClass.Lecturer?.FullName,
                enrolledAt,
                "enrolled",
                joinMethod,
                trackedClass.CourseOfferingId);

            return Result<JoinClassResponseDto>.Success(response);
        }, cancellationToken);
    }
}
