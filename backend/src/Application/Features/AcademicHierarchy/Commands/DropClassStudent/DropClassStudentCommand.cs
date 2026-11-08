using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.DropClassStudent;

public sealed record DropClassStudentCommand(
    Guid ClassId,
    Guid StudentId,
    Guid ActorUserId,
    string? ActorRole) : IRequest<Result<ClassEnrolledStudentDto>>;

public sealed class DropClassStudentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DropClassStudentCommand, Result<ClassEnrolledStudentDto>>
{
    public async Task<Result<ClassEnrolledStudentDto>> Handle(
        DropClassStudentCommand request,
        CancellationToken cancellationToken)
    {
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
            return Result<ClassEnrolledStudentDto>.Failure("FORBIDDEN: Chỉ giảng viên phụ trách lớp được xóa sinh viên.");
        }

        var enrollment = await context.ClassEnrollments
            .Include(item => item.Student)
            .FirstOrDefaultAsync(
                item => item.ClassId == targetClass.Id && item.StudentId == request.StudentId,
                cancellationToken);

        if (enrollment is null || !string.Equals(enrollment.Status, "enrolled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ClassEnrolledStudentDto>.Failure("NOT_FOUND: Sinh viên không còn trong lớp.");
        }

        enrollment.Status = "dropped";
        var droppedAt = DateTime.UtcNow;
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "CLASS_STUDENT_DROPPED",
            EntityName = "class_enrollments",
            EntityId = enrollment.Id,
            NewValues = $"classId={targetClass.Id};studentId={enrollment.StudentId};status=dropped",
            CreatedAt = droppedAt,
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result<ClassEnrolledStudentDto>.Success(new ClassEnrolledStudentDto(
            enrollment.StudentId,
            enrollment.Student.FullName,
            enrollment.Student.Email,
            enrollment.Student.StudentCode,
            enrollment.EnrolledAt,
            enrollment.Status,
            enrollment.JoinMethod));
    }
}
