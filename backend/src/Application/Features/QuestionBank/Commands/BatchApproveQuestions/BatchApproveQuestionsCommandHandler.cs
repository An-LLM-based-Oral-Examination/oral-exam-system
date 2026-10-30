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

        if (approver == null || (approver.Role != "department_head" && approver.Role != "admin"))
        {
            return Result<BatchApproveQuestionsResponseDto>.Failure(
                "FORBIDDEN: Chỉ 'department_head' hoặc 'admin' mới có quyền phê duyệt lưu hàng loạt câu hỏi.");
        }

        var courseExists = await _context.Courses
            .AnyAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (!courseExists)
        {
            return Result<BatchApproveQuestionsResponseDto>.Failure(
                "NOT_FOUND: Môn học không tồn tại trong hệ thống.");
        }

        if (request.Questions == null || request.Questions.Count == 0)
        {
            return Result<BatchApproveQuestionsResponseDto>.Failure(
                "BAD_REQUEST: Danh sách câu hỏi phê duyệt không được rỗng.");
        }

        for (int i = 0; i < request.Questions.Count; i++)
        {
            var item = request.Questions[i];
            if (!string.IsNullOrWhiteSpace(item.ImageUrl) &&
                !string.Equals((item.Source ?? "manual").Trim(), "manual", StringComparison.OrdinalIgnoreCase))
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{item.Title}') có đính kèm hình ảnh nhưng không phải câu hỏi tạo thủ công (manual).");
            }

            var hasValidModelAnswer = !string.IsNullOrWhiteSpace(item.ModelAnswer) && item.ModelAnswer.Trim().Length >= 50;
            var hasValidCriteria = item.Rubric != null &&
                                   item.Rubric.Criteria != null &&
                                   item.Rubric.Criteria.Count >= 2 &&
                                   item.Rubric.Criteria.All(c => c.MaxScore > 0) &&
                                   item.Rubric.Criteria.Sum(c => c.MaxScore) == 10.00m;

            if (!hasValidModelAnswer || !hasValidCriteria)
            {
                return Result<BatchApproveQuestionsResponseDto>.Failure(
                    $"INVARIANT: Câu hỏi tại vị trí index [{i}] ('{item.Title}') vi phạm ràng buộc Barem 10.00đ hoặc Model Answer >= 50 ký tự.");
            }
        }

        var approvedResults = new List<ApprovedQuestionResultDto>();
        var now = DateTime.UtcNow;

        foreach (var item in request.Questions)
        {
            var rubric = new Rubric
            {
                CourseId = request.CourseId,
                Name = string.IsNullOrWhiteSpace(item.Rubric.Name) ? $"Barem - {item.Title}" : item.Rubric.Name.Trim(),
                Description = item.Rubric.Description,
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = now
            };

            int order = 1;
            foreach (var c in item.Rubric.Criteria)
            {
                var normalizedWeight = c.Weight > 1.00m
                    ? Math.Round(c.Weight / 100m, 4, MidpointRounding.AwayFromZero)
                    : c.Weight;
                if (normalizedWeight <= 0m || normalizedWeight > 1.00m)
                {
                    normalizedWeight = Math.Clamp(Math.Round(c.MaxScore / 10m, 4, MidpointRounding.AwayFromZero), 0.01m, 1.00m);
                }

                rubric.Criteria.Add(new RubricCriterion
                {
                    CriterionName = c.CriterionName,
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

            var keyPointsJson = JsonSerializer.Serialize(item.KeyPoints ?? new List<string>());
            var normalizedScope = (item.UsageScope ?? "exam").Trim().ToLowerInvariant();
            var normalizedSource = string.IsNullOrWhiteSpace(item.Source) ? "flm_api" : item.Source.Trim();
            var imageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim();
            var imageDescription = string.IsNullOrWhiteSpace(item.ImageDescription) ? null : item.ImageDescription.Trim();
            var sharedQuestionId = Guid.NewGuid();
            Guid primaryQuestionId = sharedQuestionId;

            if (normalizedScope == "practice")
            {
                var pq = new PracticeQuestion
                {
                    Id = sharedQuestionId,
                    CourseId = request.CourseId,
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
                    IsActive = true,
                    CreatedAt = now
                };
                _context.PracticeQuestions.Add(pq);
            }
            else
            {
                var eq = new ExamQuestion
                {
                    Id = sharedQuestionId,
                    CourseId = request.CourseId,
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
                    SubmittedBy = request.ApproverUserId,
                    ImageUrl = imageUrl,
                    ImageDescription = imageDescription,
                    IsActive = true,
                    CreatedAt = now
                };
                _context.ExamQuestions.Add(eq);

                if (normalizedScope is "shared" or "both" or "all")
                {
                    var pq = new PracticeQuestion
                    {
                        Id = sharedQuestionId,
                        CourseId = request.CourseId,
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
                        IsActive = true,
                        CreatedAt = now
                    };
                    _context.PracticeQuestions.Add(pq);
                }
            }

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
