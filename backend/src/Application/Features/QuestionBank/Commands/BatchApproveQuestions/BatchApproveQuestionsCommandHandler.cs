using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;

public sealed class BatchApproveQuestionsCommandHandler
    : IRequestHandler<BatchApproveQuestionsCommand, Result<BatchApproveQuestionsResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public BatchApproveQuestionsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BatchApproveQuestionsResponseDto>> Handle(
        BatchApproveQuestionsCommand request,
        CancellationToken cancellationToken)
    {
        var approver = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ApproverUserId && u.IsActive, cancellationToken);

        if (approver == null || approver.Role != UserRole.DepartmentHead)
        {
            return Result<BatchApproveQuestionsResponseDto>.Failure(
                "FORBIDDEN: Chỉ Trưởng Bộ Môn ('department_head') mới có quyền phê duyệt lưu hàng loạt câu hỏi.");
        }

        var courseExists = await _context.Courses
            .AnyAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (!courseExists)
        {
            return Result<BatchApproveQuestionsResponseDto>.Failure(
                "NOT_FOUND: Môn học không tồn tại trong hệ thống.");
        }
        if (!await QuestionCourseAccess.AllowedCourseIds(_context, approver.Role, approver.Id)
                .ContainsAsync(request.CourseId, cancellationToken))
            return Result<BatchApproveQuestionsResponseDto>.Failure("FORBIDDEN: Người duyệt không phụ trách môn học này.");

        if (request.Questions == null || request.Questions.Count == 0)
        {
            return Result<BatchApproveQuestionsResponseDto>.Failure(
                "BAD_REQUEST: Danh sách câu hỏi phê duyệt không được rỗng.");
        }

        var requestedCloIds = request.Questions
            .Where(q => q != null && q.CloId.HasValue && q.CloId.Value != Guid.Empty)
            .Select(q => q.CloId!.Value)
            .Distinct()
            .ToList();

        var validCloIds = requestedCloIds.Count > 0
            ? (await _context.CourseClos
                .AsNoTracking()
                .Where(c => requestedCloIds.Contains(c.Id) && c.CourseId == request.CourseId && c.IsActive)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : new HashSet<Guid>();

        var requestedStructureIds = request.Questions
            .Where(q => q != null && q.StructureId.HasValue && q.StructureId.Value != Guid.Empty)
            .Select(q => q.StructureId!.Value)
            .Distinct()
            .ToList();

        var validStructureIds = requestedStructureIds.Count > 0
            ? (await _context.ExamStructures
                .AsNoTracking()
                .Where(s => requestedStructureIds.Contains(s.Id) && s.CourseId == request.CourseId && s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : new HashSet<Guid>();

        var canonicalTitles = new string[request.Questions.Count];
        var canonicalContents = new string[request.Questions.Count];
        var canonicalDifficulties = new string[request.Questions.Count];
        var canonicalSources = new string[request.Questions.Count];
        var canonicalQuestionBlooms = new string[request.Questions.Count];
        var canonicalCriterionBlooms = new string?[request.Questions.Count][];

        for (int i = 0; i < request.Questions.Count; i++)
        {
            var item = request.Questions[i];
            if (item is null)
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] không được để trống (null).");
            }

            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 300)
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] có tiêu đề (Title) trống hoặc vượt quá 300 ký tự.");
            }

            var trimmedTitle = item.Title.Trim();

            if (string.IsNullOrWhiteSpace(item.Content))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') có nội dung câu hỏi (Content) không được để trống.");
            }

            var trimmedContent = item.Content.Trim();

            var normalizedSource = string.IsNullOrWhiteSpace(item.Source)
                ? QuestionSource.FlmApi
                : item.Source.Trim();
            if (normalizedSource.Length > 50)
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') có nguồn câu hỏi (Source) vượt quá 50 ký tự.");
            }

            if (!string.IsNullOrWhiteSpace(item.ImageUrl) &&
                !string.Equals(normalizedSource, QuestionSource.Manual, StringComparison.OrdinalIgnoreCase))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') có đính kèm hình ảnh nhưng không phải câu hỏi tạo thủ công (manual).");
            }

            if (!string.IsNullOrWhiteSpace(item.ImageUrl))
            {
                var trimmedImageUrl = item.ImageUrl.Trim();
                if (trimmedImageUrl.Length > 1000 || !QuestionImageUrlValidator.IsSafeImageUrl(trimmedImageUrl))
                {
                    return Result<BatchApproveQuestionsResponseDto>.Failure(
                        $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') có đường dẫn hình ảnh không hợp lệ hoặc vượt quá 1000 ký tự. Chỉ chấp nhận đường dẫn /uploads/questions/... hoặc HTTPS công khai an toàn.");
                }
            }

            if (item.CloId.HasValue && item.CloId.Value != Guid.Empty && !validCloIds.Contains(item.CloId.Value))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') tham chiếu CLO không tồn tại, đã ngừng hoạt động hoặc không thuộc môn học này.");
            }

            if (item.StructureId.HasValue && item.StructureId.Value != Guid.Empty && !validStructureIds.Contains(item.StructureId.Value))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') tham chiếu cấu trúc đề (StructureId) không tồn tại, đã ngừng hoạt động hoặc không thuộc môn học này.");
            }

            var normalizedItemScope = (item.UsageScope ?? "exam").Trim().ToLowerInvariant();
            if (normalizedItemScope is not ("practice" or "exam" or "official_exam"))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi '{trimmedTitle}' có phạm vi sử dụng '{item.UsageScope}' không hợp lệ. Chỉ chấp nhận 'practice' hoặc 'exam' tách biệt.");
            }

            var canonicalDifficulty = string.IsNullOrWhiteSpace(item.Difficulty)
                ? QuestionDifficulty.Medium
                : item.Difficulty.Trim().ToLowerInvariant();
            if (canonicalDifficulty is not (QuestionDifficulty.Easy or QuestionDifficulty.Medium or QuestionDifficulty.Hard))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') có độ khó '{item.Difficulty}' không hợp lệ. Chỉ chấp nhận: {QuestionDifficulty.Easy}, {QuestionDifficulty.Medium}, {QuestionDifficulty.Hard}.");
            }

            if (string.IsNullOrWhiteSpace(item.BloomLevel))
            {
                canonicalQuestionBlooms[i] = BloomLevel.Understand;
            }
            else
            {
                var trimmedQuestionBloom = item.BloomLevel.Trim();
                var matchedQuestionBloom = BloomLevel.All.FirstOrDefault(b =>
                    string.Equals(b, trimmedQuestionBloom, StringComparison.OrdinalIgnoreCase));
                if (matchedQuestionBloom is null || trimmedQuestionBloom.Length > 20)
                {
                    return Result<BatchApproveQuestionsResponseDto>.Failure(
                        $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') có mức độ nhận thức Bloom '{trimmedQuestionBloom}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", BloomLevel.All)}.");
                }

                canonicalQuestionBlooms[i] = matchedQuestionBloom;
            }

            var hasValidModelAnswer = !string.IsNullOrWhiteSpace(item.ModelAnswer) && item.ModelAnswer.Trim().Length >= 50;
            var hasValidCriteria = item.Rubric != null &&
                                   item.Rubric.Criteria != null &&
                                   item.Rubric.Criteria.Count >= 2 &&
                                   item.Rubric.Criteria.All(c =>
                                       c != null &&
                                       !string.IsNullOrWhiteSpace(c.CriterionName) &&
                                       c.CriterionName.Trim().Length <= RubricNormalization.MaxNameLength &&
                                       c.MaxScore > 0 &&
                                       RubricNormalization.HasValidScorePrecision(c.MaxScore)) &&
                                   item.Rubric.Criteria.Sum(c => c.MaxScore) == 10.00m;

            if (!hasValidModelAnswer || !hasValidCriteria)
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') vi phạm ràng buộc Barem 10.00đ (tối đa 2 chữ số thập phân, tên tiêu chí hợp lệ <= 200 ký tự) hoặc Model Answer >= 50 ký tự.");
            }

            var rubricCriteria = item.Rubric!.Criteria!;
            var critBlooms = new string?[rubricCriteria.Count];
            for (int cIdx = 0; cIdx < rubricCriteria.Count; cIdx++)
            {
                var rawCritBloom = rubricCriteria[cIdx].BloomLevel;
                if (string.IsNullOrWhiteSpace(rawCritBloom))
                {
                    critBlooms[cIdx] = null;
                    continue;
                }

                var trimmedCritBloom = rawCritBloom.Trim();
                var matchedCritBloom = BloomLevel.All.FirstOrDefault(b =>
                    string.Equals(b, trimmedCritBloom, StringComparison.OrdinalIgnoreCase));
                if (matchedCritBloom is null || trimmedCritBloom.Length > 20)
                {
                    return Result<BatchApproveQuestionsResponseDto>.Failure(
                        $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{trimmedTitle}') có tiêu chí rubric '{rubricCriteria[cIdx].CriterionName}' với mức độ nhận thức Bloom '{trimmedCritBloom}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", BloomLevel.All)}.");
                }

                critBlooms[cIdx] = matchedCritBloom;
            }

            canonicalTitles[i] = trimmedTitle;
            canonicalContents[i] = trimmedContent;
            canonicalDifficulties[i] = canonicalDifficulty;
            canonicalSources[i] = normalizedSource;
            canonicalCriterionBlooms[i] = critBlooms;
        }

        var approvedResults = new List<ApprovedQuestionResultDto>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < request.Questions.Count; i++)
        {
            var item = request.Questions[i];
            var rawRubricName = string.IsNullOrWhiteSpace(item.Rubric.Name)
                ? $"Barem - {canonicalTitles[i]}"
                : item.Rubric.Name.Trim();
            var safeRubricName = rawRubricName.Length > RubricNormalization.MaxNameLength
                ? rawRubricName[..RubricNormalization.MaxNameLength]
                : rawRubricName;

            var rubric = new Rubric
            {
                CourseId = request.CourseId,
                Name = safeRubricName,
                Description = item.Rubric.Description,
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = now
            };

            for (int cIdx = 0; cIdx < item.Rubric.Criteria.Count; cIdx++)
            {
                var c = item.Rubric.Criteria[cIdx];
                var normalizedWeight = RubricNormalization.NormalizeWeight(c.Weight, c.MaxScore);

                rubric.Criteria.Add(new RubricCriterion
                {
                    CriterionName = c.CriterionName.Trim(),
                    Description = c.Description,
                    MaxScore = c.MaxScore,
                    Weight = normalizedWeight,
                    BloomLevel = canonicalCriterionBlooms[i][cIdx],
                    OrderIndex = c.OrderIndex > 0 ? c.OrderIndex : cIdx + 1,
                    CreatedAt = now
                });
            }

            _context.Rubrics.Add(rubric);

            var keyPointsJson = JsonSerializer.Deserialize<List<string>>(
                JsonSerializer.Serialize(item.KeyPoints ?? new List<string>())) is var kpList
                ? JsonSerializer.Serialize(kpList)
                : "[]";
            var normalizedScope = (item.UsageScope ?? "exam").Trim().ToLowerInvariant();
            var normalizedSource = canonicalSources[i];
            var imageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim();
            var imageDescription = string.IsNullOrWhiteSpace(item.ImageDescription) ? null : item.ImageDescription.Trim();
            var questionId = Guid.NewGuid();
            Guid primaryQuestionId = questionId;
            var savedQuestionIds = new List<Guid>();
            bool shouldSaveToPractice = normalizedScope == "practice";
            bool shouldSaveToExam = !shouldSaveToPractice;

            if (shouldSaveToExam)
            {
                var eq = new ExamQuestion
                {
                    Id = questionId,
                    CourseId = request.CourseId,
                    CloId = item.CloId,
                    StructureId = item.StructureId,
                    RubricId = rubric.Id,
                    Rubric = rubric,
                    Title = canonicalTitles[i],
                    Content = canonicalContents[i],
                    SampleAnswer = item.ModelAnswer.Trim(),
                    KeyPoints = keyPointsJson,
                    Difficulty = canonicalDifficulties[i],
                    BloomLevel = canonicalQuestionBlooms[i],
                    Source = normalizedSource,
                    ApprovalStatus = ApprovalStatus.Approved,
                    ApprovedBy = request.ApproverUserId,
                    ApprovedAt = now,
                    SubmittedBy = request.ApproverUserId,
                    ImageUrl = imageUrl,
                    ImageDescription = imageDescription,
                    BankCurationStatus = item.IsPaperPrivate
                        ? BankCurationStatus.PendingCuration
                        : BankCurationStatus.BankReady,
                    IsActive = !item.IsPaperPrivate,
                    CreatedAt = now
                };
                _context.ExamQuestions.Add(eq);
                savedQuestionIds.Add(eq.Id);
            }

            if (shouldSaveToPractice)
            {
                var practiceQuestion = new PracticeQuestion
                {
                    Id = questionId,
                    CourseId = request.CourseId,
                    CloId = item.CloId,
                    RubricId = rubric.Id,
                    Rubric = rubric,
                    Title = canonicalTitles[i],
                    Content = canonicalContents[i],
                    SampleAnswer = item.ModelAnswer.Trim(),
                    KeyPoints = keyPointsJson,
                    Difficulty = canonicalDifficulties[i],
                    BloomLevel = canonicalQuestionBlooms[i],
                    HasFollowUp = item.HasFollowUp,
                    FollowUpPrompt = item.FollowUpPrompt,
                    ImageUrl = imageUrl,
                    ImageDescription = imageDescription,
                    Source = normalizedSource,
                    ApprovalStatus = ApprovalStatus.Approved,
                    ApprovedBy = request.ApproverUserId,
                    ApprovedAt = now,
                    SubmittedBy = request.ApproverUserId,
                    IsActive = true,
                    CreatedAt = now
                };
                _context.PracticeQuestions.Add(practiceQuestion);
                if (!shouldSaveToExam)
                {
                    savedQuestionIds.Add(practiceQuestion.Id);
                }
            }

            primaryQuestionId = savedQuestionIds.FirstOrDefault(questionId);

            approvedResults.Add(new ApprovedQuestionResultDto(
                primaryQuestionId,
                rubric.Id,
                canonicalTitles[i],
                normalizedScope,
                normalizedSource,
                ApprovalStatus.Approved,
                request.ApproverUserId,
                now
            ));
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<BatchApproveQuestionsResponseDto>.Success(new BatchApproveQuestionsResponseDto(
            request.CourseId,
            approvedResults.Count,
            approvedResults
        ));
    }
}
