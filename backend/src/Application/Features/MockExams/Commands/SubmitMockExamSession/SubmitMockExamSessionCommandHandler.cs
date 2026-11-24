using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
/// Đồng bộ hóa đa tiến trình qua khóa tuần tự PostgreSQL (ExecuteMockExamGradingAsync) mà không giữ transaction trong lúc gọi LLM.
/// </summary>
public sealed class SubmitMockExamSessionCommandHandler(
    IApplicationDbContext context,
    IAiGradingService aiGradingService
) : IRequestHandler<SubmitMockExamSessionCommand, Result<MockExamScorecardDto>>
{
    private static readonly TimeSpan StaleGradingClaimTimeout = TimeSpan.FromMinutes(2);
    private readonly IApplicationDbContext _context = context;
    private readonly IAiGradingService _aiGradingService = aiGradingService;

    private sealed record ClaimedGradingSession(
        MockExamSession Session,
        DateTime SubmittedAt,
        bool IsOverdue);

    public async Task<Result<MockExamScorecardDto>> Handle(
        SubmitMockExamSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Pha 1 (Trong khóa PostgreSQL): Claim phiên thi thử nguyên tử in_progress/submitted -> submitted (tuân thủ ck_mock_sessions_status),
        // dọn các slot follow-up chưa trả lời và nhả khóa trước khi gọi LLM.
        var claimOutcome = await _context.ExecuteMockExamGradingAsync(request.SessionId, async lockCt =>
        {
            var claimedSession = await _context.MockExamSessions
                .Include(s => s.Course)
                .Include(s => s.ExamSet)
                    .ThenInclude(es => es!.Structure)
                .Include(s => s.Answers)
                    .ThenInclude(a => a.Question)
                        .ThenInclude(pq => pq.Rubric)
                            .ThenInclude(r => r.Criteria)
                .Include(s => s.Answers)
                    .ThenInclude(a => a.Question)
                        .ThenInclude(pq => pq.Clo)
                .Include(s => s.Answers)
                    .ThenInclude(a => a.AiEvaluations)
                        .ThenInclude(e => e.Details)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, lockCt);

            if (claimedSession == null)
            {
                return (Claimed: (ClaimedGradingSession?)null, Error: "Ca thi không tồn tại.");
            }

            if (claimedSession.StudentId != request.StudentId)
            {
                return (Claimed: (ClaimedGradingSession?)null, Error: "Bạn không có quyền nộp bài cho ca thi này.");
            }

            var nowUtc = DateTime.UtcNow;
            var isInProgress = string.Equals(claimedSession.Status, "in_progress", StringComparison.OrdinalIgnoreCase);
            var isSubmitted = string.Equals(claimedSession.Status, "submitted", StringComparison.OrdinalIgnoreCase);

            if (!isInProgress && !isSubmitted)
            {
                return (Claimed: (ClaimedGradingSession?)null, Error: "CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
            }

            DateTime claimedSubmittedAt;
            bool claimedIsOverdue;

            if (isSubmitted)
            {
                var hasPendingWork = claimedSession.Answers.Count > 0
                    && (!claimedSession.TotalScore.HasValue
                        || claimedSession.Answers.Any(a => !string.IsNullOrWhiteSpace(a.AnswerText) && a.Status != "graded"));

                if (!hasPendingWork)
                {
                    return (Claimed: (ClaimedGradingSession?)null, Error: "CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
                }

                var hasRetryableDlq = await _context.DeadLetterQueues
                    .AsNoTracking()
                    .AnyAsync(
                        q => q.CorrelationId == claimedSession.Id
                             && q.TaskType == "MockExamGrading"
                             && q.Status == "pending",
                        lockCt);

                var isAutoClosedExpiredWithoutClaim = claimedSession.SubmittedAt.HasValue
                    && claimedSession.SubmittedAt.Value == claimedSession.ExpiresAt
                    && nowUtc > claimedSession.ExpiresAt.AddSeconds(10);

                var isStaleInFlightClaim = !claimedSession.SubmittedAt.HasValue
                    || (nowUtc - claimedSession.SubmittedAt.Value) >= StaleGradingClaimTimeout;

                if (!hasRetryableDlq && !isAutoClosedExpiredWithoutClaim && !isStaleInFlightClaim)
                {
                    // Đã có worker/request khác vừa claim và đang chấm bài cho phiên này.
                    return (Claimed: (ClaimedGradingSession?)null, Error: "CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
                }

                var originalSubmittedAt = (isAutoClosedExpiredWithoutClaim || !claimedSession.SubmittedAt.HasValue)
                    ? nowUtc
                    : claimedSession.SubmittedAt.Value;
                claimedSubmittedAt = (isAutoClosedExpiredWithoutClaim || isStaleInFlightClaim || !claimedSession.SubmittedAt.HasValue)
                    ? nowUtc
                    : claimedSession.SubmittedAt.Value;
                var effectiveSubmittedAt = claimedSession.PausedAt ?? originalSubmittedAt;
                claimedIsOverdue = effectiveSubmittedAt > claimedSession.ExpiresAt.AddSeconds(10);
                claimedSession.PausedAt = null;
                claimedSession.SubmittedAt = claimedSubmittedAt;
            }
            else
            {
                // Server Master Timer check (kèm 10s Network Grace Period & hỗ trợ ca đang tạm dừng PausedAt)
                claimedSubmittedAt = claimedSession.SubmittedAt ?? nowUtc;
                var effectiveSubmittedAt = claimedSession.PausedAt ?? claimedSubmittedAt;
                claimedIsOverdue = effectiveSubmittedAt > claimedSession.ExpiresAt.AddSeconds(10);
                claimedSession.PausedAt = null;
                claimedSession.Status = "submitted";
                claimedSession.SubmittedAt = claimedSubmittedAt;
            }

            // Dọn dẹp các slot câu hỏi phụ đang chờ (pending / chưa có câu trả lời) trước khi sắp xếp và chấm điểm
            var unansweredFollowUps = claimedSession.Answers
                .Where(a => a.IsFollowUp && ((a.Status != "answered" && a.Status != "graded") || string.IsNullOrWhiteSpace(a.AnswerText)))
                .ToList();
            if (unansweredFollowUps.Count > 0)
            {
                _context.MockExamAnswers.RemoveRange(unansweredFollowUps);
                foreach (var u in unansweredFollowUps)
                {
                    claimedSession.Answers.Remove(u);
                }
            }

            await _context.SaveChangesAsync(lockCt);

            return (
                Claimed: (ClaimedGradingSession?)new ClaimedGradingSession(claimedSession, claimedSubmittedAt, claimedIsOverdue),
                Error: (string?)null);
        }, cancellationToken);

        if (claimOutcome.Error is not null || claimOutcome.Claimed is null)
        {
            return Result<MockExamScorecardDto>.Failure(claimOutcome.Error ?? "CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
        }

        var session = claimOutcome.Claimed.Session;
        var submittedAt = claimOutcome.Claimed.SubmittedAt;
        var isOverdue = claimOutcome.Claimed.IsOverdue;

        // Giữ đúng thứ tự đề thi; follow-up đi sau câu chính cùng OrderIndex.
        var sessionAnswers = MockExamAnswerOrdering.OrderAnswers(session.Answers);
        var newEvaluationsByAnswerId = new Dictionary<Guid, AiEvaluation>();
        var syncLock = new object();

        AiEvaluation? ResolveLatestAnswerEvaluation(MockExamAnswer targetAnswer)
        {
            lock (syncLock)
            {
                if (newEvaluationsByAnswerId.TryGetValue(targetAnswer.Id, out var newlyCreated))
                {
                    return newlyCreated;
                }
            }

            return targetAnswer.AiEvaluations?
                .OrderByDescending(e => e.EvaluatedAt)
                .FirstOrDefault();
        }

        // Pha 2 (Ngoài khóa DB): Chấm các câu trả lời chính trước để xác định tiêu chí yếu nhất, sau đó chấm các câu hỏi phụ theo thứ tự
        async Task<(MockExamAnswer a, MockExamQuestionScoreDto? score, AiEvaluation? eval)> EvaluateSingleAnswerAsync(MockExamAnswer a, int index)
        {
            var q = a.Question;
            int orderIndex = a.OrderIndex > 0 ? a.OrderIndex : index + 1;
            bool isAnswered = !string.IsNullOrWhiteSpace(a.AnswerText) && (a.Status == "answered" || a.Status == "graded");
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
                    a.ParentAnswerId,
                    q.Clo?.CloCode
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
                var effectiveCachedCriteria = (a.IsFollowUp && existingDetails.Count > 0 && existingDetails.Count < orderedCriteria.Count)
                    ? orderedCriteria.Where(c => existingDetails.Any(d => d.CriterionId == c.Id)).ToList()
                    : orderedCriteria;
                if (effectiveCachedCriteria.Count == 0)
                {
                    effectiveCachedCriteria = orderedCriteria;
                }

                for (int critIndex = 0; critIndex < effectiveCachedCriteria.Count; critIndex++)
                {
                    var c = effectiveCachedCriteria[critIndex];
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

                var cachedMaxSum = cachedCriterionScores.Sum(c => c.MaxScore);
                var cachedRawSum = cachedCriterionScores.Sum(c => c.Score);
                var cachedScore = existingDetails.Count > 0 && cachedCriterionScores.Count > 0
                    ? (cachedMaxSum > 0.0m && cachedMaxSum < 10.0m
                        ? Math.Clamp(Math.Round((cachedRawSum / cachedMaxSum) * 10.0m, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                        : Math.Clamp(Math.Round(cachedRawSum, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m))
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
                    a.ParentAnswerId,
                    q.Clo?.CloCode
                );

                return (a, cachedQuestionScoreDto, (AiEvaluation?)existingEval);
            }

            // Câu hỏi đã có câu trả lời và chưa có AiEvaluation -> Gửi Gemini AI chấm CoT Rubric 10.0đ
            MockExamAnswer? parentMainAnswer = null;
            int followUpOrdinal = 0;
            var previousFollowUpPrompts = new List<string>();

            if (a.IsFollowUp)
            {
                parentMainAnswer = sessionAnswers.FirstOrDefault(x => !x.IsFollowUp && x.Id == a.ParentAnswerId)
                    ?? sessionAnswers.FirstOrDefault(x => !x.IsFollowUp && x.QuestionId == a.QuestionId);

                var siblingFollowUps = sessionAnswers
                    .Where(x => x.IsFollowUp &&
                                ((a.ParentAnswerId.HasValue && x.ParentAnswerId == a.ParentAnswerId) || x.QuestionId == a.QuestionId ||
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

                var isEnglishCourse = FollowUpPromptBuilder.IsEnglishCourse(
                    session.Course?.Code,
                    orderedCriteria.Select(c => c.CriterionName),
                    q.Content);

                if (!isEnglishCourse && orderedCriteria.Count > 1)
                {
                    var rootEvalDetails = parentMainAnswer != null
                        ? ResolveLatestAnswerEvaluation(parentMainAnswer)?.Details
                        : null;
                    var alreadyTargetedFollowUpCriteria = siblingFollowUps
                        .Take(Math.Max(0, currentFollowUpIdx))
                        .Select(ResolveLatestAnswerEvaluation)
                        .Where(e => e?.Details != null && e.Details.Count == 1)
                        .Select(e => e!.Details.First().CriterionId)
                        .ToHashSet();

                    var candidateCriteria = orderedCriteria
                        .Where(c => !alreadyTargetedFollowUpCriteria.Contains(c.Id))
                        .ToList();
                    if (candidateCriteria.Count == 0)
                    {
                        candidateCriteria = orderedCriteria;
                    }

                    RubricCriterion? targetCriterion = null;
                    decimal lowestRatio = decimal.MaxValue;

                    if (rootEvalDetails != null && rootEvalDetails.Count > 0)
                    {
                        foreach (var crit in candidateCriteria)
                        {
                            var detail = rootEvalDetails.FirstOrDefault(d => d.CriterionId == crit.Id);
                            var score = detail?.Score ?? 0m;
                            var ratio = crit.MaxScore > 0m ? score / crit.MaxScore : 0m;
                            if (ratio < lowestRatio)
                            {
                                lowestRatio = ratio;
                                targetCriterion = crit;
                            }
                        }
                    }

                    targetCriterion ??= candidateCriteria[0];
                    orderedCriteria = new List<RubricCriterion> { targetCriterion };
                }
            }

            var criteriaList = orderedCriteria.Select(c => new AiGradingCriterion
            {
                Id = c.Id.ToString(),
                Name = c.CriterionName,
                Description = c.Description ?? string.Empty,
                MaxScore = c.MaxScore,
                Weight = c.Weight
            }).ToList();

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
                    if (detailDto != null)
                    {
                        var rawScore = detailDto.Score;
                        if (totalCriteriaMax > 0.0m && totalCriteriaMax < 10.0m && orderedCriteria.Count == 1 &&
                            rawScore > c.MaxScore && rawScore <= 10.0m)
                        {
                            rawScore = rawScore * (c.MaxScore / 10.0m);
                        }

                        score = Math.Clamp(Math.Round(rawScore, 2, MidpointRounding.AwayFromZero), 0.0m, c.MaxScore);
                    }
                    else
                    {
                        score = 0.0m;
                    }
                }
                else if (totalCriteriaMax > 0.0m && clampedAiScore > 0.0m)
                {
                    var targetScaleSum = (totalCriteriaMax < 10.0m && clampedAiScore > totalCriteriaMax)
                        ? clampedAiScore * (totalCriteriaMax / 10.0m)
                        : clampedAiScore;

                    if (critIndex == orderedCriteria.Count - 1)
                    {
                        score = Math.Clamp(
                            Math.Round(targetScaleSum - runningAllocatedScore, 2, MidpointRounding.AwayFromZero),
                            0.0m,
                            c.MaxScore);
                    }
                    else
                    {
                        score = Math.Clamp(
                            Math.Round(targetScaleSum * (c.MaxScore / totalCriteriaMax), 2, MidpointRounding.AwayFromZero),
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

            var sumCriterionScores = criterionScores.Sum(c => c.Score);
            var finalQuestionScore = criterionScores.Count > 0
                ? (totalCriteriaMax > 0.0m && totalCriteriaMax < 10.0m
                    ? Math.Clamp(Math.Round((sumCriterionScores / totalCriteriaMax) * 10.0m, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                    : Math.Clamp(Math.Round(sumCriterionScores, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m))
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
                a.ParentAnswerId,
                q.Clo?.CloCode
            );

            var aiEvaluation = new AiEvaluation
            {
                Id = evalId,
                AnswerType = "mock_exam",
                StudentId = session.StudentId,
                PracticeAnswerId = null,
                ExamSubmissionId = null,
                MockExamAnswerId = a.Id,
                TotalScore = finalQuestionScore,
                Feedback = gradingResult.Feedback,
                ConfidenceScore = gradingResult.ConfidenceScore,
                CotTrace = gradingResult.CotTrace,
                FollowUpPrompt = !string.IsNullOrWhiteSpace(gradingResult.FollowUpPrompt)
                    ? gradingResult.FollowUpPrompt.Trim()
                    : FollowUpPromptBuilder.TryExtractFollowUpFromCot(gradingResult.CotTrace),
                EvaluatedAt = DateTime.UtcNow,
                Details = evalDetails
            };

            return (a, questionScoreDto, (AiEvaluation?)aiEvaluation);
        }

        List<(MockExamAnswer a, MockExamQuestionScoreDto score, AiEvaluation? eval)> results;
        var rawResults = new (MockExamAnswer a, MockExamQuestionScoreDto? score, AiEvaluation? eval)[sessionAnswers.Count];
        var newEvaluationIds = new HashSet<Guid>();
        try
        {
            using var gradingGate = new SemaphoreSlim(3, 3);

            // Pha 2A: Chấm song song giới hạn tối đa 3 luồng AI cho các câu hỏi chính, ghi nhận kết quả mới vào từ điển nội bộ (chưa chạm DbContext)
            var mainIndexed = sessionAnswers
                .Select((a, idx) => (a, idx))
                .Where(x => !x.a.IsFollowUp)
                .ToList();

            var mainTasks = mainIndexed.Select(async item =>
            {
                await gradingGate.WaitAsync(cancellationToken);
                try
                {
                    return (item.idx, res: await EvaluateSingleAnswerAsync(item.a, item.idx));
                }
                finally
                {
                    gradingGate.Release();
                }
            }).ToArray();

            try
            {
                await Task.WhenAll(mainTasks);
            }
            finally
            {
                foreach (var task in mainTasks)
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        var (idx, res) = task.Result;
                        rawResults[idx] = res;
                        if (res.eval != null && (res.a.AiEvaluations == null || !res.a.AiEvaluations.Any(e => e.Id == res.eval.Id)))
                        {
                            lock (syncLock)
                            {
                                newEvaluationsByAnswerId[res.a.Id] = res.eval;
                                newEvaluationIds.Add(res.eval.Id);
                            }
                        }
                    }
                }
            }

            // Pha 2B: Gom nhóm câu phụ theo QuestionId; các nhóm câu gốc khác nhau qua SemaphoreSlim(3,3),
            // các câu phụ cùng 1 câu gốc chạy tuần tự theo thứ tự để câu sau đọc đúng alreadyTargetedFollowUpCriteria
            var followUpGroups = sessionAnswers
                .Select((a, idx) => (a, idx))
                .Where(x => x.a.IsFollowUp)
                .GroupBy(x => x.a.QuestionId)
                .ToList();

            var followUpGroupTasks = followUpGroups.Select(async group =>
            {
                foreach (var fuItem in group)
                {
                    (MockExamAnswer a, MockExamQuestionScoreDto? score, AiEvaluation? eval) res;
                    await gradingGate.WaitAsync(cancellationToken);
                    try
                    {
                        res = await EvaluateSingleAnswerAsync(fuItem.a, fuItem.idx);
                    }
                    finally
                    {
                        gradingGate.Release();
                    }

                    lock (syncLock)
                    {
                        rawResults[fuItem.idx] = res;
                        if (res.eval != null && (res.a.AiEvaluations == null || !res.a.AiEvaluations.Any(e => e.Id == res.eval.Id)))
                        {
                            newEvaluationsByAnswerId[res.a.Id] = res.eval;
                            newEvaluationIds.Add(res.eval.Id);
                        }
                    }
                }
            }).ToArray();

            await Task.WhenAll(followUpGroupTasks);

            results = rawResults
                .Where(r => r.score is not null)
                .Select(r => (a: r.a, score: r.score!, eval: r.eval))
                .ToList();
        }
        catch (Exception ex)
        {
            // Pha 3B (Trong khóa PostgreSQL): Persist First bảo toàn các câu đã chấm xong trước khi gặp lỗi
            // và TUYỆT ĐỐI KHÔNG hạ ngược phiên đã "graded" về "submitted".
            return await _context.ExecuteMockExamGradingAsync(request.SessionId, async _ =>
            {
                var freshStatus = await _context.MockExamSessions
                    .AsNoTracking()
                    .Where(s => s.Id == request.SessionId)
                    .Select(s => s.Status)
                    .FirstOrDefaultAsync(CancellationToken.None);

                if (string.Equals(freshStatus, "graded", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<MockExamScorecardDto>.Failure("CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
                }

                var trackedSession = await _context.MockExamSessions
                    .Include(s => s.Answers)
                    .FirstOrDefaultAsync(s => s.Id == request.SessionId, CancellationToken.None);

                if (trackedSession == null || string.Equals(trackedSession.Status, "graded", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<MockExamScorecardDto>.Failure("CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
                }

                var trackedAnswerById = trackedSession.Answers.ToDictionary(a => a.Id);
                var completedNewResults = rawResults
                    .Where(r => r.a != null && r.eval != null && newEvaluationIds.Contains(r.eval.Id))
                    .ToList();

                if (completedNewResults.Count > 0)
                {
                    var candidateAnswerIds = completedNewResults.Select(r => r.a.Id).Distinct().ToList();
                    var existingEvaluatedAnswerIds = (await _context.AiEvaluations
                        .AsNoTracking()
                        .Where(e => e.MockExamAnswerId.HasValue && candidateAnswerIds.Contains(e.MockExamAnswerId.Value))
                        .Select(e => e.MockExamAnswerId!.Value)
                        .ToListAsync(CancellationToken.None))
                        .ToHashSet();

                    foreach (var r in completedNewResults)
                    {
                        if (trackedAnswerById.TryGetValue(r.a.Id, out var trackedAnswer))
                        {
                            trackedAnswer.Status = "graded";
                        }

                        if (!existingEvaluatedAnswerIds.Contains(r.a.Id))
                        {
                            _context.AiEvaluations.Add(r.eval!);
                            existingEvaluatedAnswerIds.Add(r.a.Id);
                        }
                    }
                }

                trackedSession.PausedAt = null;
                trackedSession.Status = "submitted";
                trackedSession.SubmittedAt = submittedAt;

                var now = DateTime.UtcNow;
                var existingDlq = await _context.DeadLetterQueues
                    .OrderByDescending(q => q.CreatedAt)
                    .FirstOrDefaultAsync(
                        q => q.CorrelationId == trackedSession.Id
                             && q.TaskType == "MockExamGrading"
                             && q.Status != "resolved",
                        CancellationToken.None);

                if (existingDlq != null)
                {
                    existingDlq.RetryCount++;
                    existingDlq.LastAttemptAt = now;
                    existingDlq.ErrorMessage = ex.Message;
                    if (existingDlq.RetryCount >= 9)
                    {
                        existingDlq.Status = "abandoned";
                        existingDlq.NextRetryAt = null;
                    }
                    else
                    {
                        existingDlq.Status = "pending";
                        existingDlq.NextRetryAt = now.AddMinutes(5);
                    }
                }
                else
                {
                    var dlqEntry = new OralExamination.Domain.Entities.DeadLetterQueue
                    {
                        TaskType = "MockExamGrading",
                        CorrelationId = trackedSession.Id,
                        FlowType = "MOCK_EXAM",
                        ManagedByRole = "admin",
                        PayloadJson = JsonSerializer.Serialize(new { SessionId = trackedSession.Id, StudentId = trackedSession.StudentId }),
                        ErrorMessage = ex.Message,
                        RetryCount = 1,
                        Status = "pending",
                        NextRetryAt = now.AddMinutes(5),
                        LastAttemptAt = now,
                        CreatedAt = now
                    };
                    _context.DeadLetterQueues.Add(dlqEntry);
                }

                await _context.SaveChangesAsync(CancellationToken.None);
                return Result<MockExamScorecardDto>.Failure("DEFERRED_GRADING:Bài thi thử đã được lưu an toàn. Hệ thống chấm điểm AI đang tạm thời bận và sẽ tự động chấm bù trong ít phút.");
            }, CancellationToken.None);
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
                                || r.a.QuestionId == mainResult.a.QuestionId)
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
                        ?? (fuCriteria.Count == mainCriteria.Count && critIdx < fuCriteria.Count ? fuCriteria[critIdx] : null);

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

            // Đồng nhất quy tắc Follow-up (MF-01 / MF-02 / MF-05): câu hỏi phụ chỉ bổ sung/bù điểm,
            // điểm hiệu dụng của câu chính KHÔNG BAO GIỜ thấp hơn điểm câu trả lời gốc.
            decimal effectiveMainScore;
            if (hasCriterionImprovement)
            {
                effectiveMainScore = Math.Clamp(mergedSum, originalMainScore, 10.0m);
            }
            else
            {
                effectiveMainScore = Math.Clamp(Math.Max(originalMainScore, weightedScore), 0.0m, 10.0m);
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

        // Pha 3A (Trong khóa PostgreSQL): Commit điểm tổng, chuyển trạng thái "graded" và giải quyết DLQ nguyên tử
        var commitResult = await _context.ExecuteMockExamGradingAsync(request.SessionId, async lockCt =>
        {
            var freshStatus = await _context.MockExamSessions
                .AsNoTracking()
                .Where(s => s.Id == request.SessionId)
                .Select(s => s.Status)
                .FirstOrDefaultAsync(lockCt);

            if (string.Equals(freshStatus, "graded", StringComparison.OrdinalIgnoreCase))
            {
                return Result<MockExamScorecardDto>.Failure("CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
            }

            var trackedSession = await _context.MockExamSessions
                .Include(s => s.Course)
                .Include(s => s.ExamSet)
                    .ThenInclude(es => es!.Structure)
                .Include(s => s.Answers)
                    .ThenInclude(a => a.AiEvaluations)
                        .ThenInclude(e => e.Details)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, lockCt);

            if (trackedSession == null || string.Equals(trackedSession.Status, "graded", StringComparison.OrdinalIgnoreCase))
            {
                return Result<MockExamScorecardDto>.Failure("CONFLICT: Ca thi đã được nộp và chấm điểm trước đó.");
            }

            trackedSession.TotalScore = totalScore;
            trackedSession.Status = "graded";
            trackedSession.SubmittedAt = submittedAt;
            trackedSession.PausedAt = null;

            var nowForDlq = DateTime.UtcNow;
            var pendingDlqEntries = await _context.DeadLetterQueues
                .Where(q => q.CorrelationId == trackedSession.Id && q.Status == "pending")
                .ToListAsync(lockCt);
            foreach (var dlq in pendingDlqEntries)
            {
                dlq.Status = "resolved";
                dlq.ResolvedAt = nowForDlq;
                dlq.NextRetryAt = null;
                dlq.ResolutionNotes ??= "Auto-resolved after successful mock exam grading.";
            }

            var trackedAnswersById = trackedSession.Answers.ToDictionary(a => a.Id);
            var candidateAnswerIds = results
                .Where(r => r.eval != null)
                .Select(r => r.a.Id)
                .Distinct()
                .ToList();

            var existingEvalsByAnswerId = candidateAnswerIds.Count > 0
                ? (await _context.AiEvaluations
                    .Include(e => e.Details)
                    .Where(e => e.MockExamAnswerId.HasValue && candidateAnswerIds.Contains(e.MockExamAnswerId.Value))
                    .ToListAsync(lockCt))
                    .GroupBy(e => e.MockExamAnswerId!.Value)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.EvaluatedAt).First())
                : new Dictionary<Guid, AiEvaluation>();

            foreach (var r in results)
            {
                if (r.eval == null)
                {
                    continue;
                }

                trackedAnswersById.TryGetValue(r.a.Id, out var trackedAnswer);
                if (trackedAnswer != null)
                {
                    trackedAnswer.Status = "graded";
                }

                if (existingEvalsByAnswerId.TryGetValue(r.a.Id, out var existingEval))
                {
                    if (!ReferenceEquals(existingEval, r.eval))
                    {
                        existingEval.TotalScore = r.eval.TotalScore;
                        existingEval.ConfidenceScore = r.eval.ConfidenceScore;
                        existingEval.CotTrace = r.eval.CotTrace;
                        existingEval.Feedback = r.eval.Feedback;
                        existingEval.FollowUpPrompt = r.eval.FollowUpPrompt;
                        existingEval.EvaluatedAt = r.eval.EvaluatedAt;

                        if (r.eval.Details != null)
                        {
                            var existingDetailsByCriterion = existingEval.Details
                                .GroupBy(d => d.CriterionId)
                                .ToDictionary(g => g.Key, g => g.First());
                            var keptDetailIds = new HashSet<Guid>();
                            var seenIncomingCriteria = new HashSet<Guid>();

                            foreach (var updatedDetail in r.eval.Details)
                            {
                                if (!seenIncomingCriteria.Add(updatedDetail.CriterionId))
                                {
                                    continue;
                                }

                                if (existingDetailsByCriterion.TryGetValue(updatedDetail.CriterionId, out var trackedDetail))
                                {
                                    trackedDetail.Score = updatedDetail.Score;
                                    trackedDetail.Comment = updatedDetail.Comment;
                                    keptDetailIds.Add(trackedDetail.Id);
                                }
                                else
                                {
                                    var replacementDetail = new AiEvaluationDetail
                                    {
                                        Id = Guid.NewGuid(),
                                        EvaluationId = existingEval.Id,
                                        CriterionId = updatedDetail.CriterionId,
                                        Score = updatedDetail.Score,
                                        Comment = updatedDetail.Comment
                                    };
                                    existingEval.Details.Add(replacementDetail);
                                    _context.AiEvaluationDetails.Add(replacementDetail);
                                    keptDetailIds.Add(replacementDetail.Id);
                                }
                            }

                            var staleDetails = existingEval.Details
                                .Where(d => !keptDetailIds.Contains(d.Id))
                                .ToList();
                            if (staleDetails.Count > 0)
                            {
                                _context.AiEvaluationDetails.RemoveRange(staleDetails);
                                foreach (var stale in staleDetails)
                                {
                                    existingEval.Details.Remove(stale);
                                }
                            }
                        }
                    }
                }
                else
                {
                    r.eval.MockExamAnswerId = r.a.Id;
                    _context.AiEvaluations.Add(r.eval);
                    existingEvalsByAnswerId[r.a.Id] = r.eval;
                }
            }

            await _context.SaveChangesAsync(lockCt);

            // 6. Xây dựng Bảng điểm Instant Scorecard hoàn chỉnh (bảo toàn DurationMinutes không bị phình do pause)
            int baseDuration = await MockExamClockHelper.ResolveBaseDurationMinutesAsync(_context, trackedSession, lockCt);
            int computedDuration = trackedSession.ExpiresAt > trackedSession.StartedAt
                ? (int)Math.Round((trackedSession.ExpiresAt - trackedSession.StartedAt).TotalMinutes)
                : baseDuration;
            int durationMinutes = Math.Min(computedDuration, baseDuration);

            var scorecard = new MockExamScorecardDto(
                SessionId: trackedSession.Id,
                CourseId: trackedSession.CourseId,
                CourseCode: trackedSession.Course?.Code ?? string.Empty,
                CourseName: trackedSession.Course?.Name ?? string.Empty,
                TotalScore: totalScore,
                Classification: classification,
                TotalQuestions: totalQuestions,
                AnsweredQuestions: answeredQuestions,
                DurationMinutes: durationMinutes,
                IsOverdue: isOverdue,
                StartedAt: trackedSession.StartedAt,
                SubmittedAt: submittedAt,
                Questions: results.Select(r => r.score).ToList()
            );

            return Result<MockExamScorecardDto>.Success(scorecard);
        }, cancellationToken);

        return commitResult;
    }
}
