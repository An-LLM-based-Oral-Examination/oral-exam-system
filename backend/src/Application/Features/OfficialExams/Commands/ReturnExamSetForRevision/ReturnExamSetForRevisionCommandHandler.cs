using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.ReturnExamSetForRevision;

public sealed class ReturnExamSetForRevisionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ReturnExamSetForRevisionCommand, Result<ReturnExamSetForRevisionResultDto>>
{
    public async Task<Result<ReturnExamSetForRevisionResultDto>> Handle(
        ReturnExamSetForRevisionCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context, request.ActorUserId, request.ActorRole, UserRole.DepartmentHead, cancellationToken))
        {
            return Result<ReturnExamSetForRevisionResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var trimmedNotes = request.ReviewNotes?.Trim() ?? string.Empty;
        if (trimmedNotes.Length < 5 || trimmedNotes.Length > 2000)
        {
            return Result<ReturnExamSetForRevisionResultDto>.Failure(
                "UNPROCESSABLE: Vui lòng nhập lý do trả về chỉnh sửa từ 5 đến 2000 ký tự.");
        }

        var examSet = await context.ExamSets
            .Include(es => es.ExamSetQuestions)
                .ThenInclude(esq => esq.ExamQuestion)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result<ReturnExamSetForRevisionResultDto>.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (!await OfficialExamAuthorization.OwnsAcademicCourseAsync(
                context, examSet.CourseId, request.ActorUserId, cancellationToken))
        {
            return Result<ReturnExamSetForRevisionResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!examSet.IsActive)
        {
            return Result<ReturnExamSetForRevisionResultDto>.Failure(
                "CONFLICT: Bộ đề đã bị xóa mềm và không thể trả về chỉnh sửa.");
        }

        if (examSet.IsSampleSet)
        {
            return Result<ReturnExamSetForRevisionResultDto>.Failure(
                "UNPROCESSABLE: Không áp dụng thao tác trả về chỉnh sửa cho Bộ Đề Mẫu (Sample Set).");
        }

        if (examSet.Status != ExamSetStatus.SubmittedForReview)
        {
            return Result<ReturnExamSetForRevisionResultDto>.Failure(
                "CONFLICT: Chỉ có thể trả về chỉnh sửa bộ đề đang ở trạng thái chờ duyệt (SUBMITTED_FOR_REVIEW).");
        }

        var now = DateTime.UtcNow;
        var previousStatus = examSet.Status;

        examSet.Status = ExamSetStatus.Rejected;
        examSet.ReviewNotes = trimmedNotes;

        var returnedInlineCount = 0;
        if (examSet.CreatedBy.HasValue)
        {
            foreach (var esq in examSet.ExamSetQuestions)
            {
                var q = esq.ExamQuestion;
                if (q is not null
                    && !q.IsDeleted
                    && q.BankCurationStatus == BankCurationStatus.PendingCuration
                    && q.SubmittedBy == examSet.CreatedBy
                    && q.ApprovalStatus == ApprovalStatus.SubmittedForReview)
                {
                    q.ApprovalStatus = ApprovalStatus.NeedsRevision;
                    q.ReviewNotes = trimmedNotes;
                    q.IsActive = false;
                    returnedInlineCount++;
                }
            }
        }

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "RETURN_EXAM_SET_FOR_REVISION",
            EntityName = "exam_sets",
            EntityId = examSet.Id,
            OldValues = $"status={previousStatus}",
            NewValues = $"status={examSet.Status};reviewNotes={trimmedNotes};returnedInlineQuestions={returnedInlineCount}",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result<ReturnExamSetForRevisionResultDto>.Success(new ReturnExamSetForRevisionResultDto
        {
            ExamSetId = examSet.Id,
            SetCode = examSet.SetCode,
            Status = examSet.Status,
            ReviewNotes = trimmedNotes,
            ReturnedAt = now
        });
    }
}
