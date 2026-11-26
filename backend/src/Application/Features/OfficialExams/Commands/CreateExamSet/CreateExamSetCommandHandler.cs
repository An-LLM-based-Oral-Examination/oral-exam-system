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
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;

public sealed class CreateExamSetCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateExamSetCommand, Result<ExamSetDto>>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead
    ];

    public async Task<Result<ExamSetDto>> Handle(CreateExamSetCommand request, CancellationToken cancellationToken)
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
        if (actor is null ||
            !actor.IsActive ||
            !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var course = await context.Courses
            .AsNoTracking()
            .Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null || !course.IsActive)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.CourseNotFound);
        }

        var canAccessCourse = actor.Role switch
        {
            UserRole.DepartmentHead => AcademicCourseOwnership.IsOwnedBy(course, actor.Id),
            UserRole.Lecturer => await OfficialExamAuthorization.CanLecturerAccessCourseAsync(
                context, course.Id, actor.Id, cancellationToken),
            _ => false
        };
        if (!canAccessCourse)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var structure = await context.ExamStructures
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StructureId && s.CourseId == request.CourseId, cancellationToken);
        if (structure is null)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.StructureNotFound);
        }

        if (request.SessionId.HasValue)
        {
            var validSession = await context.OfficialExamSessions
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.SessionId.Value && s.CourseId == request.CourseId, cancellationToken);
            if (!validSession)
            {
                return Result<ExamSetDto>.Failure(
                    "UNPROCESSABLE: Kỳ thi được chọn không tồn tại hoặc không thuộc môn học này.");
            }
        }

        if (request.IsSampleSet)
        {
            if (!string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
            {
                return Result<ExamSetDto>.Failure(
                    "FORBIDDEN: Chỉ Trưởng Bộ Môn mới có quyền tạo Bộ Đề Mẫu (Sample Set).");
            }

            var hasActiveSampleSet = await context.ExamSets
                .AsNoTracking()
                .AnyAsync(es =>
                    es.StructureId == request.StructureId
                    && es.IsSampleSet
                    && es.IsActive
                    && es.Status != ExamSetStatus.Rejected,
                    cancellationToken);
            if (hasActiveSampleSet)
            {
                return Result<ExamSetDto>.Failure(
                    "CONFLICT: Cấu trúc đề này đã có bộ đề mẫu đang hoạt động. Mỗi cấu trúc đề chỉ được phép có tối đa 1 bộ đề mẫu.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.SetCode))
        {
            return Result<ExamSetDto>.Failure("VALIDATION: Mã bộ đề (SetCode) không được để trống.");
        }

        var existingQuestionIds = request.QuestionIds ?? [];
        var inlineQuestions = request.InlineManualQuestions ?? [];
        var totalRequestedQuestions = existingQuestionIds.Count + inlineQuestions.Count;
        if (totalRequestedQuestions == 0)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNoQuestions);
        }

        if (!request.IsSampleSet && structure.TotalQuestions > 0 && totalRequestedQuestions > structure.TotalQuestions)
        {
            return Result<ExamSetDto>.Failure(
                $"UNPROCESSABLE: Tổng số câu hỏi trong bộ đề ({totalRequestedQuestions}) không được vượt quá cấu trúc đề ({structure.TotalQuestions} câu).");
        }

        if (existingQuestionIds.Distinct().Count() != existingQuestionIds.Count)
        {
            return Result<ExamSetDto>.Failure("VALIDATION: Danh sách câu hỏi trong bộ đề không được chứa câu hỏi trùng lặp.");
        }

        var normalizedCode = request.SetCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length > 50)
        {
            return Result<ExamSetDto>.Failure("VALIDATION: Mã bộ đề (SetCode) không được vượt quá 50 ký tự.");
        }

        var codeExists = await context.ExamSets
            .AsNoTracking()
            .AnyAsync(es => es.CourseId == request.CourseId && es.IsActive && es.SetCode == normalizedCode, cancellationToken);
        if (codeExists)
        {
            return Result<ExamSetDto>.Failure($"CONFLICT: Mã bộ đề '{normalizedCode}' đã tồn tại trong môn học này.");
        }

        List<ExamQuestion> existingQuestions = [];
        if (existingQuestionIds.Count > 0)
        {
            existingQuestions = await context.ExamQuestions
                .AsNoTracking()
                .Where(q =>
                    existingQuestionIds.Contains(q.Id)
                    && q.CourseId == request.CourseId
                    && !q.IsDeleted
                    && ((q.ApprovalStatus == ApprovalStatus.Approved && q.IsActive)
                        || q.ApprovalStatus == ApprovalStatus.Draft
                        || q.ApprovalStatus == ApprovalStatus.SubmittedForReview))
                .ToListAsync(cancellationToken);

            if (existingQuestions.Count != existingQuestionIds.Count)
            {
                return Result<ExamSetDto>.Failure("UNPROCESSABLE: Một hoặc nhiều câu hỏi không tồn tại, không thuộc môn học này hoặc không ở trạng thái hợp lệ để đưa vào bộ đề.");
            }

            if (existingQuestions.Any(q => q.BankCurationStatus == BankCurationStatus.ExcludedFromBank))
            {
                return Result<ExamSetDto>.Failure(
                    "UNPROCESSABLE: Không thể đưa câu hỏi đã bị loại khỏi ngân hàng đề (EXCLUDED_FROM_BANK) vào bộ đề.");
            }

            if (!request.IsSampleSet)
            {
                if (existingQuestions.Any(q =>
                    q.BankCurationStatus == BankCurationStatus.PendingCuration
                    && q.SubmittedBy != actor.Id))
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Không thể sử dụng câu hỏi tạo tay riêng tư (PENDING_CURATION) của giảng viên khác trong bộ đề thi thật.");
                }

                if (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase)
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
                        && esq.ExamSet.CourseId == request.CourseId
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
                        && esq.ExamSet.IsActive
                        && !esq.ExamSet.IsSampleSet
                        && (esq.ExamSet.Status == ExamSetStatus.Official || esq.ExamSet.Status == ExamSetStatus.HandedOver)
                        && (esq.ExamSet.StructureId == request.StructureId
                            || (request.SessionId.HasValue && esq.ExamSet.SessionId == request.SessionId.Value)),
                        cancellationToken);
                if (usedInApprovedSet)
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Một hoặc nhiều câu hỏi đã được phê duyệt trong bộ đề chính thức của đợt thi này và không thể chọn lại.");
                }

                if (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
                {
                    var reusedBySameLecturer = await context.ExamSetQuestions
                        .AsNoTracking()
                        .AnyAsync(esq =>
                            existingQuestionIds.Contains(esq.ExamQuestionId)
                            && esq.ExamSet.StructureId == request.StructureId
                            && esq.ExamSet.IsActive
                            && !esq.ExamSet.IsSampleSet
                            && esq.ExamSet.CreatedBy == actor.Id
                            && esq.ExamSet.Status != ExamSetStatus.Rejected,
                            cancellationToken);
                    if (reusedBySameLecturer)
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: Câu hỏi đã được bạn chọn ở bộ đề trước trong cùng cấu trúc đề này.");
                    }
                }
            }
        }

        var now = DateTime.UtcNow;
        var builtInlineResult = await ExamSetInlineQuestionHelper.BuildAsync(
            context,
            request.CourseId,
            request.StructureId,
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

        if (!request.IsSampleSet && (structure.TotalQuestions <= 0 || orderedQuestions.Count == structure.TotalQuestions))
        {
            var bloomValidation = ExamSetStructureValidator.Validate(
                structure,
                orderedQuestions.Select(q => q.BloomLevel).ToList(),
                enforceExactTotal: false,
                enforceBloomDistribution: true);
            if (!bloomValidation.IsSuccess)
            {
                return Result<ExamSetDto>.Failure(bloomValidation.Error);
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

        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = request.StructureId,
            CourseId = request.CourseId,
            SessionId = request.SessionId,
            CreatedBy = request.ActorUserId != Guid.Empty ? request.ActorUserId : null,
            SetCode = normalizedCode,
            Status = ExamSetStatus.Draft,
            GeneratedAt = now,
            IsSampleSet = request.IsSampleSet,
            IsActive = true
        };

        var order = 1;
        foreach (var question in orderedQuestions)
        {
            examSet.ExamSetQuestions.Add(new ExamSetQuestion
            {
                Id = Guid.NewGuid(),
                ExamSetId = examSet.Id,
                ExamQuestionId = question.Id,
                OrderIndex = order++
            });
        }

        context.ExamSets.Add(examSet);
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = request.IsSampleSet ? "CREATE_SAMPLE_EXAM_SET" : "CREATE_EXAM_SET",
            EntityName = "exam_sets",
            EntityId = examSet.Id,
            NewValues = $"setCode={examSet.SetCode};isSampleSet={examSet.IsSampleSet};questionCount={orderedQuestions.Count};inlineManualCount={createdInlineQuestions.Count}",
            CreatedAt = now
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (request.IsSampleSet && IsActiveSampleSetUniqueConflict(ex))
        {
            return Result<ExamSetDto>.Failure(
                "CONFLICT: Cấu trúc đề thi này đã có Bộ Đề Mẫu (Sample Set) đang hoạt động. Mỗi cấu trúc đề chỉ được có 1 Bộ Đề Mẫu duy nhất.");
        }
        catch (DbUpdateException ex) when (IsActiveSetCodeUniqueConflict(ex))
        {
            return Result<ExamSetDto>.Failure(
                $"CONFLICT: Mã bộ đề '{normalizedCode}' đã tồn tại trong môn học này.");
        }

        var dto = new ExamSetDto
        {
            Id = examSet.Id,
            StructureId = examSet.StructureId,
            CourseId = examSet.CourseId,
            CourseCode = course.Code,
            SessionId = examSet.SessionId,
            CreatedBy = examSet.CreatedBy,
            SetCode = examSet.SetCode,
            Status = examSet.Status,
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

    private static bool IsActiveSampleSetUniqueConflict(DbUpdateException ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            var constraintName = current.GetType().GetProperty("ConstraintName")?.GetValue(current) as string;
            if (string.Equals(constraintName, "uq_exam_sets_active_sample_structure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(sqlState, "23505", StringComparison.Ordinal)
                && (current.Message.Contains("uq_exam_sets_active_sample_structure", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(constraintName)))
            {
                return true;
            }

            if (current.Message.Contains("uq_exam_sets_active_sample_structure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
