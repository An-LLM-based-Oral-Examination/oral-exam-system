using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Common;

public static class PaperBankRules
{
    public static async Task<List<BankPromotionCandidate>> Candidates(IApplicationDbContext db, Guid paperId, CancellationToken ct)
    {
        var paper = await db.ExamSets.AsNoTracking().Include(x => x.ExamSetQuestions).ThenInclude(x => x.ExamQuestion).SingleAsync(x => x.Id == paperId, ct);
        if (paper.Status is not (ExamSetStatus.Official or ExamSetStatus.HandedOver)) return [];
        var bank = await db.ExamQuestions.AsNoTracking().Where(x => x.CourseId == paper.CourseId && !x.IsPaperCopy && x.IsActive && !x.IsDeleted &&
            x.ApprovalStatus == ApprovalStatus.Approved && (x.BankCurationStatus == BankCurationStatus.BankReady || x.BankCurationStatus == BankCurationStatus.CuratedIntoBank))
            .Select(x => new { x.Id, x.OriginQuestionId, x.Title, x.Content }).ToListAsync(ct);
        var indexed = bank.Select(x => new { Row = x, Grams = TrigramSimilarityCalculator.ExtractTrigrams(TrigramSimilarityCalculator.ComposeQuestionText(x.Title, x.Content)) }).ToList();
        var result = new List<BankPromotionCandidate>();
        foreach (var q in paper.ExamSetQuestions.Where(x => !x.IsBankQuestion))
        {
            var source = q.SourceQuestionId ?? q.ExamQuestion.OriginQuestionId ?? q.ExamQuestionId;
            // Also exclude a previously published source whose bank entry has since been disabled.
            if (bank.Any(x => (x.OriginQuestionId ?? x.Id) == source) || await db.ExamQuestions.AsNoTracking().AnyAsync(x =>
                    x.OriginQuestionId == source || (x.Id == source && x.BankCurationStatus == BankCurationStatus.ExcludedFromBank), ct)) continue;
            var grams = TrigramSimilarityCalculator.ExtractTrigrams(TrigramSimilarityCalculator.ComposeQuestionText(q.ExamQuestion.Title, q.ExamQuestion.Content));
            var match = indexed.Select(x => new { x.Row, Percent = TrigramSimilarityCalculator.ToSimilarityPercent(TrigramSimilarityCalculator.CalculateSimilarity(grams, x.Grams)) })
                .OrderByDescending(x => x.Percent).ThenBy(x => x.Row.Id).FirstOrDefault();
            var percent = match?.Percent ?? 0;
            result.Add(new(q.ExamQuestionId, source, q.ExamQuestion.Title, percent, ExamSimilarityRules.Band(percent),
                percent >= 85 ? "DO_NOT_ADD_RECOMMENDED" : percent >= 70 ? "PREFER_EXISTING" : percent >= 50 ? "REVIEW_MATCH" : "CAN_ADD",
                match?.Row.Id, match?.Row.Title, match?.Row.Content));
        }
        return result;
    }
}
