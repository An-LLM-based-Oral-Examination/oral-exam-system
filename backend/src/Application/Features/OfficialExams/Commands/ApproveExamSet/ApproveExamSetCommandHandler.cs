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
using OralExamination.Application.Features.QuestionBank;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
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
                    .ThenInclude(q => q.Rubric)
                        .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (!await OfficialExamAuthorization.OwnsAcademicCourseAsync(context, examSet.CourseId, request.ActorUserId, cancellationToken))
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!examSet.IsActive)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "CONFLICT: Bộ đề đã bị xóa mềm và không thể phê duyệt.");
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
            || (esq.ExamQuestion.ApprovalStatus == ApprovalStatus.Approved && !esq.ExamQuestion.IsActive)
            || esq.ExamQuestion.BankCurationStatus == BankCurationStatus.ExcludedFromBank);
        if (hasInvalidQuestion)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "CONFLICT: Bộ đề chứa câu hỏi đã bị xóa mềm, tạm tắt (inactive), bị từ chối, yêu cầu chỉnh sửa hoặc đã bị loại khỏi kho; vui lòng cập nhật lại bộ đề trước khi phê duyệt.");
        }

        // Chỉ cascade và kiểm định Invariant #7 + chống tự duyệt trên các câu CHƯA ở trạng thái APPROVED.
        var unapprovedQuestions = examSet.ExamSetQuestions
            .Where(esq => esq.ExamQuestion is not null && esq.ExamQuestion.ApprovalStatus != ApprovalStatus.Approved)
            .Select(esq => esq.ExamQuestion!)
            .GroupBy(q => q.Id)
            .Select(g => g.First())
            .ToList();

        foreach (var question in unapprovedQuestions)
        {
            if (question.SubmittedBy.HasValue && question.SubmittedBy.Value == request.ActorUserId)
            {
                return Result<ApproveExamSetResultDto>.Failure(
                    $"FORBIDDEN: Trưởng Bộ Môn không được tự phê duyệt câu hỏi '{question.Title}' do chính mình biên soạn.");
            }

            if (string.IsNullOrWhiteSpace(question.SampleAnswer) || question.SampleAnswer.Trim().Length < 50)
            {
                return Result<ApproveExamSetResultDto>.Failure(
                    $"UNPROCESSABLE: Câu hỏi '{question.Title}' có Model Answer dưới 50 ký tự, không thể phê duyệt bộ đề.");
            }

            if (question.Rubric is null || question.Rubric.Criteria is null || question.Rubric.Criteria.Count < 2)
            {
                return Result<ApproveExamSetResultDto>.Failure(
                    $"UNPROCESSABLE: Câu hỏi '{question.Title}' chưa có Barem Rubric hoặc ít hơn 2 tiêu chí đánh giá con, không thể phê duyệt bộ đề.");
            }

            if (question.Rubric.Criteria.Any(c =>
                string.IsNullOrWhiteSpace(c.CriterionName)
                || c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength))
            {
                return Result<ApproveExamSetResultDto>.Failure(
                    $"UNPROCESSABLE: Câu hỏi '{question.Title}' có tiêu chí Barem Rubric thiếu tên hoặc vượt quá 200 ký tự, không thể phê duyệt bộ đề.");
            }

            if (question.Rubric.Criteria.Any(c => c.MaxScore <= 0m))
            {
                return Result<ApproveExamSetResultDto>.Failure(
                    $"UNPROCESSABLE: Câu hỏi '{question.Title}' có tiêu chí Barem Rubric mang điểm nhỏ hơn hoặc bằng 0, không thể phê duyệt bộ đề.");
            }

            if (question.Rubric.Criteria.Any(c => !RubricNormalization.HasValidScorePrecision(c.MaxScore)))
            {
                return Result<ApproveExamSetResultDto>.Failure(
                    $"UNPROCESSABLE: Câu hỏi '{question.Title}' có tiêu chí Barem Rubric vượt quá 2 chữ số thập phân (numeric(4,2)), không thể phê duyệt bộ đề.");
            }

            if (question.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
            {
                return Result<ApproveExamSetResultDto>.Failure(
                    $"UNPROCESSABLE: Câu hỏi '{question.Title}' có tổng điểm Barem Rubric lệch 10.00đ, không thể phê duyệt bộ đề.");
            }

            if (!string.IsNullOrWhiteSpace(question.ImageUrl)
                && !string.Equals((question.Source ?? QuestionSource.Manual).Trim(), QuestionSource.Manual, StringComparison.OrdinalIgnoreCase))
            {
                return Result<ApproveExamSetResultDto>.Failure(
                    $"UNPROCESSABLE: Câu hỏi '{question.Title}' không phải câu hỏi thủ công (manual) nên không được đính kèm hình ảnh.");
            }
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

        // Chỉ cascade các câu hỏi chưa ở trạng thái APPROVED (giữ nguyên ApprovedBy/ApprovedAt của các câu đã duyệt trước đó)
        var cascadedCount = 0;
        var cascadedQuestionIds = new List<Guid>(unapprovedQuestions.Count);
        var approvedAt = DateTime.UtcNow;
        foreach (var question in unapprovedQuestions)
        {
            ReviewQuestionDecisionCommandHandler.NormalizeRubricWeights(question.Rubric!);
            question.ApprovalStatus = ApprovalStatus.Approved;
            question.ApprovedBy = request.ActorUserId;
            question.ApprovedAt = approvedAt;
            question.IsActive = true;
            cascadedQuestionIds.Add(question.Id);
            cascadedCount++;
        }

        if (cascadedQuestionIds.Count > 0)
        {
            var pendingReviewItems = await context.QuestionReviewRequestItems
                .Include(i => i.Request)
                .Where(i =>
                    cascadedQuestionIds.Contains(i.QuestionId)
                    && i.UsageScope == "exam"
                    && i.Decision == "PENDING")
                .ToListAsync(cancellationToken);

            foreach (var item in pendingReviewItems)
            {
                item.Decision = ApprovalStatus.Approved;
                item.ReviewedBy = request.ActorUserId;
                item.ReviewedAt = approvedAt;
                if (!string.IsNullOrWhiteSpace(request.ReviewNotes) && string.IsNullOrWhiteSpace(item.ReviewNotes))
                {
                    item.ReviewNotes = request.ReviewNotes.Trim();
                }
                if (item.Request is not null)
                {
                    item.Request.Version++;
                }
            }
        }

        examSet.Status = ExamSetStatus.Official;
        examSet.ApprovedBy = request.ActorUserId;
        examSet.ApprovedAt = approvedAt;
        if (!string.IsNullOrWhiteSpace(request.ReviewNotes))
        {
            examSet.ReviewNotes = request.ReviewNotes.Trim();
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ApproveExamSetResultDto>.Failure(
                "CONFLICT: Bộ đề hoặc yêu cầu duyệt câu hỏi vừa được xử lý đồng thời bởi người khác.");
        }

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
