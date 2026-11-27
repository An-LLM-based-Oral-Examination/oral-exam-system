using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Common;

public static class ExamSimilarityRules
{
    public static string Band(decimal percent) => percent >= 85 ? "VERY_HIGH" : percent >= 70 ? "HIGH" : percent >= 50 ? "MODERATE" : "LOW";

    public static async Task<PaperSimilarityReport> Report(IApplicationDbContext db, Guid id, CancellationToken ct)
    {
        var paper = await db.ExamSets.AsNoTracking().Include(x => x.Session).ThenInclude(x => x!.Semester)
            .Include(x => x.ExamSetQuestions).ThenInclude(x => x.ExamQuestion).SingleAsync(x => x.Id == id, ct);
        var current = paper.ExamSetQuestions.OrderBy(x => x.OrderIndex).ThenBy(x => x.ExamQuestionId).Select(q => new {
            q.ExamQuestionId, q.OrderIndex, Source = q.SourceQuestionId ?? q.ExamQuestion.OriginQuestionId ?? q.ExamQuestionId,
            Grams = TrigramSimilarityCalculator.ExtractTrigrams(q.ExamQuestion.Content),
            q.ExamQuestion.SampleAnswer
        }).ToList();
        var empty = new PaperSimilarityReport(id, 0, current.Select(q => new PaperQuestionSimilarity(q.ExamQuestionId, q.OrderIndex, 0, Band(0), [])).ToList());
        if (paper.IsSampleSet || paper.Session?.Semester is null || current.Count == 0) return empty;

        var previous = await db.OfficialExamSessions.AsNoTracking()
            .Where(x => x.CourseId == paper.CourseId && x.Status != "cancelled" && x.Semester != null && x.Semester.StartDate < paper.Session.Semester.StartDate)
            .OrderByDescending(x => x.Semester!.StartDate).Select(x => (Guid?)x.SemesterId).FirstOrDefaultAsync(ct);
        var semesterId = paper.Session.SemesterId;
        var comparisons = await db.ExamSetQuestions.AsNoTracking().Where(x => x.ExamSetId != id && x.ExamSet.CourseId == paper.CourseId &&
            x.ExamSet.IsActive && !x.ExamSet.IsSampleSet && x.ExamSet.Session != null && x.ExamSet.Session.Status != "cancelled" &&
            (x.ExamSet.Session.SemesterId == semesterId || previous != null && x.ExamSet.Session.SemesterId == previous) &&
            (x.ExamSet.Status == ExamSetStatus.Official || x.ExamSet.Status == ExamSetStatus.HandedOver))
            .Select(x => new { Source = x.SourceQuestionId ?? x.ExamQuestion.OriginQuestionId ?? x.ExamQuestionId,
                x.ExamQuestionId, x.OrderIndex, x.ExamSetId, x.ExamSet.SetCode, SessionId = x.ExamSet.SessionId!.Value,
                SessionTitle = x.ExamSet.Session!.Title, SemesterCode = x.ExamSet.Session!.Semester!.Code,
                x.ExamQuestion.Title, x.ExamQuestion.Content, x.ExamQuestion.SampleAnswer }).ToListAsync(ct);
        var indexed = comparisons.Select(x => new { Row = x, Grams = TrigramSimilarityCalculator.ExtractTrigrams(x.Content) }).ToList();
        var answerGrams = new Dictionary<Guid, HashSet<string>>();

        PaperSimilarityReport? best = null;
        decimal bestScore = -1;
        // Stable order resolves equal scores without returning multiple papers.
        foreach (var group in indexed.GroupBy(x => x.Row.ExamSetId).OrderBy(x => x.First().Row.SetCode, StringComparer.Ordinal).ThenBy(x => x.Key))
        {
            ct.ThrowIfCancellationRequested();
            var other = group.OrderBy(x => x.Row.OrderIndex).ThenBy(x => x.Row.ExamQuestionId).ToList();
            var size = Math.Max(current.Count, other.Count);
            var weights = new decimal[size, size];
            for (var i = 0; i < current.Count; i++)
            for (var j = 0; j < other.Count; j++)
                weights[i, j] = QuestionPercent(current[i].Grams, other[j].Grams,
                    () => Answer(current[i].ExamQuestionId, current[i].SampleAnswer),
                    () => Answer(other[j].Row.ExamQuestionId, other[j].Row.SampleAnswer));
            var assignment = MaximumWeightMatching(weights, ct);
            var score = Enumerable.Range(0, size).Sum(i => weights[i, assignment[i]]) / size;
            if (score <= bestScore) continue;
            var questions = current.Select((q, i) =>
            {
                var j = assignment[i];
                if (j >= other.Count) return new PaperQuestionSimilarity(q.ExamQuestionId, q.OrderIndex, 0, Band(0), []);
                var row = other[j].Row;
                var percent = weights[i, j];
                return new PaperQuestionSimilarity(q.ExamQuestionId, q.OrderIndex, percent, Band(percent),
                    [new(row.ExamQuestionId, row.ExamSetId, row.SetCode, row.SessionTitle, row.SemesterCode,
                        row.Title, row.Content, percent, q.Source == row.Source, row.OrderIndex)]);
            }).ToList();
            var first = other[0].Row;
            best = new(id, Math.Round(score, 1, MidpointRounding.AwayFromZero), questions,
                new(first.ExamSetId, first.SetCode, first.SessionId, first.SessionTitle, first.SemesterCode, other.Count));
            bestScore = score;
        }
        return best ?? empty;

        HashSet<string> Answer(Guid questionId, string? text)
        {
            if (!answerGrams.TryGetValue(questionId, out var grams))
                answerGrams[questionId] = grams = TrigramSimilarityCalculator.ExtractTrigrams(text);
            return grams;
        }
    }

    // Only ambiguous content needs answer evidence. Answers are evaluated lazily and cached per report.
    private static decimal QuestionPercent(IReadOnlySet<string> left, IReadOnlySet<string> right,
        Func<IReadOnlySet<string>> leftAnswer, Func<IReadOnlySet<string>> rightAnswer)
    {
        var content = TrigramSimilarityCalculator.CalculateSimilarity(left, right);
        if (content < 0.50m || content >= 0.85m)
            return TrigramSimilarityCalculator.ToSimilarityPercent(content);
        var a = leftAnswer(); var b = rightAnswer();
        if (a.Count == 0 || b.Count == 0)
            return TrigramSimilarityCalculator.ToSimilarityPercent(content);
        return TrigramSimilarityCalculator.ToSimilarityPercent(
            content * 0.70m + TrigramSimilarityCalculator.CalculateSimilarity(a, b) * 0.30m);
    }

    // Hungarian assignment: O(n^3), maximum total similarity, one match per question.
    // Zero-weight padding accounts for different lengths without reusing a question.
    private static int[] MaximumWeightMatching(decimal[,] weights, CancellationToken ct)
    {
        var n = weights.GetLength(0);
        var u = new decimal[n + 1]; var v = new decimal[n + 1];
        var p = new int[n + 1]; var way = new int[n + 1];
        for (var i = 1; i <= n; i++)
        {
            ct.ThrowIfCancellationRequested();
            p[0] = i;
            var j0 = 0;
            var min = Enumerable.Repeat(decimal.MaxValue, n + 1).ToArray();
            var used = new bool[n + 1];
            do
            {
                used[j0] = true;
                var i0 = p[j0]; var delta = decimal.MaxValue; var j1 = 0;
                for (var j = 1; j <= n; j++)
                {
                    if (used[j]) continue;
                    var cost = -weights[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cost < min[j]) { min[j] = cost; way[j] = j0; }
                    if (min[j] < delta) { delta = min[j]; j1 = j; }
                }
                for (var j = 0; j <= n; j++)
                    if (used[j]) { u[p[j]] += delta; v[j] -= delta; }
                    else if (j > 0) min[j] -= delta;
                j0 = j1;
            } while (p[j0] != 0);
            do { var j1 = way[j0]; p[j0] = p[j1]; j0 = j1; } while (j0 != 0);
        }
        var result = new int[n];
        for (var j = 1; j <= n; j++) result[p[j] - 1] = j - 1;
        return result;
    }
}
