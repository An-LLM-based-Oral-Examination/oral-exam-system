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

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AddClassStudent;

public sealed class AddClassStudentCommandHandler
    : IRequestHandler<AddClassStudentCommand, Result<AddClassStudentResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<AddClassStudentCommand> _validator;

    public AddClassStudentCommandHandler(
        IApplicationDbContext context,
        IValidator<AddClassStudentCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public AddClassStudentCommandHandler(IApplicationDbContext context)
        : this(context, new AddClassStudentCommandValidator())
    {
    }

    public async Task<Result<AddClassStudentResponseDto>> Handle(
        AddClassStudentCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AddClassStudentResponseDto>.Failure($"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        // 1. Tải thông tin lớp học kèm môn học và ngành đào tạo
        var targetClass = await _context.Classes
            .Include(c => c.Course)
                .ThenInclude(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (targetClass is null)
        {
            return Result<AddClassStudentResponseDto>.Failure("NOT_FOUND: Không tìm thấy lớp học phần.");
        }

        // 2. Xác minh quyền sở hữu lớp
        var actor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is not null && !actor.IsActive)
        {
            return Result<AddClassStudentResponseDto>.Failure("FORBIDDEN: Tài khoản người thực hiện đang bị khóa hoặc vô hiệu hóa.");
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
            return Result<AddClassStudentResponseDto>.Failure(
                "FORBIDDEN: Giảng viên chỉ được phép quản lý danh sách sinh viên của lớp học phần do chính mình phụ trách.");
        }

        if (!targetClass.IsActive)
        {
            return Result<AddClassStudentResponseDto>.Failure("VALIDATION: Lớp học phần đã bị vô hiệu hóa.");
        }

        // 3. Phân giải sinh viên linh hoạt (StudentId, MSSV, Email, Identifier)
        var student = await ResolveStudentAsync(
            request.StudentId,
            request.StudentCode,
            request.Email,
            request.Identifier,
            cancellationToken);

        if (student is null)
        {
            return Result<AddClassStudentResponseDto>.Failure("NOT_FOUND: Không tìm thấy sinh viên trong hệ thống với thông tin nhận diện đã cung cấp.");
        }

        if (!string.Equals(student.Role, UserRole.Student, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AddClassStudentResponseDto>.Failure("VALIDATION: Người dùng được chỉ định không có vai trò Sinh viên (role != student).");
        }

        if (!student.IsActive)
        {
            return Result<AddClassStudentResponseDto>.Failure("VALIDATION: Tài khoản sinh viên hiện đang bị khóa hoặc vô hiệu hóa.");
        }

        // 4. Kiểm tra sĩ số tối đa
        var currentCount = await _context.ClassEnrollments
            .CountAsync(e => e.ClassId == targetClass.Id && e.Status == "enrolled", cancellationToken);

        if (currentCount >= targetClass.MaxStudents)
        {
            return Result<AddClassStudentResponseDto>.Failure(
                $"CONFLICT: Lớp học đã đạt sĩ số sinh viên tối đa ({targetClass.MaxStudents} sinh viên).");
        }

        // 5. Kiểm tra ghi danh hiện hữu
        var existingEnrollment = await _context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.ClassId == targetClass.Id && e.StudentId == student.Id, cancellationToken);

        DateTime enrolledAt;
        if (existingEnrollment is not null)
        {
            if (string.Equals(existingEnrollment.Status, "enrolled", StringComparison.OrdinalIgnoreCase))
            {
                return Result<AddClassStudentResponseDto>.Failure(
                    $"CONFLICT: Sinh viên '{student.FullName}' ({student.StudentCode ?? student.Email}) đã ghi danh vào lớp học này rồi.");
            }

            existingEnrollment.Status = "enrolled";
            existingEnrollment.JoinedViaCode = false;
            existingEnrollment.JoinMethod = "assigned";
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
                StudentId = student.Id,
                JoinedViaCode = false,
                JoinMethod = "assigned",
                Status = "enrolled",
                EnrolledAt = enrolledAt
            };
            _context.ClassEnrollments.Add(newEnrollment);
        }

        // 6. Ghi AuditLog
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "STUDENT_ADDED_TO_CLASS",
            EntityName = "class_enrollments",
            EntityId = targetClass.Id,
            NewValues = $"StudentId={student.Id},StudentCode={student.StudentCode},Email={student.Email},JoinMethod=assigned",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        var response = new AddClassStudentResponseDto(
            targetClass.Id,
            student.Id,
            student.FullName,
            student.Email,
            student.StudentCode,
            enrolledAt,
            "enrolled",
            "assigned");

        return Result<AddClassStudentResponseDto>.Success(response);
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
