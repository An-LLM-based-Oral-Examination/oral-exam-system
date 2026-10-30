using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OralExamination.API.Hubs;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Domain.Entities;
using Polly;
using Polly.Retry;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.API.Workers;

public class GradingQueueWorker : BackgroundService
{
    private readonly IGradingQueueChannel _queueChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<PracticeHub, IPracticeClient> _hubContext;
    private readonly ILogger<GradingQueueWorker> _logger;

    // Cấu hình Polly Resilience Pipeline: Retry 3 lần với Exponential Backoff (2s, 4s, 8s)
    private readonly ResiliencePipeline _resiliencePipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(2),
            ShouldHandle = new PredicateBuilder().Handle<Exception>()
        })
        .Build();

    public GradingQueueWorker(
        IGradingQueueChannel queueChannel,
        IServiceProvider serviceProvider,
        IHubContext<PracticeHub, IPracticeClient> hubContext,
        ILogger<GradingQueueWorker> logger)
    {
        _queueChannel = queueChannel;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var task in _queueChannel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Processing GradingTask for Answer {AnswerId}", task.AnswerId);
                    
                    // Thực thi qua Polly Resilience Pipeline
                    await _resiliencePipeline.ExecuteAsync(async ct =>
                    {
                        await ProcessGradingTaskAsync(task, ct);
                    }, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi nghiêm trọng khi chấm điểm AI cho Answer {AnswerId}. Đẩy vào Dead-Letter Queue (DLQ).", task.AnswerId);

                    // 1. Thực thi logic lưu DLQ và cập nhật answer.Status = "failed" TRƯỚC TIÊN (bảo đảm bền vững dữ liệu)
                    try
                    {
                        using var errorScope = _serviceProvider.CreateScope();
                        var errorDbContext = errorScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                        var now = DateTime.UtcNow;

                        var existingDlq = await errorDbContext.DeadLetterQueues
                            .OrderByDescending(d => d.CreatedAt)
                            .FirstOrDefaultAsync(d => d.TaskType == "PracticeGrading" && d.CorrelationId == task.AnswerId, CancellationToken.None);
                        if (existingDlq != null)
                        {
                            existingDlq.RetryCount += 3;
                            existingDlq.ErrorMessage = ex.Message;
                            existingDlq.LastAttemptAt = now;
                            existingDlq.ResolvedAt = null;
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
                            var dlqEntry = new DeadLetterQueue
                            {
                                TaskType = "PracticeGrading",
                                CorrelationId = task.AnswerId,
                                PayloadJson = System.Text.Json.JsonSerializer.Serialize(task),
                                ErrorMessage = ex.Message,
                                RetryCount = 3,
                                Status = "pending",
                                NextRetryAt = now.AddMinutes(5),
                                LastAttemptAt = now,
                                CreatedAt = now
                            };
                            errorDbContext.DeadLetterQueues.Add(dlqEntry);
                        }

                        var answer = await errorDbContext.PracticeAnswers.FindAsync(new object[] { task.AnswerId });
                        if (answer != null)
                        {
                            answer.Status = "failed";
                        }

                        await errorDbContext.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (Exception dlqEx)
                    {
                        _logger.LogCritical(dlqEx, "Không thể ghi bản ghi vào DeadLetterQueue cho Answer {AnswerId}!", task.AnswerId);
                    }

                    // 2. Gửi thông báo thất bại cho frontend qua SignalR Group trong khối try/catch độc lập riêng biệt
                    try
                    {
                        await _hubContext.Clients.Group($"session_{task.SessionId}").ReceiveGradingError(task.AnswerId, "Hệ thống AI hiện đang quá tải. Đã lưu bài làm và sẽ chấm lại sau.");
                    }
                    catch (Exception signalrEx)
                    {
                        _logger.LogWarning(signalrEx, "Không thể gửi SignalR ReceiveGradingError cho Session {SessionId}, Answer {AnswerId}", task.SessionId, task.AnswerId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown khi Host dừng
        }
    }

    private async Task ProcessGradingTaskAsync(GradingTask gradingTask, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var aiGradingService = scope.ServiceProvider.GetRequiredService<IAiGradingService>();

        var question = await dbContext.PracticeQuestions
            .Include(q => q.Course)
            .Include(q => q.Rubric)
            .ThenInclude(r => r.Criteria)
            .AsSingleQuery()
            .FirstOrDefaultAsync(q => q.Id == gradingTask.QuestionId, cancellationToken);

        if (question == null || question.Rubric == null || !question.Rubric.Criteria.Any())
        {
            throw new Exception("Không tìm thấy câu hỏi hoặc barem rubric.");
        }

        // Tải lịch sử các câu trả lời trước đó của cùng câu hỏi này trong phiên luyện tập
        var priorQuestionAnswers = await dbContext.PracticeAnswers
            .AsNoTracking()
            .Include(a => a.AiEvaluations)
                .ThenInclude(e => e.Details)
            .Where(a => a.SessionId == gradingTask.SessionId &&
                        a.QuestionId == gradingTask.QuestionId &&
                        a.Id != gradingTask.AnswerId)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync(cancellationToken);

        // Đếm tổng số lượt câu trả lời phụ của câu hỏi này trong phiên (bao gồm lượt hiện tại nếu IsFollowUp == true)
        var existingFollowUpsCount = priorQuestionAnswers.Count(a => a.IsFollowUp) + (gradingTask.IsFollowUp ? 1 : 0);

        var rootAnswer = priorQuestionAnswers.FirstOrDefault(a => !a.IsFollowUp);
        var orderedPriorAnswers = rootAnswer != null
            ? new[] { rootAnswer }.Concat(priorQuestionAnswers.Where(a => a.Id != rootAnswer.Id).OrderBy(a => a.SubmittedAt))
            : priorQuestionAnswers.OrderBy(a => a.SubmittedAt);
        var priorTranscripts = orderedPriorAnswers
            .Where(a => !string.IsNullOrWhiteSpace(a.AnswerText))
            .Select(a => a.AnswerText!.Trim())
            .ToList();
        var originalAnswerTranscript = priorTranscripts.Count > 0
            ? string.Join(" ", priorTranscripts)
            : (!gradingTask.IsFollowUp ? gradingTask.AnswerText : null);

        var previousFollowUpPrompts = priorQuestionAnswers
            .SelectMany(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).Take(1))
            .Select(e => FollowUpPromptBuilder.TryExtractFollowUpFromCot(e.CotTrace))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? answeredFollowUpPrompt = null;
        if (gradingTask.IsFollowUp)
        {
            var currentAnswerPrompt = await dbContext.PracticeAnswers
                .AsNoTracking()
                .Where(a => a.Id == gradingTask.AnswerId)
                .Select(a => a.PromptText)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(currentAnswerPrompt))
            {
                answeredFollowUpPrompt = currentAnswerPrompt.Trim();
            }
            else if (gradingTask.ParentAnswerId.HasValue)
            {
                var parentCot = await dbContext.AiEvaluations
                    .AsNoTracking()
                    .Where(e => e.PracticeAnswerId == gradingTask.ParentAnswerId.Value)
                    .OrderByDescending(e => e.EvaluatedAt)
                    .Select(e => e.CotTrace)
                    .FirstOrDefaultAsync(cancellationToken);

                answeredFollowUpPrompt = FollowUpPromptBuilder.TryExtractFollowUpFromCot(parentCot);
            }

            if (string.IsNullOrWhiteSpace(answeredFollowUpPrompt))
            {
                answeredFollowUpPrompt = previousFollowUpPrompts.LastOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(answeredFollowUpPrompt) &&
                !previousFollowUpPrompts.Contains(answeredFollowUpPrompt, StringComparer.OrdinalIgnoreCase))
            {
                previousFollowUpPrompts.Add(answeredFollowUpPrompt);
            }
        }

        var criteriaContext = question.Rubric.Criteria.Select(c => new AiGradingCriterion
        {
            Id = c.Id.ToString(),
            Name = c.CriterionName,
            Description = c.Description ?? string.Empty,
            MaxScore = c.MaxScore,
            Weight = c.Weight
        }).ToList();

        var request = new AiGradingRequest
        {
            QuestionContent = question.Content,
            SampleAnswer = question.SampleAnswer ?? string.Empty,
            CourseCode = question.Course?.Code,
            Transcript = gradingTask.AnswerText,
            Criteria = criteriaContext,
            QuestionImageUrl = question.ImageUrl,
            QuestionImageDescription = question.ImageDescription,
            IsFollowUp = gradingTask.IsFollowUp,
            FollowUpPrompt = answeredFollowUpPrompt,
            FollowUpIndex = existingFollowUpsCount,
            OriginalAnswerTranscript = originalAnswerTranscript,
            PreviousFollowUpPrompts = previousFollowUpPrompts,
            ThrowOnTransientFailure = true
        };

        var gradingResult = await aiGradingService.GradeAnswerAsync(request, cancellationToken);

        var orderedCriteria = question.Rubric.Criteria
            .OrderBy(c => c.OrderIndex)
            .ToList();

        var evalDetails = new System.Collections.Generic.List<AiEvaluationDetail>();
        var criteriaScoreDtos = new System.Collections.Generic.List<PracticeEvaluationDetailDto>();
        var rawDetails = gradingResult.Details ?? new System.Collections.Generic.List<GradingDetailDto>();
        var matchedByCriterionIndex = new GradingDetailDto?[orderedCriteria.Count];
        var usedRawIndices = new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < orderedCriteria.Count; i++)
        {
            var criterion = orderedCriteria[i];
            int matchedIdx = -1;

            for (int rIdx = 0; rIdx < rawDetails.Count; rIdx++)
            {
                if (usedRawIndices.Contains(rIdx))
                {
                    continue;
                }

                var candidateId = rawDetails[rIdx].CriterionId?.Trim();
                if (string.Equals(candidateId, criterion.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    matchedIdx = rIdx;
                    break;
                }
            }

            if (matchedIdx < 0 && !string.IsNullOrWhiteSpace(criterion.CriterionName))
            {
                for (int rIdx = 0; rIdx < rawDetails.Count; rIdx++)
                {
                    if (usedRawIndices.Contains(rIdx))
                    {
                        continue;
                    }

                    var candidateId = rawDetails[rIdx].CriterionId?.Trim();
                    if (string.Equals(candidateId, criterion.CriterionName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        matchedIdx = rIdx;
                        break;
                    }
                }
            }

            if (matchedIdx >= 0)
            {
                usedRawIndices.Add(matchedIdx);
                matchedByCriterionIndex[i] = rawDetails[matchedIdx];
            }
        }

        var totalCriteriaMax = orderedCriteria.Sum(c => c.MaxScore);
        var clampedOverallScore = Math.Clamp(Math.Round(gradingResult.Score, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m);
        decimal runningAllocatedScore = 0.0m;

        for (int i = 0; i < orderedCriteria.Count; i++)
        {
            var criterion = orderedCriteria[i];
            var matchedDetail = matchedByCriterionIndex[i];
            if (matchedDetail == null && i < rawDetails.Count && !usedRawIndices.Contains(i))
            {
                usedRawIndices.Add(i);
                matchedDetail = rawDetails[i];
            }

            decimal clampedScore;
            if (rawDetails.Count > 0)
            {
                clampedScore = matchedDetail != null
                    ? Math.Clamp(Math.Round(matchedDetail.Score, 2, MidpointRounding.AwayFromZero), 0.0m, criterion.MaxScore)
                    : 0.0m;
            }
            else if (totalCriteriaMax > 0.0m && clampedOverallScore > 0.0m)
            {
                if (i == orderedCriteria.Count - 1)
                {
                    clampedScore = Math.Clamp(
                        Math.Round(clampedOverallScore - runningAllocatedScore, 2, MidpointRounding.AwayFromZero),
                        0.0m,
                        criterion.MaxScore);
                }
                else
                {
                    clampedScore = Math.Clamp(
                        Math.Round(clampedOverallScore * (criterion.MaxScore / totalCriteriaMax), 2, MidpointRounding.AwayFromZero),
                        0.0m,
                        criterion.MaxScore);
                    runningAllocatedScore += clampedScore;
                }
            }
            else
            {
                clampedScore = 0.0m;
            }

            var comment = !string.IsNullOrWhiteSpace(matchedDetail?.Comment)
                ? matchedDetail!.Comment
                : clampedScore > 0.0m
                    ? $"Đạt {clampedScore:0.0}/{criterion.MaxScore:0.0}đ cho tiêu chí '{criterion.CriterionName}'."
                    : $"Chưa đáp ứng tiêu chí '{criterion.CriterionName}' (0.0/{criterion.MaxScore:0.0}đ).";

            evalDetails.Add(new AiEvaluationDetail
            {
                CriterionId = criterion.Id,
                Score = clampedScore,
                Comment = comment
            });

            criteriaScoreDtos.Add(new PracticeEvaluationDetailDto
            {
                CriterionId = criterion.Id,
                CriterionName = criterion.CriterionName,
                Score = clampedScore,
                MaxScore = criterion.MaxScore > 0m ? criterion.MaxScore : 10.0m,
                Comment = comment
            });
        }

        var finalTotalScore = evalDetails.Count > 0
            ? Math.Clamp(Math.Round(evalDetails.Sum(d => d.Score), 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
            : clampedOverallScore;

        // KÍCH HOẠT FOLLOW-UP ENGINE MF-01: Điểm ranh giới 4.0 <= Score <= 8.0 ở chế độ Per-Question
        bool isBorderlineScore = finalTotalScore >= 4.0m && finalTotalScore <= 8.0m;
        bool needsFollowUp = false;
        string? resolvedFollowUpPrompt = null;

        if (!gradingTask.IsFullSession && isBorderlineScore)
        {
            // Đọc cấu hình số câu hỏi phụ tối đa từ bảng system_configs (1-5 câu, mặc định 2 câu)
            var maxFollowUpConfig = await dbContext.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Key == "MaxPracticeFollowUpQuestions", cancellationToken);
            int maxAllowedFollowUps = (maxFollowUpConfig != null && int.TryParse(maxFollowUpConfig.Value, out var parsedMaxFollowUps))
                ? Math.Clamp(parsedMaxFollowUps, 1, 5)
                : 2;

            if (existingFollowUpsCount < maxAllowedFollowUps)
            {
                try
                {
                    // Tìm tiêu chí có tỷ lệ điểm thấp nhất để đặt câu hỏi phụ xoáy sâu đúng chỗ hổng kiến thức
                    RubricCriterion? weakestCriterion = null;
                    decimal lowestRatio = decimal.MaxValue;
                    for (int i = 0; i < orderedCriteria.Count; i++)
                    {
                        var crit = orderedCriteria[i];
                        var detailScore = i < evalDetails.Count ? evalDetails[i].Score : 0m;
                        var ratio = crit.MaxScore > 0m ? detailScore / crit.MaxScore : 0m;
                        if (ratio < lowestRatio)
                        {
                            lowestRatio = ratio;
                            weakestCriterion = crit;
                        }
                    }

                    int nextFollowUpSequence = existingFollowUpsCount + 1;
                    var candidateFollowUp = FollowUpPromptBuilder.ResolveFollowUpPrompt(
                        question.Course?.Code,
                        null,
                        gradingResult.FollowUpPrompt,
                        nextFollowUpSequence,
                        weakestCriterion?.CriterionName,
                        weakestCriterion?.Description,
                        orderedCriteria.Select(c => c.CriterionName),
                        question.Content,
                        null,
                        gradingTask.AnswerText,
                        originalAnswerTranscript,
                        previousFollowUpPrompts);

                    if (!string.IsNullOrWhiteSpace(candidateFollowUp))
                    {
                        needsFollowUp = true;
                        resolvedFollowUpPrompt = candidateFollowUp;
                    }
                    else
                    {
                        needsFollowUp = false;
                        resolvedFollowUpPrompt = null;
                        gradingResult.Feedback = string.IsNullOrWhiteSpace(gradingResult.Feedback)
                            ? "Không thể tạo câu hỏi đào sâu, hệ thống chuyển sang câu hỏi tiếp theo."
                            : $"{gradingResult.Feedback.TrimEnd()} Không thể tạo câu hỏi đào sâu, hệ thống chuyển sang câu hỏi tiếp theo.";
                    }
                }
                catch (Exception followUpEx)
                {
                    _logger.LogWarning(
                        followUpEx,
                        "Lỗi khi tạo câu hỏi follow-up cho Answer {AnswerId}. Bỏ qua follow-up và chuyển sang câu hỏi tiếp theo.",
                        gradingTask.AnswerId);
                    needsFollowUp = false;
                    resolvedFollowUpPrompt = null;
                    gradingResult.Feedback = string.IsNullOrWhiteSpace(gradingResult.Feedback)
                        ? "Không thể tạo câu hỏi đào sâu, hệ thống chuyển sang câu hỏi tiếp theo."
                        : $"{gradingResult.Feedback.TrimEnd()} Không thể tạo câu hỏi đào sâu, hệ thống chuyển sang câu hỏi tiếp theo.";
                }
            }
        }

        var cotToPersist = needsFollowUp
            ? FollowUpPromptBuilder.AppendFollowUpTagToCot(gradingResult.CotTrace, resolvedFollowUpPrompt)
            : gradingResult.CotTrace;

        var evaluation = new AiEvaluation
        {
            AnswerType = "practice",
            PracticeAnswerId = gradingTask.AnswerId,
            TotalScore = finalTotalScore,
            ConfidenceScore = gradingResult.ConfidenceScore,
            Feedback = gradingResult.Feedback,
            CotTrace = cotToPersist,
            Details = evalDetails
        };

        dbContext.AiEvaluations.Add(evaluation);

        // Cập nhật câu trả lời: DÙNG 'graded' THAY VÌ 'AI_GRADED' (Tuân thủ ck_practice_answers_status)
        var answer = await dbContext.PracticeAnswers.FindAsync(new object[] { gradingTask.AnswerId }, cancellationToken);
        if (answer != null)
        {
            answer.Status = "graded";
            if (gradingTask.IsFollowUp && string.IsNullOrWhiteSpace(answer.PromptText) && !string.IsNullOrWhiteSpace(answeredFollowUpPrompt))
            {
                answer.PromptText = answeredFollowUpPrompt;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var detailDto = new PracticeAnswerDetailDto
        {
            AnswerId = gradingTask.AnswerId,
            QuestionId = gradingTask.QuestionId,
            AnswerText = gradingTask.AnswerText,
            IsFollowUp = gradingTask.IsFollowUp,
            ParentAnswerId = gradingTask.ParentAnswerId,
            PromptText = answer?.PromptText ?? (gradingTask.IsFollowUp ? answeredFollowUpPrompt : null),
            Status = "graded",
            SubmittedAt = answer?.SubmittedAt ?? DateTime.UtcNow,
            TotalScore = evaluation.TotalScore,
            Feedback = evaluation.Feedback,
            IsSuspicious = gradingResult.IsSuspicious,
            ConfidenceScore = evaluation.ConfidenceScore,
            NeedsFollowUp = needsFollowUp,
            FollowUpPrompt = resolvedFollowUpPrompt,
            CriteriaScores = criteriaScoreDtos
        };

        // Bắn SignalR RIÊNG cho nhóm Session (Bảo vệ tính riêng tư, không dùng Clients.All)
        await _hubContext.Clients.Group($"session_{gradingTask.SessionId}").ReceiveGradingResult(detailDto);
    }
}
