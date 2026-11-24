using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AssignClassStudent;

public sealed record AssignClassStudentCommand(
    Guid ClassId,
    Guid StudentId,
    Guid ActorUserId,
    string? ActorRole) : IRequest<Result<ClassEnrolledStudentDto>>;

public sealed class AssignClassStudentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AssignClassStudentCommand, Result<ClassEnrolledStudentDto>>
{
    public async Task<Result<ClassEnrolledStudentDto>> Handle(
        AssignClassStudentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.StudentId == Guid.Empty)
        {
            return Result<ClassEnrolledStudentDto>.Failure("VALIDATION: Chưa chọn sinh viên để thêm.");
        }

        var targetClass = await context.Classes
            .Include(item => item.Course)
                .ThenInclude(course => course.AcademicProgram)
            .FirstOrDefaultAsync(item => item.Id == request.ClassId, cancellationToken);

        if (targetClass is null || !targetClass.IsActive)
        {
            return Result<ClassEnrolledStudentDto>.Failure("NOT_FOUND: Không tìm thấy lớp học phần.");
        }

        if (!ClassRosterAccess.CanManage(targetClass, request.ActorUserId, request.ActorRole))
        {
            return Result<ClassEnrolledStudentDto>.Failure("FORBIDDEN: Chỉ giảng viên phụ trách lớp được thêm sinh viên.");
        }

        var student = await context.Users
            .FirstOrDefaultAsync(user => user.Id == request.StudentId, cancellationToken);

        if (student is null || !string.Equals(student.Role, UserRole.Student, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ClassEnrolledStudentDto>.Failure("NOT_FOUND: Không tìm thấy sinh viên với mã hoặc email này.");
        }

        if (!student.IsActive)
        {
            return Result<ClassEnrolledStudentDto>.Failure("VALIDATION: Tài khoản sinh viên đang bị khóa.");
        }

        var enrolledCount = await context.ClassEnrollments
            .CountAsync(
                enrollment => enrollment.ClassId == targetClass.Id && enrollment.Status == "enrolled",
                cancellationToken);

        var existing = await context.ClassEnrollments
            .FirstOrDefaultAsync(
                enrollment => enrollment.ClassId == targetClass.Id && enrollment.StudentId == student.Id,
                cancellationToken);

        if (existing is not null && string.Equals(existing.Status, "enrolled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ClassEnrolledStudentDto>.Failure("CONFLICT: Sinh viên đã có trong lớp.");
        }

        if (enrolledCount >= targetClass.MaxStudents)
        {
            return Result<ClassEnrolledStudentDto>.Failure("CONFLICT: Lớp đã đủ sĩ số.");
        }

        var enrolledAt = DateTime.UtcNow;
        ClassEnrollment enrollment;
        if (existing is null)
        {
            enrollment = new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                StudentId = student.Id,
                Status = "enrolled",
                JoinMethod = "assigned",
                JoinedViaCode = false,
                EnrolledAt = enrolledAt,
            };
            context.ClassEnrollments.Add(enrollment);
        }
        else
        {
            enrollment = existing;
            enrollment.Status = "enrolled";
            enrollment.JoinMethod = "assigned";
            enrollment.JoinedViaCode = false;
            enrollment.EnrolledAt = enrolledAt;
        }

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "CLASS_STUDENT_ASSIGNED",
            EntityName = "class_enrollments",
            EntityId = enrollment.Id,
            NewValues = $"classId={targetClass.Id};studentId={student.Id};joinMethod=assigned",
            CreatedAt = enrolledAt,
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result<ClassEnrolledStudentDto>.Success(new ClassEnrolledStudentDto(
            student.Id,
            student.FullName,
            student.Email,
            student.StudentCode,
            enrollment.EnrolledAt,
            enrollment.Status,
            enrollment.JoinMethod));
    }
}
