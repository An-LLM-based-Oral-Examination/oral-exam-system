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

        // Kiểm tra Barem 10.00
        if (request.Rubric?.Criteria == null || request.Rubric.Criteria.Count < 2)
            return Result<Guid>.Failure("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");

        if (request.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
            return Result<Guid>.Failure("Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");

        if (request.Rubric.Criteria.Any(c => c.MaxScore <= 0))
            return Result<Guid>.Failure("Điểm tối đa của từng tiêu chí con trong Barem Rubric bắt buộc phải lớn hơn 0.");

        // Kiểm tra SampleAnswer >= 50 nếu có
        if (!string.IsNullOrEmpty(request.SampleAnswer) && request.SampleAnswer.Trim().Length < 50)
            return Result<Guid>.Failure("Câu trả lời mẫu (Model Answer) khi cung cấp bắt buộc phải từ 50 ký tự trở lên.");

        // 1. Tạo Rubric
        var rubric = new Rubric
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

        // 2. Tạo Question
        var keyPointsJson = JsonSerializer.Serialize(request.KeyPoints ?? new());
        Guid createdId;

        if (request.UsageScope == "practice")
        {
            var pq = new PracticeQuestion
            {
                CourseId = request.CourseId,
                Rubric = rubric,
                Title = request.Title,
                Content = request.Content,
                SampleAnswer = request.SampleAnswer,
                KeyPoints = keyPointsJson,
                Difficulty = request.Difficulty,
                BloomLevel = request.BloomLevel,
                HasFollowUp = request.HasFollowUp,
                FollowUpPrompt = request.FollowUpPrompt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.PracticeQuestions.Add(pq);
            createdId = pq.Id;
        }
        else
        {
            var eq = new ExamQuestion
            {
                CourseId = request.CourseId,
                Rubric = rubric,
                Title = request.Title,
                Content = request.Content,
                SampleAnswer = request.SampleAnswer,
                KeyPoints = keyPointsJson,
                Difficulty = request.Difficulty,
                BloomLevel = request.BloomLevel,
                Source = request.Source ?? "manual",
                ApprovalStatus = ApprovalStatus.Draft,
                SubmittedBy = request.LecturerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.ExamQuestions.Add(eq);
            createdId = eq.Id;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(createdId);
    }
}
