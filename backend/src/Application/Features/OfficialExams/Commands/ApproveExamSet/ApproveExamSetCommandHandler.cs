using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;

public sealed class ApproveExamSetCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ApproveExamSetCommand, Result<ApproveExamSetResultDto>>
{
    public async Task<Result<ApproveExamSetResultDto>> Handle(ApproveExamSetCommand request, CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context, request.ActorUserId, request.ActorRole, UserRole.DepartmentHead, cancellationToken))
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .Include(es => es.ExamSetQuestions)
                .ThenInclude(esq => esq.ExamQuestion)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (!await OfficialExamAuthorization.OwnsAcademicCourseAsync(context, examSet.CourseId, request.ActorUserId, cancellationToken))
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        if (examSet.IsSampleSet)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "UNPROCESSABLE: Bộ đề mẫu không đi qua quy trình phê duyệt bộ đề thi chính thức.");
        }

        if (examSet.CreatedBy.HasValue && examSet.CreatedBy.Value == request.ActorUserId)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "FORBIDDEN: Trưởng Bộ Môn không được tự duyệt bộ đề do chính mình tạo.");
        }

        if (examSet.Status == ExamSetStatus.Official)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetAlreadyApproved);
        }

        if (examSet.Status == ExamSetStatus.HandedOver)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetAlreadyHandedOver);
        }

        if (examSet.Status != ExamSetStatus.SubmittedForReview)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "CONFLICT: Chỉ có thể phê duyệt bộ đề đang ở trạng thái chờ duyệt (SUBMITTED_FOR_REVIEW).");
        }

        if (examSet.ExamSetQuestions.Count == 0)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "UNPROCESSABLE: Bộ đề không có câu hỏi nào để phê duyệt.");
        }

        var hasInvalidQuestion = examSet.ExamSetQuestions.Any(esq =>
            esq.ExamQuestion is null
            || esq.ExamQuestion.IsDeleted
            || esq.ExamQuestion.ApprovalStatus == ApprovalStatus.Rejected
            || esq.ExamQuestion.ApprovalStatus == ApprovalStatus.NeedsRevision
            || esq.ExamQuestion.BankCurationStatus == BankCurationStatus.ExcludedFromBank);
        if (hasInvalidQuestion)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "CONFLICT: Bộ đề chứa câu hỏi đã bị xóa mềm, bị từ chối, yêu cầu chỉnh sửa hoặc đã bị loại khỏi kho; vui lòng cập nhật lại bộ đề trước khi phê duyệt.");
        }

        var questionIds = examSet.ExamSetQuestions
            .Select(esq => esq.ExamQuestionId)
            .Distinct()
            .ToList();

        var hasSampleSetQuestion = await context.ExamSetQuestions
            .AsNoTracking()
            .AnyAsync(esq =>
                questionIds.Contains(esq.ExamQuestionId)
                && esq.ExamSetId != examSet.Id
                && esq.ExamSet.CourseId == examSet.CourseId
                && esq.ExamSet.IsSampleSet
                && esq.ExamSet.IsActive
                && esq.ExamSet.Status != ExamSetStatus.Rejected,
                cancellationToken);
        if (hasSampleSetQuestion)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "CONFLICT: Bộ đề chứa câu hỏi thuộc Bộ Đề Mẫu (Sample Set) đang hoạt động; vui lòng thay thế trước khi phê duyệt.");
        }

        // Check for duplicate questions in other active official / handed over sets of the same structure/cycle
        var duplicateUsages = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq => questionIds.Contains(esq.ExamQuestionId) &&
                          esq.ExamSetId != examSet.Id &&
                          esq.ExamSet.StructureId == examSet.StructureId &&
                          esq.ExamSet.IsActive &&
                          !esq.ExamSet.IsSampleSet &&
                          (esq.ExamSet.Status == ExamSetStatus.Official || esq.ExamSet.Status == ExamSetStatus.HandedOver))
            .Select(esq => new
            {
                esq.ExamQuestionId,
                esq.ExamSet.SetCode,
                esq.ExamQuestion.Title
            })
            .ToListAsync(cancellationToken);

        var duplicateWarnings = new List<string>();
        var groupedDuplicates = duplicateUsages
            .GroupBy(d => d.ExamQuestionId)
            .ToList();

        foreach (var group in groupedDuplicates)
        {
            var title = group.First().Title;
            var setCodes = string.Join(", ", group.Select(g => g.SetCode).Distinct());
            duplicateWarnings.Add($"CẢNH BÁO TRÙNG LẶP: Câu hỏi '{title}' (ID: {group.Key}) đã được sử dụng trong các bộ đề chính thức: {setCodes}.");
        }

        // Cascade contained questions to APPROVED
        var cascadedCount = 0;
        var approvedAt = DateTime.UtcNow;
        foreach (var esq in examSet.ExamSetQuestions)
        {
            if (esq.ExamQuestion != null)
            {
                esq.ExamQuestion.ApprovalStatus = ApprovalStatus.Approved;
                esq.ExamQuestion.ApprovedBy = request.ActorUserId;
                esq.ExamQuestion.ApprovedAt = approvedAt;
                esq.ExamQuestion.IsActive = true;
                cascadedCount++;
            }
        }

        examSet.Status = ExamSetStatus.Official;
        examSet.ApprovedBy = request.ActorUserId;
        examSet.ApprovedAt = approvedAt;
        if (!string.IsNullOrWhiteSpace(request.ReviewNotes))
        {
            examSet.ReviewNotes = request.ReviewNotes.Trim();
        }

        await context.SaveChangesAsync(cancellationToken);

        var result = new ApproveExamSetResultDto
        {
            ExamSetId = examSet.Id,
            Status = examSet.Status,
            QuestionsCascadedCount = cascadedCount,
            HasDuplicates = groupedDuplicates.Count > 0,
            DuplicateQuestionCount = groupedDuplicates.Count,
            DuplicateWarnings = duplicateWarnings
        };

        return Result<ApproveExamSetResultDto>.Success(result);
    }
}
