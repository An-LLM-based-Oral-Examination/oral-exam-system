using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamSet;

public sealed class UpdateExamSetCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateExamSetCommand, Result<ExamSetDto>>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead
    ];

    public async Task<Result<ExamSetDto>> Handle(UpdateExamSetCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        if (actor is null
            || !actor.IsActive
            || !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase)
            || !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .Include(es => es.Course)
                .ThenInclude(c => c!.AcademicProgram)
            .Include(es => es.Structure)
            .Include(es => es.ExamSetQuestions)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (examSet.Course is null || !examSet.Course.IsActive)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.CourseNotFound);
        }

        var isLecturer = string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase);
        var isDeptHead = string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase);

        if (examSet.IsSampleSet && !isDeptHead)
        {
            return Result<ExamSetDto>.Failure(
                "FORBIDDEN: Chỉ Trưởng Bộ Môn mới có quyền chỉnh sửa Bộ Đề Mẫu (Sample Set).");
        }

        var canAccessExamSet = actor.Role switch
        {
            UserRole.DepartmentHead => AcademicCourseOwnership.IsOwnedBy(examSet.Course, actor.Id),
            UserRole.Lecturer => examSet.CreatedBy == actor.Id
                && await OfficialExamAuthorization.CanLecturerAccessCourseAsync(
                    context, examSet.CourseId, actor.Id, cancellationToken),
            _ => false
        };
        if (!canAccessExamSet)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!examSet.IsActive)
        {
            return Result<ExamSetDto>.Failure(
                "CONFLICT: Bộ đề đã bị xóa mềm và không thể chỉnh sửa.");
        }

        if (examSet.Status != ExamSetStatus.Draft && examSet.Status != ExamSetStatus.Rejected)
        {
            return Result<ExamSetDto>.Failure(
                "CONFLICT: Chỉ có thể chỉnh sửa bộ đề đang ở trạng thái DRAFT hoặc REJECTED.");
        }

        var hasStudentTickets = await context.StudentExamTickets
            .AsNoTracking()
            .AnyAsync(t => t.ExamSetId == examSet.Id, cancellationToken);
        if (hasStudentTickets)
        {
            return Result<ExamSetDto>.Failure(
                "CONFLICT: Không thể chỉnh sửa bộ đề đã được gán cho vé thi sinh viên.");
        }

        var targetSetCode = examSet.SetCode;
        if (request.SetCode is not null)
        {
            if (string.IsNullOrWhiteSpace(request.SetCode))
            {
                return Result<ExamSetDto>.Failure("VALIDATION: Mã bộ đề (SetCode) không được để trống.");
            }

            var normalizedCode = request.SetCode.Trim().ToUpperInvariant();
            if (normalizedCode.Length > 50)
            {
                return Result<ExamSetDto>.Failure("VALIDATION: Mã bộ đề (SetCode) không được vượt quá 50 ký tự.");
            }

            if (!string.Equals(normalizedCode, examSet.SetCode, StringComparison.OrdinalIgnoreCase))
            {
                var codeExists = await context.ExamSets
                    .AsNoTracking()
                    .AnyAsync(es =>
                        es.Id != examSet.Id
                        && es.CourseId == examSet.CourseId
                        && es.IsActive
                        && es.SetCode == normalizedCode,
                        cancellationToken);
                if (codeExists)
                {
                    return Result<ExamSetDto>.Failure(
                        $"CONFLICT: Mã bộ đề '{normalizedCode}' đã tồn tại trong môn học này.");
                }
            }

            targetSetCode = normalizedCode;
        }

        List<Guid> existingQuestionIds;
        List<CreateInlineExamQuestionDto> inlineQuestions;
        if (request.QuestionIds is null && request.InlineManualQuestions is null)
        {
            existingQuestionIds = examSet.ExamSetQuestions
                .OrderBy(esq => esq.OrderIndex)
                .Select(esq => esq.ExamQuestionId)
                .ToList();
            inlineQuestions = [];
        }
        else
        {
            existingQuestionIds = request.QuestionIds ?? [];
            inlineQuestions = request.InlineManualQuestions ?? [];
        }

        var totalRequestedQuestions = existingQuestionIds.Count + inlineQuestions.Count;
        if (totalRequestedQuestions == 0)
        {
            return Result<ExamSetDto>.Failure("UNPROCESSABLE: Bộ đề phải có ít nhất một câu hỏi.");
        }

        if (existingQuestionIds.Distinct().Count() != existingQuestionIds.Count)
        {
            return Result<ExamSetDto>.Failure(
                "VALIDATION: Danh sách câu hỏi trong bộ đề không được chứa câu hỏi trùng lặp.");
        }

        if (!examSet.IsSampleSet
            && examSet.Structure is not null
            && examSet.Structure.TotalQuestions > 0
            && totalRequestedQuestions != examSet.Structure.TotalQuestions)
        {
            return Result<ExamSetDto>.Failure(
                $"UNPROCESSABLE: Tổng số câu hỏi trong bộ đề ({totalRequestedQuestions}) phải khớp chính xác với cấu trúc đề ({examSet.Structure.TotalQuestions} câu).");
        }

        var currentSetQuestionIds = examSet.ExamSetQuestions
            .Select(esq => esq.ExamQuestionId)
            .ToHashSet();
        var currentSetQuestionIdList = currentSetQuestionIds.ToList();

        List<ExamQuestion> existingQuestions = [];
        if (existingQuestionIds.Count > 0)
        {
            existingQuestions = await context.ExamQuestions
                .AsNoTracking()
                .Where(q =>
                    existingQuestionIds.Contains(q.Id)
                    && q.CourseId == examSet.CourseId
                    && !q.IsDeleted
                    && ((q.ApprovalStatus == ApprovalStatus.Approved && q.IsActive)
                        || q.ApprovalStatus == ApprovalStatus.Draft
                        || q.ApprovalStatus == ApprovalStatus.SubmittedForReview
                        || (q.ApprovalStatus == ApprovalStatus.NeedsRevision
                            && currentSetQuestionIdList.Contains(q.Id))))
                .ToListAsync(cancellationToken);

            if (existingQuestions.Count != existingQuestionIds.Count)
            {
                return Result<ExamSetDto>.Failure(
                    "UNPROCESSABLE: Một hoặc nhiều câu hỏi không tồn tại, không thuộc môn học này hoặc không ở trạng thái hợp lệ để đưa vào bộ đề.");
            }

            var hasUnauthorizedNeedsRevision = existingQuestions.Any(q =>
                q.ApprovalStatus == ApprovalStatus.NeedsRevision
                && (!currentSetQuestionIds.Contains(q.Id)
                    || (isLecturer
                        ? q.SubmittedBy != actor.Id
                        : (q.SubmittedBy != actor.Id
                           && (!examSet.CreatedBy.HasValue || q.SubmittedBy != examSet.CreatedBy.Value)))));
            if (hasUnauthorizedNeedsRevision)
            {
                return Result<ExamSetDto>.Failure(
                    "UNPROCESSABLE: Chỉ được giữ lại câu hỏi đang ở trạng thái NEEDS_REVISION thuộc chính bộ đề này và do đúng tác giả biên soạn.");
            }

            if (existingQuestions.Any(q => q.BankCurationStatus == BankCurationStatus.ExcludedFromBank))
            {
                return Result<ExamSetDto>.Failure(
                    "UNPROCESSABLE: Không thể đưa câu hỏi đã bị loại khỏi ngân hàng đề (EXCLUDED_FROM_BANK) vào bộ đề.");
            }

            if (!examSet.IsSampleSet)
            {
                if (existingQuestions.Any(q =>
                    q.BankCurationStatus == BankCurationStatus.PendingCuration
                    && q.SubmittedBy != actor.Id
                    && (!examSet.CreatedBy.HasValue || q.SubmittedBy != examSet.CreatedBy.Value || isLecturer)))
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Không thể sử dụng câu hỏi tạo tay riêng tư (PENDING_CURATION) của giảng viên khác trong bộ đề thi thật.");
                }

                if (isLecturer
                    && existingQuestions.Any(q =>
                        (q.ApprovalStatus == ApprovalStatus.Draft || q.ApprovalStatus == ApprovalStatus.SubmittedForReview)
                        && q.SubmittedBy.HasValue
                        && q.SubmittedBy.Value != actor.Id))
                {
                    return Result<ExamSetDto>.Failure(
                        "FORBIDDEN: Giảng viên không thể đưa câu hỏi nháp hoặc đang chờ duyệt của giảng viên khác vào bộ đề.");
                }

                var usedInSampleSet = await context.ExamSetQuestions
                    .AsNoTracking()
                    .AnyAsync(esq =>
                        existingQuestionIds.Contains(esq.ExamQuestionId)
                        && esq.ExamSetId != examSet.Id
                        && esq.ExamSet.CourseId == examSet.CourseId
                        && esq.ExamSet.IsSampleSet
                        && esq.ExamSet.IsActive
                        && esq.ExamSet.Status != ExamSetStatus.Rejected,
                        cancellationToken);
                if (usedInSampleSet)
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Không thể đưa câu hỏi thuộc Đề mẫu vào bộ đề thi thật.");
                }

                var usedInApprovedSet = await context.ExamSetQuestions
                    .AsNoTracking()
                    .AnyAsync(esq =>
                        existingQuestionIds.Contains(esq.ExamQuestionId)
                        && esq.ExamSetId != examSet.Id
                        && esq.ExamSet.IsActive
                        && !esq.ExamSet.IsSampleSet
                        && (esq.ExamSet.Status == ExamSetStatus.Official || esq.ExamSet.Status == ExamSetStatus.HandedOver)
                        && (esq.ExamSet.StructureId == examSet.StructureId
                            || (examSet.SessionId.HasValue && esq.ExamSet.SessionId == examSet.SessionId.Value)),
                        cancellationToken);
                if (usedInApprovedSet)
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Một hoặc nhiều câu hỏi đã được phê duyệt trong bộ đề chính thức của đợt thi này và không thể chọn lại.");
                }

                if (isLecturer)
                {
                    var reusedBySameLecturer = await context.ExamSetQuestions
                        .AsNoTracking()
                        .AnyAsync(esq =>
                            existingQuestionIds.Contains(esq.ExamQuestionId)
                            && esq.ExamSetId != examSet.Id
                            && esq.ExamSet.StructureId == examSet.StructureId
                            && esq.ExamSet.IsActive
                            && !esq.ExamSet.IsSampleSet
                            && esq.ExamSet.CreatedBy == actor.Id
                            && esq.ExamSet.Status != ExamSetStatus.Rejected,
                            cancellationToken);
                    if (reusedBySameLecturer)
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: Câu hỏi đã được bạn chọn ở bộ đề khác trong cùng cấu trúc đề này.");
                    }
                }
            }
        }

        var now = DateTime.UtcNow;
        var builtInlineResult = await ExamSetInlineQuestionHelper.BuildAsync(
            context,
            examSet.CourseId,
            examSet.StructureId,
            actor.Id,
            inlineQuestions,
            now,
            cancellationToken);
        if (!builtInlineResult.IsSuccess)
        {
            return Result<ExamSetDto>.Failure(builtInlineResult.Error);
        }

        var createdInlineQuestions = builtInlineResult.Value!.Questions;
        var createdInlineRubrics = builtInlineResult.Value.Rubrics;

        var orderedQuestions = existingQuestions
            .OrderBy(q => existingQuestionIds.IndexOf(q.Id))
            .Concat(createdInlineQuestions)
            .ToList();

        if (!examSet.IsSampleSet)
        {
            var structureValidation = ExamSetStructureValidator.Validate(
                examSet.Structure,
                orderedQuestions.Select(q => q.BloomLevel).ToList());
            if (!structureValidation.IsSuccess)
            {
                return Result<ExamSetDto>.Failure(structureValidation.Error);
            }
        }

        if (createdInlineRubrics.Count > 0)
        {
            context.Rubrics.AddRange(createdInlineRubrics);
        }

        if (createdInlineQuestions.Count > 0)
        {
            context.ExamQuestions.AddRange(createdInlineQuestions);
        }

        var oldValues = $"setCode={examSet.SetCode};status={examSet.Status};questionCount={examSet.ExamSetQuestions.Count}";

        var desiredQuestionIds = orderedQuestions.Select(q => q.Id).ToHashSet();
        var existingLinksByQuestionId = new Dictionary<Guid, ExamSetQuestion>();
        var linksToRemove = new List<ExamSetQuestion>();

        foreach (var existingLink in examSet.ExamSetQuestions.ToList())
        {
            if (desiredQuestionIds.Contains(existingLink.ExamQuestionId)
                && existingLinksByQuestionId.TryAdd(existingLink.ExamQuestionId, existingLink))
            {
                continue;
            }

            linksToRemove.Add(existingLink);
        }

        if (linksToRemove.Count > 0)
        {
            context.ExamSetQuestions.RemoveRange(linksToRemove);
            foreach (var removed in linksToRemove)
            {
                examSet.ExamSetQuestions.Remove(removed);
            }
        }

        var order = 1;
        foreach (var question in orderedQuestions)
        {
            if (existingLinksByQuestionId.TryGetValue(question.Id, out var keptLink))
            {
                keptLink.OrderIndex = order++;
            }
            else
            {
                var link = new ExamSetQuestion
                {
                    Id = Guid.NewGuid(),
                    ExamSetId = examSet.Id,
                    ExamQuestionId = question.Id,
                    OrderIndex = order++
                };
                examSet.ExamSetQuestions.Add(link);
                context.ExamSetQuestions.Add(link);
            }
        }

        examSet.SetCode = targetSetCode;
        if (examSet.Status == ExamSetStatus.Rejected)
        {
            examSet.Status = ExamSetStatus.Draft;
            examSet.SubmittedAt = null;

            // Khi GV sửa bộ đề REJECTED (ví dụ thay câu kho trùng nhưng giữ lại câu tạo tay inline),
            // tự động đưa các câu inline NEEDS_REVISION được giữ lại về DRAFT để SubmitExamSetForReview không bị kẹt 409.
            var retainedInlineNeedsRevisionIds = existingQuestions
                .Where(q =>
                    q.BankCurationStatus == BankCurationStatus.PendingCuration
                    && q.ApprovalStatus == ApprovalStatus.NeedsRevision)
                .Select(q => q.Id)
                .ToList();

            if (retainedInlineNeedsRevisionIds.Count > 0)
            {
                var trackedInlineQuestions = await context.ExamQuestions
                    .Where(q => retainedInlineNeedsRevisionIds.Contains(q.Id))
                    .ToListAsync(cancellationToken);

                foreach (var inlineQ in trackedInlineQuestions)
                {
                    inlineQ.ApprovalStatus = ApprovalStatus.Draft;
                }
            }
        }

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "UPDATE_EXAM_SET",
            EntityName = "exam_sets",
            EntityId = examSet.Id,
            OldValues = oldValues,
            NewValues = $"setCode={examSet.SetCode};status={examSet.Status};questionCount={orderedQuestions.Count};inlineManualCount={createdInlineQuestions.Count}",
            CreatedAt = now
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsActiveSetCodeUniqueConflict(ex))
        {
            return Result<ExamSetDto>.Failure(
                $"CONFLICT: Mã bộ đề '{targetSetCode}' đã tồn tại trong môn học này.");
        }

        var dto = new ExamSetDto
        {
            Id = examSet.Id,
            StructureId = examSet.StructureId,
            CourseId = examSet.CourseId,
            CourseCode = examSet.Course.Code,
            SessionId = examSet.SessionId,
            CreatedBy = examSet.CreatedBy,
            SetCode = examSet.SetCode,
            Status = examSet.Status,
            ApprovedBy = examSet.ApprovedBy,
            ReviewNotes = examSet.ReviewNotes,
            SubmittedAt = examSet.SubmittedAt,
            ApprovedAt = examSet.ApprovedAt,
            HandedOverAt = examSet.HandedOverAt,
            GeneratedAt = examSet.GeneratedAt,
            IsSampleSet = examSet.IsSampleSet,
            IsActive = examSet.IsActive,
            OverlapSeverity = "NONE",
            YellowOverlapRatio = 0m,
            Questions = [.. orderedQuestions
                .Select((q, idx) => new ExamSetQuestionItemDto
                {
                    QuestionId = q.Id,
                    OrderIndex = idx + 1,
                    Title = q.Title,
                    Content = q.Content,
                    BloomLevel = q.BloomLevel,
                    Difficulty = q.Difficulty,
                    IsDuplicate = false,
                    DuplicateInSets = [],
                    OverlapSeverity = "NONE",
                    BankCurationStatus = q.BankCurationStatus,
                    CloId = q.CloId
                })]
        };

        return Result<ExamSetDto>.Success(dto);
    }

    private static bool IsActiveSetCodeUniqueConflict(DbUpdateException ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            var constraintName = current.GetType().GetProperty("ConstraintName")?.GetValue(current) as string;
            if (string.Equals(constraintName, "uq_exam_sets_active_course_set_code", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(sqlState, "23505", StringComparison.Ordinal)
                && current.Message.Contains("uq_exam_sets_active_course_set_code", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current.Message.Contains("uq_exam_sets_active_course_set_code", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
