using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;

public sealed class CreateDraftQuestionCommandHandler : IRequestHandler<CreateDraftQuestionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateDraftQuestionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateDraftQuestionCommand request, CancellationToken cancellationToken)
    {
        var courseExists = await _context.Courses.AnyAsync(c => c.Id == request.CourseId, cancellationToken);
        if (!courseExists)
            return Result<Guid>.Failure("Môn học không tồn tại.");

        var lecturer = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.LecturerId, cancellationToken);
        if (lecturer == null || (lecturer.Role != "lecturer" && lecturer.Role != "department_head" && lecturer.Role != "admin"))
            return Result<Guid>.Failure("Người dùng không có quyền biên soạn câu hỏi.");

        var normalizedSource = (request.Source ?? "manual").Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(request.ImageUrl) && normalizedSource != "manual")
        {
            return Result<Guid>.Failure("Chỉ câu hỏi do giảng viên tạo thủ công (manual) mới được phép đính kèm hình ảnh.");
        }

        // Kiểm tra SampleAnswer >= 50 nếu có
        if (!string.IsNullOrEmpty(request.SampleAnswer) && request.SampleAnswer.Trim().Length < 50)
            return Result<Guid>.Failure("Câu trả lời mẫu (Model Answer) khi cung cấp bắt buộc phải từ 50 ký tự trở lên.");

        Rubric rubric;

        if (request.RubricId.HasValue && request.RubricId.Value != Guid.Empty && request.Rubric == null)
        {
            var existingRubric = await _context.Rubrics
                .Include(r => r.Criteria)
                .FirstOrDefaultAsync(r => r.Id == request.RubricId.Value && r.CourseId == request.CourseId && r.IsActive, cancellationToken);

            if (existingRubric == null)
                return Result<Guid>.Failure("Barem Rubric không tồn tại hoặc không thuộc môn học này.");

            if (existingRubric.Criteria == null || existingRubric.Criteria.Count < 2)
                return Result<Guid>.Failure("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");

            if (existingRubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                return Result<Guid>.Failure("Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");

            rubric = existingRubric;
        }
        else
        {
            // Kiểm tra Barem 10.00
            if (request.Rubric?.Criteria == null || request.Rubric.Criteria.Count < 2)
                return Result<Guid>.Failure("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");

            if (request.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                return Result<Guid>.Failure("Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");

            if (request.Rubric.Criteria.Any(c => c.MaxScore <= 0))
                return Result<Guid>.Failure("Điểm tối đa của từng tiêu chí con trong Barem Rubric bắt buộc phải lớn hơn 0.");

            // 1. Tạo Rubric mới
            rubric = new Rubric
            {
                CourseId = request.CourseId,
                Name = request.Rubric.Name,
                Description = request.Rubric.Description,
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            int order = 1;
            foreach (var c in request.Rubric.Criteria)
            {
                rubric.Criteria.Add(new RubricCriterion
                {
                    CriterionName = c.CriterionName,
                    Description = c.Description,
                    MaxScore = c.MaxScore,
                    Weight = c.Weight,
                    BloomLevel = c.BloomLevel,
                    OrderIndex = order++,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.Rubrics.Add(rubric);
        }

        // 2. Tạo Question (hỗ trợ cả 2 kho: practice_questions, exam_questions, hoặc both)
        var keyPointsJson = JsonSerializer.Serialize(request.KeyPoints ?? new());
        var normalizedScope = (request.UsageScope ?? "exam").Trim().ToLowerInvariant();
        var imageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        var imageDescription = string.IsNullOrWhiteSpace(request.ImageDescription) ? null : request.ImageDescription.Trim();
        Guid createdId;

        if (normalizedScope == "practice")
        {
            var pq = new PracticeQuestion
            {
                CourseId = request.CourseId,
                RubricId = rubric.Id,
                Rubric = rubric,
                Title = request.Title,
                Content = request.Content,
                SampleAnswer = request.SampleAnswer,
                KeyPoints = keyPointsJson,
                Difficulty = request.Difficulty,
                BloomLevel = request.BloomLevel,
                HasFollowUp = request.HasFollowUp,
                FollowUpPrompt = request.FollowUpPrompt,
                ImageUrl = imageUrl,
                ImageDescription = imageDescription,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.PracticeQuestions.Add(pq);
            createdId = pq.Id;
        }
        else
        {
            var approvalStatus = ApprovalStatus.Draft;
            Guid? approvedBy = null;

            if (request.ApprovedBy.HasValue && request.ApprovedBy.Value != Guid.Empty &&
                (lecturer.Role == "department_head" || lecturer.Role == "admin"))
            {
                approvalStatus = ApprovalStatus.Approved;
                approvedBy = request.ApprovedBy.Value;
            }

            var eq = new ExamQuestion
            {
                CourseId = request.CourseId,
                RubricId = rubric.Id,
                Rubric = rubric,
                Title = request.Title,
                Content = request.Content,
                SampleAnswer = request.SampleAnswer,
                KeyPoints = keyPointsJson,
                Difficulty = request.Difficulty,
                BloomLevel = request.BloomLevel,
                Source = request.Source ?? "manual",
                ApprovalStatus = approvalStatus,
                ApprovedBy = approvedBy,
                SubmittedBy = request.LecturerId,
                ImageUrl = imageUrl,
                ImageDescription = imageDescription,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.ExamQuestions.Add(eq);
            createdId = eq.Id;

            if (normalizedScope is "both" or "all" or "shared")
            {
                var pq = new PracticeQuestion
                {
                    CourseId = request.CourseId,
                    RubricId = rubric.Id,
                    Rubric = rubric,
                    Title = request.Title,
                    Content = request.Content,
                    SampleAnswer = request.SampleAnswer,
                    KeyPoints = keyPointsJson,
                    Difficulty = request.Difficulty,
                    BloomLevel = request.BloomLevel,
                    HasFollowUp = request.HasFollowUp,
                    FollowUpPrompt = request.FollowUpPrompt,
                    ImageUrl = imageUrl,
                    ImageDescription = imageDescription,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PracticeQuestions.Add(pq);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(createdId);
    }
}
