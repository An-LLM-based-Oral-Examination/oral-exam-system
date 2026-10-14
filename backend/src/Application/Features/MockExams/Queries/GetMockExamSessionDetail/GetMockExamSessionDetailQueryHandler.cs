using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;

/// <summary>
/// Handler tra cứu chi tiết một bài thi thử trong quá khứ kèm Bảng điểm Scorecard (MF-02 - FE-02).
/// Tái tạo đầy đủ cấu trúc câu hỏi, câu trả lời, nhận xét và điểm tiêu chí Rubric theo CLO.
/// </summary>
public sealed class GetMockExamSessionDetailQueryHandler(
    IApplicationDbContext context
) : IRequestHandler<GetMockExamSessionDetailQuery, Result<MockExamScorecardDto>>
{
    private readonly IApplicationDbContext _context = context;

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

        if (session.StudentId != request.StudentId)
        {
            return Result<MockExamScorecardDto>.Failure("Bạn không có quyền truy cập ca thi này.");
        }

        var sessionAnswers = session.Answers.OrderBy(a => a.AnsweredAt).ToList();
        var questionScores = new List<MockExamQuestionScoreDto>();

        for (int i = 0; i < sessionAnswers.Count; i++)
        {
            var a = sessionAnswers[i];
            var q = a.Question;
            int orderIndex = i + 1;
            var aiEval = a.AiEvaluations?.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();

            var criterionScores = new List<MockExamCriterionScoreDto>();
            foreach (var c in q.Rubric?.Criteria?.OrderBy(x => x.OrderIndex) ?? Enumerable.Empty<RubricCriterion>())
            {
                var detail = aiEval?.Details?.FirstOrDefault(d => d.CriterionId == c.Id);
                var score = detail?.Score ?? 0.0m;
                var comment = detail?.Comment ?? (string.IsNullOrWhiteSpace(a.AnswerText)
                    ? "Thí sinh không trả lời câu hỏi này"
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
                Title: q.Title,
                Content: q.Content,
                BloomLevel: q.BloomLevel ?? "Remember",
                Difficulty: q.Difficulty,
                Transcript: a.AnswerText,
                Score: aiEval?.TotalScore ?? 0.0m,
                Feedback: aiEval?.Feedback ?? (string.IsNullOrWhiteSpace(a.AnswerText) ? "Thí sinh không trả lời câu hỏi này" : null),
                ConfidenceScore: aiEval?.ConfidenceScore,
                CriteriaScores: criterionScores
            ));
        }

        decimal totalScore = session.TotalScore ?? (questionScores.Count > 0
            ? Math.Round(questionScores.Sum(x => x.Score) / questionScores.Count, 2, MidpointRounding.AwayFromZero)
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

        bool isOverdue = session.SubmittedAt.HasValue && session.SubmittedAt.Value > session.ExpiresAt.AddSeconds(10);
        int answeredQuestions = session.Answers.Count(a => !string.IsNullOrWhiteSpace(a.AnswerText) && (a.Status == "answered" || a.Status == "graded"));

        var response = new MockExamScorecardDto(
            SessionId: session.Id,
            CourseId: session.CourseId,
            CourseCode: session.Course?.Code ?? string.Empty,
            CourseName: session.Course?.Name ?? string.Empty,
            TotalScore: totalScore,
            Classification: classification,
            TotalQuestions: questionScores.Count,
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
