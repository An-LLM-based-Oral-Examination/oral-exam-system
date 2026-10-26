using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Application.Features.MockExams.Common;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;

/// <summary>
/// Handler tra cứu chi tiết một bài thi thử trong quá khứ kèm Bảng điểm Scorecard (MF-02 - FE-02).
/// Tái tạo đầy đủ cấu trúc câu hỏi, câu trả lời, nhận xét và điểm tiêu chí Rubric theo CLO.
/// </summary>
public sealed class GetMockExamSessionDetailQueryHandler(
    IApplicationDbContext context,
    IAiGradingService? aiGradingService = null
) : IRequestHandler<GetMockExamSessionDetailQuery, Result<MockExamScorecardDto>>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IAiGradingService? _aiGradingService = aiGradingService;

    public async Task<Result<MockExamScorecardDto>> Handle(
        GetMockExamSessionDetailQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _context.MockExamSessions
            .AsNoTracking()
            .Include(s => s.Course)
            .Include(s => s.Answers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(pq => pq.Rubric)
                        .ThenInclude(r => r.Criteria)
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
                    .ThenInclude(e => e.Details)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<MockExamScorecardDto>.Failure("Không tìm thấy thông tin ca thi thử.");
        }

        if (!request.IsElevatedRole && session.StudentId != request.StudentId)
        {
            return Result<MockExamScorecardDto>.Failure("Bạn không có quyền truy cập ca thi này.");
        }

        var isExpiredInProgress = string.Equals(session.Status, "in_progress", StringComparison.OrdinalIgnoreCase)
            && DateTime.UtcNow > session.ExpiresAt.AddSeconds(10);
        var isPendingDeferredGrading = string.Equals(session.Status, "submitted", StringComparison.OrdinalIgnoreCase)
            && (!session.TotalScore.HasValue
                || session.Answers.Any(a => !string.IsNullOrWhiteSpace(a.AnswerText) && a.Status != "graded"));

        if (_aiGradingService != null && (isExpiredInProgress || isPendingDeferredGrading))
        {
            try
            {
                var submitHandler = new SubmitMockExamSessionCommandHandler(_context, _aiGradingService);
                var autoGradedResult = await submitHandler.Handle(
                    new SubmitMockExamSessionCommand(session.Id, session.StudentId),
                    cancellationToken);
                if (autoGradedResult.IsSuccess && autoGradedResult.Value != null)
                {
                    return autoGradedResult;
                }
            }
            catch
            {
                // Nếu AI vẫn đang tạm thời gián đoạn, tiếp tục trả về dữ liệu hiện có đã lưu của các câu trả lời
            }
        }

        // Giữ đúng thứ tự đề thi; follow-up đi sau câu chính. Bỏ slot phụ pending chưa trả lời.
        var orderedAnswers = MockExamAnswerOrdering.OrderAnswers(session.Answers);
        var questionScores = new List<MockExamQuestionScoreDto>();
        bool isOverdue = session.SubmittedAt.HasValue && session.SubmittedAt.Value > session.ExpiresAt.AddSeconds(10);
        var unansweredReason = isOverdue
            ? "Hết giờ làm bài - Không có câu trả lời"
            : "Thí sinh không trả lời câu hỏi này";

        int mainOrderIndex = 0;
        for (int i = 0; i < orderedAnswers.Count; i++)
        {
            var a = orderedAnswers[i];
            if (a.IsFollowUp && string.IsNullOrWhiteSpace(a.AnswerText))
            {
                continue;
            }

            if (!a.IsFollowUp)
            {
                mainOrderIndex++;
            }

            var q = a.Question;
            int orderIndex = a.OrderIndex > 0 ? a.OrderIndex : mainOrderIndex;
            var aiEval = a.AiEvaluations?.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
            var displayTitle = a.IsFollowUp ? $"Câu hỏi phụ · {q.Title}" : q.Title;
            var displayContent = a.IsFollowUp
                ? (!string.IsNullOrWhiteSpace(a.PromptText) ? a.PromptText! : q.Content)
                : q.Content;

            var criterionScores = new List<MockExamCriterionScoreDto>();
            foreach (var c in q.Rubric?.Criteria?.OrderBy(x => x.OrderIndex) ?? Enumerable.Empty<RubricCriterion>())
            {
                var detail = aiEval?.Details?.FirstOrDefault(d => d.CriterionId == c.Id);
                var score = detail?.Score ?? 0.0m;
                var comment = detail?.Comment ?? (string.IsNullOrWhiteSpace(a.AnswerText)
                    ? unansweredReason
                    : string.Empty);

                criterionScores.Add(new MockExamCriterionScoreDto(
                    CriterionId: c.Id,
                    CriterionName: c.CriterionName,
                    BloomLevel: c.BloomLevel,
                    MaxScore: c.MaxScore,
                    Score: score,
                    Comment: comment
                ));
            }

            questionScores.Add(new MockExamQuestionScoreDto(
                QuestionId: q.Id,
                OrderIndex: orderIndex,
                Title: displayTitle,
                Content: displayContent,
                BloomLevel: q.BloomLevel ?? "Remember",
                Difficulty: q.Difficulty,
                Transcript: a.AnswerText,
                Score: aiEval?.TotalScore ?? 0.0m,
                Feedback: aiEval?.Feedback ?? (string.IsNullOrWhiteSpace(a.AnswerText) ? unansweredReason : null),
                ConfidenceScore: aiEval?.ConfidenceScore ?? 1.0m,
                CriteriaScores: criterionScores,
                ImageUrl: a.IsFollowUp ? null : q.ImageUrl,
                ImageDescription: a.IsFollowUp ? null : q.ImageDescription,
                IsFollowUp: a.IsFollowUp,
                AnswerId: a.Id,
                ParentAnswerId: a.ParentAnswerId
            ));
        }

        var mainAnswers = orderedAnswers.Where(a => !a.IsFollowUp).ToList();
        var examSet = session.ExamSetId != Guid.Empty
            ? await _context.ExamSets
                .AsNoTracking()
                .Include(es => es.Structure)
                .FirstOrDefaultAsync(es => es.Id == session.ExamSetId, cancellationToken)
            : null;

        int expectedTotalQuestions = examSet?.Structure?.TotalQuestions ?? 0;
        if (expectedTotalQuestions > mainAnswers.Count || mainAnswers.Count == 0)
        {
            var existingQuestionIds = mainAnswers.Select(a => a.QuestionId).ToHashSet();
            var missingCandidates = await _context.PracticeQuestions
                .AsNoTracking()
                .Include(pq => pq.Rubric)
                    .ThenInclude(r => r.Criteria)
                .Where(pq => pq.CourseId == session.CourseId && pq.IsActive && !existingQuestionIds.Contains(pq.Id))
                .OrderBy(pq => pq.CreatedAt)
                .ToListAsync(cancellationToken);

            int targetTotal = expectedTotalQuestions > 0
                ? expectedTotalQuestions
                : mainAnswers.Count + missingCandidates.Count;
            int neededCount = Math.Max(0, targetTotal - mainAnswers.Count);

            foreach (var missingQ in missingCandidates.Take(neededCount))
            {
                mainOrderIndex++;
                var criterionScores = (missingQ.Rubric?.Criteria?.OrderBy(x => x.OrderIndex) ?? Enumerable.Empty<RubricCriterion>())
                    .Select(c => new MockExamCriterionScoreDto(
                        CriterionId: c.Id,
                        CriterionName: c.CriterionName,
                        BloomLevel: c.BloomLevel,
                        MaxScore: c.MaxScore,
                        Score: 0.0m,
                        Comment: unansweredReason
                    ))
                    .ToList();

                questionScores.Add(new MockExamQuestionScoreDto(
                    QuestionId: missingQ.Id,
                    OrderIndex: mainOrderIndex,
                    Title: missingQ.Title,
                    Content: missingQ.Content,
                    BloomLevel: missingQ.BloomLevel ?? "Remember",
                    Difficulty: missingQ.Difficulty,
                    Transcript: null,
                    Score: 0.0m,
                    Feedback: unansweredReason,
                    ConfidenceScore: 1.0m,
                    CriteriaScores: criterionScores,
                    ImageUrl: missingQ.ImageUrl,
                    ImageDescription: missingQ.ImageDescription,
                    IsFollowUp: false,
                    AnswerId: null,
                    ParentAnswerId: null
                ));
            }
        }

        var mainScores = questionScores.Where(x => !x.IsFollowUp).ToList();
        decimal totalScore = session.TotalScore ?? (mainScores.Count > 0
            ? Math.Round(mainScores.Sum(x => x.Score) / mainScores.Count, 2, MidpointRounding.AwayFromZero)
            : 0.0m);

        string classification = totalScore switch
        {
            >= 9.0m => "Xuất sắc",
            >= 8.0m => "Giỏi",
            >= 6.5m => "Khá",
            >= 5.0m => "Trung bình",
            _ => "Yếu"
        };

        int durationMinutes = session.ExpiresAt > session.StartedAt
            ? (int)Math.Round((session.ExpiresAt - session.StartedAt).TotalMinutes)
            : 30;

        int answeredQuestions = mainAnswers.Count(a => !string.IsNullOrWhiteSpace(a.AnswerText));

        var response = new MockExamScorecardDto(
            SessionId: session.Id,
            CourseId: session.CourseId,
            CourseCode: session.Course?.Code ?? string.Empty,
            CourseName: session.Course?.Name ?? string.Empty,
            TotalScore: totalScore,
            Classification: classification,
            TotalQuestions: mainScores.Count,
            AnsweredQuestions: answeredQuestions,
            DurationMinutes: durationMinutes,
            IsOverdue: isOverdue,
            StartedAt: session.StartedAt,
            SubmittedAt: session.SubmittedAt ?? session.ExpiresAt,
            Questions: questionScores
        );

        return Result<MockExamScorecardDto>.Success(response);
    }
}
