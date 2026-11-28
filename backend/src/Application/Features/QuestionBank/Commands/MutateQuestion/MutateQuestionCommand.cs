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
    string Difficulty, string BloomLevel, RubricDraftDto? Rubric, Guid? CloId,
    string? ImageUrl = null, string? ImageDescription = null,
    Guid? StructureId = null, bool? HasFollowUp = null,
    string? FollowUpPrompt = null, Guid? RubricId = null,
    bool ClearImage = false);

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
            actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead))
            return Result<Guid>.Failure("FORBIDDEN: Không có quyền sửa câu hỏi.");

        var exam = await _context.ExamQuestions.FirstOrDefaultAsync(
            q => q.Id == request.QuestionId && !q.IsDeleted && !q.IsPaperCopy, cancellationToken);
        var practice = exam is null
            ? await _context.PracticeQuestions.FirstOrDefaultAsync(
                q => q.Id == request.QuestionId && !q.IsDeleted, cancellationToken)
            : null;
        if (exam is null && practice is null)
            return Result<Guid>.Failure("NOT_FOUND: Không tìm thấy câu hỏi.");

        var courseId = exam is not null ? exam.CourseId : practice!.CourseId;
        var ownerId = exam is not null ? exam.SubmittedBy : practice!.SubmittedBy;
        var status = exam is not null ? exam.ApprovalStatus : practice!.ApprovalStatus;
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
                var questionSource = exam is not null ? exam.Source : practice!.Source;
                var (error, existingRubric) = await ValidateDraftAsync(
                    request.Draft,
                    courseId,
                    questionSource,
                    exam is not null,
                    cancellationToken);
                if (error is not null) return Result<Guid>.Failure($"VALIDATION: {error}");
                var draft = request.Draft!;
                Rubric rubric;
                if (existingRubric is not null)
                {
                    rubric = existingRubric;
                }
                else
                {
                    rubric = BuildRubric(draft.Rubric!, courseId);
                    _context.Rubrics.Add(rubric);
                }

                if (exam is not null)
                {
                    exam.Title = draft.Title.Trim();
                    exam.Content = draft.Content.Trim();
                    exam.SampleAnswer = draft.SampleAnswer.Trim();
                    exam.KeyPoints = JsonSerializer.Serialize(draft.KeyPoints);
                    exam.Difficulty = draft.Difficulty;
                    exam.BloomLevel = draft.BloomLevel;
                    exam.CloId = draft.CloId;
                    if (draft.StructureId.HasValue)
                    {
                        exam.StructureId = draft.StructureId.Value == Guid.Empty ? null : draft.StructureId.Value;
                    }
                    ApplyImageUpdates(
                        draft,
                        v => exam.ImageUrl = v,
                        v => exam.ImageDescription = v);
                    exam.RubricId = rubric.Id;
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
                    if (draft.HasFollowUp.HasValue)
                    {
                        practice.HasFollowUp = draft.HasFollowUp.Value;
                    }
                    if (draft.FollowUpPrompt is not null)
                    {
                        practice.FollowUpPrompt = string.IsNullOrWhiteSpace(draft.FollowUpPrompt)
                            ? null
                            : draft.FollowUpPrompt.Trim();
                    }
                    ApplyImageUpdates(
                        draft,
                        v => practice.ImageUrl = v,
                        v => practice.ImageDescription = v);
                    practice.RubricId = rubric.Id;
                    practice.Rubric = rubric;
                    practice.ApprovalStatus = ApprovalStatus.Draft;
                    practice.IsActive = false;
                }
                break;

            case QuestionMutationKind.SetActive:
                if (actor.Role == UserRole.Lecturer)
                    return Result<Guid>.Failure("FORBIDDEN: Chỉ Trưởng bộ môn được bật/tắt câu hỏi.");
                if (status != ApprovalStatus.Approved || request.Active is null)
                    return Result<Guid>.Failure("CONFLICT: Chỉ câu hỏi đã duyệt được bật/tắt.");
                if (exam is not null)
                {
                    if (request.Active.Value &&
                        exam.BankCurationStatus is BankCurationStatus.PendingCuration or BankCurationStatus.ExcludedFromBank)
                    {
                        return Result<Guid>.Failure(
                            "CONFLICT: Câu hỏi đang chờ tổng hợp lọc kho hoặc đã bị loại khỏi ngân hàng đề nên không thể kích hoạt trực tiếp.");
                    }
                    if (request.Active.Value && await _context.ExamQuestions.AsNoTracking().AnyAsync(
                        promoted => !promoted.IsDeleted && !promoted.IsPaperCopy && promoted.OriginQuestionId == exam.Id,
                        cancellationToken))
                    {
                        return Result<Guid>.Failure(
                            "CONFLICT: Câu hỏi gốc này đã được nạp thành bản chính thức trong ngân hàng đề; hãy thao tác trực tiếp trên bản ngân hàng.");
                    }
                    exam.IsActive = request.Active.Value;
                }
                else practice!.IsActive = request.Active.Value;
                break;

            case QuestionMutationKind.SoftDelete:
                if (status != ApprovalStatus.Draft)
                    return Result<Guid>.Failure("CONFLICT: Chỉ thu hồi câu hỏi nháp chưa sử dụng.");
                var hasUsage = await HasQuestionUsageAsync(request.QuestionId, exam is not null, cancellationToken, forDeletion: true);
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

    private async Task<bool> HasQuestionUsageAsync(
        Guid questionId,
        bool isExam,
        CancellationToken cancellationToken,
        bool forDeletion = false) =>
        isExam
            ? (forDeletion
                ? await _context.ExamSetQuestions.AnyAsync(
                    esq => (esq.ExamQuestionId == questionId || esq.SourceQuestionId == questionId)
                        && esq.ExamSet.IsActive,
                    cancellationToken)
                : await _context.ExamSetQuestions.AnyAsync(
                    esq => (esq.ExamQuestionId == questionId || esq.SourceQuestionId == questionId)
                        && esq.ExamSet.IsActive
                        && esq.ExamSet.Status != ExamSetStatus.Draft
                        && esq.ExamSet.Status != ExamSetStatus.Rejected,
                    cancellationToken))
              || await _context.ExamQuestionSubmissions.AnyAsync(q => q.ExamQuestionId == questionId, cancellationToken)
            : await _context.PracticeAnswers.AnyAsync(q => q.QuestionId == questionId, cancellationToken)
              || await _context.MockExamAnswers.AnyAsync(q => q.QuestionId == questionId, cancellationToken)
              || await _context.InClassExamSubmissions.AnyAsync(s => s.QuestionId == questionId, cancellationToken);

    private static void ApplyImageUpdates(
        UpdateQuestionDraftDto draft,
        Action<string?> setImageUrl,
        Action<string?> setImageDescription)
    {
        if (draft.ClearImage)
        {
            setImageUrl(null);
            setImageDescription(null);
            return;
        }

        if (draft.ImageUrl is not null)
        {
            var trimmedUrl = draft.ImageUrl.Trim();
            if (trimmedUrl.Length == 0)
            {
                setImageUrl(null);
                setImageDescription(null);
                return;
            }

            setImageUrl(trimmedUrl);
        }

        if (draft.ImageDescription is not null)
        {
            setImageDescription(string.IsNullOrWhiteSpace(draft.ImageDescription)
                ? null
                : draft.ImageDescription.Trim());
        }
    }

    private async Task<(string? Error, Rubric? ExistingRubric)> ValidateDraftAsync(
        UpdateQuestionDraftDto? draft,
        Guid courseId,
        string? questionSource,
        bool isExamQuestion,
        CancellationToken cancellationToken)
    {
        if (draft is null) return ("Thiếu nội dung câu hỏi.", null);
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(draft.Content)) return ("Tiêu đề hoặc nội dung câu hỏi không hợp lệ.", null);
        if (string.IsNullOrWhiteSpace(draft.SampleAnswer) || draft.SampleAnswer.Trim().Length < 50)
            return ("Câu trả lời mẫu phải có ít nhất 50 ký tự.", null);
        if (draft.Difficulty is not (QuestionDifficulty.Easy or QuestionDifficulty.Medium or QuestionDifficulty.Hard))
            return ("Độ khó không hợp lệ.", null);
        if (draft.BloomLevel is not (BloomLevel.Remember or BloomLevel.Understand or BloomLevel.Apply or
            BloomLevel.Analyze or BloomLevel.Evaluate or BloomLevel.Create)) return ("Mức Bloom không hợp lệ.", null);
        if (draft.KeyPoints is null)
            return ("Danh sách ý chính (KeyPoints) không được để trống.", null);

        var normalizedSource = (questionSource ?? QuestionSource.Manual).Trim().ToLowerInvariant();
        if (!draft.ClearImage && (!string.IsNullOrWhiteSpace(draft.ImageUrl) || !string.IsNullOrWhiteSpace(draft.ImageDescription)))
        {
            if (normalizedSource != QuestionSource.Manual)
            {
                return ("Chỉ câu hỏi do giảng viên tạo thủ công (manual) mới được phép đính kèm hình ảnh hoặc mô tả hình ảnh.", null);
            }
        }

        if (!draft.ClearImage && !string.IsNullOrWhiteSpace(draft.ImageUrl))
        {
            if (!QuestionImageUrlValidator.IsSafeImageUrl(draft.ImageUrl.Trim()))
            {
                return (QuestionImageUrlValidator.InvalidImageUrlError, null);
            }
        }

        if (draft.ImageDescription is not null && draft.ImageDescription.Trim().Length > 2000)
            return ("Mô tả hình ảnh câu hỏi không được vượt quá 2000 ký tự.", null);

        if (isExamQuestion && draft.StructureId.HasValue && draft.StructureId.Value != Guid.Empty &&
            !await _context.ExamStructures.AsNoTracking().AnyAsync(
                s => s.Id == draft.StructureId.Value && s.CourseId == courseId && s.IsActive, cancellationToken))
        {
            return ("Cấu trúc đề (ExamStructure) không tồn tại, không hoạt động hoặc không thuộc môn học của câu hỏi.", null);
        }

        if (draft.CloId.HasValue && !await _context.CourseClos.AnyAsync(
                c => c.Id == draft.CloId.Value && c.CourseId == courseId && c.IsActive, cancellationToken))
            return ("CLO không hoạt động hoặc không thuộc môn học.", null);

        if (draft.Rubric is null && draft.RubricId.HasValue && draft.RubricId.Value != Guid.Empty)
        {
            var existingRubric = await _context.Rubrics
                .Include(r => r.Criteria)
                .FirstOrDefaultAsync(
                    r => r.Id == draft.RubricId.Value
                        && r.CourseId == courseId
                        && r.IsActive
                        && !_context.ExamQuestions.Any(q => q.IsPaperCopy && q.RubricId == r.Id),
                    cancellationToken);

            if (existingRubric is null)
                return ("Barem Rubric không tồn tại, đã ngừng hoạt động hoặc không thuộc môn học này.", null);

            if (existingRubric.Criteria is null || existingRubric.Criteria.Count < 2 ||
                existingRubric.Criteria.Any(c => c is null || string.IsNullOrWhiteSpace(c.CriterionName) || c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength) ||
                existingRubric.Criteria.Any(c => c.MaxScore <= 0) ||
                existingRubric.Criteria.Any(c => !RubricNormalization.HasValidScorePrecision(c.MaxScore)) ||
                existingRubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
            {
                return ("Barem Rubric được chọn không đạt chuẩn 10.00 điểm (tối thiểu 2 tiêu chí dương, tối đa 2 chữ số thập phân).", null);
            }

            return (null, existingRubric);
        }

        if (draft.Rubric?.Criteria is null || draft.Rubric.Criteria.Count < 2 ||
            string.IsNullOrWhiteSpace(draft.Rubric.Name) ||
            draft.Rubric.Name.Trim().Length > RubricNormalization.MaxNameLength ||
            draft.Rubric.TotalMaxScore != 10.00m ||
            draft.Rubric.Criteria.Any(c => c is null || string.IsNullOrWhiteSpace(c.CriterionName) || c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength) ||
            draft.Rubric.Criteria.Any(c => c.MaxScore <= 0) ||
            draft.Rubric.Criteria.Any(c => !RubricNormalization.HasValidScorePrecision(c.MaxScore)) ||
            draft.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
            return ("Barem phải có ít nhất 2 tiêu chí dương (tên <= 200 ký tự, tối đa 2 chữ số thập phân) và tổng đúng 10.00 điểm.", null);

        foreach (var criterion in draft.Rubric.Criteria)
        {
            if (string.IsNullOrWhiteSpace(criterion.BloomLevel))
            {
                continue;
            }

            var trimmedBloom = criterion.BloomLevel.Trim();
            if (trimmedBloom.Length > 20 ||
                !BloomLevel.All.Any(b => string.Equals(b, trimmedBloom, StringComparison.OrdinalIgnoreCase)))
            {
                return ($"Mức độ nhận thức Bloom '{trimmedBloom}' của tiêu chí không hợp lệ. Chỉ chấp nhận: {string.Join(", ", BloomLevel.All)}.", null);
            }
        }

        return (null, null);
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
            var normalizedWeight = RubricNormalization.NormalizeWeight(criterion.Weight, criterion.MaxScore);
            var normalizedBloom = string.IsNullOrWhiteSpace(criterion.BloomLevel)
                ? null
                : BloomLevel.All.FirstOrDefault(b =>
                    string.Equals(b, criterion.BloomLevel.Trim(), StringComparison.OrdinalIgnoreCase));
            rubric.Criteria.Add(new RubricCriterion
            {
                CriterionName = criterion.CriterionName.Trim(), Description = criterion.Description,
                MaxScore = criterion.MaxScore, Weight = normalizedWeight,
                BloomLevel = normalizedBloom, OrderIndex = order++, CreatedAt = DateTime.UtcNow
            });
        }
        return rubric;
    }
}
