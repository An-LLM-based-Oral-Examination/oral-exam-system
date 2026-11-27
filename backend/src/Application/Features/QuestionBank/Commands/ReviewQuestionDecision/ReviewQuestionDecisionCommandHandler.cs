using System;
using System.Linq;
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
using OralExamination.Application.Common.Authorization;

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

        if (reviewer == null || reviewer.Role != UserRole.DepartmentHead)
        {
            return Result<QuestionReviewDecisionResponseDto>.Failure(
                "Chỉ Trưởng Bộ Môn ('department_head') mới có quyền thẩm định và phê duyệt câu hỏi.");
        }

        // 2. Tìm câu hỏi trong ExamQuestions hoặc PracticeQuestions (hỗ trợ cả 2 kho độc lập theo R8)
        var question = await _context.ExamQuestions
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(q => !q.IsPaperCopy && q.Id == request.QuestionId && !q.IsDeleted, cancellationToken);

        var practiceOnlyQuestion = question == null
            ? await _context.PracticeQuestions
                .Include(q => q.Rubric)
                    .ThenInclude(r => r.Criteria)
                .FirstOrDefaultAsync(q => q.Id == request.QuestionId && !q.IsDeleted, cancellationToken)
            : null;

        if (question == null && practiceOnlyQuestion == null)
        {
            return Result<QuestionReviewDecisionResponseDto>.Failure("Không tìm thấy câu hỏi tương ứng.");
        }

        var courseId = question?.CourseId ?? practiceOnlyQuestion!.CourseId;
        if (!await QuestionCourseAccess.AllowedCourseIds(_context, reviewer.Role, reviewer.Id)
                .ContainsAsync(courseId, cancellationToken))
            return Result<QuestionReviewDecisionResponseDto>.Failure("FORBIDDEN: Người duyệt không phụ trách môn học này.");

        if ((question?.ApprovalStatus ?? practiceOnlyQuestion!.ApprovalStatus) != ApprovalStatus.SubmittedForReview)
            return Result<QuestionReviewDecisionResponseDto>.Failure("CONFLICT: Chỉ câu hỏi đã gửi thẩm định mới được ra quyết định.");

        var now = DateTime.UtcNow;
        var course = await _context.Courses.Include(c => c.AcademicProgram).FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course is null || !AcademicCourseOwnership.IsOwnedBy(course, reviewer.Id))
            return Result<QuestionReviewDecisionResponseDto>.Failure("FORBIDDEN: Câu hỏi không thuộc môn phụ trách.");
        var normalizedDecision = (request.Decision ?? string.Empty).Trim();
        var normalizedReviewNotes = string.IsNullOrWhiteSpace(request.ReviewNotes) ? null : request.ReviewNotes.Trim();
        var pendingItems = await _context.QuestionReviewRequestItems.Include(i => i.Request)
            .Where(i => i.QuestionId == request.QuestionId && i.Decision == "PENDING" &&
                i.UsageScope == (question == null ? "practice" : "exam"))
            .ToListAsync(cancellationToken);
        void RecordRequestDecision()
        {
            foreach (var item in pendingItems)
            {
                item.Decision = normalizedDecision;
                item.ReviewNotes = normalizedReviewNotes;
                item.ReviewedBy = request.ReviewerId;
                item.ReviewedAt = now;
                item.Request.Version++;
            }
        }
        string message;

        // Nhánh câu hỏi chỉ thuộc kho Luyện tập / Thi thử (PracticeQuestions)
        if (question == null && practiceOnlyQuestion != null)
        {
            switch (normalizedDecision)
            {
                case ApprovalStatus.Approved:
                    var practiceTargetScope = (request.TargetUsageScope ?? string.Empty).Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(practiceTargetScope) && practiceTargetScope != "practice")
                    {
                        return Result<QuestionReviewDecisionResponseDto>.Failure(
                            "Phạm vi sử dụng khi duyệt câu hỏi luyện tập không hợp lệ. Kho câu hỏi luyện tập ('practice') tách biệt hoàn toàn với kho đề thi thật ('exam').");
                    }

                    var pqError = ValidateQuestionBeforeApprove(
                        practiceOnlyQuestion.SampleAnswer,
                        practiceOnlyQuestion.Rubric,
                        practiceOnlyQuestion.ImageUrl,
                        practiceOnlyQuestion.Source);
                    if (pqError != null)
                    {
                        return Result<QuestionReviewDecisionResponseDto>.Failure(pqError);
                    }
                    if (practiceOnlyQuestion.SubmittedBy == reviewer.Id)
                        return Result<QuestionReviewDecisionResponseDto>.Failure("FORBIDDEN: Không được tự phê duyệt câu hỏi của mình.");

                    NormalizeRubricWeights(practiceOnlyQuestion.Rubric!);
                    practiceOnlyQuestion.ApprovalStatus = ApprovalStatus.Approved;
                    practiceOnlyQuestion.ApprovedBy = request.ReviewerId;
                    practiceOnlyQuestion.ApprovedAt = now;
                    practiceOnlyQuestion.ReviewNotes = normalizedReviewNotes;
                    practiceOnlyQuestion.IsActive = true;
                    message = "Câu hỏi luyện tập đã được Trưởng Bộ Môn phê duyệt chính thức và đưa vào ngân hàng câu hỏi luyện tập.";
                    break;

                case ApprovalStatus.NeedsRevision:
                    practiceOnlyQuestion.ApprovalStatus = ApprovalStatus.NeedsRevision;
                    practiceOnlyQuestion.ReviewNotes = normalizedReviewNotes;
                    practiceOnlyQuestion.IsActive = false;
                    message = "Đã yêu cầu Giảng viên chỉnh sửa câu hỏi kèm ý kiến thẩm định chi tiết.";
                    break;

                case ApprovalStatus.Rejected:
                    practiceOnlyQuestion.ApprovalStatus = ApprovalStatus.Rejected;
                    practiceOnlyQuestion.ReviewNotes = normalizedReviewNotes;
                    practiceOnlyQuestion.IsActive = false;
                    message = "Câu hỏi đã bị từ chối và loại khỏi danh sách thẩm định.";
                    break;

                default:
                    return Result<QuestionReviewDecisionResponseDto>.Failure("Quyết định thẩm định không hợp lệ.");
            }

            RecordRequestDecision();
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<QuestionReviewDecisionResponseDto>.Failure(
                    "CONFLICT: Câu hỏi hoặc yêu cầu duyệt vừa được xử lý đồng thời bởi người khác.");
            }

            return Result<QuestionReviewDecisionResponseDto>.Success(new QuestionReviewDecisionResponseDto(
                practiceOnlyQuestion.Id,
                practiceOnlyQuestion.ApprovalStatus,
                request.ReviewerId,
                now,
                normalizedReviewNotes,
                message
            ));
        }

        // 3. Xử lý quyết định cho câu hỏi trong kho đề thi thật (ExamQuestions — cô lập hoàn toàn với PracticeQuestions theo R8)
        switch (normalizedDecision)
        {
            case ApprovalStatus.Approved:
                var targetScope = (request.TargetUsageScope ?? string.Empty).Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(targetScope) && targetScope is not ("exam" or "official_exam"))
                {
                    return Result<QuestionReviewDecisionResponseDto>.Failure(
                        "Phạm vi sử dụng khi duyệt câu hỏi thi thật không hợp lệ. Kho câu hỏi thi thật ('exam') tách biệt hoàn toàn với kho luyện tập ('practice').");
                }

                var eqError = ValidateQuestionBeforeApprove(
                    question!.SampleAnswer,
                    question.Rubric,
                    question.ImageUrl,
                    question.Source);
                if (eqError != null)
                {
                    return Result<QuestionReviewDecisionResponseDto>.Failure(eqError);
                }
                if (question.SubmittedBy == reviewer.Id)
                    return Result<QuestionReviewDecisionResponseDto>.Failure("FORBIDDEN: Không được tự phê duyệt câu hỏi của mình.");

                NormalizeRubricWeights(question.Rubric!);

                question.ApprovalStatus = ApprovalStatus.Approved;
                question.ApprovedBy = request.ReviewerId;
                question.ApprovedAt = now;
                question.ReviewNotes = normalizedReviewNotes;
                question.IsActive = true;

                message = "Câu hỏi đã được Trưởng Bộ Môn phê duyệt chính thức và đưa vào ngân hàng đề thi.";
                break;

            case ApprovalStatus.NeedsRevision:
                question!.ApprovalStatus = ApprovalStatus.NeedsRevision;
                question.ReviewNotes = normalizedReviewNotes;
                question.IsActive = false;
                message = "Đã yêu cầu Giảng viên chỉnh sửa câu hỏi kèm ý kiến thẩm định chi tiết.";
                break;

            case ApprovalStatus.Rejected:
                question!.ApprovalStatus = ApprovalStatus.Rejected;
                question.ReviewNotes = normalizedReviewNotes;
                question.IsActive = false;
                message = "Câu hỏi đã bị từ chối và loại khỏi danh sách thẩm định.";
                break;

            default:
                return Result<QuestionReviewDecisionResponseDto>.Failure("Quyết định thẩm định không hợp lệ.");
        }

        RecordRequestDecision();
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<QuestionReviewDecisionResponseDto>.Failure(
                "CONFLICT: Câu hỏi hoặc yêu cầu duyệt vừa được xử lý đồng thời bởi người khác.");
        }

        var response = new QuestionReviewDecisionResponseDto(
            question.Id,
            question.ApprovalStatus,
            request.ReviewerId,
            now,
            normalizedReviewNotes,
            message
        );

        return Result<QuestionReviewDecisionResponseDto>.Success(response);
    }

    internal static string? ValidateQuestionBeforeApprove(string? sampleAnswer, Rubric? rubric, string? imageUrl, string? source)
    {
        if (string.IsNullOrWhiteSpace(sampleAnswer) || sampleAnswer.Trim().Length < 50)
        {
            return "Câu hỏi có Model Answer < 50 ký tự, không thể phê duyệt.";
        }
        if (rubric == null || rubric.Criteria.Count < 2)
        {
            return "Câu hỏi chưa có Barem Rubric hoặc ít hơn 2 tiêu chí, không thể phê duyệt.";
        }
        if (rubric.Criteria.Any(c => string.IsNullOrWhiteSpace(c.CriterionName) || c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength))
        {
            return "Barem Rubric có tiêu chí con thiếu tên hoặc vượt quá 200 ký tự, không thể phê duyệt.";
        }
        if (rubric.Criteria.Any(c => c.MaxScore <= 0))
        {
            return "Barem Rubric có tiêu chí con mang điểm nhỏ hơn hoặc bằng 0, không thể phê duyệt.";
        }
        if (rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
        {
            return "Barem Rubric lệch 10.00đ, không thể phê duyệt.";
        }
        if (rubric.Criteria.Any(c => !RubricNormalization.HasValidScorePrecision(c.MaxScore)))
        {
            return "Barem Rubric có tiêu chí con vượt quá 2 chữ số thập phân (numeric(4,2)), không thể phê duyệt.";
        }
        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            !string.Equals((source ?? "manual").Trim(), "manual", StringComparison.OrdinalIgnoreCase))
        {
            return "Chỉ câu hỏi tạo thủ công (manual) mới được phép đính kèm hình ảnh, không thể phê duyệt câu hỏi AI có hình ảnh.";
        }

        return null;
    }

    internal static void NormalizeRubricWeights(Rubric rubric)
    {
        foreach (var criterion in rubric.Criteria)
        {
            criterion.Weight = RubricNormalization.NormalizeWeight(criterion.Weight, criterion.MaxScore);
        }
    }
}
