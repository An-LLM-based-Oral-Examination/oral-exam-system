using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Practice.Commands.CompletePracticeSession;

public sealed class CompletePracticeSessionCommandHandler : IRequestHandler<CompletePracticeSessionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public CompletePracticeSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(CompletePracticeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.PracticeSessions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null || session.StudentId != request.StudentId)
        {
            return Result.Failure("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.");
        }

        var hasAddedSkippedAnswers = false;
        if (string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase)
            && session.SelectedQuestionIds.Count > 0)
        {
            var answeredQuestionIds = session.Answers
                .Select(a => a.QuestionId)
                .ToHashSet();

            var missingQuestionIds = session.SelectedQuestionIds
                .Where(qid => qid != Guid.Empty && !answeredQuestionIds.Contains(qid))
                .Distinct()
                .ToList();

            if (missingQuestionIds.Count > 0)
            {
                var skippedQuestionsWithRubrics = await _context.PracticeQuestions
                    .AsNoTracking()
                    .Include(q => q.Rubric)
                        .ThenInclude(r => r.Criteria)
                    .Where(q => missingQuestionIds.Contains(q.Id))
                    .ToDictionaryAsync(q => q.Id, cancellationToken);

                var now = DateTime.UtcNow;
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
                        SubmittedAt = now
                    };

                    var eval = new AiEvaluation
                    {
                        AnswerType = "practice",
                        PracticeAnswerId = skippedAnswer.Id,
                        TotalScore = 0.0m,
                        ConfidenceScore = 1.0m,
                        Feedback = "Sinh viên bỏ qua câu hỏi này (0.0/10đ).",
                        CotTrace = "[SKIPPED_QUESTION] Sinh viên kết thúc phiên sớm mà không trả lời câu hỏi này.",
                        EvaluatedAt = now
                    };

                    if (skippedQuestionsWithRubrics.TryGetValue(missingQid, out var skippedQ) && skippedQ.Rubric?.Criteria != null)
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

                    session.Answers.Add(skippedAnswer);
                    _context.PracticeAnswers.Add(skippedAnswer);
                    _context.AiEvaluations.Add(eval);
                }

                hasAddedSkippedAnswers = true;
            }
        }

        if (session.Status == "completed")
        {
            if (hasAddedSkippedAnswers)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(); // Idempotent: nếu đã hoàn thành thì trả về thành công
        }

        session.Status = "completed";
        session.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
