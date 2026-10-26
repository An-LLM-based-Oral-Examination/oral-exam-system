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
            var skippedAnswer = new PracticeAnswer
            {
                SessionId = session.Id,
                QuestionId = missingQid,
                StudentId = session.StudentId,
                AnswerText = string.Empty,
                IsFollowUp = false,
                ParentAnswerId = null,
                Status = "graded",
                SubmittedAt = submittedAtUtc
            };

            var eval = new AiEvaluation
            {
                AnswerType = "practice",
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
        CancellationToken cancellationToken)
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
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return string.Equals(session.Status, "completed", StringComparison.OrdinalIgnoreCase);
    }
}
