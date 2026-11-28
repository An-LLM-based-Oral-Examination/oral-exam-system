using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CurateExamQuestionBank;

public sealed class CurateExamQuestionBankCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CurateExamQuestionBankCommand, Result<CurateExamQuestionBankResultDto>>
{
    public async Task<Result<CurateExamQuestionBankResultDto>> Handle(
        CurateExamQuestionBankCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context, request.ActorUserId, request.ActorRole, UserRole.DepartmentHead, cancellationToken))
        {
            return Result<CurateExamQuestionBankResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var course = await context.Courses
            .AsNoTracking()
            .Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (course is null)
        {
            return Result<CurateExamQuestionBankResultDto>.Failure(OfficialExamErrors.CourseNotFound);
        }

        if (!AcademicCourseOwnership.IsOwnedBy(course, request.ActorUserId))
        {
            return Result<CurateExamQuestionBankResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var distinctIds = (request.QuestionIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
        {
            return Result<CurateExamQuestionBankResultDto>.Failure(
                "UNPROCESSABLE: Danh sách câu hỏi cần quyết định nạp/loại kho không được để trống.");
        }

        var normalizedDecision = (request.Decision ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedDecision is not (BankCurationStatus.CuratedIntoBank or BankCurationStatus.ExcludedFromBank))
        {
            return Result<CurateExamQuestionBankResultDto>.Failure(
                "UNPROCESSABLE: Quyết định lọc kho chỉ chấp nhận 'CURATED_INTO_BANK' hoặc 'EXCLUDED_FROM_BANK'.");
        }

        var questions = await context.ExamQuestions
            .Where(q =>
                distinctIds.Contains(q.Id)
                && q.CourseId == request.CourseId
                && !q.IsPaperCopy
                && !q.IsDeleted)
            .ToListAsync(cancellationToken);

        if (questions.Count != distinctIds.Count)
        {
            return Result<CurateExamQuestionBankResultDto>.Failure(
                "NOT_FOUND: Một hoặc nhiều câu hỏi không tồn tại, đã bị xóa mềm hoặc không thuộc môn học này.");
        }

        if (questions.Any(q => q.BankCurationStatus != BankCurationStatus.PendingCuration))
        {
            return Result<CurateExamQuestionBankResultDto>.Failure(
                "CONFLICT: Chỉ có thể phân loại các câu hỏi đang ở trạng thái chờ tổng hợp nạp kho (PENDING_CURATION).");
        }

        if (normalizedDecision == BankCurationStatus.CuratedIntoBank
            && questions.Any(q => q.ApprovalStatus != ApprovalStatus.Approved))
        {
            return Result<CurateExamQuestionBankResultDto>.Failure(
                "CONFLICT: Chỉ có thể nạp vào ngân hàng đề chung các câu hỏi đã được phê duyệt (APPROVED).");
        }

        var now = DateTime.UtcNow;
        var trimmedNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        foreach (var question in questions)
        {
            question.BankCurationStatus = normalizedDecision;
            question.CuratedBy = request.ActorUserId;
            question.CuratedAt = now;

            if (normalizedDecision == BankCurationStatus.CuratedIntoBank)
            {
                question.IsActive = true;
            }
            // Với EXCLUDED_FROM_BANK: giữ nguyên IsDeleted = false để bảo toàn lịch sử bài thi và phúc khảo.
        }

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "CURATE_EXAM_QUESTION_BANK",
            EntityName = "exam_questions",
            EntityId = course.Id,
            OldValues = $"status={BankCurationStatus.PendingCuration};count={questions.Count}",
            NewValues = $"decision={normalizedDecision};count={questions.Count};questionIds={string.Join(",", distinctIds)};notes={trimmedNotes ?? string.Empty}",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result<CurateExamQuestionBankResultDto>.Success(new CurateExamQuestionBankResultDto
        {
            CourseId = course.Id,
            Decision = normalizedDecision,
            ProcessedCount = questions.Count,
            QuestionIds = distinctIds,
            CuratedAt = now
        });
    }
}
