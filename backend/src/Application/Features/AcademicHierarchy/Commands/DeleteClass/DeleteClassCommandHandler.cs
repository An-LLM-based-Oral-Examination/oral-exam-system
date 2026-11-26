using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.DeleteClass;

public sealed class DeleteClassCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteClassCommand, Result>
{
    public async Task<Result> Handle(
        DeleteClassCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ClassId == Guid.Empty)
        {
            return Result.Failure("VALIDATION: Mã lớp học không hợp lệ.");
        }

        var targetClass = await context.Classes
            .Include(c => c.Course)
                .ThenInclude(c => c.AcademicProgram)
            .Include(c => c.InClassExamShifts)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (targetClass is null)
        {
            return Result.Failure("NOT_FOUND: Không tìm thấy lớp học phần.");
        }

        if (!ClassRosterAccess.CanManage(targetClass, request.ActorUserId, request.ActorRole))
        {
            return Result.Failure(
                "FORBIDDEN: Chỉ Giảng viên phụ trách lớp học hoặc Trưởng bộ môn của môn học mới có quyền xóa lớp học phần.");
        }

        if (targetClass.InClassExamShifts.Any(s =>
                s.IsLocked ||
                string.Equals(s.Status, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(
                "CONFLICT: Không thể xóa/vô hiệu hóa lớp học phần đang có ca kiểm tra hoạt động, đã khóa hoặc đã chốt điểm.");
        }

        var oldValues = $"IsActive={targetClass.IsActive};IsJoinEnabled={targetClass.IsJoinEnabled}";
        targetClass.IsActive = false;
        targetClass.IsJoinEnabled = false;

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "CLASS_DELETED",
            EntityName = "classes",
            EntityId = targetClass.Id,
            OldValues = oldValues,
            NewValues = "IsActive=False;IsJoinEnabled=False",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
