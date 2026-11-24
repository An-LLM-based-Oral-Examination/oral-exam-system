using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
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

    private static readonly string[] AllowedDifficulties =
    [
        QuestionDifficulty.Easy,
        QuestionDifficulty.Medium,
        QuestionDifficulty.Hard
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
        if (existingQuestionIds.Count + inlineQuestions.Count == 0)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNoQuestions);
        }

        if (existingQuestionIds.Distinct().Count() != existingQuestionIds.Count)
        {
            return Result<ExamSetDto>.Failure("VALIDATION: Danh sách câu hỏi trong bộ đề không được chứa câu hỏi trùng lặp.");
        }

        var normalizedCode = request.SetCode.Trim().ToUpperInvariant();
        var codeExists = await context.ExamSets
            .AsNoTracking()
            .AnyAsync(es => es.CourseId == request.CourseId && es.SetCode == normalizedCode, cancellationToken);
        if (codeExists)
        {
            return Result<ExamSetDto>.Failure($"Mã bộ đề '{normalizedCode}' đã tồn tại trong môn học này.");
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
                return Result<ExamSetDto>.Failure("Một hoặc nhiều câu hỏi không tồn tại, không thuộc môn học này hoặc không ở trạng thái hợp lệ để đưa vào bộ đề.");
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
        List<ExamQuestion> createdInlineQuestions = [];
        List<Rubric> createdInlineRubrics = [];

        if (inlineQuestions.Count > 0)
        {
            var inlineCloIds = inlineQuestions
                .Where(q => q.CloId.HasValue && q.CloId.Value != Guid.Empty)
                .Select(q => q.CloId!.Value)
                .Distinct()
                .ToList();

            var validCloIds = inlineCloIds.Count > 0
                ? (await context.CourseClos
                    .AsNoTracking()
                    .Where(c => inlineCloIds.Contains(c.Id) && c.CourseId == request.CourseId && c.IsActive)
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken))
                    .ToHashSet()
                : new HashSet<Guid>();

            var referencedRubricIds = inlineQuestions
                .Where(q => q.Rubric is null && q.RubricId.HasValue && q.RubricId.Value != Guid.Empty)
                .Select(q => q.RubricId!.Value)
                .Distinct()
                .ToList();

            var existingRubricsById = referencedRubricIds.Count > 0
                ? await context.Rubrics
                    .AsNoTracking()
                    .Include(r => r.Criteria)
                    .Where(r => referencedRubricIds.Contains(r.Id) && r.CourseId == request.CourseId && r.IsActive)
                    .ToDictionaryAsync(r => r.Id, cancellationToken)
                : new Dictionary<Guid, Rubric>();

            foreach (var item in inlineQuestions)
            {
                if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 300)
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Tiêu đề câu hỏi tạo tay không được để trống và tối đa 300 ký tự.");
                }

                if (string.IsNullOrWhiteSpace(item.Content))
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Nội dung câu hỏi tạo tay không được để trống.");
                }

                if (string.IsNullOrWhiteSpace(item.SampleAnswer) || item.SampleAnswer.Trim().Length < 50)
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Câu trả lời mẫu (Model Answer) của câu hỏi tạo tay bắt buộc phải cung cấp và từ 50 ký tự trở lên.");
                }

                var normalizedDifficulty = string.IsNullOrWhiteSpace(item.Difficulty)
                    ? QuestionDifficulty.Medium
                    : item.Difficulty.Trim().ToLowerInvariant();
                if (!AllowedDifficulties.Contains(normalizedDifficulty, StringComparer.Ordinal))
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Độ khó câu hỏi tạo tay chỉ chấp nhận 'easy', 'medium' hoặc 'hard'.");
                }

                var canonicalBloom = BloomLevel.All.FirstOrDefault(b =>
                    string.Equals(b, (item.BloomLevel ?? BloomLevel.Understand).Trim(), StringComparison.OrdinalIgnoreCase));
                if (canonicalBloom is null)
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Mức độ nhận thức Bloom của câu hỏi tạo tay không hợp lệ.");
                }

                Guid? resolvedCloId = null;
                if (item.CloId.HasValue && item.CloId.Value != Guid.Empty)
                {
                    if (!validCloIds.Contains(item.CloId.Value))
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: CLO của câu hỏi tạo tay không tồn tại, đã ngừng hoạt động hoặc không thuộc môn học này.");
                    }
                    resolvedCloId = item.CloId.Value;
                }

                Guid resolvedRubricId;
                if (item.Rubric is not null)
                {
                    if (item.Rubric.Criteria is null || item.Rubric.Criteria.Count < 2)
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: Barem Rubric của câu hỏi tạo tay phải có tối thiểu 2 tiêu chí đánh giá con.");
                    }

                    if (item.Rubric.Criteria.Any(c =>
                        string.IsNullOrWhiteSpace(c.CriterionName)
                        || c.CriterionName.Trim().Length > 200
                        || c.MaxScore <= 0m))
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: Mỗi tiêu chí trong Barem Rubric phải có tên hợp lệ (tối đa 200 ký tự) và điểm tối đa lớn hơn 0.");
                    }

                    if (item.Rubric.Criteria.Any(c => decimal.Round(c.MaxScore, 2) != c.MaxScore))
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: Điểm tối đa của từng tiêu chí trong Barem Rubric chỉ chấp nhận tối đa 2 chữ số thập phân (numeric(4,2)).");
                    }

                    var canonicalCriterionBlooms = new string?[item.Rubric.Criteria.Count];
                    for (var i = 0; i < item.Rubric.Criteria.Count; i++)
                    {
                        var rawCriterionBloom = item.Rubric.Criteria[i].BloomLevel;
                        if (string.IsNullOrWhiteSpace(rawCriterionBloom))
                        {
                            canonicalCriterionBlooms[i] = null;
                            continue;
                        }

                        var trimmedCriterionBloom = rawCriterionBloom.Trim();
                        var matchedCriterionBloom = BloomLevel.All.FirstOrDefault(b =>
                            string.Equals(b, trimmedCriterionBloom, StringComparison.OrdinalIgnoreCase));
                        if (matchedCriterionBloom is null || trimmedCriterionBloom.Length > 20)
                        {
                            return Result<ExamSetDto>.Failure(
                                $"UNPROCESSABLE: Mức độ Bloom '{trimmedCriterionBloom}' trên tiêu chí Rubric không hợp lệ.");
                        }

                        canonicalCriterionBlooms[i] = matchedCriterionBloom;
                    }

                    if (item.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");
                    }

                    var rawRubricName = string.IsNullOrWhiteSpace(item.Rubric.Name)
                        ? $"Rubric - {item.Title.Trim()}"
                        : item.Rubric.Name.Trim();
                    var safeRubricName = rawRubricName.Length > 200 ? rawRubricName[..200] : rawRubricName;

                    var newRubric = new Rubric
                    {
                        Id = Guid.NewGuid(),
                        CourseId = request.CourseId,
                        Name = safeRubricName,
                        Description = item.Rubric.Description,
                        TotalMaxScore = 10.00m,
                        IsActive = true,
                        CreatedAt = now
                    };

                    for (var idx = 0; idx < item.Rubric.Criteria.Count; idx++)
                    {
                        var c = item.Rubric.Criteria[idx];
                        var normalizedWeight = c.Weight > 1.00m
                            ? Math.Round(c.Weight / 100m, 2, MidpointRounding.AwayFromZero)
                            : c.Weight;
                        if (normalizedWeight <= 0m || normalizedWeight > 1.00m)
                        {
                            normalizedWeight = Math.Clamp(Math.Round(c.MaxScore / 10m, 2, MidpointRounding.AwayFromZero), 0.01m, 1.00m);
                        }

                        newRubric.Criteria.Add(new RubricCriterion
                        {
                            Id = Guid.NewGuid(),
                            RubricId = newRubric.Id,
                            CriterionName = c.CriterionName.Trim(),
                            Description = c.Description,
                            MaxScore = c.MaxScore,
                            Weight = normalizedWeight,
                            BloomLevel = canonicalCriterionBlooms[idx],
                            OrderIndex = c.OrderIndex > 0 ? c.OrderIndex : idx + 1,
                            CreatedAt = now
                        });
                    }

                    createdInlineRubrics.Add(newRubric);
                    resolvedRubricId = newRubric.Id;
                }
                else if (item.RubricId.HasValue && item.RubricId.Value != Guid.Empty)
                {
                    if (!existingRubricsById.TryGetValue(item.RubricId.Value, out var existingRubric))
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: Barem Rubric của câu hỏi tạo tay không tồn tại hoặc không thuộc môn học này.");
                    }

                    if (existingRubric.Criteria is null || existingRubric.Criteria.Count < 2 || existingRubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                    {
                        return Result<ExamSetDto>.Failure(
                            "UNPROCESSABLE: Barem Rubric tham chiếu phải có tối thiểu 2 tiêu chí và tổng điểm bằng chính xác 10.00 điểm.");
                    }

                    resolvedRubricId = existingRubric.Id;
                }
                else
                {
                    return Result<ExamSetDto>.Failure(
                        "UNPROCESSABLE: Câu hỏi tạo tay phải cung cấp Barem Rubric hợp lệ (Rubric) hoặc mã RubricId sẵn có.");
                }

                var newQuestion = new ExamQuestion
                {
                    Id = Guid.NewGuid(),
                    CourseId = request.CourseId,
                    CloId = resolvedCloId,
                    StructureId = request.StructureId,
                    RubricId = resolvedRubricId,
                    Title = item.Title.Trim(),
                    Content = item.Content.Trim(),
                    SampleAnswer = item.SampleAnswer.Trim(),
                    KeyPoints = JsonSerializer.Serialize(item.KeyPoints ?? []),
                    Difficulty = normalizedDifficulty,
                    BloomLevel = canonicalBloom,
                    Source = QuestionSource.Manual,
                    ApprovalStatus = ApprovalStatus.Draft,
                    SubmittedBy = actor.Id,
                    ImageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim(),
                    ImageDescription = string.IsNullOrWhiteSpace(item.ImageDescription) ? null : item.ImageDescription.Trim(),
                    BankCurationStatus = BankCurationStatus.PendingCuration,
                    IsActive = false,
                    IsDeleted = false,
                    CreatedAt = now
                };

                createdInlineQuestions.Add(newQuestion);
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

        var orderedQuestions = existingQuestions
            .OrderBy(q => existingQuestionIds.IndexOf(q.Id))
            .Concat(createdInlineQuestions)
            .ToList();

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
}
