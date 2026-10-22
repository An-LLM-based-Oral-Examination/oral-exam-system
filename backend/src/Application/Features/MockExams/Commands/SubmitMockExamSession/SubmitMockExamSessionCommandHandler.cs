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

namespace OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;

/// <summary>
/// Handler xử lý nộp bài thi thử và chấm điểm AI tức thì (Fast-Path Synchronous Instant Scorecard - MF-02).
/// Kiểm soát Server Master Timer, Network Grace Period 10s, chấm song song qua Gemini 1.5 Flash theo Barem Rubric 10.0đ.
/// </summary>
public sealed class SubmitMockExamSessionCommandHandler(
    IApplicationDbContext context,
    IAiGradingService aiGradingService
) : IRequestHandler<SubmitMockExamSessionCommand, Result<MockExamScorecardDto>>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IAiGradingService _aiGradingService = aiGradingService;

    public async Task<Result<MockExamScorecardDto>> Handle(
        SubmitMockExamSessionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Tải toàn bộ ca thi cùng môn học, câu trả lời, câu hỏi và rubric
        var session = await _context.MockExamSessions
            .Include(s => s.Course)
            .Include(s => s.Answers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(pq => pq.Rubric)
                        .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<MockExamScorecardDto>.Failure("Ca thi không tồn tại.");
        }

        if (session.StudentId != request.StudentId)
        {
            return Result<MockExamScorecardDto>.Failure("Bạn không có quyền nộp bài cho ca thi này.");
        }

        if (session.Status == "submitted" || session.Status == "graded")
        {
            return Result<MockExamScorecardDto>.Failure("Ca thi đã được nộp và chấm điểm trước đó.");
        }

        // Chống Race Condition Concurrent Double-Submit: Khóa trạng thái submitted ngay lập tức
        session.Status = "submitted";
        await _context.SaveChangesAsync(cancellationToken);

        // 2. Server Master Timer check (kèm 10s Network Grace Period)
        var now = DateTime.UtcNow;
        var isOverdue = now > session.ExpiresAt.AddSeconds(10);

        // Giữ đúng thứ tự đề thi (OrderIndex); follow-up đi sau câu chính cùng OrderIndex.
        var sessionAnswers = session.Answers
            .OrderBy(a => a.OrderIndex)
            .ThenBy(a => a.IsFollowUp)
            .ThenBy(a => a.AnsweredAt)
            .ThenBy(a => a.Id)
            .ToList();

        // 3. Chấm điểm đồng thời tất cả các câu hỏi qua Task.WhenAll
        var gradingTasks = sessionAnswers.Select(async (a, index) =>
        {
            var q = a.Question;
            int orderIndex = a.OrderIndex > 0 ? a.OrderIndex : index + 1;
            bool isAnswered = !string.IsNullOrWhiteSpace(a.AnswerText) && a.Status == "answered";
            var displayTitle = a.IsFollowUp ? $"Câu hỏi phụ · {q.Title}" : q.Title;
            var displayContent = a.IsFollowUp
                ? (!string.IsNullOrWhiteSpace(a.PromptText) ? a.PromptText! : q.Content)
                : q.Content;

            if (!isAnswered)
            {
                // Câu hỏi chưa làm hoặc hết giờ chưa kịp trả lời -> 0 điểm
                // Bỏ qua slot follow-up pending khi tính điểm (không hiện trên scorecard)
                if (a.IsFollowUp && string.IsNullOrWhiteSpace(a.AnswerText))
                {
                    return (a, (MockExamQuestionScoreDto?)null, (AiEvaluation?)null);
                }

                var reason = isOverdue
                    ? "Hết giờ làm bài - Không có câu trả lời"
                    : "Thí sinh không trả lời câu hỏi này";

                var emptyCriteria = q.Rubric?.Criteria?.OrderBy(c => c.OrderIndex).Select(c => new MockExamCriterionScoreDto(
                    c.Id,
                    c.CriterionName,
                    c.BloomLevel,
                    c.MaxScore,
                    0.0m,
                    reason
                )).ToList() ?? new List<MockExamCriterionScoreDto>();

                var emptyQuestionScore = new MockExamQuestionScoreDto(
                    q.Id,
                    orderIndex,
                    displayTitle,
                    displayContent,
                    q.BloomLevel ?? "Remember",
                    q.Difficulty,
                    null,
                    0.0m,
                    reason,
                    1.0m,
                    emptyCriteria,
                    a.IsFollowUp ? null : q.ImageUrl,
                    a.IsFollowUp ? null : q.ImageDescription,
                    a.IsFollowUp,
                    a.Id,
                    a.ParentAnswerId
                );

                return (a, emptyQuestionScore, (AiEvaluation?)null);
            }

            // Câu hỏi đã có câu trả lời -> Gửi Gemini AI chấm CoT Rubric 10.0đ
            var criteriaList = q.Rubric?.Criteria?.OrderBy(c => c.OrderIndex).Select(c => new AiGradingCriterion
            {
                Id = c.Id.ToString(),
                Name = c.CriterionName,
                Description = c.Description ?? string.Empty,
                MaxScore = c.MaxScore,
                Weight = c.Weight
            }).ToList() ?? new List<AiGradingCriterion>();

            var gradingReq = new AiGradingRequest
            {
                Transcript = a.AnswerText!,
                QuestionContent = displayContent,
                SampleAnswer = a.IsFollowUp ? string.Empty : (q.SampleAnswer ?? string.Empty),
                CourseCode = session.Course?.Code,
                QuestionImageUrl = a.IsFollowUp ? null : q.ImageUrl,
                QuestionImageDescription = a.IsFollowUp ? null : q.ImageDescription,
                Criteria = criteriaList
            };

            var gradingResult = await _aiGradingService.GradeAnswerAsync(gradingReq, cancellationToken);

            var criterionScores = new List<MockExamCriterionScoreDto>();
            var evalDetails = new List<AiEvaluationDetail>();
            var evalId = Guid.NewGuid();
            var orderedCriteria = q.Rubric?.Criteria?.OrderBy(x => x.OrderIndex).ToList() ?? new List<RubricCriterion>();
            var rawDetails = gradingResult.Details ?? new List<GradingDetailDto>();

            for (int critIndex = 0; critIndex < orderedCriteria.Count; critIndex++)
            {
                var c = orderedCriteria[critIndex];
                var detailDto = rawDetails.FirstOrDefault(d =>
                        string.Equals(d.CriterionId?.Trim(), c.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                    ?? rawDetails.FirstOrDefault(d =>
                        !string.IsNullOrWhiteSpace(c.CriterionName) &&
                        string.Equals(d.CriterionId?.Trim(), c.CriterionName.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? (critIndex < rawDetails.Count ? rawDetails[critIndex] : null);

                var score = detailDto != null
                    ? Math.Clamp(Math.Round(detailDto.Score, 2, MidpointRounding.AwayFromZero), 0.0m, c.MaxScore)
                    : 0.0m;
                var comment = !string.IsNullOrWhiteSpace(detailDto?.Comment)
                    ? detailDto!.Comment
                    : score > 0.0m
                        ? $"Đạt {score:0.0}/{c.MaxScore:0.0}đ cho tiêu chí '{c.CriterionName}'."
                        : $"Chưa đáp ứng tiêu chí '{c.CriterionName}' (0.0/{c.MaxScore:0.0}đ).";

                criterionScores.Add(new MockExamCriterionScoreDto(
                    c.Id,
                    c.CriterionName,
                    c.BloomLevel,
                    c.MaxScore,
                    score,
                    comment
                ));

                evalDetails.Add(new AiEvaluationDetail
                {
                    Id = Guid.NewGuid(),
                    EvaluationId = evalId,
                    CriterionId = c.Id,
                    Score = score,
                    Comment = comment
                });
            }

            var finalQuestionScore = rawDetails.Count > 0 && criterionScores.Count > 0
                ? Math.Clamp(Math.Round(criterionScores.Sum(c => c.Score), 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                : Math.Clamp(Math.Round(gradingResult.Score, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m);

            var questionScoreDto = new MockExamQuestionScoreDto(
                q.Id,
                orderIndex,
                displayTitle,
                displayContent,
                q.BloomLevel ?? "Remember",
                q.Difficulty,
                a.AnswerText,
                finalQuestionScore,
                gradingResult.Feedback,
                gradingResult.ConfidenceScore,
                criterionScores,
                a.IsFollowUp ? null : q.ImageUrl,
                a.IsFollowUp ? null : q.ImageDescription,
                a.IsFollowUp,
                a.Id,
                a.ParentAnswerId
            );

            var aiEvaluation = new AiEvaluation
            {
                Id = evalId,
                AnswerType = "practice",
                PracticeAnswerId = null,
                ExamSubmissionId = null,
                MockExamAnswerId = a.Id,
                TotalScore = finalQuestionScore,
                Feedback = gradingResult.Feedback,
                ConfidenceScore = gradingResult.ConfidenceScore,
                CotTrace = gradingResult.CotTrace,
                EvaluatedAt = DateTime.UtcNow,
                Details = evalDetails
            };

            return (a, questionScoreDto, (AiEvaluation?)aiEvaluation);
        });

        var results = (await Task.WhenAll(gradingTasks))
            .Where(r => r.Item2 is not null)
            .Select(r => (a: r.a, score: r.Item2!, eval: r.Item3))
            .ToList();

        // 4. Điểm tổng = trung bình các câu chính (follow-up không làm loãng điểm đề)
        var mainResults = results.Where(r => !r.a.IsFollowUp).ToList();
        int totalQuestions = mainResults.Count;
        int answeredQuestions = mainResults.Count(r =>
            !string.IsNullOrWhiteSpace(r.a.AnswerText) && r.a.Status == "answered");

        decimal totalScore = 0.0m;
        if (totalQuestions > 0)
        {
            var sumScores = mainResults.Sum(r => r.score.Score);
            totalScore = Math.Round(sumScores / totalQuestions, 2, MidpointRounding.AwayFromZero);
        }

        string classification = totalScore switch
        {
            >= 9.0m => "Xuất sắc",
            >= 8.0m => "Giỏi",
            >= 6.5m => "Khá",
            >= 5.0m => "Trung bình",
            _ => "Yếu"
        };

        // 5. Cập nhật phiên thi và lưu đánh giá AI vào CSDL
        session.TotalScore = totalScore;
        session.Status = "graded";
        session.SubmittedAt = DateTime.UtcNow;

        foreach (var r in results)
        {
            if (r.eval != null)
            {
                r.a.Status = "graded";
                _context.AiEvaluations.Add(r.eval);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 6. Xây dựng Bảng điểm Instant Scorecard hoàn chỉnh
        int durationMinutes = session.ExpiresAt > session.StartedAt
            ? (int)Math.Round((session.ExpiresAt - session.StartedAt).TotalMinutes)
            : 30;

        var scorecard = new MockExamScorecardDto(
            SessionId: session.Id,
            CourseId: session.CourseId,
            CourseCode: session.Course?.Code ?? string.Empty,
            CourseName: session.Course?.Name ?? string.Empty,
            TotalScore: totalScore,
            Classification: classification,
            TotalQuestions: totalQuestions,
            AnsweredQuestions: answeredQuestions,
            DurationMinutes: durationMinutes,
            IsOverdue: isOverdue,
            StartedAt: session.StartedAt,
            SubmittedAt: session.SubmittedAt.Value,
            Questions: results.Select(r => r.score).ToList()
        );

        return Result<MockExamScorecardDto>.Success(scorecard);
    }
}
