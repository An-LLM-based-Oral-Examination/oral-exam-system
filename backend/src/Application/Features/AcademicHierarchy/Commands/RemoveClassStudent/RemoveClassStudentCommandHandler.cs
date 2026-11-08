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
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.RemoveClassStudent;

public sealed class RemoveClassStudentCommandHandler
    : IRequestHandler<RemoveClassStudentCommand, Result<RemoveClassStudentResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<RemoveClassStudentCommand> _validator;

    public RemoveClassStudentCommandHandler(
        IApplicationDbContext context,
        IValidator<RemoveClassStudentCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public RemoveClassStudentCommandHandler(IApplicationDbContext context)
        : this(context, new RemoveClassStudentCommandValidator())
    {
    }

    public async Task<Result<RemoveClassStudentResponseDto>> Handle(
        RemoveClassStudentCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<RemoveClassStudentResponseDto>.Failure($"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        // 1. Tải thông tin lớp học
        var targetClass = await _context.Classes
            .Include(c => c.Course)
                .ThenInclude(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (targetClass is null)
        {
            return Result<RemoveClassStudentResponseDto>.Failure("NOT_FOUND: Không tìm thấy lớp học phần.");
        }

        // 2. Xác minh quyền sở hữu lớp
        var actor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is not null && !actor.IsActive)
        {
            return Result<RemoveClassStudentResponseDto>.Failure("FORBIDDEN: Tài khoản người thực hiện đang bị khóa hoặc vô hiệu hóa.");
        }

        var effectiveRole = !string.IsNullOrWhiteSpace(request.ActorRole)
            ? request.ActorRole
            : actor?.Role;

        var isAdmin = string.Equals(effectiveRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase);
        var isClassLecturer = targetClass.LecturerId.HasValue && targetClass.LecturerId.Value == request.ActorUserId;
        var departmentHeadId = targetClass.Course.AcademicProgram?.DepartmentHeadUserId
                               ?? targetClass.Course.DepartmentHeadUserId;
        var isCourseDeptHead = departmentHeadId == request.ActorUserId;

        if (!isAdmin && !isClassLecturer && !isCourseDeptHead)
        {
            return Result<RemoveClassStudentResponseDto>.Failure(
                "FORBIDDEN: Giảng viên chỉ được phép quản lý danh sách sinh viên của lớp học phần do chính mình phụ trách.");
        }

        // 3. Phân giải sinh viên linh hoạt
        var student = await ResolveStudentAsync(
            request.StudentId,
            request.StudentCode,
            request.Email,
            request.Identifier,
            cancellationToken);

        if (student is null)
        {
            return Result<RemoveClassStudentResponseDto>.Failure("NOT_FOUND: Không tìm thấy sinh viên trong hệ thống với thông tin nhận diện đã cung cấp.");
        }

        // 4. Tìm bản ghi ghi danh trong lớp
        var enrollment = await _context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.ClassId == targetClass.Id && e.StudentId == student.Id, cancellationToken);

        if (enrollment is null || !string.Equals(enrollment.Status, "enrolled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<RemoveClassStudentResponseDto>.Failure(
                $"NOT_FOUND: Sinh viên '{student.FullName}' ({student.StudentCode ?? student.Email}) hiện không ghi danh trong lớp học này.");
        }

        // 5. Xóa bản ghi ClassEnrollment
        _context.ClassEnrollments.Remove(enrollment);

        // 6. Ghi AuditLog
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "STUDENT_REMOVED_FROM_CLASS",
            EntityName = "class_enrollments",
            EntityId = targetClass.Id,
            OldValues = $"StudentId={student.Id},StudentCode={student.StudentCode},Email={student.Email},EnrollmentId={enrollment.Id}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        var response = new RemoveClassStudentResponseDto(
            targetClass.Id,
            student.Id,
            student.FullName,
            student.Email,
            student.StudentCode,
            $"Đã xóa sinh viên '{student.FullName}' ({student.StudentCode ?? student.Email}) khỏi lớp học phần thành công.");

        return Result<RemoveClassStudentResponseDto>.Success(response);
    }

    private async Task<User?> ResolveStudentAsync(
        Guid? studentId,
        string? studentCode,
        string? email,
        string? identifier,
        CancellationToken cancellationToken)
    {
        if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == studentId.Value, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(identifier) && Guid.TryParse(identifier.Trim(), out var parsedId))
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == parsedId, cancellationToken);
        }

        var normalizedCode = !string.IsNullOrWhiteSpace(studentCode)
            ? studentCode.Trim().ToUpperInvariant()
            : null;

        var normalizedEmail = !string.IsNullOrWhiteSpace(email)
            ? email.Trim().ToLowerInvariant()
            : null;

        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StudentCode != null && u.StudentCode.ToUpper() == normalizedCode, cancellationToken);
            if (user is not null) return user;
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
            if (user is not null) return user;
        }

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            var raw = identifier.Trim();
            var upper = raw.ToUpperInvariant();
            var lower = raw.ToLowerInvariant();

            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    (u.StudentCode != null && u.StudentCode.ToUpper() == upper) ||
                    u.Email.ToLower() == lower ||
                    (u.Username != null && u.Username.ToLower() == lower),
                    cancellationToken);
        }

        return null;
    }
}
