using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Common;

public sealed record QuestionUsage(Guid SourceQuestionId, int UsedCount, int MaxUses)
{
    public int RemainingUses => Math.Max(0, MaxUses - UsedCount);
    public bool CanSelect => RemainingUses > 0;
}

public static class ExamReuseRules
{
    public static int Limit(int requiredSets, int percent) => Math.Max(1, (int)Math.Ceiling(requiredSets * (decimal)percent / 100));

    public static async Task<Dictionary<Guid, int>> Counts(IApplicationDbContext db, Guid sessionId, CancellationToken ct,
        Guid? draftOwnerId = null, Guid? excludeDraftId = null)
    {
        var rows = await db.ExamSetQuestions.AsNoTracking().Where(x => x.ExamSet.SessionId == sessionId &&
            x.ExamSet.IsActive && !x.ExamSet.IsSampleSet &&
            (x.ExamSet.Status == ExamSetStatus.SubmittedForReview || x.ExamSet.Status == ExamSetStatus.Official || x.ExamSet.Status == ExamSetStatus.HandedOver ||
                draftOwnerId != null && x.ExamSet.Status == ExamSetStatus.Draft && x.ExamSet.CreatedBy == draftOwnerId &&
                (excludeDraftId == null || x.ExamSetId != excludeDraftId)))
            .Select(x => new { Source = x.SourceQuestionId ?? x.ExamQuestion.OriginQuestionId ?? x.ExamQuestionId, x.ExamSetId }).Distinct().ToListAsync(ct);
        return rows.GroupBy(x => x.Source).ToDictionary(x => x.Key, x => x.Count());
    }

    // Call inside the same advisory-locked transaction as the state transition.
    public static async Task<string?> ValidateSubmission(IApplicationDbContext db, IReadOnlyList<ExamSet> papers, CancellationToken ct)
    {
        foreach (var group in papers.GroupBy(x => x.SessionId))
        {
            var preparation = await db.ExamPreparations.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == group.Key, ct);
            if (preparation is null) continue;
            var counts = await Counts(db, preparation.SessionId, ct);
            var limit = Limit(preparation.RequiredSetCount, preparation.ReusePercent);
            var additions = group.SelectMany(p => p.ExamSetQuestions.Select(q => new {
                Source = q.SourceQuestionId ?? q.ExamQuestion.OriginQuestionId ?? q.ExamQuestionId, p.Id, p.SetCode, q.ExamQuestion.Title }))
                .DistinctBy(x => (x.Source, x.Id)).GroupBy(x => x.Source);
            foreach (var addition in additions)
                if (counts.GetValueOrDefault(addition.Key) + addition.Count() > limit)
                    return $"CONFLICT: Câu '{addition.First().Title}' có trong các bộ đề đang gửi: {string.Join(", ", addition.Select(x => x.SetCode).OrderBy(x => x, StringComparer.Ordinal))}. Đã dùng {counts.GetValueOrDefault(addition.Key)}/{limit} đề; gửi thêm {addition.Count()} đề sẽ vượt giới hạn. Vui lòng thay câu trong các bộ đề trên.";

            if (limit > 1)
            {
                foreach (var paper in group)
                {
                    var paperSourceIds = paper.ExamSetQuestions
                        .Select(q => q.SourceQuestionId ?? q.ExamQuestion.OriginQuestionId ?? q.ExamQuestionId)
                        .Distinct()
                        .ToList();
                    if (paperSourceIds.Count == 0) continue;

                    var otherPendingIds = (await db.ExamSetQuestions.AsNoTracking()
                        .Where(esq =>
                            esq.ExamSet.SessionId == preparation.SessionId
                            && esq.ExamSetId != paper.Id
                            && esq.ExamSet.IsActive
                            && !esq.ExamSet.IsSampleSet
                            && esq.ExamSet.Status == ExamSetStatus.SubmittedForReview
                            && esq.ExamSet.CreatedBy.HasValue
                            && paper.CreatedBy.HasValue
                            && esq.ExamSet.CreatedBy.Value != paper.CreatedBy.Value)
                        .Select(esq => esq.SourceQuestionId ?? esq.ExamQuestion.OriginQuestionId ?? esq.ExamQuestionId)
                        .Distinct()
                        .ToListAsync(ct))
                        .ToHashSet();

                    var yellowCount = paperSourceIds.Count(otherPendingIds.Contains);
                    var yellowRatio = (decimal)yellowCount / paperSourceIds.Count;
                    if (yellowRatio > 0.70m)
                    {
                        var ratioPercent = Math.Round(yellowRatio * 100m, 1, MidpointRounding.AwayFromZero);
                        return $"UNPROCESSABLE: Tỷ lệ câu hỏi trùng chờ duyệt với giảng viên khác là {ratioPercent:0.#}% ({yellowCount}/{paperSourceIds.Count}), vượt quá ngưỡng tối đa 70%. Vui lòng thay thế câu hỏi trước khi nộp duyệt.";
                    }
                }
            }
        }
        return null;
    }
}
