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

namespace OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;

public sealed class BatchSubmitQuestionsForReviewCommandHandler 
    : IRequestHandler<BatchSubmitQuestionsForReviewCommand, Result<BatchSubmitQuestionsForReviewResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public BatchSubmitQuestionsForReviewCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BatchSubmitQuestionsForReviewResponseDto>> Handle(
        BatchSubmitQuestionsForReviewCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra Giảng viên
        var lecturer = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.LecturerId && u.IsActive, cancellationToken);

        if (lecturer == null || (lecturer.Role != "lecturer" && lecturer.Role != "admin"))
        {
            return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(
                "Chỉ Giảng viên ('lecturer') hoặc Quản trị viên mới có quyền đệ trình câu hỏi lên Bộ Môn.");
        }

        // 2. Tìm danh sách câu hỏi trong ExamQuestions kèm Rubric và Criteria
        var questions = await _context.ExamQuestions
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => request.QuestionIds.Contains(q.Id) && q.CourseId == request.CourseId)
            .ToListAsync(cancellationToken);

        if (questions.Count != request.QuestionIds.Count)
        {
            return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(
                "Một số câu hỏi không tồn tại hoặc không thuộc môn học đã chọn.");
        }

        // 3. HARD VERIFICATION GATE: Kiểm tra Barem 10.0 và Model Answer >= 50 ký tự cho TỪNG CÂU
        foreach (var q in questions)
        {
            if (string.IsNullOrWhiteSpace(q.SampleAnswer) || q.SampleAnswer.Trim().Length < 50)
            {
                return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(
                    $"Câu hỏi '{q.Title}' có Model Answer ngắn hơn 50 ký tự. Vui lòng bổ sung đầy đủ trước khi gửi thẩm định.");
            }

            if (q.Rubric == null || q.Rubric.Criteria.Count < 2)
            {
                return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(
                    $"Câu hỏi '{q.Title}' chưa có Barem Rubric hoặc ít hơn 2 tiêu chí.");
            }

            var sumScore = q.Rubric.Criteria.Sum(c => c.MaxScore);
            if (sumScore != 10.00m)
            {
                return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(
                    $"Câu hỏi '{q.Title}' có tổng điểm Barem Rubric ({sumScore:N2}đ) lệch 10.00đ. Bắt buộc phải bằng chính xác 10.00đ.");
            }

            if (q.Rubric.Criteria.Any(c => c.MaxScore <= 0))
            {
                return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(
                    $"Câu hỏi '{q.Title}' có tiêu chí con trong Barem Rubric mang điểm nhỏ hơn hoặc bằng 0.");
            }
        }

        // 4. Cập nhật trạng thái sang SUBMITTED_FOR_REVIEW
        var now = DateTime.UtcNow;
        foreach (var q in questions)
        {
            q.ApprovalStatus = ApprovalStatus.SubmittedForReview;
            q.SubmittedBy = request.LecturerId;
            if (!string.IsNullOrWhiteSpace(request.SubmissionNotes))
            {
                q.ReviewNotes = request.SubmissionNotes;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var response = new BatchSubmitQuestionsForReviewResponseDto(
            request.CourseId,
            questions.Count,
            ApprovalStatus.SubmittedForReview,
            request.LecturerId,
            now,
            $"Đã gửi thành công {questions.Count} câu hỏi lên Trưởng Bộ Môn để thẩm định và phê duyệt."
        );

        return Result<BatchSubmitQuestionsForReviewResponseDto>.Success(response);
    }
}
