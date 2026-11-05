using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Practice.Common;

public static class PracticeSessionTimeoutHelper
{
    public static async Task<int> GetTimeoutMinutesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var timeoutConfig = await context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "SessionInactivityTimeoutMinutes", cancellationToken);

        return timeoutConfig != null &&
               int.TryParse(timeoutConfig.Value, out var parsedTimeout) &&
               parsedTimeout > 0
            ? parsedTimeout
            : 5;
    }

    public static decimal? ComputeSessionTotalScore(
        PracticeSession session,
        ICollection<PracticeAnswer>? answersOverride = null)
    {
        var answers = answersOverride ?? session.Answers ?? Array.Empty<PracticeAnswer>();
        bool hasPendingAnswers = answers.Any(a =>
            string.Equals(a.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Status, "grading", StringComparison.OrdinalIgnoreCase));

        if (hasPendingAnswers)
        {
            return null;
        }

        var isFullSession = string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase);
        var isCompleted = string.Equals(session.Status, "completed", StringComparison.OrdinalIgnoreCase);
        var distinctMainCount = answers
            .Where(a => !a.IsFollowUp)
            .Select(a => a.QuestionId)
            .Distinct()
            .Count();
        var totalQuestions = Math.Max(session.SelectedQuestionIds.Count, distinctMainCount);

        var mainScores = answers
            .Where(a => a.AiEvaluations != null && a.AiEvaluations.Any())
            .GroupBy(a => a.QuestionId)
            .Select(g =>
            {
                var mainAnswerEval = g
                    .Where(a => !a.IsFollowUp)
                    .SelectMany(a => a.AiEvaluations)
                    .OrderByDescending(e => e.EvaluatedAt)
                    .FirstOrDefault();
                var followUpEvals = g
                    .Where(a => a.IsFollowUp)
                    .Select(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault())
                    .Where(e => e != null)
                    .Cast<AiEvaluation>()
                    .ToList();

                return FollowUpPromptBuilder.ComputeEffectiveQuestionScore(mainAnswerEval, followUpEvals);
            })
            .ToList();

        if (isFullSession && isCompleted && totalQuestions > 0)
        {
            if (mainScores.Count > 0)
            {
                return Math.Clamp(
                    Math.Round(mainScores.Sum() / totalQuestions, 2, MidpointRounding.AwayFromZero),
                    0.0m,
                    10.0m);
            }

            return session.SelectedQuestionIds.Count > 0 ? 0.0m : null;
        }

        var fallbackScores = mainScores.Count > 0
            ? mainScores
            : answers
                .Where(a => a.AiEvaluations != null && a.AiEvaluations.Any())
                .Select(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).First().TotalScore)
                .ToList();

        return fallbackScores.Count > 0
            ? Math.Clamp(
                Math.Round(fallbackScores.Average(), 2, MidpointRounding.AwayFromZero),
                0.0m,
                10.0m)
            : null;
    }

    public static async Task<bool> PopulateMissingFullSessionAnswersAsync(
        IApplicationDbContext context,
        PracticeSession session,
        DateTime submittedAtUtc,
        string cotReason,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase) ||
            session.SelectedQuestionIds.Count == 0)
        {
            return false;
        }

        ICollection<PracticeAnswer> existingAnswers = session.Answers;
        if (existingAnswers == null || existingAnswers.Count == 0)
        {
            existingAnswers = await context.PracticeAnswers
                .AsNoTracking()
                .Where(a => a.SessionId == session.Id)
                .ToListAsync(cancellationToken);
        }

        var answeredQuestionIds = existingAnswers
            .Select(a => a.QuestionId)
            .ToHashSet();

        var missingQuestionIds = session.SelectedQuestionIds
            .Where(qid => qid != Guid.Empty && !answeredQuestionIds.Contains(qid))
            .Distinct()
            .ToList();

        if (missingQuestionIds.Count == 0)
        {
            return false;
        }

        var skippedQuestionsWithRubrics = await context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => missingQuestionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, cancellationToken);

        session.Answers ??= new List<PracticeAnswer>();

        foreach (var missingQid in missingQuestionIds)
        {
            var selectedIdx = session.SelectedQuestionIds.IndexOf(missingQid);
            var orderIndex = selectedIdx >= 0 ? selectedIdx + 1 : session.Answers.Count + 1;

            var skippedAnswer = new PracticeAnswer
            {
                SessionId = session.Id,
                QuestionId = missingQid,
                StudentId = session.StudentId,
                OrderIndex = orderIndex,
                AnswerText = string.Empty,
                IsFollowUp = false,
                ParentAnswerId = null,
                Status = "graded",
                SubmittedAt = submittedAtUtc
            };

            var eval = new AiEvaluation
            {
                AnswerType = "practice",
                StudentId = session.StudentId,
                PracticeAnswerId = skippedAnswer.Id,
                TotalScore = 0.0m,
                ConfidenceScore = 1.0m,
                Feedback = "Sinh viên bỏ qua câu hỏi này (0.0/10đ).",
                CotTrace = cotReason,
                EvaluatedAt = submittedAtUtc
            };

            if (skippedQuestionsWithRubrics.TryGetValue(missingQid, out var skippedQ) &&
                skippedQ.Rubric?.Criteria != null)
            {
                foreach (var crit in skippedQ.Rubric.Criteria.OrderBy(c => c.OrderIndex))
                {
                    eval.Details.Add(new AiEvaluationDetail
                    {
                        EvaluationId = eval.Id,
                        CriterionId = crit.Id,
                        Score = 0.0m,
                        Comment = "Chưa trả lời tiêu chí này (0 điểm)."
                    });
                }
            }

            skippedAnswer.AiEvaluations.Add(eval);
            session.Answers.Add(skippedAnswer);
            context.PracticeAnswers.Add(skippedAnswer);
            context.AiEvaluations.Add(eval);
        }

        return true;
    }

    public static async Task<bool> TryAutoCloseIfTimedOutAsync(
        IApplicationDbContext context,
        PracticeSession session,
        int timeoutMinutes,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        if (DateTime.UtcNow - session.LastActivityAt <= TimeSpan.FromMinutes(timeoutMinutes))
        {
            return false;
        }

        var changed = false;
        var completedAt = session.CompletedAt ?? session.LastActivityAt.AddMinutes(timeoutMinutes);

        if (string.Equals(session.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            session.Status = "completed";
            session.CompletedAt = completedAt;
            changed = true;
        }

        if (string.Equals(session.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            var addedSkipped = await PopulateMissingFullSessionAnswersAsync(
                context,
                session,
                completedAt,
                "[SKIPPED_QUESTION] Phiên luyện tập đóng tự động do quá 5 phút không có tương tác.",
                cancellationToken);
            if (addedSkipped)
            {
                changed = true;
            }

            if (changed || !session.TotalScore.HasValue)
            {
                var allSessionAnswers = await context.PracticeAnswers
                    .AsNoTracking()
                    .Include(a => a.AiEvaluations)
                    .Where(a => a.SessionId == session.Id)
                    .ToListAsync(cancellationToken);
                if (session.Answers != null)
                {
                    foreach (var localAns in session.Answers)
                    {
                        if (allSessionAnswers.All(a => a.Id != localAns.Id))
                        {
                            allSessionAnswers.Add(localAns);
                        }
                    }
                }

                var computedScore = ComputeSessionTotalScore(session, allSessionAnswers);
                if (session.TotalScore != computedScore)
                {
                    session.TotalScore = computedScore;
                    changed = true;
                }
            }
        }

        if (changed && saveChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return string.Equals(session.Status, "completed", StringComparison.OrdinalIgnoreCase);
    }
}
