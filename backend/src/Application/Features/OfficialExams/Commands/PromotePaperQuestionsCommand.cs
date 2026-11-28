using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands;

public sealed record PromotePaperQuestionsCommand(Guid ExamSetId, List<Guid>? QuestionIds, Guid ActorId, string? ActorRole) : IRequest<Result<object>>;
public sealed class PromotePaperQuestionsCommandHandler(IApplicationDbContext db) : IRequestHandler<PromotePaperQuestionsCommand, Result<object>>
{
    public Task<Result<object>> Handle(PromotePaperQuestionsCommand c, CancellationToken ct) =>
        db.ExecuteOfficialSemesterBatchAsync(Guid.Empty, token => Promote(c, token), ct);
    private async Task<Result<object>> Promote(PromotePaperQuestionsCommand c, CancellationToken ct)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(db, c.ActorId, c.ActorRole, UserRole.DepartmentHead, ct)) return Fail(OfficialExamErrors.Forbidden);
        var paper = await db.ExamSets.Include(x => x.ExamSetQuestions).ThenInclude(x => x.ExamQuestion).ThenInclude(x => x.Rubric).ThenInclude(x => x.Criteria)
            .SingleOrDefaultAsync(x => x.Id == c.ExamSetId, ct);
        if (paper is null) return Fail(OfficialExamErrors.ExamSetNotFound);
        if (!await OfficialExamAuthorization.OwnsAcademicCourseAsync(db, paper.CourseId, c.ActorId, ct)) return Fail(OfficialExamErrors.Forbidden);
        if (!paper.IsActive || paper.IsSampleSet || paper.Status is not (ExamSetStatus.Official or ExamSetStatus.HandedOver)) return Fail("CONFLICT: Chỉ thêm câu từ bộ đề đã duyệt.");
        if (c.QuestionIds is null || c.QuestionIds.Count == 0 || c.QuestionIds.Distinct().Count() != c.QuestionIds.Count ||
            c.QuestionIds.Any(id => !paper.ExamSetQuestions.Any(q => q.ExamQuestionId == id && !q.IsBankQuestion && q.ExamQuestion.IsPaperCopy)))
            return Fail("VALIDATION: Chỉ chọn câu mới thuộc bộ đề đã duyệt này.");
        var results = new List<object>();
        foreach (var item in paper.ExamSetQuestions.Where(q => c.QuestionIds.Contains(q.ExamQuestionId)))
        {
            var q = item.ExamQuestion;
            var source = item.SourceQuestionId ?? q.OriginQuestionId ?? q.Id;
            var isExcluded = await db.ExamQuestions.AsNoTracking().AnyAsync(x =>
                !x.IsPaperCopy
                && !x.IsDeleted
                && (x.Id == source || x.OriginQuestionId == source)
                && x.BankCurationStatus == BankCurationStatus.ExcludedFromBank, ct);
            if (isExcluded)
                return Fail("CONFLICT: Câu hỏi đã bị loại khỏi kho chọn chung, không thể nạp lại.");
            var existing = await db.ExamQuestions.AsNoTracking()
                .Where(x =>
                    (x.OriginQuestionId == source || x.Id == source)
                    && !x.IsPaperCopy
                    && !x.IsDeleted
                    && x.ApprovalStatus == ApprovalStatus.Approved
                    && (x.BankCurationStatus == BankCurationStatus.BankReady || x.BankCurationStatus == BankCurationStatus.CuratedIntoBank))
                .OrderByDescending(x => x.OriginQuestionId == source)
                .ThenByDescending(x => x.IsActive)
                .FirstOrDefaultAsync(ct);
            if (existing is not null) { results.Add(new { QuestionId = q.Id, BankQuestionId = existing.Id, AlreadyAdded = true }); continue; }
            var now = DateTime.UtcNow;
            var pendingSource = await db.ExamQuestions.FirstOrDefaultAsync(x =>
                x.Id == source && !x.IsPaperCopy && !x.IsDeleted && x.BankCurationStatus == BankCurationStatus.PendingCuration, ct);
            if (pendingSource is not null)
            {
                pendingSource.BankCurationStatus = BankCurationStatus.CuratedIntoBank;
                pendingSource.CuratedBy = c.ActorId;
                pendingSource.CuratedAt = now;
                pendingSource.IsActive = false;
            }
            var rubric = new Rubric { CourseId = q.CourseId, Name = q.Rubric.Name, Description = q.Rubric.Description, TotalMaxScore = q.Rubric.TotalMaxScore,
                Criteria = q.Rubric.Criteria.Select(r => new RubricCriterion { CriterionName = r.CriterionName, Description = r.Description, MaxScore = r.MaxScore,
                    Weight = r.Weight, BloomLevel = r.BloomLevel, OrderIndex = r.OrderIndex }).ToList() };
            var bank = new ExamQuestion { OriginQuestionId = source, CourseId = q.CourseId, CloId = q.CloId, RubricId = rubric.Id, Rubric = rubric,
                Title = q.Title, Content = q.Content, SampleAnswer = q.SampleAnswer, KeyPoints = q.KeyPoints, Difficulty = q.Difficulty, BloomLevel = q.BloomLevel,
                ImageUrl = q.ImageUrl, ImageDescription = q.ImageDescription, Source = q.Source, SubmittedBy = q.SubmittedBy,
                ApprovalStatus = ApprovalStatus.Approved, ApprovedBy = c.ActorId, ApprovedAt = now,
                BankCurationStatus = BankCurationStatus.CuratedIntoBank, CuratedBy = c.ActorId, CuratedAt = now, IsActive = true };
            db.ExamQuestions.Add(bank);
            results.Add(new { QuestionId = q.Id, BankQuestionId = bank.Id, AlreadyAdded = false });
        }
        db.AuditLogs.Add(new AuditLog { UserId = c.ActorId, Action = "PROMOTE_PAPER_QUESTIONS", EntityName = "exam_sets", EntityId = paper.Id,
            NewValues = System.Text.Json.JsonSerializer.Serialize(results) });
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(results);
    }
    private static Result<object> Fail(string error) => Result<object>.Failure(error);
}
