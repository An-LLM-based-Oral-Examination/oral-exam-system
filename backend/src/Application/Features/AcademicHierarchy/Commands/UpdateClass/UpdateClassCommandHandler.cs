using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.UpdateClass;

public sealed class UpdateClassCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateClassCommand, Result<LecturerClassItemDto>>
{
    public async Task<Result<LecturerClassItemDto>> Handle(
        UpdateClassCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ClassId == Guid.Empty)
        {
            return Result<LecturerClassItemDto>.Failure("VALIDATION: Mã lớp học không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<LecturerClassItemDto>.Failure("VALIDATION: Tên lớp (Name) không được để trống.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 150)
        {
            return Result<LecturerClassItemDto>.Failure("VALIDATION: Tên lớp không được vượt quá 150 ký tự.");
        }

        if (request.MaxStudents is <= 0 or > 200)
        {
            return Result<LecturerClassItemDto>.Failure("VALIDATION: Sĩ số tối đa phải nằm trong khoảng từ 1 đến 200.");
        }

        string? normalizedCode = null;
        if (request.Code is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return Result<LecturerClassItemDto>.Failure("VALIDATION: Mã lớp (Code) không được để trống.");
            }

            normalizedCode = request.Code.Trim().ToUpperInvariant();
            if (normalizedCode.Length > 50)
            {
                return Result<LecturerClassItemDto>.Failure("VALIDATION: Mã lớp không được vượt quá 50 ký tự.");
            }
        }

        var targetClass = await context.Classes
            .Include(c => c.Course)
                .ThenInclude(c => c.AcademicProgram)
            .Include(c => c.Semester)
            .Include(c => c.Lecturer)
            .Include(c => c.Enrollments)
            .Include(c => c.InClassExamShifts)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (targetClass is null)
        {
            return Result<LecturerClassItemDto>.Failure("NOT_FOUND: Không tìm thấy lớp học phần.");
        }

        if (!ClassRosterAccess.CanManage(targetClass, request.ActorUserId, request.ActorRole))
        {
            return Result<LecturerClassItemDto>.Failure(
                "FORBIDDEN: Chỉ Giảng viên phụ trách lớp học hoặc Trưởng bộ môn của môn học mới có quyền cập nhật lớp học phần.");
        }

        if (!targetClass.IsActive)
        {
            return Result<LecturerClassItemDto>.Failure(
                "CONFLICT: Lớp học phần đã bị vô hiệu hóa, không thể cập nhật.");
        }

        var enrolledCount = targetClass.Enrollments.Count(e => e.Status == "enrolled");
        if (request.MaxStudents < enrolledCount)
        {
            return Result<LecturerClassItemDto>.Failure(
                $"CONFLICT: Sĩ số tối đa ({request.MaxStudents}) không được nhỏ hơn số sinh viên đã ghi danh hiện tại ({enrolledCount}).");
        }

        if (normalizedCode is not null &&
            !string.Equals(targetClass.Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await context.Classes
                .AsNoTracking()
                .AnyAsync(c => c.Id != targetClass.Id && c.Code.ToUpper() == normalizedCode, cancellationToken);

            if (codeExists)
            {
                return Result<LecturerClassItemDto>.Failure("CONFLICT: Mã lớp học đã tồn tại trong hệ thống.");
            }
        }

        var oldValues = $"Code={targetClass.Code};Name={targetClass.Name};MaxStudents={targetClass.MaxStudents};AllowSelfEnrollment={targetClass.AllowSelfEnrollment};IsJoinEnabled={targetClass.IsJoinEnabled}";

        if (normalizedCode is not null)
        {
            targetClass.Code = normalizedCode;
        }

        targetClass.Name = trimmedName;
        targetClass.MaxStudents = request.MaxStudents;
        if (request.AllowSelfEnrollment.HasValue)
        {
            targetClass.AllowSelfEnrollment = request.AllowSelfEnrollment.Value;
        }
        if (request.IsJoinEnabled.HasValue)
        {
            targetClass.IsJoinEnabled = request.IsJoinEnabled.Value;
        }

        var now = DateTime.UtcNow;
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "CLASS_UPDATED",
            EntityName = "classes",
            EntityId = targetClass.Id,
            OldValues = oldValues,
            NewValues = $"Code={targetClass.Code};Name={targetClass.Name};MaxStudents={targetClass.MaxStudents};AllowSelfEnrollment={targetClass.AllowSelfEnrollment};IsJoinEnabled={targetClass.IsJoinEnabled}",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result<LecturerClassItemDto>.Success(new LecturerClassItemDto(
            targetClass.Id,
            targetClass.Code,
            targetClass.Name,
            targetClass.JoinCode,
            targetClass.CourseId,
            targetClass.Course?.Code ?? string.Empty,
            targetClass.Course?.Name ?? string.Empty,
            targetClass.SemesterId,
            targetClass.Semester?.Code ?? string.Empty,
            targetClass.CourseOfferingId,
            targetClass.LecturerId,
            targetClass.Lecturer?.FullName,
            enrolledCount,
            targetClass.MaxStudents,
            targetClass.InClassExamShifts.Count,
            !string.IsNullOrWhiteSpace(targetClass.JoinPasswordHash),
            targetClass.IsJoinEnabled,
            targetClass.AllowSelfEnrollment,
            targetClass.IsActive,
            targetClass.CreatedAt,
            targetClass.InClassExamShifts.Count(s => s.Status == "ACTIVE" || s.Status == "IN_PROGRESS")));
    }
}
