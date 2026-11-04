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

        // 2. Tìm câu hỏi trong ExamQuestions hoặc PracticeQuestions (hỗ trợ cả 2 kho độc lập theo R8)
        var question = await _context.ExamQuestions
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);

        var practiceOnlyQuestion = question == null
            ? await _context.PracticeQuestions
                .Include(q => q.Rubric)
                    .ThenInclude(r => r.Criteria)
                .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken)
            : null;

        if (question == null && practiceOnlyQuestion == null)
        {
            return Result<QuestionReviewDecisionResponseDto>.Failure("Không tìm thấy câu hỏi tương ứng.");
        }

        var now = DateTime.UtcNow;
        string message;

        // Nhánh câu hỏi chỉ thuộc kho Luyện tập / Thi thử (PracticeQuestions)
        if (question == null && practiceOnlyQuestion != null)
        {
            switch (request.Decision)
            {
                case ApprovalStatus.Approved:
                    var pqError = ValidateQuestionBeforeApprove(
                        practiceOnlyQuestion.SampleAnswer,
                        practiceOnlyQuestion.Rubric,
                        practiceOnlyQuestion.ImageUrl,
                        practiceOnlyQuestion.Source);
                    if (pqError != null)
                    {
                        return Result<QuestionReviewDecisionResponseDto>.Failure(pqError);
                    }

                    NormalizeRubricWeights(practiceOnlyQuestion.Rubric!);
                    practiceOnlyQuestion.ApprovalStatus = ApprovalStatus.Approved;
                    practiceOnlyQuestion.ApprovedBy = request.ReviewerId;
                    practiceOnlyQuestion.ReviewNotes = request.ReviewNotes;
                    practiceOnlyQuestion.IsActive = true;
                    message = "Câu hỏi luyện tập đã được Trưởng Bộ Môn phê duyệt chính thức và đưa vào ngân hàng câu hỏi luyện tập.";
                    break;

                case ApprovalStatus.NeedsRevision:
                    practiceOnlyQuestion.ApprovalStatus = ApprovalStatus.NeedsRevision;
                    practiceOnlyQuestion.ReviewNotes = request.ReviewNotes;
                    practiceOnlyQuestion.IsActive = false;
                    message = "Đã yêu cầu Giảng viên chỉnh sửa câu hỏi kèm ý kiến thẩm định chi tiết.";
                    break;

                case ApprovalStatus.Rejected:
                    practiceOnlyQuestion.ApprovalStatus = ApprovalStatus.Rejected;
                    practiceOnlyQuestion.ReviewNotes = request.ReviewNotes;
                    practiceOnlyQuestion.IsActive = false;
                    message = "Câu hỏi đã bị từ chối và loại khỏi danh sách thẩm định.";
                    break;

                default:
                    return Result<QuestionReviewDecisionResponseDto>.Failure("Quyết định thẩm định không hợp lệ.");
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<QuestionReviewDecisionResponseDto>.Success(new QuestionReviewDecisionResponseDto(
                practiceOnlyQuestion.Id,
                practiceOnlyQuestion.ApprovalStatus,
                request.ReviewerId,
                now,
                request.ReviewNotes,
                message
            ));
        }

        // 3. Xử lý quyết định cho câu hỏi trong ExamQuestions (và đồng bộ sang PracticeQuestions nếu có)
        switch (request.Decision)
        {
            case ApprovalStatus.Approved:
                var eqError = ValidateQuestionBeforeApprove(
                    question!.SampleAnswer,
                    question.Rubric,
                    question.ImageUrl,
                    question.Source);
                if (eqError != null)
                {
                    return Result<QuestionReviewDecisionResponseDto>.Failure(eqError);
                }

                NormalizeRubricWeights(question.Rubric!);

                question.ApprovalStatus = ApprovalStatus.Approved;
                question.ApprovedBy = request.ReviewerId;
                question.ReviewNotes = request.ReviewNotes;

                var targetScope = (request.TargetUsageScope ?? string.Empty).Trim().ToLowerInvariant();
                if (targetScope == "practice")
                {
                    question.IsActive = false;
                }
                else if (targetScope is "exam" or "official_exam" or "both" or "all" or "")
                {
                    question.IsActive = true;
                }

                var existingPracticeQ = await _context.PracticeQuestions
                    .FirstOrDefaultAsync(
                        p => p.Id == question.Id,
                        cancellationToken);

                if (targetScope is "both" or "practice" or "all" || existingPracticeQ != null)
                {
                    if (existingPracticeQ == null)
                    {
                        _context.PracticeQuestions.Add(new PracticeQuestion
                        {
                            Id = question.Id,
                            CourseId = question.CourseId,
                            CloId = question.CloId,
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
                            Source = question.Source ?? "manual",
                            ApprovalStatus = ApprovalStatus.Approved,
                            SubmittedBy = question.SubmittedBy,
                            ApprovedBy = request.ReviewerId,
                            ReviewNotes = request.ReviewNotes,
                            IsActive = true,
                            CreatedAt = now
                        });
                    }
                    else
                    {
                        existingPracticeQ.IsActive = true;
                        existingPracticeQ.CloId = question.CloId;
                        existingPracticeQ.Content = question.Content;
                        existingPracticeQ.SampleAnswer = question.SampleAnswer;
                        existingPracticeQ.KeyPoints = question.KeyPoints;
                        existingPracticeQ.Difficulty = question.Difficulty;
                        existingPracticeQ.BloomLevel = question.BloomLevel;
                        existingPracticeQ.ImageUrl = question.ImageUrl;
                        existingPracticeQ.ImageDescription = question.ImageDescription;
                        existingPracticeQ.Source = question.Source ?? existingPracticeQ.Source;
                        existingPracticeQ.ApprovalStatus = ApprovalStatus.Approved;
                        existingPracticeQ.SubmittedBy = question.SubmittedBy ?? existingPracticeQ.SubmittedBy;
                        existingPracticeQ.ApprovedBy = request.ReviewerId;
                        existingPracticeQ.ReviewNotes = request.ReviewNotes;
                    }
                }

                message = "Câu hỏi đã được Trưởng Bộ Môn phê duyệt chính thức và đưa vào ngân hàng đề thi.";
                break;

            case ApprovalStatus.NeedsRevision:
                question!.ApprovalStatus = ApprovalStatus.NeedsRevision;
                question.ReviewNotes = request.ReviewNotes;
                var revisionPracticeQ = await _context.PracticeQuestions
                    .FirstOrDefaultAsync(
                        p => p.Id == question.Id,
                        cancellationToken);
                if (revisionPracticeQ != null)
                {
                    revisionPracticeQ.ApprovalStatus = ApprovalStatus.NeedsRevision;
                    revisionPracticeQ.ReviewNotes = request.ReviewNotes;
                    revisionPracticeQ.IsActive = false;
                }
                message = "Đã yêu cầu Giảng viên chỉnh sửa câu hỏi kèm ý kiến thẩm định chi tiết.";
                break;

            case ApprovalStatus.Rejected:
                question!.ApprovalStatus = ApprovalStatus.Rejected;
                question.ReviewNotes = request.ReviewNotes;
                question.IsActive = false;
                var rejectedPracticeQ = await _context.PracticeQuestions
                    .FirstOrDefaultAsync(
                        p => p.Id == question.Id,
                        cancellationToken);
                if (rejectedPracticeQ != null)
                {
                    rejectedPracticeQ.ApprovalStatus = ApprovalStatus.Rejected;
                    rejectedPracticeQ.ReviewNotes = request.ReviewNotes;
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

    private static string? ValidateQuestionBeforeApprove(string? sampleAnswer, Rubric? rubric, string? imageUrl, string? source)
    {
        if (string.IsNullOrWhiteSpace(sampleAnswer) || sampleAnswer.Trim().Length < 50)
        {
            return "Câu hỏi có Model Answer < 50 ký tự, không thể phê duyệt.";
        }
        if (rubric == null || rubric.Criteria.Count < 2)
        {
            return "Câu hỏi chưa có Barem Rubric hoặc ít hơn 2 tiêu chí, không thể phê duyệt.";
        }
        if (rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
        {
            return "Barem Rubric lệch 10.00đ, không thể phê duyệt.";
        }
        if (rubric.Criteria.Any(c => c.MaxScore <= 0))
        {
            return "Barem Rubric có tiêu chí con mang điểm nhỏ hơn hoặc bằng 0, không thể phê duyệt.";
        }
        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            !string.Equals((source ?? "manual").Trim(), "manual", StringComparison.OrdinalIgnoreCase))
        {
            return "Chỉ câu hỏi tạo thủ công (manual) mới được phép đính kèm hình ảnh, không thể phê duyệt câu hỏi AI có hình ảnh.";
        }

        return null;
    }

    private static void NormalizeRubricWeights(Rubric rubric)
    {
        foreach (var criterion in rubric.Criteria)
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
    }
}
