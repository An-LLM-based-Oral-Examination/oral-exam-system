using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.DeleteExamSet;

public sealed class DeleteExamSetCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteExamSetCommand, Result>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead
    ];

    public async Task<Result> Handle(DeleteExamSetCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        if (actor is null
            || !actor.IsActive
            || !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase)
            || !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .Include(es => es.Course)
                .ThenInclude(c => c!.AcademicProgram)
            .Include(es => es.ExamSetQuestions)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (examSet.Course is null || !examSet.Course.IsActive)
        {
            return Result.Failure(OfficialExamErrors.CourseNotFound);
        }

        var isDeptHead = string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase);
        if (examSet.IsSampleSet && !isDeptHead)
        {
            return Result.Failure(
                "FORBIDDEN: Chỉ Trưởng Bộ Môn mới có quyền xóa Bộ Đề Mẫu (Sample Set).");
        }

        var canDeleteExamSet = actor.Role switch
        {
            UserRole.DepartmentHead => AcademicCourseOwnership.IsOwnedBy(examSet.Course, actor.Id),
            UserRole.Lecturer => examSet.CreatedBy == actor.Id
                && await OfficialExamAuthorization.CanLecturerAccessCourseAsync(
                    context, examSet.CourseId, actor.Id, cancellationToken),
            _ => false
        };
        if (!canDeleteExamSet)
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        if (!examSet.IsActive)
        {
            return Result.Failure("CONFLICT: Bộ đề đã bị xóa trước đó.");
        }

        if (examSet.Status != ExamSetStatus.Draft && examSet.Status != ExamSetStatus.Rejected)
        {
            return Result.Failure(
                "CONFLICT: Chỉ có thể xóa bộ đề đang ở trạng thái DRAFT hoặc REJECTED.");
        }

        var hasStudentTickets = await context.StudentExamTickets
            .AsNoTracking()
            .AnyAsync(t => t.ExamSetId == examSet.Id, cancellationToken);
        if (hasStudentTickets)
        {
            return Result.Failure(
                "CONFLICT: Không thể xóa bộ đề đã được gán cho vé thi sinh viên.");
        }

        var now = DateTime.UtcNow;
        var releasedLinks = examSet.ExamSetQuestions.ToList();
        var releasedCount = releasedLinks.Count;

        if (releasedCount > 0)
        {
            context.ExamSetQuestions.RemoveRange(releasedLinks);
            examSet.ExamSetQuestions.Clear();
        }

        examSet.IsActive = false;

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "DELETE_EXAM_SET",
            EntityName = "exam_sets",
            EntityId = examSet.Id,
            OldValues = $"setCode={examSet.SetCode};status={examSet.Status};isActive=true;questionCount={releasedCount}",
            NewValues = $"setCode={examSet.SetCode};status={examSet.Status};isActive=false;releasedQuestions={releasedCount}",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
