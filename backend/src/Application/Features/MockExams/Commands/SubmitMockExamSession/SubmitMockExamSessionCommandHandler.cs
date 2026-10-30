using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Common;
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
    private static readonly ConcurrentDictionary<Guid, byte> ActiveGradingSessions = new();
    private readonly IApplicationDbContext _context = context;
    private readonly IAiGradingService _aiGradingService = aiGradingService;

    public async Task<Result<MockExamScorecardDto>> Handle(
        SubmitMockExamSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (!ActiveGradingSessions.TryAdd(request.SessionId, 0))
        {
            return Result<MockExamScorecardDto>.Failure("CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
        }

        try
        {
            return await HandleCoreAsync(request, cancellationToken);
        }
        finally
        {
            ActiveGradingSessions.TryRemove(request.SessionId, out _);
        }
    }

    private async Task<Result<MockExamScorecardDto>> HandleCoreAsync(
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
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
                    .ThenInclude(e => e.Details)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<MockExamScorecardDto>.Failure("Ca thi không tồn tại.");
        }

        if (session.StudentId != request.StudentId)
        {
            return Result<MockExamScorecardDto>.Failure("Bạn không có quyền nộp bài cho ca thi này.");
        }

        var isRecoverableStuckSubmission = session.Status == "submitted"
            && session.Answers.Count > 0
            && (!session.TotalScore.HasValue
                || session.Answers.Any(a => !string.IsNullOrWhiteSpace(a.AnswerText) && a.Status != "graded"));

        if (session.Status != "in_progress" && !isRecoverableStuckSubmission)
        {
            return Result<MockExamScorecardDto>.Failure("CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
        }

        // 2. Server Master Timer check (kèm 10s Network Grace Period)
        var submittedAt = session.SubmittedAt ?? DateTime.UtcNow;
        var isOverdue = submittedAt > session.ExpiresAt.AddSeconds(10);
        session.Status = "submitted";
        session.SubmittedAt = submittedAt;
        await _context.SaveChangesAsync(cancellationToken);

        // Dọn dẹp các slot câu hỏi phụ đang chờ (pending / chưa có câu trả lời) trước khi sắp xếp và chấm điểm
        var unansweredFollowUps = session.Answers
            .Where(a => a.IsFollowUp && (a.Status != "answered" || string.IsNullOrWhiteSpace(a.AnswerText)))
            .ToList();
        if (unansweredFollowUps.Count > 0)
        {
            _context.MockExamAnswers.RemoveRange(unansweredFollowUps);
            foreach (var u in unansweredFollowUps)
            {
                session.Answers.Remove(u);
            }
        }

        // Giữ đúng thứ tự đề thi; follow-up đi sau câu chính cùng OrderIndex.
        var sessionAnswers = MockExamAnswerOrdering.OrderAnswers(session.Answers);

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

            var orderedCriteria = q.Rubric?.Criteria?.OrderBy(x => x.OrderIndex).ToList() ?? new List<RubricCriterion>();
            var existingEval = a.AiEvaluations?
                .OrderByDescending(e => e.EvaluatedAt)
                .FirstOrDefault();

            if (existingEval != null)
            {
                var cachedCriterionScores = new List<MockExamCriterionScoreDto>();
                var existingDetails = existingEval.Details?.ToList() ?? new List<AiEvaluationDetail>();

                for (int critIndex = 0; critIndex < orderedCriteria.Count; critIndex++)
                {
                    var c = orderedCriteria[critIndex];
                    var matchedDetail = existingDetails.FirstOrDefault(d => d.CriterionId == c.Id)
                        ?? (critIndex < existingDetails.Count ? existingDetails[critIndex] : null);

                    var score = matchedDetail != null
                        ? Math.Clamp(Math.Round(matchedDetail.Score, 2, MidpointRounding.AwayFromZero), 0.0m, c.MaxScore)
                        : 0.0m;
                    var comment = !string.IsNullOrWhiteSpace(matchedDetail?.Comment)
                        ? matchedDetail!.Comment!
                        : score > 0.0m
                            ? $"Đạt {score:0.0}/{c.MaxScore:0.0}đ cho tiêu chí '{c.CriterionName}'."
                            : $"Chưa đáp ứng tiêu chí '{c.CriterionName}' (0.0/{c.MaxScore:0.0}đ).";

                    cachedCriterionScores.Add(new MockExamCriterionScoreDto(
                        c.Id,
                        c.CriterionName,
                        c.BloomLevel,
                        c.MaxScore,
                        score,
                        comment
                    ));
                }

                var cachedScore = existingDetails.Count > 0 && cachedCriterionScores.Count > 0
                    ? Math.Clamp(Math.Round(cachedCriterionScores.Sum(c => c.Score), 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                    : Math.Clamp(Math.Round(existingEval.TotalScore, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m);

                var cachedQuestionScoreDto = new MockExamQuestionScoreDto(
                    q.Id,
                    orderIndex,
                    displayTitle,
                    displayContent,
                    q.BloomLevel ?? "Remember",
                    q.Difficulty,
                    a.AnswerText,
                    cachedScore,
                    existingEval.Feedback,
                    existingEval.ConfidenceScore ?? 0.90m,
                    cachedCriterionScores,
                    a.IsFollowUp ? null : q.ImageUrl,
                    a.IsFollowUp ? null : q.ImageDescription,
                    a.IsFollowUp,
                    a.Id,
                    a.ParentAnswerId
                );

                return (a, cachedQuestionScoreDto, (AiEvaluation?)existingEval);
            }

            // Câu hỏi đã có câu trả lời và chưa có AiEvaluation -> Gửi Gemini AI chấm CoT Rubric 10.0đ
            var criteriaList = orderedCriteria.Select(c => new AiGradingCriterion
            {
                Id = c.Id.ToString(),
                Name = c.CriterionName,
                Description = c.Description ?? string.Empty,
                MaxScore = c.MaxScore,
                Weight = c.Weight
            }).ToList();

            MockExamAnswer? parentMainAnswer = null;
            int followUpOrdinal = 0;
            var previousFollowUpPrompts = new List<string>();

            if (a.IsFollowUp)
            {
                parentMainAnswer = sessionAnswers.FirstOrDefault(x => !x.IsFollowUp && x.Id == a.ParentAnswerId)
                    ?? sessionAnswers.FirstOrDefault(x => !x.IsFollowUp && x.QuestionId == a.QuestionId);

                var siblingFollowUps = sessionAnswers
                    .Where(x => x.IsFollowUp &&
                                ((a.ParentAnswerId.HasValue && x.ParentAnswerId == a.ParentAnswerId) ||
                                 (!a.ParentAnswerId.HasValue && x.QuestionId == a.QuestionId)))
                    .ToList();

                var currentFollowUpIdx = siblingFollowUps.FindIndex(x => x.Id == a.Id);
                followUpOrdinal = currentFollowUpIdx >= 0 ? currentFollowUpIdx + 1 : 1;

                if (currentFollowUpIdx > 0)
                {
                    previousFollowUpPrompts = siblingFollowUps
                        .Take(currentFollowUpIdx)
                        .Select(x => x.PromptText)
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => p!.Trim())
                        .ToList();
                }
            }

            var gradingReq = new AiGradingRequest
            {
                Transcript = a.AnswerText!,
                QuestionContent = q.Content,
                SampleAnswer = q.SampleAnswer ?? string.Empty,
                CourseCode = session.Course?.Code,
                QuestionImageUrl = q.ImageUrl,
                QuestionImageDescription = q.ImageDescription,
                IsFollowUp = a.IsFollowUp,
                FollowUpPrompt = a.IsFollowUp ? a.PromptText : null,
                FollowUpIndex = followUpOrdinal,
                OriginalAnswerTranscript = a.IsFollowUp ? parentMainAnswer?.AnswerText : null,
                PreviousFollowUpPrompts = previousFollowUpPrompts,
                Criteria = criteriaList,
                ThrowOnTransientFailure = true
            };

            var gradingResult = await _aiGradingService.GradeAnswerAsync(gradingReq, cancellationToken);

            var criterionScores = new List<MockExamCriterionScoreDto>();
            var evalDetails = new List<AiEvaluationDetail>();
            var evalId = Guid.NewGuid();
            var rawDetails = gradingResult.Details ?? new List<GradingDetailDto>();
            var matchedByCriterionIndex = new GradingDetailDto?[orderedCriteria.Count];
            var usedRawIndices = new HashSet<int>();

            for (int critIndex = 0; critIndex < orderedCriteria.Count; critIndex++)
            {
                var c = orderedCriteria[critIndex];
                int matchedIdx = -1;

                for (int rIdx = 0; rIdx < rawDetails.Count; rIdx++)
                {
                    if (usedRawIndices.Contains(rIdx))
                    {
                        continue;
                    }

                    var candidateId = rawDetails[rIdx].CriterionId?.Trim();
                    if (string.Equals(candidateId, c.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        matchedIdx = rIdx;
                        break;
                    }
                }

                if (matchedIdx < 0 && !string.IsNullOrWhiteSpace(c.CriterionName))
                {
                    for (int rIdx = 0; rIdx < rawDetails.Count; rIdx++)
                    {
                        if (usedRawIndices.Contains(rIdx))
                        {
                            continue;
                        }

                        var candidateId = rawDetails[rIdx].CriterionId?.Trim();
                        if (string.Equals(candidateId, c.CriterionName.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            matchedIdx = rIdx;
                            break;
                        }
                    }
                }

                if (matchedIdx >= 0)
                {
                    usedRawIndices.Add(matchedIdx);
                    matchedByCriterionIndex[critIndex] = rawDetails[matchedIdx];
                }
            }

            var totalCriteriaMax = orderedCriteria.Sum(c => c.MaxScore);
            var clampedAiScore = Math.Clamp(Math.Round(gradingResult.Score, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m);
            decimal runningAllocatedScore = 0.0m;

            for (int critIndex = 0; critIndex < orderedCriteria.Count; critIndex++)
            {
                var c = orderedCriteria[critIndex];
                var detailDto = matchedByCriterionIndex[critIndex];
                if (detailDto == null && critIndex < rawDetails.Count && !usedRawIndices.Contains(critIndex))
                {
                    usedRawIndices.Add(critIndex);
                    detailDto = rawDetails[critIndex];
                }

                decimal score;
                if (rawDetails.Count > 0)
                {
                    score = detailDto != null
                        ? Math.Clamp(Math.Round(detailDto.Score, 2, MidpointRounding.AwayFromZero), 0.0m, c.MaxScore)
                        : 0.0m;
                }
                else if (totalCriteriaMax > 0.0m && clampedAiScore > 0.0m)
                {
                    if (critIndex == orderedCriteria.Count - 1)
                    {
                        score = Math.Clamp(
                            Math.Round(clampedAiScore - runningAllocatedScore, 2, MidpointRounding.AwayFromZero),
                            0.0m,
                            c.MaxScore);
                    }
                    else
                    {
                        score = Math.Clamp(
                            Math.Round(clampedAiScore * (c.MaxScore / totalCriteriaMax), 2, MidpointRounding.AwayFromZero),
                            0.0m,
                            c.MaxScore);
                        runningAllocatedScore += score;
                    }
                }
                else
                {
                    score = 0.0m;
                }

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

            var finalQuestionScore = criterionScores.Count > 0
                ? Math.Clamp(Math.Round(criterionScores.Sum(c => c.Score), 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                : clampedAiScore;

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
                AnswerType = "mock_exam",
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

        List<(MockExamAnswer a, MockExamQuestionScoreDto score, AiEvaluation? eval)> results;
        try
        {
            results = (await Task.WhenAll(gradingTasks))
                .Where(r => r.Item2 is not null)
                .Select(r => (a: r.a, score: r.Item2!, eval: r.Item3))
                .ToList();
        }
        catch
        {
            // Persist First: Giữ nguyên trạng thái "submitted" và thời điểm SubmittedAt để bảo toàn bài làm của sinh viên,
            // cho phép hệ thống chấm điểm bù tự động (Background Worker / Lazy Recovery) ngay khi AI phục hồi.
            session.Status = "submitted";
            session.SubmittedAt = submittedAt;
            await _context.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        // 3b. Tổng hợp điểm tích lũy từ các câu trả lời phụ (Follow-up) vào câu trả lời chính tương ứng
        for (int i = 0; i < results.Count; i++)
        {
            var mainResult = results[i];
            if (mainResult.a.IsFollowUp)
            {
                continue;
            }

            var answeredFollowUps = results
                .Where(r => r.a.IsFollowUp
                            && ((r.a.ParentAnswerId.HasValue && r.a.ParentAnswerId == mainResult.a.Id)
                                || (!r.a.ParentAnswerId.HasValue && r.a.QuestionId == mainResult.a.QuestionId))
                            && !string.IsNullOrWhiteSpace(r.a.AnswerText))
                .ToList();

            if (answeredFollowUps.Count == 0)
            {
                continue;
            }

            var mainCriteria = mainResult.score.CriteriaScores ?? new List<MockExamCriterionScoreDto>();
            decimal avgFollowUpScore = answeredFollowUps.Average(f => f.score.Score);
            decimal weightedScore = Math.Round(
                mainResult.score.Score * 0.7m + avgFollowUpScore * 0.3m,
                2,
                MidpointRounding.AwayFromZero);

            var mergedCriteria = new List<MockExamCriterionScoreDto>(mainCriteria.Count);

            for (int critIdx = 0; critIdx < mainCriteria.Count; critIdx++)
            {
                var mainCrit = mainCriteria[critIdx];
                decimal bestCritScore = mainCrit.Score;
                string? bestFollowUpComment = null;

                foreach (var fu in answeredFollowUps)
                {
                    var fuCriteria = fu.score.CriteriaScores;
                    if (fuCriteria == null || fuCriteria.Count == 0)
                    {
                        continue;
                    }

                    var matchedFuCrit = fuCriteria.FirstOrDefault(fc =>
                            fc.CriterionId == mainCrit.CriterionId && mainCrit.CriterionId != Guid.Empty)
                        ?? fuCriteria.FirstOrDefault(fc =>
                            !string.IsNullOrWhiteSpace(mainCrit.CriterionName) &&
                            string.Equals(fc.CriterionName, mainCrit.CriterionName, StringComparison.OrdinalIgnoreCase))
                        ?? (critIdx < fuCriteria.Count ? fuCriteria[critIdx] : null);

                    if (matchedFuCrit != null && matchedFuCrit.Score > bestCritScore)
                    {
                        bestCritScore = Math.Clamp(
                            Math.Round(matchedFuCrit.Score, 2, MidpointRounding.AwayFromZero),
                            0.0m,
                            mainCrit.MaxScore);
                        bestFollowUpComment = matchedFuCrit.Comment;
                    }
                }

                var mergedComment = mainCrit.Comment;
                if (bestCritScore > mainCrit.Score && !string.IsNullOrWhiteSpace(bestFollowUpComment))
                {
                    mergedComment = string.IsNullOrWhiteSpace(mergedComment)
                        ? bestFollowUpComment
                        : $"{mergedComment} (Bổ sung từ câu hỏi phụ: {bestFollowUpComment})";
                }

                mergedCriteria.Add(mainCrit with
                {
                    Score = bestCritScore,
                    Comment = mergedComment
                });

                if (mainResult.eval?.Details != null)
                {
                    var evalDetail = mainResult.eval.Details.FirstOrDefault(d => d.CriterionId == mainCrit.CriterionId)
                        ?? (critIdx < mainResult.eval.Details.Count ? mainResult.eval.Details.ElementAtOrDefault(critIdx) : null);
                    if (evalDetail != null)
                    {
                        evalDetail.Score = bestCritScore;
                        if (!string.IsNullOrWhiteSpace(mergedComment))
                        {
                            evalDetail.Comment = mergedComment;
                        }
                    }
                }
            }

            decimal originalMainScore = mainResult.score.Score;
            decimal mergedSum = mergedCriteria.Count > 0
                ? Math.Round(mergedCriteria.Sum(c => c.Score), 2, MidpointRounding.AwayFromZero)
                : 0.0m;
            bool hasCriterionImprovement = mergedSum > originalMainScore;

            decimal effectiveMainScore;
            if (hasCriterionImprovement || avgFollowUpScore >= originalMainScore)
            {
                effectiveMainScore = Math.Clamp(
                    mergedSum > 0.0m
                        ? Math.Max(mergedSum, weightedScore)
                        : Math.Max(originalMainScore, weightedScore),
                    0.0m,
                    10.0m);
            }
            else
            {
                effectiveMainScore = Math.Clamp(weightedScore, 0.0m, 10.0m);
                if (mergedCriteria.Count > 0 && mergedSum > 0.0m && effectiveMainScore != mergedSum)
                {
                    var scaledCriteria = new List<MockExamCriterionScoreDto>(mergedCriteria.Count);
                    decimal runningScaled = 0.0m;

                    for (int cIdx = 0; cIdx < mergedCriteria.Count; cIdx++)
                    {
                        var critDto = mergedCriteria[cIdx];
                        decimal scaledScore;
                        if (cIdx == mergedCriteria.Count - 1)
                        {
                            scaledScore = Math.Clamp(
                                Math.Round(effectiveMainScore - runningScaled, 2, MidpointRounding.AwayFromZero),
                                0.0m,
                                critDto.MaxScore);
                        }
                        else
                        {
                            scaledScore = Math.Clamp(
                                Math.Round(effectiveMainScore * (critDto.Score / mergedSum), 2, MidpointRounding.AwayFromZero),
                                0.0m,
                                critDto.MaxScore);
                            runningScaled += scaledScore;
                        }

                        scaledCriteria.Add(critDto with { Score = scaledScore });

                        if (mainResult.eval?.Details != null)
                        {
                            var evalDetail = mainResult.eval.Details.FirstOrDefault(d => d.CriterionId == critDto.CriterionId)
                                ?? (cIdx < mainResult.eval.Details.Count ? mainResult.eval.Details.ElementAtOrDefault(cIdx) : null);
                            if (evalDetail != null)
                            {
                                evalDetail.Score = scaledScore;
                            }
                        }
                    }

                    mergedCriteria = scaledCriteria;
                }
            }

            var effectiveCriteria = mergedCriteria;

            if (mainResult.eval != null)
            {
                mainResult.eval.TotalScore = effectiveMainScore;
            }

            results[i] = (
                mainResult.a,
                mainResult.score with
                {
                    Score = effectiveMainScore,
                    CriteriaScores = effectiveCriteria
                },
                mainResult.eval
            );
        }

        // 4. Điểm tổng = trung bình các câu chính (đã bao gồm điểm tích lũy từ câu hỏi phụ)
        var mainResults = results.Where(r => !r.a.IsFollowUp).ToList();
        int totalQuestions = mainResults.Count;
        int answeredQuestions = mainResults.Count(r =>
            !string.IsNullOrWhiteSpace(r.a.AnswerText) &&
            (r.a.Status == "answered" || r.a.Status == "graded"));

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
        session.SubmittedAt = submittedAt;

        foreach (var r in results)
        {
            if (r.eval != null)
            {
                r.a.Status = "graded";
                if (r.a.AiEvaluations == null || !r.a.AiEvaluations.Any(e => e.Id == r.eval.Id))
                {
                    _context.AiEvaluations.Add(r.eval);
                }
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

        SubmitMockExamAnswer.SubmitMockExamAnswerCommandHandler.ClearVoiceBaselines(
            session.Answers.Select(a => a.Id));

        return Result<MockExamScorecardDto>.Success(scorecard);
    }
}
