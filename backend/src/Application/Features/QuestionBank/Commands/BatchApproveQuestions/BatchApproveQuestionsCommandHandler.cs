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
            .Where(q => q.CloId.HasValue && q.CloId.Value != Guid.Empty)
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
            .Where(q => q.StructureId.HasValue && q.StructureId.Value != Guid.Empty)
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

        for (int i = 0; i < request.Questions.Count; i++)
        {
            var item = request.Questions[i];
            if (!string.IsNullOrWhiteSpace(item.ImageUrl) &&
                !string.Equals((item.Source ?? "manual").Trim(), "manual", StringComparison.OrdinalIgnoreCase))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{item.Title}') có đính kèm hình ảnh nhưng không phải câu hỏi tạo thủ công (manual).");
            }

            if (!string.IsNullOrWhiteSpace(item.ImageUrl) &&
                !QuestionImageUrlValidator.IsSafeImageUrl(item.ImageUrl.Trim()))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{item.Title}') có đường dẫn hình ảnh không hợp lệ. Chỉ chấp nhận đường dẫn /uploads/questions/... hoặc HTTPS công khai an toàn.");
            }

            if (item.CloId.HasValue && item.CloId.Value != Guid.Empty && !validCloIds.Contains(item.CloId.Value))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{item.Title}') tham chiếu CLO không tồn tại, đã ngừng hoạt động hoặc không thuộc môn học này.");
            }

            if (item.StructureId.HasValue && item.StructureId.Value != Guid.Empty && !validStructureIds.Contains(item.StructureId.Value))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{item.Title}') tham chiếu cấu trúc đề (StructureId) không tồn tại, đã ngừng hoạt động hoặc không thuộc môn học này.");
            }

            var hasValidModelAnswer = !string.IsNullOrWhiteSpace(item.ModelAnswer) && item.ModelAnswer.Trim().Length >= 50;
            var hasValidCriteria = item.Rubric != null &&
                                   item.Rubric.Criteria != null &&
                                   item.Rubric.Criteria.Count >= 2 &&
                                   item.Rubric.Criteria.All(c =>
                                       !string.IsNullOrWhiteSpace(c.CriterionName) &&
                                       c.CriterionName.Trim().Length <= RubricNormalization.MaxNameLength &&
                                       c.MaxScore > 0 &&
                                       RubricNormalization.HasValidScorePrecision(c.MaxScore)) &&
                                   item.Rubric.Criteria.Sum(c => c.MaxScore) == 10.00m;

            if (!hasValidModelAnswer || !hasValidCriteria)
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{item.Title}') vi phạm ràng buộc Barem 10.00đ (tối đa 2 chữ số thập phân, tên tiêu chí hợp lệ <= 200 ký tự) hoặc Model Answer >= 50 ký tự.");
            }
        }

        var approvedResults = new List<ApprovedQuestionResultDto>();
        var now = DateTime.UtcNow;

        foreach (var item in request.Questions)
        {
            var rawRubricName = string.IsNullOrWhiteSpace(item.Rubric.Name)
                ? $"Barem - {item.Title.Trim()}"
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

            int order = 1;
            foreach (var c in item.Rubric.Criteria)
            {
                var normalizedWeight = RubricNormalization.NormalizeWeight(c.Weight, c.MaxScore);

                rubric.Criteria.Add(new RubricCriterion
                {
                    CriterionName = c.CriterionName.Trim(),
                    Description = c.Description,
                    MaxScore = c.MaxScore,
                    Weight = normalizedWeight,
                    BloomLevel = c.BloomLevel,
                    OrderIndex = c.OrderIndex > 0 ? c.OrderIndex : order,
                    CreatedAt = now
                });
                order++;
            }

            _context.Rubrics.Add(rubric);

            var keyPointsJson = JsonSerializer.Deserialize<List<string>>(
                JsonSerializer.Serialize(item.KeyPoints ?? new List<string>())) is var kpList
                ? JsonSerializer.Serialize(kpList)
                : "[]";
            var normalizedScope = (item.UsageScope ?? "exam").Trim().ToLowerInvariant();
            if (normalizedScope is not ("practice" or "exam" or "official_exam"))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi '{item.Title}' có phạm vi sử dụng '{item.UsageScope}' không hợp lệ. Chỉ chấp nhận 'practice' hoặc 'exam' tách biệt.");
            }

            var normalizedSource = string.IsNullOrWhiteSpace(item.Source) ? "flm_api" : item.Source.Trim();
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
                    Title = item.Title.Trim(),
                    Content = item.Content.Trim(),
                    SampleAnswer = item.ModelAnswer.Trim(),
                    KeyPoints = keyPointsJson,
                    Difficulty = string.IsNullOrWhiteSpace(item.Difficulty) ? "medium" : item.Difficulty.Trim().ToLowerInvariant(),
                    BloomLevel = string.IsNullOrWhiteSpace(item.BloomLevel) ? "Understand" : item.BloomLevel.Trim(),
                    Source = normalizedSource,
                    ApprovalStatus = ApprovalStatus.Approved,
                    ApprovedBy = request.ApproverUserId,
                    ApprovedAt = now,
                    SubmittedBy = request.ApproverUserId,
                    ImageUrl = imageUrl,
                    ImageDescription = imageDescription,
                    IsActive = true,
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
                    Title = item.Title.Trim(),
                    Content = item.Content.Trim(),
                    SampleAnswer = item.ModelAnswer.Trim(),
                    KeyPoints = keyPointsJson,
                    Difficulty = string.IsNullOrWhiteSpace(item.Difficulty) ? "medium" : item.Difficulty.Trim().ToLowerInvariant(),
                    BloomLevel = string.IsNullOrWhiteSpace(item.BloomLevel) ? "Understand" : item.BloomLevel.Trim(),
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
                item.Title.Trim(),
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
