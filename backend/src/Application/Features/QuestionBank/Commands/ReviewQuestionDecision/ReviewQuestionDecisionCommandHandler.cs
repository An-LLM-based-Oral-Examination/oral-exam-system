using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;

public sealed class ReviewQuestionDecisionCommandHandler 
    : IRequestHandler<ReviewQuestionDecisionCommand, Result<QuestionReviewDecisionResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public ReviewQuestionDecisionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<QuestionReviewDecisionResponseDto>> Handle(
        ReviewQuestionDecisionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra quyền Trưởng Bộ Môn
        var reviewer = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ReviewerId && u.IsActive, cancellationToken);

        if (reviewer == null || (reviewer.Role != "department_head" && reviewer.Role != "admin"))
        {
            return Result<QuestionReviewDecisionResponseDto>.Failure(
                "Chỉ Trưởng Bộ Môn ('department_head') hoặc Quản trị viên mới có quyền thẩm định và phê duyệt câu hỏi.");
        }

        // 2. Tìm câu hỏi
        var question = await _context.ExamQuestions
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);

        if (question == null)
        {
            return Result<QuestionReviewDecisionResponseDto>.Failure("Không tìm thấy câu hỏi tương ứng.");
        }

        // 3. Xử lý quyết định
        var now = DateTime.UtcNow;
        string message;

        switch (request.Decision)
        {
            case ApprovalStatus.Approved:
                // Hard gate xác minh lại Barem 10.0 và Model Answer >= 50
                if (string.IsNullOrWhiteSpace(question.SampleAnswer) || question.SampleAnswer.Trim().Length < 50)
                {
                    return Result<QuestionReviewDecisionResponseDto>.Failure("Câu hỏi có Model Answer < 50 ký tự, không thể phê duyệt.");
                }
                if (question.Rubric == null || question.Rubric.Criteria.Count < 2)
                {
                    return Result<QuestionReviewDecisionResponseDto>.Failure("Câu hỏi chưa có Barem Rubric hoặc ít hơn 2 tiêu chí, không thể phê duyệt.");
                }
                if (question.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                {
                    return Result<QuestionReviewDecisionResponseDto>.Failure("Barem Rubric lệch 10.00đ, không thể phê duyệt.");
                }
                if (question.Rubric.Criteria.Any(c => c.MaxScore <= 0))
                {
                    return Result<QuestionReviewDecisionResponseDto>.Failure(
                        "Barem Rubric có tiêu chí con mang điểm nhỏ hơn hoặc bằng 0, không thể phê duyệt.");
                }
                if (!string.IsNullOrWhiteSpace(question.ImageUrl) &&
                    !string.Equals((question.Source ?? "manual").Trim(), "manual", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<QuestionReviewDecisionResponseDto>.Failure(
                        "Chỉ câu hỏi tạo thủ công (manual) mới được phép đính kèm hình ảnh, không thể phê duyệt câu hỏi AI có hình ảnh.");
                }

                foreach (var criterion in question.Rubric.Criteria)
                {
                    if (criterion.Weight > 1.00m)
                    {
                        criterion.Weight = Math.Round(criterion.Weight / 100m, 4, MidpointRounding.AwayFromZero);
                    }
                    if (criterion.Weight <= 0m || criterion.Weight > 1.00m)
                    {
                        criterion.Weight = Math.Clamp(Math.Round(criterion.MaxScore / 10m, 4, MidpointRounding.AwayFromZero), 0.01m, 1.00m);
                    }
                }

                question.ApprovalStatus = ApprovalStatus.Approved;
                question.ApprovedBy = request.ReviewerId;
                question.ReviewNotes = request.ReviewNotes;

                var targetScope = (request.TargetUsageScope ?? string.Empty).Trim().ToLowerInvariant();
                if (targetScope == "practice")
                {
                    question.IsActive = false;
                }
                else if (targetScope is "exam" or "official_exam" or "both" or "all")
                {
                    question.IsActive = true;
                }

                var existingPracticeQ = await _context.PracticeQuestions
                    .FirstOrDefaultAsync(
                        p => p.Id == question.Id || (p.CourseId == question.CourseId && p.Title == question.Title),
                        cancellationToken);

                if (targetScope is "both" or "practice" or "all" || existingPracticeQ != null)
                {
                    if (existingPracticeQ == null)
                    {
                        _context.PracticeQuestions.Add(new PracticeQuestion
                        {
                            Id = question.Id,
                            CourseId = question.CourseId,
                            RubricId = question.RubricId,
                            Title = question.Title,
                            Content = question.Content,
                            SampleAnswer = question.SampleAnswer,
                            KeyPoints = question.KeyPoints,
                            Difficulty = question.Difficulty,
                            BloomLevel = question.BloomLevel,
                            HasFollowUp = true,
                            ImageUrl = question.ImageUrl,
                            ImageDescription = question.ImageDescription,
                            IsActive = true,
                            CreatedAt = now
                        });
                    }
                    else
                    {
                        existingPracticeQ.IsActive = true;
                        existingPracticeQ.Content = question.Content;
                        existingPracticeQ.SampleAnswer = question.SampleAnswer;
                        existingPracticeQ.KeyPoints = question.KeyPoints;
                        existingPracticeQ.Difficulty = question.Difficulty;
                        existingPracticeQ.BloomLevel = question.BloomLevel;
                        existingPracticeQ.ImageUrl = question.ImageUrl;
                        existingPracticeQ.ImageDescription = question.ImageDescription;
                    }
                }

                message = "Câu hỏi đã được Trưởng Bộ Môn phê duyệt chính thức và đưa vào ngân hàng đề thi.";
                break;

            case ApprovalStatus.NeedsRevision:
                question.ApprovalStatus = ApprovalStatus.NeedsRevision;
                question.ReviewNotes = request.ReviewNotes;
                message = "Đã yêu cầu Giảng viên chỉnh sửa câu hỏi kèm ý kiến thẩm định chi tiết.";
                break;

            case ApprovalStatus.Rejected:
                question.ApprovalStatus = ApprovalStatus.Rejected;
                question.ReviewNotes = request.ReviewNotes;
                question.IsActive = false; // Loại hẳn khỏi kho thi
                var rejectedPracticeQ = await _context.PracticeQuestions
                    .FirstOrDefaultAsync(
                        p => p.Id == question.Id || (p.CourseId == question.CourseId && p.Title == question.Title),
                        cancellationToken);
                if (rejectedPracticeQ != null)
                {
                    rejectedPracticeQ.IsActive = false;
                }
                message = "Câu hỏi đã bị từ chối và loại khỏi danh sách thẩm định.";
                break;

            default:
                return Result<QuestionReviewDecisionResponseDto>.Failure("Quyết định thẩm định không hợp lệ.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        var response = new QuestionReviewDecisionResponseDto(
            question.Id,
            question.ApprovalStatus,
            request.ReviewerId,
            now,
            request.ReviewNotes,
            message
        );

        return Result<QuestionReviewDecisionResponseDto>.Success(response);
    }
}
