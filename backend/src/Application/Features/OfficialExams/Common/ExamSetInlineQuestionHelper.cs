using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.QuestionBank;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Common;

internal static class ExamSetInlineQuestionHelper
{
    private static readonly string[] AllowedDifficulties =
    [
        QuestionDifficulty.Easy,
        QuestionDifficulty.Medium,
        QuestionDifficulty.Hard
    ];

    public sealed record BuiltInlineQuestions(
        List<ExamQuestion> Questions,
        List<Rubric> Rubrics);

    public static async Task<Result<BuiltInlineQuestions>> BuildAsync(
        IApplicationDbContext context,
        Guid courseId,
        Guid structureId,
        Guid actorId,
        IReadOnlyList<CreateInlineExamQuestionDto> inlineQuestions,
        DateTime now,
        CancellationToken cancellationToken)
    {
        List<ExamQuestion> createdInlineQuestions = [];
        List<Rubric> createdInlineRubrics = [];

        if (inlineQuestions.Count == 0)
        {
            return Result<BuiltInlineQuestions>.Success(new BuiltInlineQuestions(createdInlineQuestions, createdInlineRubrics));
        }

        var inlineCloIds = inlineQuestions
            .Where(q => q.CloId.HasValue && q.CloId.Value != Guid.Empty)
            .Select(q => q.CloId!.Value)
            .Distinct()
            .ToList();

        var validCloIds = inlineCloIds.Count > 0
            ? (await context.CourseClos
                .AsNoTracking()
                .Where(c => inlineCloIds.Contains(c.Id) && c.CourseId == courseId && c.IsActive)
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
                .Where(r => referencedRubricIds.Contains(r.Id) && r.CourseId == courseId && r.IsActive)
                .ToDictionaryAsync(r => r.Id, cancellationToken)
            : new Dictionary<Guid, Rubric>();

        foreach (var item in inlineQuestions)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 300)
            {
                return Result<BuiltInlineQuestions>.Failure(
                    "UNPROCESSABLE: Tiêu đề câu hỏi tạo tay không được để trống và tối đa 300 ký tự.");
            }

            if (string.IsNullOrWhiteSpace(item.Content))
            {
                return Result<BuiltInlineQuestions>.Failure(
                    "UNPROCESSABLE: Nội dung câu hỏi tạo tay không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(item.SampleAnswer) || item.SampleAnswer.Trim().Length < 50)
            {
                return Result<BuiltInlineQuestions>.Failure(
                    "UNPROCESSABLE: Câu trả lời mẫu (Model Answer) của câu hỏi tạo tay bắt buộc phải cung cấp và từ 50 ký tự trở lên.");
            }

            var normalizedDifficulty = string.IsNullOrWhiteSpace(item.Difficulty)
                ? QuestionDifficulty.Medium
                : item.Difficulty.Trim().ToLowerInvariant();
            if (!AllowedDifficulties.Contains(normalizedDifficulty, StringComparer.Ordinal))
            {
                return Result<BuiltInlineQuestions>.Failure(
                    "UNPROCESSABLE: Độ khó câu hỏi tạo tay chỉ chấp nhận 'easy', 'medium' hoặc 'hard'.");
            }

            var canonicalBloom = BloomLevel.All.FirstOrDefault(b =>
                string.Equals(b, (item.BloomLevel ?? BloomLevel.Understand).Trim(), StringComparison.OrdinalIgnoreCase));
            if (canonicalBloom is null)
            {
                return Result<BuiltInlineQuestions>.Failure(
                    "UNPROCESSABLE: Mức độ nhận thức Bloom của câu hỏi tạo tay không hợp lệ.");
            }

            if (!string.IsNullOrWhiteSpace(item.ImageUrl) &&
                !QuestionImageUrlValidator.IsSafeImageUrl(item.ImageUrl.Trim()))
            {
                return Result<BuiltInlineQuestions>.Failure(
                    $"UNPROCESSABLE: {QuestionImageUrlValidator.InvalidImageUrlError}");
            }

            Guid? resolvedCloId = null;
            if (item.CloId.HasValue && item.CloId.Value != Guid.Empty)
            {
                if (!validCloIds.Contains(item.CloId.Value))
                {
                    return Result<BuiltInlineQuestions>.Failure(
                        "UNPROCESSABLE: CLO của câu hỏi tạo tay không tồn tại, đã ngừng hoạt động hoặc không thuộc môn học này.");
                }
                resolvedCloId = item.CloId.Value;
            }

            Guid resolvedRubricId;
            if (item.Rubric is not null)
            {
                if (item.Rubric.Criteria is null || item.Rubric.Criteria.Count < 2)
                {
                    return Result<BuiltInlineQuestions>.Failure(
                        "UNPROCESSABLE: Barem Rubric của câu hỏi tạo tay phải có tối thiểu 2 tiêu chí đánh giá con.");
                }

                if (item.Rubric.Criteria.Any(c =>
                    string.IsNullOrWhiteSpace(c.CriterionName)
                    || c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength
                    || c.MaxScore <= 0m))
                {
                    return Result<BuiltInlineQuestions>.Failure(
                        "UNPROCESSABLE: Mỗi tiêu chí trong Barem Rubric phải có tên hợp lệ (tối đa 200 ký tự) và điểm tối đa lớn hơn 0.");
                }

                if (item.Rubric.Criteria.Any(c => !RubricNormalization.HasValidScorePrecision(c.MaxScore)))
                {
                    return Result<BuiltInlineQuestions>.Failure(
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
                        return Result<BuiltInlineQuestions>.Failure(
                            $"UNPROCESSABLE: Mức độ Bloom '{trimmedCriterionBloom}' trên tiêu chí Rubric không hợp lệ.");
                    }

                    canonicalCriterionBlooms[i] = matchedCriterionBloom;
                }

                if (item.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                {
                    return Result<BuiltInlineQuestions>.Failure(
                        "UNPROCESSABLE: Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");
                }

                var rawRubricName = string.IsNullOrWhiteSpace(item.Rubric.Name)
                    ? $"Rubric - {item.Title.Trim()}"
                    : item.Rubric.Name.Trim();
                var safeRubricName = rawRubricName.Length > RubricNormalization.MaxNameLength
                    ? rawRubricName[..RubricNormalization.MaxNameLength]
                    : rawRubricName;

                var newRubric = new Rubric
                {
                    Id = Guid.NewGuid(),
                    CourseId = courseId,
                    Name = safeRubricName,
                    Description = item.Rubric.Description,
                    TotalMaxScore = 10.00m,
                    IsActive = true,
                    CreatedAt = now
                };

                for (var idx = 0; idx < item.Rubric.Criteria.Count; idx++)
                {
                    var c = item.Rubric.Criteria[idx];
                    var normalizedWeight = RubricNormalization.NormalizeWeight(c.Weight, c.MaxScore);

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
                    return Result<BuiltInlineQuestions>.Failure(
                        "UNPROCESSABLE: Barem Rubric của câu hỏi tạo tay không tồn tại hoặc không thuộc môn học này.");
                }

                if (existingRubric.Criteria is null
                    || existingRubric.Criteria.Count < 2
                    || existingRubric.Criteria.Any(c =>
                        string.IsNullOrWhiteSpace(c.CriterionName)
                        || c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength
                        || c.MaxScore <= 0m
                        || !RubricNormalization.HasValidScorePrecision(c.MaxScore))
                    || existingRubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                {
                    return Result<BuiltInlineQuestions>.Failure(
                        "UNPROCESSABLE: Barem Rubric tham chiếu phải có tối thiểu 2 tiêu chí hợp lệ và tổng điểm bằng chính xác 10.00 điểm.");
                }

                resolvedRubricId = existingRubric.Id;
            }
            else
            {
                return Result<BuiltInlineQuestions>.Failure(
                    "UNPROCESSABLE: Câu hỏi tạo tay phải cung cấp Barem Rubric hợp lệ (Rubric) hoặc mã RubricId sẵn có.");
            }

            var newQuestion = new ExamQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                CloId = resolvedCloId,
                StructureId = structureId,
                RubricId = resolvedRubricId,
                Title = item.Title.Trim(),
                Content = item.Content.Trim(),
                SampleAnswer = item.SampleAnswer.Trim(),
                KeyPoints = JsonSerializer.Serialize(item.KeyPoints ?? []),
                Difficulty = normalizedDifficulty,
                BloomLevel = canonicalBloom,
                Source = QuestionSource.Manual,
                ApprovalStatus = ApprovalStatus.Draft,
                SubmittedBy = actorId,
                ImageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim(),
                ImageDescription = string.IsNullOrWhiteSpace(item.ImageDescription) ? null : item.ImageDescription.Trim(),
                BankCurationStatus = BankCurationStatus.PendingCuration,
                IsActive = false,
                IsDeleted = false,
                CreatedAt = now
            };

            createdInlineQuestions.Add(newQuestion);
        }

        return Result<BuiltInlineQuestions>.Success(new BuiltInlineQuestions(createdInlineQuestions, createdInlineRubrics));
    }
}
