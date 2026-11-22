using System;
using System.Collections.Generic;
using System.Linq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Common;

/// <summary>
/// Aggregates official exam ticket scores across main questions and optional follow-up turns (1..5),
/// computing each main question's effective score via <see cref="FollowUpPromptBuilder.ComputeEffectiveQuestionScore"/>
/// and dividing strictly by the number of main questions (never by main + follow-up row count).
/// </summary>
public static class OfficialExamScoreAggregator
{
    public static decimal? ComputeTicketAiScore(IEnumerable<ExamQuestionSubmission>? submissions)
    {
        if (submissions is null)
        {
            return null;
        }

        var list = submissions.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var groups = GroupByMainQuestion(list);
        if (groups.Count == 0)
        {
            return null;
        }

        var hasAnyScore = list.Any(s => s.AiScore.HasValue || (s.AiEvaluations is { Count: > 0 }));
        if (!hasAnyScore)
        {
            return null;
        }

        decimal sum = 0m;
        foreach (var group in groups)
        {
            sum += ComputeQuestionEffectiveScore(group.Main, group.FollowUps, useFinalScore: false);
        }

        return Math.Round(sum / groups.Count, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal? ComputeTicketFinalScore(IEnumerable<ExamQuestionSubmission>? submissions)
    {
        if (submissions is null)
        {
            return null;
        }

        var list = submissions.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var groups = GroupByMainQuestion(list);
        if (groups.Count == 0)
        {
            return null;
        }

        var hasAnyScore = list.Any(s => s.FinalScore.HasValue || s.AiScore.HasValue || (s.AiEvaluations is { Count: > 0 }));
        if (!hasAnyScore)
        {
            return null;
        }

        decimal sum = 0m;
        foreach (var group in groups)
        {
            sum += ComputeQuestionEffectiveScore(group.Main, group.FollowUps, useFinalScore: true);
        }

        return Math.Round(sum / groups.Count, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ComputeQuestionEffectiveScore(
        ExamQuestionSubmission? main,
        IReadOnlyList<ExamQuestionSubmission> followUps,
        bool useFinalScore)
    {
        // When an examiner explicitly overrode the main submission's FinalScore (differs from AiScore) and there is no follow-up override,
        // or when there are no follow-ups at all, respect the direct score cleanly.
        if (followUps.Count == 0)
        {
            return ResolveSubmissionScore(main, useFinalScore) ?? 0m;
        }

        if (useFinalScore && main is not null && main.FinalScore.HasValue)
        {
            var mainWasManuallyOverridden = !main.AiScore.HasValue || main.FinalScore.Value != main.AiScore.Value;
            var anyFollowUpOverridden = followUps.Any(fu => fu.FinalScore.HasValue && (!fu.AiScore.HasValue || fu.FinalScore.Value != fu.AiScore.Value));
            if (mainWasManuallyOverridden && !anyFollowUpOverridden)
            {
                return Math.Clamp(Math.Round(main.FinalScore.Value, 2, MidpointRounding.AwayFromZero), 0m, 10m);
            }
        }

        var mainEval = ToEvaluation(main, useFinalScore);
        var followUpEvals = followUps
            .Select(fu => ToEvaluation(fu, useFinalScore))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        return FollowUpPromptBuilder.ComputeEffectiveQuestionScore(mainEval, followUpEvals);
    }

    public static IReadOnlyList<(Guid ExamQuestionId, ExamQuestionSubmission? Main, IReadOnlyList<ExamQuestionSubmission> FollowUps)> GroupByMainQuestion(
        IEnumerable<ExamQuestionSubmission> submissions)
    {
        var result = new List<(Guid, ExamQuestionSubmission?, IReadOnlyList<ExamQuestionSubmission>)>();
        foreach (var group in submissions
            .GroupBy(s => s.ExamQuestionId)
            .OrderBy(g => g.Min(s => s.SubmittedAt)))
        {
            var ordered = group
                .OrderBy(s => s.FollowUpIndex)
                .ThenBy(s => s.SubmittedAt)
                .ToList();

            var main = ordered.FirstOrDefault(s => !s.IsFollowUp && s.FollowUpIndex == 0)
                ?? ordered.FirstOrDefault(s => !s.IsFollowUp);

            var followUps = ordered
                .Where(s => main is null || s.Id != main.Id)
                .OrderBy(s => s.FollowUpIndex)
                .ThenBy(s => s.SubmittedAt)
                .ToList();

            result.Add((group.Key, main, followUps));
        }

        return result;
    }

    private static decimal? ResolveSubmissionScore(ExamQuestionSubmission? submission, bool useFinalScore)
    {
        if (submission is null)
        {
            return null;
        }

        if (useFinalScore && submission.FinalScore.HasValue)
        {
            return submission.FinalScore.Value;
        }

        if (submission.AiScore.HasValue)
        {
            return submission.AiScore.Value;
        }

        var latestEval = submission.AiEvaluations?
            .OrderByDescending(e => e.EvaluatedAt)
            .FirstOrDefault();

        return latestEval?.TotalScore;
    }

    private static AiEvaluation? ToEvaluation(ExamQuestionSubmission? submission, bool useFinalScore)
    {
        if (submission is null)
        {
            return null;
        }

        var latestEval = submission.AiEvaluations?
            .OrderByDescending(e => e.EvaluatedAt)
            .FirstOrDefault();

        var resolvedScore = ResolveSubmissionScore(submission, useFinalScore);
        if (!resolvedScore.HasValue)
        {
            return latestEval;
        }

        if (latestEval is not null && latestEval.TotalScore == resolvedScore.Value)
        {
            return latestEval;
        }

        return new AiEvaluation
        {
            TotalScore = resolvedScore.Value,
            Details = latestEval?.Details ?? new List<AiEvaluationDetail>()
        };
    }
}
