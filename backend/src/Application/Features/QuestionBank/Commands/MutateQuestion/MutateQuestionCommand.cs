using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion;

public enum QuestionMutationKind { Update, SetActive, SoftDelete }

public sealed record UpdateQuestionDraftDto(
    string Title, string Content, string SampleAnswer, List<string> KeyPoints,
    string Difficulty, string BloomLevel, RubricDraftDto Rubric, Guid? CloId);

public sealed record MutateQuestionCommand(
    Guid QuestionId, Guid ActorId, string ActorRole, QuestionMutationKind Kind,
    UpdateQuestionDraftDto? Draft = null, bool? Active = null) : IRequest<Result<Guid>>;

public sealed class MutateQuestionCommandHandler : IRequestHandler<MutateQuestionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public MutateQuestionCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<Guid>> Handle(MutateQuestionCommand request, CancellationToken cancellationToken)
    {
        if (request.QuestionId == Guid.Empty || request.ActorId == Guid.Empty)
            return Result<Guid>.Failure("FORBIDDEN: Thiếu định danh hợp lệ.");

        var actor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(
            u => u.Id == request.ActorId && u.IsActive, cancellationToken);
        if (actor is null || actor.Role != request.ActorRole ||
            actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
            return Result<Guid>.Failure("FORBIDDEN: Không có quyền sửa câu hỏi.");

        var exam = await _context.ExamQuestions.FirstOrDefaultAsync(
            q => q.Id == request.QuestionId && !q.IsDeleted, cancellationToken);
        var practice = exam is null
            ? await _context.PracticeQuestions.FirstOrDefaultAsync(
                q => q.Id == request.QuestionId && !q.IsDeleted, cancellationToken)
            : null;
        if (exam is null && practice is null)
            return Result<Guid>.Failure("NOT_FOUND: Không tìm thấy câu hỏi.");

        var courseId = exam?.CourseId ?? practice!.CourseId;
        var ownerId = exam?.SubmittedBy ?? practice!.SubmittedBy;
        var status = exam?.ApprovalStatus ?? practice!.ApprovalStatus;
        var canAccess = await QuestionCourseAccess.AllowedCourseIds(_context, actor.Role, actor.Id)
            .ContainsAsync(courseId, cancellationToken);
        if (!canAccess && actor.Role == UserRole.Lecturer)
            canAccess = await _context.Classes.AnyAsync(c => c.CourseId == courseId &&
                c.LecturerId == actor.Id && c.IsActive, cancellationToken);
        if (!canAccess || (actor.Role == UserRole.Lecturer && ownerId != actor.Id))
            return Result<Guid>.Failure("FORBIDDEN: Câu hỏi không thuộc quyền quản lý của người dùng.");

        switch (request.Kind)
        {
            case QuestionMutationKind.Update:
                if (status is not (ApprovalStatus.Draft or ApprovalStatus.NeedsRevision))
                    return Result<Guid>.Failure("CONFLICT: Chỉ sửa câu hỏi nháp hoặc cần chỉnh sửa.");
                if (await HasQuestionUsageAsync(request.QuestionId, exam is not null, cancellationToken))
                    return Result<Guid>.Failure("CONFLICT: Câu hỏi đã được sử dụng nên không thể sửa.");
                var error = await ValidateDraftAsync(request.Draft, courseId, cancellationToken);
                if (error is not null) return Result<Guid>.Failure($"VALIDATION: {error}");
                var draft = request.Draft!;
                var rubric = BuildRubric(draft.Rubric, courseId);
                _context.Rubrics.Add(rubric);
                if (exam is not null)
                {
                    exam.Title = draft.Title.Trim();
                    exam.Content = draft.Content.Trim();
                    exam.SampleAnswer = draft.SampleAnswer.Trim();
                    exam.KeyPoints = JsonSerializer.Serialize(draft.KeyPoints);
                    exam.Difficulty = draft.Difficulty;
                    exam.BloomLevel = draft.BloomLevel;
                    exam.CloId = draft.CloId;
                    exam.Rubric = rubric;
                    exam.ApprovalStatus = ApprovalStatus.Draft;
                    exam.IsActive = false;
                }
                else
                {
                    practice!.Title = draft.Title.Trim();
                    practice.Content = draft.Content.Trim();
                    practice.SampleAnswer = draft.SampleAnswer.Trim();
                    practice.KeyPoints = JsonSerializer.Serialize(draft.KeyPoints);
                    practice.Difficulty = draft.Difficulty;
                    practice.BloomLevel = draft.BloomLevel;
                    practice.CloId = draft.CloId;
                    practice.Rubric = rubric;
                    practice.ApprovalStatus = ApprovalStatus.Draft;
                    practice.IsActive = false;
                }
                break;

            case QuestionMutationKind.SetActive:
                if (actor.Role == UserRole.Lecturer)
                    return Result<Guid>.Failure("FORBIDDEN: Chỉ Trưởng bộ môn hoặc Admin được bật/tắt câu hỏi.");
                if (status != ApprovalStatus.Approved || request.Active is null)
                    return Result<Guid>.Failure("CONFLICT: Chỉ câu hỏi đã duyệt được bật/tắt.");
                if (exam is not null) exam.IsActive = request.Active.Value;
                else practice!.IsActive = request.Active.Value;
                break;

            case QuestionMutationKind.SoftDelete:
                if (status != ApprovalStatus.Draft)
                    return Result<Guid>.Failure("CONFLICT: Chỉ thu hồi câu hỏi nháp chưa sử dụng.");
                var hasUsage = await HasQuestionUsageAsync(request.QuestionId, exam is not null, cancellationToken);
                if (hasUsage)
                    return Result<Guid>.Failure("CONFLICT: Câu hỏi đã được sử dụng nên không thể thu hồi.");
                if (exam is not null) { exam.IsDeleted = true; exam.IsActive = false; }
                else { practice!.IsDeleted = true; practice.IsActive = false; }
                break;

            default:
                return Result<Guid>.Failure("VALIDATION: Thao tác không hợp lệ.");
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(request.QuestionId);
    }

    private async Task<bool> HasQuestionUsageAsync(Guid questionId, bool isExam, CancellationToken cancellationToken) =>
        isExam
            ? await _context.ExamSetQuestions.AnyAsync(q => q.ExamQuestionId == questionId, cancellationToken)
              || await _context.ExamQuestionSubmissions.AnyAsync(q => q.ExamQuestionId == questionId, cancellationToken)
            : await _context.PracticeAnswers.AnyAsync(q => q.QuestionId == questionId, cancellationToken)
              || await _context.MockExamAnswers.AnyAsync(q => q.QuestionId == questionId, cancellationToken);

    private async Task<string?> ValidateDraftAsync(UpdateQuestionDraftDto? draft, Guid courseId, CancellationToken cancellationToken)
    {
        if (draft is null) return "Thiếu nội dung câu hỏi.";
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(draft.Content)) return "Tiêu đề hoặc nội dung câu hỏi không hợp lệ.";
        if (string.IsNullOrWhiteSpace(draft.SampleAnswer) || draft.SampleAnswer.Trim().Length < 50)
            return "Câu trả lời mẫu phải có ít nhất 50 ký tự.";
        if (draft.Difficulty is not (QuestionDifficulty.Easy or QuestionDifficulty.Medium or QuestionDifficulty.Hard))
            return "Độ khó không hợp lệ.";
        if (draft.BloomLevel is not (BloomLevel.Remember or BloomLevel.Understand or BloomLevel.Apply or
            BloomLevel.Analyze or BloomLevel.Evaluate or BloomLevel.Create)) return "Mức Bloom không hợp lệ.";
        if (draft.KeyPoints is null || draft.Rubric?.Criteria is null || draft.Rubric.Criteria.Count < 2 ||
            string.IsNullOrWhiteSpace(draft.Rubric.Name) ||
            draft.Rubric.TotalMaxScore != 10.00m ||
            draft.Rubric.Criteria.Any(c => string.IsNullOrWhiteSpace(c.CriterionName)) ||
            draft.Rubric.Criteria.Any(c => c.MaxScore <= 0) ||
            draft.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
            return "Barem phải có ít nhất 2 tiêu chí dương và tổng đúng 10.00 điểm.";
        if (draft.CloId.HasValue && !await _context.CourseClos.AnyAsync(
                c => c.Id == draft.CloId.Value && c.CourseId == courseId && c.IsActive, cancellationToken))
            return "CLO không hoạt động hoặc không thuộc môn học.";
        return null;
    }

    private static Rubric BuildRubric(RubricDraftDto draft, Guid courseId)
    {
        var rubric = new Rubric
        {
            CourseId = courseId, Name = draft.Name.Trim(), Description = draft.Description,
            TotalMaxScore = 10.00m, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var order = 1;
        foreach (var criterion in draft.Criteria)
        {
            var weight = criterion.Weight > 1m ? criterion.Weight / 100m : criterion.Weight;
            if (weight <= 0m || weight > 1m) weight = criterion.MaxScore / 10m;
            rubric.Criteria.Add(new RubricCriterion
            {
                CriterionName = criterion.CriterionName, Description = criterion.Description,
                MaxScore = criterion.MaxScore, Weight = Math.Round(weight, 4),
                BloomLevel = criterion.BloomLevel, OrderIndex = order++, CreatedAt = DateTime.UtcNow
            });
        }
        return rubric;
    }
}
