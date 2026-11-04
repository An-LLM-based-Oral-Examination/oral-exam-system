using System;
using System.Collections.Generic;
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

        if (lecturer == null || (lecturer.Role != "lecturer" && lecturer.Role != "department_head" && lecturer.Role != "admin"))
        {
            return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(
                "Chỉ Giảng viên ('lecturer') hoặc Quản trị viên mới có quyền đệ trình câu hỏi lên Bộ Môn.");
        }

        var distinctIds = request.QuestionIds.Distinct().ToList();

        // 2. Tìm danh sách câu hỏi trong cả ExamQuestions và PracticeQuestions kèm Rubric và Criteria
        var examQuestions = await _context.ExamQuestions
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => distinctIds.Contains(q.Id) && q.CourseId == request.CourseId)
            .ToListAsync(cancellationToken);

        var practiceQuestions = await _context.PracticeQuestions
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => distinctIds.Contains(q.Id) && q.CourseId == request.CourseId)
            .ToListAsync(cancellationToken);

        var foundIds = examQuestions.Select(q => q.Id)
            .Union(practiceQuestions.Select(q => q.Id))
            .ToHashSet();

        if (foundIds.Count != distinctIds.Count)
        {
            return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(
                "Một số câu hỏi không tồn tại hoặc không thuộc môn học đã chọn.");
        }

        // 3. HARD VERIFICATION GATE: Kiểm tra Barem 10.0 và Model Answer >= 50 ký tự cho TỪNG CÂU
        foreach (var q in examQuestions)
        {
            var validationError = ValidateQuestionForSubmission(q.Title, q.SampleAnswer, q.Rubric);
            if (validationError != null)
            {
                return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(validationError);
            }
        }

        var examIdSet = examQuestions.Select(q => q.Id).ToHashSet();
        foreach (var pq in practiceQuestions.Where(p => !examIdSet.Contains(p.Id)))
        {
            var validationError = ValidateQuestionForSubmission(pq.Title, pq.SampleAnswer, pq.Rubric);
            if (validationError != null)
            {
                return Result<BatchSubmitQuestionsForReviewResponseDto>.Failure(validationError);
            }
        }

        // 4. Cập nhật trạng thái sang SUBMITTED_FOR_REVIEW cho cả 2 kho
        var now = DateTime.UtcNow;
        foreach (var q in examQuestions)
        {
            q.ApprovalStatus = ApprovalStatus.SubmittedForReview;
            q.SubmittedBy = request.LecturerId;
            if (!string.IsNullOrWhiteSpace(request.SubmissionNotes))
            {
                q.ReviewNotes = request.SubmissionNotes;
            }
        }

        foreach (var pq in practiceQuestions)
        {
            pq.ApprovalStatus = ApprovalStatus.SubmittedForReview;
            pq.SubmittedBy = request.LecturerId;
            if (!string.IsNullOrWhiteSpace(request.SubmissionNotes))
            {
                pq.ReviewNotes = request.SubmissionNotes;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var response = new BatchSubmitQuestionsForReviewResponseDto(
            request.CourseId,
            foundIds.Count,
            ApprovalStatus.SubmittedForReview,
            request.LecturerId,
            now,
            $"Đã gửi thành công {foundIds.Count} câu hỏi lên Trưởng Bộ Môn để thẩm định và phê duyệt."
        );

        return Result<BatchSubmitQuestionsForReviewResponseDto>.Success(response);
    }

    private static string? ValidateQuestionForSubmission(string title, string? sampleAnswer, Rubric? rubric)
    {
        if (string.IsNullOrWhiteSpace(sampleAnswer) || sampleAnswer.Trim().Length < 50)
        {
            return $"Câu hỏi '{title}' có Model Answer ngắn hơn 50 ký tự. Vui lòng bổ sung đầy đủ trước khi gửi thẩm định.";
        }

        if (rubric == null || rubric.Criteria.Count < 2)
        {
            return $"Câu hỏi '{title}' chưa có Barem Rubric hoặc ít hơn 2 tiêu chí.";
        }

        var sumScore = rubric.Criteria.Sum(c => c.MaxScore);
        if (sumScore != 10.00m)
        {
            return $"Câu hỏi '{title}' có tổng điểm Barem Rubric ({sumScore:N2}đ) lệch 10.00đ. Bắt buộc phải bằng chính xác 10.00đ.";
        }

        if (rubric.Criteria.Any(c => c.MaxScore <= 0))
        {
            return $"Câu hỏi '{title}' có tiêu chí con trong Barem Rubric mang điểm nhỏ hơn hoặc bằng 0.";
        }

        return null;
    }
}
