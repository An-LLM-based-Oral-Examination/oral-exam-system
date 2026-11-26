using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;

public sealed class SubmitExamSetForReviewCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SubmitExamSetForReviewCommand, Result>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead
    ];

    public async Task<Result> Handle(SubmitExamSetForReviewCommand request, CancellationToken cancellationToken)
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
        if (actor is null ||
            !actor.IsActive ||
            !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .Include(es => es.Course)
                .ThenInclude(c => c!.AcademicProgram)
            .Include(es => es.Structure)
            .Include(es => es.ExamSetQuestions)
                .ThenInclude(esq => esq.ExamQuestion)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (examSet.Course is null || !examSet.Course.IsActive)
        {
            return Result.Failure(OfficialExamErrors.CourseNotFound);
        }

        var canSubmitExamSet = actor.Role switch
        {
            UserRole.DepartmentHead => AcademicCourseOwnership.IsOwnedBy(examSet.Course, actor.Id),
            UserRole.Lecturer => examSet.CreatedBy == actor.Id
                && await OfficialExamAuthorization.CanLecturerAccessCourseAsync(
                    context, examSet.CourseId, actor.Id, cancellationToken),
            _ => false
        };
        if (!canSubmitExamSet)
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        if (!examSet.IsActive)
        {
            return Result.Failure("CONFLICT: Bộ đề đã bị xóa mềm và không thể nộp duyệt.");
        }

        if (examSet.Status != ExamSetStatus.Draft && examSet.Status != ExamSetStatus.Rejected)
        {
            return Result.Failure(OfficialExamErrors.ExamSetStatusInvalid);
        }

        if (examSet.IsSampleSet)
        {
            return Result.Failure(
                "UNPROCESSABLE: Bộ Đề Mẫu (Sample Set) do Trưởng Bộ Môn tạo và quản lý trực tiếp, không cần gửi duyệt theo luồng bộ đề thi thật.");
        }

        if (examSet.ExamSetQuestions.Count == 0)
        {
            return Result.Failure(OfficialExamErrors.ExamSetNoQuestions);
        }

        var hasInvalidQuestion = examSet.ExamSetQuestions.Any(esq =>
            esq.ExamQuestion is null
            || esq.ExamQuestion.IsDeleted
            || esq.ExamQuestion.ApprovalStatus == ApprovalStatus.Rejected
            || esq.ExamQuestion.ApprovalStatus == ApprovalStatus.NeedsRevision
            || esq.ExamQuestion.BankCurationStatus == BankCurationStatus.ExcludedFromBank);
        if (hasInvalidQuestion)
        {
            return Result.Failure(
                "CONFLICT: Bộ đề chứa câu hỏi đã bị xóa mềm, bị từ chối, yêu cầu chỉnh sửa hoặc đã bị loại khỏi kho; vui lòng cập nhật lại bộ đề trước khi nộp duyệt.");
        }

        var structureValidation = ExamSetStructureValidator.Validate(
            examSet.Structure,
            examSet.ExamSetQuestions.Select(esq => esq.ExamQuestion.BloomLevel).ToList());
        if (!structureValidation.IsSuccess)
        {
            return structureValidation;
        }

        var questionIds = examSet.ExamSetQuestions
            .Select(esq => esq.ExamQuestionId)
            .Distinct()
            .ToList();

        var hasSampleQuestion = await context.ExamSetQuestions
            .AsNoTracking()
            .AnyAsync(esq =>
                questionIds.Contains(esq.ExamQuestionId)
                && esq.ExamSet.CourseId == examSet.CourseId
                && esq.ExamSet.IsSampleSet
                && esq.ExamSet.IsActive
                && esq.ExamSet.Status != ExamSetStatus.Rejected,
                cancellationToken);
        if (hasSampleQuestion)
        {
            return Result.Failure(
                "UNPROCESSABLE: Không thể nộp duyệt bộ đề thi thật có chứa câu hỏi thuộc Đề mẫu.");
        }

        // (a) Ẩn sau khi duyệt: chặn nộp bộ đề chứa câu hỏi đã thuộc bộ đề OFFICIAL / HANDED_OVER trong cùng đợt/cấu trúc đề
        var hasApprovedQuestionInCycle = await context.ExamSetQuestions
            .AsNoTracking()
            .AnyAsync(esq =>
                questionIds.Contains(esq.ExamQuestionId)
                && esq.ExamSetId != examSet.Id
                && esq.ExamSet.IsActive
                && !esq.ExamSet.IsSampleSet
                && (esq.ExamSet.Status == ExamSetStatus.Official || esq.ExamSet.Status == ExamSetStatus.HandedOver)
                && (esq.ExamSet.StructureId == examSet.StructureId
                    || (examSet.SessionId.HasValue && esq.ExamSet.SessionId == examSet.SessionId.Value)),
                cancellationToken);
        if (hasApprovedQuestionInCycle)
        {
            return Result.Failure(
                "UNPROCESSABLE: Bộ đề chứa câu hỏi đã được phê duyệt trong bộ đề chính thức của đợt thi này. Vui lòng thay thế câu hỏi trước khi nộp duyệt.");
        }

        // (b) Loại trừ nội bộ GV khi nộp lại: chặn câu hỏi trùng với bộ đề thi thật khác của cùng tác giả trong cùng cấu trúc đề
        Guid? selfOwnerId = examSet.CreatedBy.HasValue
            ? examSet.CreatedBy.Value
            : (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) ? actor.Id : null);
        if (selfOwnerId.HasValue)
        {
            var hasSelfReusedQuestion = await context.ExamSetQuestions
                .AsNoTracking()
                .AnyAsync(esq =>
                    questionIds.Contains(esq.ExamQuestionId)
                    && esq.ExamSetId != examSet.Id
                    && esq.ExamSet.IsActive
                    && !esq.ExamSet.IsSampleSet
                    && esq.ExamSet.Status != ExamSetStatus.Rejected
                    && esq.ExamSet.CreatedBy == selfOwnerId.Value
                    && esq.ExamSet.StructureId == examSet.StructureId,
                    cancellationToken);
            if (hasSelfReusedQuestion)
            {
                return Result.Failure(
                    "UNPROCESSABLE: Bộ đề chứa câu hỏi trùng với bộ đề khác do bạn soạn trong cùng cấu trúc đề này.");
            }
        }

        var creatorId = examSet.CreatedBy ?? actor.Id;
        var yellowQuestionIds = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq =>
                questionIds.Contains(esq.ExamQuestionId)
                && esq.ExamSetId != examSet.Id
                && esq.ExamSet.StructureId == examSet.StructureId
                && esq.ExamSet.IsActive
                && !esq.ExamSet.IsSampleSet
                && esq.ExamSet.Status == ExamSetStatus.SubmittedForReview
                && (!esq.ExamSet.CreatedBy.HasValue || esq.ExamSet.CreatedBy.Value != creatorId))
            .Select(esq => esq.ExamQuestionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var totalQuestions = questionIds.Count;
        var yellowCount = yellowQuestionIds.Count;
        var yellowRatio = totalQuestions > 0 ? (decimal)yellowCount / totalQuestions : 0m;
        if (yellowRatio > 0.70m)
        {
            var ratioPercent = Math.Round(yellowRatio * 100m, 1, MidpointRounding.AwayFromZero);
            return Result.Failure(
                $"UNPROCESSABLE: Tỷ lệ câu hỏi trùng chờ duyệt với giảng viên khác là {ratioPercent:0.#}% ({yellowCount}/{totalQuestions}), vượt quá ngưỡng tối đa 70%. Vui lòng thay thế câu hỏi trước khi nộp duyệt.");
        }

        examSet.Status = ExamSetStatus.SubmittedForReview;
        examSet.SubmittedAt = DateTime.UtcNow;

        // Cascade any draft questions in this set to submitted_for_review
        foreach (var esq in examSet.ExamSetQuestions)
        {
            if (esq.ExamQuestion != null && esq.ExamQuestion.ApprovalStatus == ApprovalStatus.Draft)
            {
                esq.ExamQuestion.ApprovalStatus = ApprovalStatus.SubmittedForReview;
                esq.ExamQuestion.SubmittedBy ??= request.ActorUserId;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
