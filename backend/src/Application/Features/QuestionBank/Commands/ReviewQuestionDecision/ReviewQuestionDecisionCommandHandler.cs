using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;
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

                question.ApprovalStatus = ApprovalStatus.Approved;
                question.ApprovedBy = request.ReviewerId;
                question.ReviewNotes = request.ReviewNotes;
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
