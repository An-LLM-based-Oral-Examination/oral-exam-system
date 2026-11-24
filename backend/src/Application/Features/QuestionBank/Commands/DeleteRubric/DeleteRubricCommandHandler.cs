using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.DeleteRubric;

public sealed class DeleteRubricCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteRubricCommand, Result>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result> Handle(
        DeleteRubricCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result.Failure("FORBIDDEN: Bạn không có quyền xóa Barem Rubric.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null ||
            (!string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(
                "FORBIDDEN: Chỉ Giảng viên hoặc Trưởng bộ môn đang hoạt động mới được xóa Barem Rubric.");
        }

        var rubric = await _context.Rubrics
            .FirstOrDefaultAsync(r => r.Id == request.RubricId && r.IsActive, cancellationToken);

        if (rubric is null)
        {
            return Result.Failure("NOT_FOUND: Không tìm thấy Barem Rubric đang hoạt động.");
        }

        var canAccessCourse = await QuestionCourseAccess
            .AllowedCourseIds(_context, actor.Role, actor.Id)
            .AnyAsync(id => id == rubric.CourseId, cancellationToken);

        if (!canAccessCourse)
        {
            return Result.Failure(
                "FORBIDDEN: Bạn không phụ trách hoặc không được phân công giảng dạy môn học này.");
        }

        var isReferencedByPracticeQuestion = await _context.PracticeQuestions
            .AsNoTracking()
            .AnyAsync(q => q.RubricId == rubric.Id && !q.IsDeleted, cancellationToken);

        var isReferencedByExamQuestion = await _context.ExamQuestions
            .AsNoTracking()
            .AnyAsync(q => q.RubricId == rubric.Id && !q.IsDeleted, cancellationToken);

        if (isReferencedByPracticeQuestion || isReferencedByExamQuestion)
        {
            return Result.Failure(
                "CONFLICT: Không thể xóa Barem Rubric vì đang được tham chiếu bởi câu hỏi trong ngân hàng đề.");
        }

        var now = DateTime.UtcNow;
        rubric.IsActive = false;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "DELETE_RUBRIC",
            EntityName = "rubrics",
            EntityId = rubric.Id,
            OldValues = "isActive=true",
            NewValues = "isActive=false",
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
