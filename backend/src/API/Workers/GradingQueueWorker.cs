using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
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
    private readonly IHubContext<InClassShiftHub, IInClassShiftClient>? _inClassHubContext;
    private readonly ILogger<GradingQueueWorker> _logger;
    private readonly int _maxDegreeOfParallelism;

    // Cấu hình Polly Resilience Pipeline: Retry 3 lần với Exponential Backoff (mặc định 2s, 4s, 8s)
    private readonly ResiliencePipeline _resiliencePipeline;

    public GradingQueueWorker(
        IGradingQueueChannel queueChannel,
        IServiceProvider serviceProvider,
        IHubContext<PracticeHub, IPracticeClient> hubContext,
        ILogger<GradingQueueWorker> logger,
        IConfiguration? configuration = null,
        IHubContext<InClassShiftHub, IInClassShiftClient>? inClassHubContext = null)
    {
        _queueChannel = queueChannel;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _inClassHubContext = inClassHubContext;
        _logger = logger;

        var configuredParallelism = configuration?["GradingQueue:MaxDegreeOfParallelism"]
            ?? configuration?["GradingQueue__MaxDegreeOfParallelism"]
            ?? Environment.GetEnvironmentVariable("GRADING_QUEUE_MAX_PARALLELISM");
        _maxDegreeOfParallelism = int.TryParse(configuredParallelism, out var parsedParallelism)
            ? Math.Clamp(parsedParallelism, 1, 32)
            : 12;

        var configuredRetryDelayMs = configuration?["GradingQueue:RetryDelayMilliseconds"]
            ?? configuration?["GradingQueue__RetryDelayMilliseconds"];
        var retryDelayMs = int.TryParse(configuredRetryDelayMs, out var parsedRetryDelayMs)
            ? Math.Clamp(parsedRetryDelayMs, 0, 60000)
            : 2000;

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(retryDelayMs),
                ShouldHandle = new PredicateBuilder().Handle<Exception>()
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = stoppingToken
            };

            await Parallel.ForEachAsync(
                _queueChannel.ReadAllAsync(stoppingToken),
                parallelOptions,
                async (task, workerToken) =>
                {
                    try
                    {
                        _logger.LogInformation("Processing GradingTask for Answer {AnswerId}, InClassSubmissionId {InClassId}", task.AnswerId, task.InClassSubmissionId);
                        
                        // Thực thi qua Polly Resilience Pipeline
                        await _resiliencePipeline.ExecuteAsync(async ct =>
                        {
                            if (task.InClassSubmissionId.HasValue)
                            {
                                await ProcessInClassGradingTaskAsync(task, ct);
                            }
                            else
                            {
                                await ProcessGradingTaskAsync(task, ct);
                            }
                        }, workerToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || workerToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        var skipErrorNotification = false;

                        if (task.InClassSubmissionId.HasValue)
                        {
                            _logger.LogError(ex, "Lỗi nghiêm trọng khi chấm điểm AI cho InClassSubmission {SubmissionId}. Đẩy vào Dead-Letter Queue (DLQ).", task.InClassSubmissionId);

                            try
                            {
                                using var errorScope = _serviceProvider.CreateScope();
                                var errorDbContext = errorScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                                var now = DateTime.UtcNow;

                                var latestInClassSub = await errorDbContext.InClassExamSubmissions
                                    .AsNoTracking()
                                    .Where(s => s.Id == task.InClassSubmissionId.Value)
                                    .Select(s => new { s.Status, s.AiScore, s.ShiftId })
                                    .FirstOrDefaultAsync(CancellationToken.None);
                                var isShiftLocked = latestInClassSub != null && await errorDbContext.InClassExamShifts
                                    .AsNoTracking()
                                    .AnyAsync(
                                        sh => sh.Id == latestInClassSub.ShiftId && (sh.IsLocked || sh.Status == "FINALIZED"),
                                        CancellationToken.None);
                                var isInClassFinalOrGraded = latestInClassSub != null &&
                                    (latestInClassSub.AiScore.HasValue ||
                                     isShiftLocked ||
                                     string.Equals(latestInClassSub.Status, "AI_GRADED", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(latestInClassSub.Status, "AUDITED", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(latestInClassSub.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase));

                                if (isInClassFinalOrGraded)
                                {
                                    skipErrorNotification = true;
                                    _logger.LogInformation(
                                        "Bỏ qua ghi DLQ cho InClassSubmission {SubmissionId} vì bài đã ở trạng thái {Status} hoặc ca đã khóa.",
                                        task.InClassSubmissionId.Value,
                                        latestInClassSub!.Status);
                                }
                                else
                                {
                                    var existingDlq = await errorDbContext.DeadLetterQueues
                                        .Where(d => d.TaskType == "InClassGrading" &&
                                                    d.CorrelationId == task.InClassSubmissionId.Value &&
                                                    d.Status != "resolved")
                                        .OrderByDescending(d => d.CreatedAt)
                                        .FirstOrDefaultAsync(CancellationToken.None);
                                    if (existingDlq != null)
                                    {
                                        existingDlq.ManagedByRole = "admin";
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
                                            TaskType = "InClassGrading",
                                            CorrelationId = task.InClassSubmissionId.Value,
                                            FlowType = "IN_CLASS_EXAM",
                                            ManagedByRole = "admin",
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

                                    await errorDbContext.SaveChangesAsync(CancellationToken.None);
                                }
                            }
                            catch (Exception dlqEx)
                            {
                                _logger.LogCritical(dlqEx, "Không thể ghi bản ghi vào DeadLetterQueue cho InClassSubmission {SubmissionId}!", task.InClassSubmissionId);
                            }
                        }
                        else
                        {
                            _logger.LogError(ex, "Lỗi nghiêm trọng khi chấm điểm AI cho Answer {AnswerId}. Đẩy vào Dead-Letter Queue (DLQ).", task.AnswerId);

                            // 1. Thực thi logic lưu DLQ và cập nhật answer.Status = "failed" TRƯỚC TIÊN (bảo đảm bền vững dữ liệu)
                            try
                            {
                                using var errorScope = _serviceProvider.CreateScope();
                                var errorDbContext = errorScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                                var now = DateTime.UtcNow;

                                var latestPracticeStatus = await errorDbContext.PracticeAnswers
                                    .AsNoTracking()
                                    .Where(a => a.Id == task.AnswerId)
                                    .Select(a => a.Status)
                                    .FirstOrDefaultAsync(CancellationToken.None);
                                var isPracticeAlreadyGraded = string.Equals(latestPracticeStatus, "graded", StringComparison.OrdinalIgnoreCase);

                                if (isPracticeAlreadyGraded)
                                {
                                    skipErrorNotification = true;
                                    _logger.LogInformation(
                                        "Bỏ qua ghi DLQ và không hạ ngược trạng thái graded cho PracticeAnswer {AnswerId}.",
                                        task.AnswerId);
                                }
                                else
                                {
                                    var existingDlq = await errorDbContext.DeadLetterQueues
                                        .Where(d => d.TaskType == "PracticeGrading" &&
                                                    d.CorrelationId == task.AnswerId &&
                                                    d.Status != "resolved")
                                        .OrderByDescending(d => d.CreatedAt)
                                        .FirstOrDefaultAsync(CancellationToken.None);
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
                                            FlowType = "PRACTICE",
                                            ManagedByRole = "admin",
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
                                    if (answer != null && !string.Equals(answer.Status, "graded", StringComparison.OrdinalIgnoreCase))
                                    {
                                        answer.Status = "failed";
                                    }

                                    await errorDbContext.SaveChangesAsync(CancellationToken.None);
                                }
                            }
                            catch (Exception dlqEx)
                            {
                                _logger.LogCritical(dlqEx, "Không thể ghi bản ghi vào DeadLetterQueue cho Answer {AnswerId}!", task.AnswerId);
                            }
                        }

                        // Gửi thông báo thất bại cho frontend qua SignalR Group trong khối try/catch độc lập riêng biệt (áp dụng cho cả MF-05 và MF-01)
                        if (!skipErrorNotification)
                        {
                            try
                            {
                                const string gradingError = "Hệ thống AI hiện đang quá tải. Đã lưu bài làm và sẽ chấm lại sau.";
                                if (task.InClassSubmissionId.HasValue)
                                {
                                    if (_inClassHubContext is not null)
                                    {
                                        var errorEntityId = task.AnswerId != Guid.Empty ? task.AnswerId : task.InClassSubmissionId.Value;
                                        await _inClassHubContext.Clients
                                            .Group(InClassShiftHub.StaffGroupName(task.SessionId))
                                            .ReceiveGradingError(errorEntityId, gradingError);
                                        if (task.StudentId != Guid.Empty)
                                        {
                                            await _inClassHubContext.Clients
                                                .Group(InClassShiftHub.StudentGroupName(task.SessionId, task.StudentId))
                                                .ReceiveGradingError(errorEntityId, gradingError);
                                        }
                                    }
                                }
                                else
                                {
                                    await _hubContext.Clients.Group($"session_{task.SessionId}")
                                        .ReceiveGradingError(task.AnswerId, gradingError);
                                }
                            }
                            catch (Exception signalrEx)
                            {
                                _logger.LogWarning(signalrEx, "Không thể gửi SignalR ReceiveGradingError cho Session {SessionId}, Answer {AnswerId}", task.SessionId, task.AnswerId);
                            }
                        }
                    }
                });
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

        if (gradingTask.AnswerId != Guid.Empty)
        {
            var existingAnswerStatus = await dbContext.PracticeAnswers
                .AsNoTracking()
                .Where(a => a.Id == gradingTask.AnswerId)
                .Select(a => a.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.Equals(existingAnswerStatus, "graded", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Bỏ qua chấm AI cho PracticeAnswer {AnswerId} vì bài đã ở trạng thái graded.", gradingTask.AnswerId);
                await ResolvePendingDlqEntriesAsync(
                    dbContext,
                    gradingTask.AnswerId,
                    "Auto-resolved because practice answer was already graded.",
                    cancellationToken);
                return;
            }
        }

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
            .Select(FollowUpPromptBuilder.ResolveEvaluationFollowUpPrompt)
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
                var parentEval = await dbContext.AiEvaluations
                    .AsNoTracking()
                    .Where(e => e.PracticeAnswerId == gradingTask.ParentAnswerId.Value)
                    .OrderByDescending(e => e.EvaluatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                answeredFollowUpPrompt = FollowUpPromptBuilder.ResolveEvaluationFollowUpPrompt(parentEval);
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

        var allOrderedCriteria = question.Rubric.Criteria
            .OrderBy(c => c.OrderIndex)
            .ToList();

        var orderedCriteria = allOrderedCriteria;
        var isEnglishCourse = FollowUpPromptBuilder.IsEnglishCourse(
            question.Course?.Code,
            allOrderedCriteria.Select(c => c.CriterionName),
            question.Content);

        if (gradingTask.IsFollowUp && !isEnglishCourse && allOrderedCriteria.Count > 1)
        {
            var rootEvalDetails = rootAnswer?.AiEvaluations
                .OrderByDescending(e => e.EvaluatedAt)
                .FirstOrDefault()?.Details;

            var alreadyTargetedFollowUpCriteria = priorQuestionAnswers
                .Where(a => a.IsFollowUp)
                .SelectMany(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).Take(1))
                .Where(e => e.Details.Count == 1)
                .Select(e => e.Details.First().CriterionId)
                .ToHashSet();

            RubricCriterion? targetCriterion = null;
            decimal lowestRatio = decimal.MaxValue;

            if (rootEvalDetails != null && rootEvalDetails.Count > 0)
            {
                var candidates = allOrderedCriteria
                    .Where(c => !alreadyTargetedFollowUpCriteria.Contains(c.Id))
                    .ToList();
                if (candidates.Count == 0)
                {
                    candidates = allOrderedCriteria;
                }

                foreach (var crit in candidates)
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

            targetCriterion ??= allOrderedCriteria[0];
            orderedCriteria = new System.Collections.Generic.List<RubricCriterion> { targetCriterion };
        }

        var criteriaContext = orderedCriteria.Select(c => new AiGradingCriterion
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
                if (matchedDetail != null)
                {
                    var rawScore = matchedDetail.Score;
                    if (totalCriteriaMax > 0.0m && totalCriteriaMax < 10.0m && orderedCriteria.Count == 1 &&
                        rawScore > criterion.MaxScore && rawScore <= 10.0m)
                    {
                        rawScore = rawScore * (criterion.MaxScore / 10.0m);
                    }

                    clampedScore = Math.Clamp(Math.Round(rawScore, 2, MidpointRounding.AwayFromZero), 0.0m, criterion.MaxScore);
                }
                else
                {
                    clampedScore = 0.0m;
                }
            }
            else if (totalCriteriaMax > 0.0m && clampedOverallScore > 0.0m)
            {
                var targetScaleSum = (totalCriteriaMax < 10.0m && clampedOverallScore > totalCriteriaMax)
                    ? clampedOverallScore * (totalCriteriaMax / 10.0m)
                    : clampedOverallScore;

                if (i == orderedCriteria.Count - 1)
                {
                    clampedScore = Math.Clamp(
                        Math.Round(targetScaleSum - runningAllocatedScore, 2, MidpointRounding.AwayFromZero),
                        0.0m,
                        criterion.MaxScore);
                }
                else
                {
                    clampedScore = Math.Clamp(
                        Math.Round(targetScaleSum * (criterion.MaxScore / totalCriteriaMax), 2, MidpointRounding.AwayFromZero),
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

        var sumDetailScores = evalDetails.Sum(d => d.Score);
        var finalTotalScore = evalDetails.Count > 0
            ? (totalCriteriaMax > 0.0m && totalCriteriaMax < 10.0m
                ? Math.Clamp(Math.Round((sumDetailScores / totalCriteriaMax) * 10.0m, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                : Math.Clamp(Math.Round(sumDetailScores, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m))
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
                    // Tìm tiêu chí có tỷ lệ điểm thấp nhất tiếp theo để đặt câu hỏi phụ xoáy sâu đúng chỗ hổng kiến thức
                    RubricCriterion? weakestCriterion = null;
                    decimal lowestRatio = decimal.MaxValue;
                    if (gradingTask.IsFollowUp && !isEnglishCourse && allOrderedCriteria.Count > 1)
                    {
                        var rootEvalDetails = rootAnswer?.AiEvaluations
                            .OrderByDescending(e => e.EvaluatedAt)
                            .FirstOrDefault()?.Details;
                        var usedFollowUpCriteria = priorQuestionAnswers
                            .Where(a => a.IsFollowUp)
                            .SelectMany(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).Take(1))
                            .Where(e => e.Details.Count == 1)
                            .Select(e => e.Details.First().CriterionId)
                            .Concat(evalDetails.Select(d => d.CriterionId))
                            .ToHashSet();

                        var nextCandidates = allOrderedCriteria
                            .Where(c => !usedFollowUpCriteria.Contains(c.Id))
                            .ToList();
                        if (nextCandidates.Count == 0)
                        {
                            nextCandidates = allOrderedCriteria;
                        }

                        foreach (var crit in nextCandidates)
                        {
                            var detail = rootEvalDetails?.FirstOrDefault(d => d.CriterionId == crit.Id);
                            var detailScore = detail?.Score ?? 0m;
                            var ratio = crit.MaxScore > 0m ? detailScore / crit.MaxScore : 0m;
                            if (ratio < lowestRatio)
                            {
                                lowestRatio = ratio;
                                weakestCriterion = crit;
                            }
                        }
                    }
                    else
                    {
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
                    }

                    int nextFollowUpSequence = existingFollowUpsCount + 1;
                    var candidateFollowUp = FollowUpPromptBuilder.ResolveFollowUpPrompt(
                        question.Course?.Code,
                        null,
                        gradingResult.FollowUpPrompt,
                        nextFollowUpSequence,
                        weakestCriterion?.CriterionName,
                        weakestCriterion?.Description,
                        allOrderedCriteria.Select(c => c.CriterionName),
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

        var answer = await dbContext.PracticeAnswers.FindAsync(new object[] { gradingTask.AnswerId }, cancellationToken);
        if (answer != null && string.Equals(answer.Status, "graded", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Bỏ qua ghi đè kết quả AI cho PracticeAnswer {AnswerId} vì bài đã ở trạng thái graded trong lúc AI đang chấm.", gradingTask.AnswerId);
            await ResolvePendingDlqEntriesAsync(
                dbContext,
                gradingTask.AnswerId,
                "Auto-resolved because practice answer was already graded.",
                cancellationToken);
            return;
        }

        var evaluation = await dbContext.AiEvaluations
            .Include(e => e.Details)
            .FirstOrDefaultAsync(e => e.PracticeAnswerId == gradingTask.AnswerId, cancellationToken);

        if (evaluation != null)
        {
            evaluation.AnswerType = "practice";
            evaluation.StudentId = gradingTask.StudentId != Guid.Empty ? gradingTask.StudentId : answer?.StudentId;
            evaluation.TotalScore = finalTotalScore;
            evaluation.ConfidenceScore = gradingResult.ConfidenceScore;
            evaluation.Feedback = gradingResult.Feedback;
            evaluation.CotTrace = gradingResult.CotTrace;
            evaluation.FollowUpPrompt = needsFollowUp ? resolvedFollowUpPrompt : null;
            evaluation.EvaluatedAt = DateTime.UtcNow;

            if (evaluation.Details.Count > 0)
            {
                dbContext.AiEvaluationDetails.RemoveRange(evaluation.Details);
                evaluation.Details.Clear();
            }

            foreach (var detail in evalDetails)
            {
                detail.EvaluationId = evaluation.Id;
                evaluation.Details.Add(detail);
                dbContext.AiEvaluationDetails.Add(detail);
            }
        }
        else
        {
            evaluation = new AiEvaluation
            {
                AnswerType = "practice",
                StudentId = gradingTask.StudentId != Guid.Empty ? gradingTask.StudentId : answer?.StudentId,
                PracticeAnswerId = gradingTask.AnswerId,
                TotalScore = finalTotalScore,
                ConfidenceScore = gradingResult.ConfidenceScore,
                Feedback = gradingResult.Feedback,
                CotTrace = gradingResult.CotTrace,
                FollowUpPrompt = needsFollowUp ? resolvedFollowUpPrompt : null,
                EvaluatedAt = DateTime.UtcNow,
                Details = evalDetails
            };

            dbContext.AiEvaluations.Add(evaluation);

            if (answer != null)
            {
                answer.AiEvaluations.Add(evaluation);
            }
        }

        // Cập nhật câu trả lời: DÙNG 'graded' THAY VÌ 'AI_GRADED' (Tuân thủ ck_practice_answers_status)
        if (answer != null)
        {
            answer.Status = "graded";
            if (gradingTask.IsFollowUp && string.IsNullOrWhiteSpace(answer.PromptText) && !string.IsNullOrWhiteSpace(answeredFollowUpPrompt))
            {
                answer.PromptText = answeredFollowUpPrompt;
            }
        }

        var sessionEntity = await dbContext.PracticeSessions
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
            .FirstOrDefaultAsync(s => s.Id == gradingTask.SessionId, cancellationToken);
        if (sessionEntity != null)
        {
            sessionEntity.TotalScore = OralExamination.Application.Features.Practice.Common.PracticeSessionTimeoutHelper
                .ComputeSessionTotalScore(sessionEntity, sessionEntity.Answers);
        }

        var nowForDlq = DateTime.UtcNow;
        var pendingDlqEntries = await dbContext.DeadLetterQueues
            .Where(q => q.CorrelationId == gradingTask.AnswerId && q.Status == "pending")
            .ToListAsync(cancellationToken);
        foreach (var dlq in pendingDlqEntries)
        {
            dlq.Status = "resolved";
            dlq.ResolvedAt = nowForDlq;
            dlq.NextRetryAt = null;
            dlq.ResolutionNotes ??= "Auto-resolved after successful practice answer grading.";
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
        try
        {
            await _hubContext.Clients.Group($"session_{gradingTask.SessionId}").ReceiveGradingResult(detailDto);
        }
        catch (Exception signalrEx)
        {
            _logger.LogWarning(
                signalrEx,
                "Không thể gửi SignalR ReceiveGradingResult cho Session {SessionId}, Answer {AnswerId}",
                gradingTask.SessionId,
                gradingTask.AnswerId);
        }
    }

    private async Task ProcessInClassGradingTaskAsync(GradingTask gradingTask, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var aiGradingService = scope.ServiceProvider.GetRequiredService<IAiGradingService>();

        var submissionId = gradingTask.InClassSubmissionId!.Value;
        var submission = await dbContext.InClassExamSubmissions
            .Include(s => s.Shift)
                .ThenInclude(sh => sh.Class)
            .Include(s => s.ParentSubmission)
            .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

        if (submission == null)
        {
            _logger.LogWarning("Không tìm thấy InClassExamSubmission {SubmissionId}", submissionId);
            return;
        }

        var isShiftLockedOrFinalized = submission.Shift?.IsLocked == true ||
            string.Equals(submission.Shift?.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase);
        var isAlreadyGradedOrFinalized = submission.AiScore.HasValue ||
            isShiftLockedOrFinalized ||
            string.Equals(submission.Status, "AI_GRADED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(submission.Status, "AUDITED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(submission.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase);

        if (isAlreadyGradedOrFinalized)
        {
            _logger.LogInformation(
                "Bỏ qua chấm AI cho InClassExamSubmission {SubmissionId} vì bài đã ở trạng thái {Status} hoặc ca đã khóa.",
                submissionId,
                submission.Status);
            await ResolvePendingDlqEntriesAsync(
                dbContext,
                submissionId,
                "Auto-resolved because in-class submission was already graded or shift was locked.",
                cancellationToken);
            return;
        }

        // 1. Tải câu hỏi và barem Rubric chuẩn 10.0đ từ kho PracticeQuestions (Cô lập R8)
        var practiceQ = await dbContext.PracticeQuestions
            .Include(q => q.Course)
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == submission.QuestionId, cancellationToken);

        if (practiceQ == null || practiceQ.Rubric == null || practiceQ.Rubric.Criteria == null || practiceQ.Rubric.Criteria.Count == 0)
        {
            throw new InvalidOperationException($"Không tìm thấy câu hỏi luyện tập hoặc barem rubric hợp lệ cho InClassExamSubmission {submissionId} (QuestionId={submission.QuestionId}).");
        }

        string questionContent = practiceQ.Content;
        string sampleAnswer = practiceQ.SampleAnswer ?? string.Empty;
        string? courseCode = practiceQ.Course?.Code;
        var orderedCriteria = practiceQ.Rubric.Criteria
            .OrderBy(c => c.OrderIndex)
            .ToList();
        var criteriaContext = orderedCriteria
            .Select(c => new AiGradingCriterion
            {
                Id = c.Id.ToString(),
                Name = c.CriterionName,
                Description = c.Description ?? string.Empty,
                MaxScore = c.MaxScore,
                Weight = c.Weight
            }).ToList();

        // 2. Truy xuất lịch sử các lượt trả lời trước đó của cùng câu hỏi trong ca thi để cung cấp đủ ngữ cảnh Follow-up cho Gemini CoT
        var priorQuestionSubmissions = await dbContext.InClassExamSubmissions
            .AsNoTracking()
            .Where(s => s.ShiftId == submission.ShiftId &&
                        s.StudentId == submission.StudentId &&
                        s.QuestionId == submission.QuestionId &&
                        s.Id != submission.Id)
            .OrderBy(s => s.FollowUpIndex)
            .ThenBy(s => s.SubmittedAt)
            .ToListAsync(cancellationToken);

        var priorTranscripts = priorQuestionSubmissions
            .Where(s => !string.IsNullOrWhiteSpace(s.Transcript))
            .Select(s => s.Transcript.Trim())
            .ToList();

        string? originalAnswerTranscript = priorTranscripts.Count > 0
            ? string.Join(" ", priorTranscripts)
            : (!submission.IsFollowUp ? submission.Transcript : submission.ParentSubmission?.Transcript);

        var previousFollowUpPrompts = priorQuestionSubmissions
            .Select(s => s.FollowUpPrompt)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? answeredFollowUpPrompt = null;
        if (submission.IsFollowUp)
        {
            answeredFollowUpPrompt = !string.IsNullOrWhiteSpace(submission.ParentSubmission?.FollowUpPrompt)
                ? submission.ParentSubmission!.FollowUpPrompt!.Trim()
                : previousFollowUpPrompts.LastOrDefault();
        }

        var gradingRequest = new AiGradingRequest
        {
            QuestionContent = questionContent,
            SampleAnswer = sampleAnswer,
            CourseCode = courseCode,
            Transcript = submission.Transcript,
            Criteria = criteriaContext,
            QuestionImageUrl = practiceQ.ImageUrl,
            QuestionImageDescription = practiceQ.ImageDescription,
            IsFollowUp = submission.IsFollowUp,
            FollowUpPrompt = answeredFollowUpPrompt,
            FollowUpIndex = submission.FollowUpIndex,
            OriginalAnswerTranscript = originalAnswerTranscript,
            PreviousFollowUpPrompts = previousFollowUpPrompts,
            ThrowOnTransientFailure = true
        };

        var gradingResult = await aiGradingService.GradeAnswerAsync(gradingRequest, cancellationToken);

        decimal score = Math.Clamp(Math.Round(gradingResult.Score, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m);

        var evalDetails = new System.Collections.Generic.List<AiEvaluationDetail>();
        var rawDetails = gradingResult.Details ?? new System.Collections.Generic.List<GradingDetailDto>();
        if (orderedCriteria.Count > 0)
        {
            var totalCriteriaMax = orderedCriteria.Sum(c => c.MaxScore);
            decimal runningAllocated = 0.0m;

            for (int i = 0; i < orderedCriteria.Count; i++)
            {
                var criterion = orderedCriteria[i];
                var matchedDetail = rawDetails.FirstOrDefault(r =>
                        string.Equals(r.CriterionId?.Trim(), criterion.Id.ToString(), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.CriterionId?.Trim(), criterion.CriterionName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? (i < rawDetails.Count ? rawDetails[i] : null);

                decimal detailScore;
                if (matchedDetail != null)
                {
                    detailScore = Math.Clamp(Math.Round(matchedDetail.Score, 2, MidpointRounding.AwayFromZero), 0.0m, criterion.MaxScore);
                }
                else if (totalCriteriaMax > 0.0m && score > 0.0m)
                {
                    if (i == orderedCriteria.Count - 1)
                    {
                        detailScore = Math.Clamp(Math.Round(score - runningAllocated, 2, MidpointRounding.AwayFromZero), 0.0m, criterion.MaxScore);
                    }
                    else
                    {
                        detailScore = Math.Clamp(Math.Round(score * (criterion.MaxScore / totalCriteriaMax), 2, MidpointRounding.AwayFromZero), 0.0m, criterion.MaxScore);
                        runningAllocated += detailScore;
                    }
                }
                else
                {
                    detailScore = 0.0m;
                }

                var comment = !string.IsNullOrWhiteSpace(matchedDetail?.Comment)
                    ? matchedDetail.Comment
                    : detailScore > 0.0m
                        ? $"Đạt {detailScore:0.0}/{criterion.MaxScore:0.0}đ cho tiêu chí '{criterion.CriterionName}'."
                        : $"Chưa đáp ứng tiêu chí '{criterion.CriterionName}' (0.0/{criterion.MaxScore:0.0}đ).";

                evalDetails.Add(new AiEvaluationDetail
                {
                    CriterionId = criterion.Id,
                    Score = detailScore,
                    Comment = comment
                });
            }

            if (evalDetails.Count > 0)
            {
                var sumDetails = evalDetails.Sum(d => d.Score);
                score = totalCriteriaMax > 0.0m && totalCriteriaMax < 10.0m
                    ? Math.Clamp(Math.Round((sumDetails / totalCriteriaMax) * 10.0m, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                    : Math.Clamp(Math.Round(sumDetails, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m);
            }
        }

        var latestTranscript = await dbContext.InClassExamSubmissions
            .AsNoTracking()
            .Where(s => s.Id == submission.Id)
            .Select(s => s.Transcript)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.Equals(latestTranscript, submission.Transcript, StringComparison.Ordinal))
        {
            _logger.LogInformation("Bỏ qua kết quả chấm cũ vì bài {SubmissionId} đã được nộp lại.", submissionId);
            return;
        }

        // Tải lại trạng thái mới nhất từ CSDL để chống race condition khi giảng viên đã hậu kiểm trong lúc AI đang chấm
        var freshSubmission = await dbContext.InClassExamSubmissions
            .AsNoTracking()
            .Include(s => s.Shift)
            .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

        if (freshSubmission == null ||
            freshSubmission.AiScore.HasValue ||
            string.Equals(freshSubmission.Status, "AI_GRADED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(freshSubmission.Status, "AUDITED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(freshSubmission.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase) ||
            freshSubmission.Shift?.IsLocked == true ||
            string.Equals(freshSubmission.Shift?.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Bỏ qua cập nhật điểm AI cho InClassExamSubmission {SubmissionId} vì bài đã được chấm/hậu kiểm hoặc ca đã khóa trong lúc AI đang chấm.", submissionId);
            if (freshSubmission != null)
            {
                await ResolvePendingDlqEntriesAsync(
                    dbContext,
                    submissionId,
                    "Auto-resolved because in-class submission was already graded or shift was locked.",
                    cancellationToken);
            }
            return;
        }

        var existingInClassEval = await dbContext.AiEvaluations
            .Include(e => e.Details)
            .FirstOrDefaultAsync(e => e.InClassSubmissionId == submission.Id, cancellationToken);

        if (existingInClassEval != null)
        {
            existingInClassEval.AnswerType = "in_class";
            existingInClassEval.StudentId = submission.StudentId;
            existingInClassEval.TotalScore = score;
            existingInClassEval.ConfidenceScore = gradingResult.ConfidenceScore;
            existingInClassEval.Feedback = gradingResult.Feedback;
            existingInClassEval.CotTrace = gradingResult.CotTrace;
            existingInClassEval.EvaluatedAt = DateTime.UtcNow;

            if (existingInClassEval.Details.Count > 0)
            {
                dbContext.AiEvaluationDetails.RemoveRange(existingInClassEval.Details);
                existingInClassEval.Details.Clear();
            }

            foreach (var detail in evalDetails)
            {
                detail.EvaluationId = existingInClassEval.Id;
                existingInClassEval.Details.Add(detail);
                dbContext.AiEvaluationDetails.Add(detail);
            }
        }
        else
        {
            var inClassEvaluation = new AiEvaluation
            {
                AnswerType = "in_class",
                StudentId = submission.StudentId,
                InClassSubmissionId = submission.Id,
                TotalScore = score,
                ConfidenceScore = gradingResult.ConfidenceScore,
                Feedback = gradingResult.Feedback,
                CotTrace = gradingResult.CotTrace,
                EvaluatedAt = DateTime.UtcNow,
                Details = evalDetails
            };

            dbContext.AiEvaluations.Add(inClassEvaluation);
        }

        submission.AiScore = score;
        submission.AiConfidenceScore = gradingResult.ConfidenceScore;
        submission.CotTrace = gradingResult.CotTrace;
        submission.AiFeedback = gradingResult.Feedback;
        submission.FinalScore = score;
        submission.Status = "AI_GRADED";
        submission.GradedAt = DateTime.UtcNow;

        bool hasMoreFollowUp = false;
        if (submission.Shift != null &&
            submission.Shift.HasFollowUp &&
            submission.FollowUpIndex < submission.Shift.MaxFollowUpQuestions)
        {
            var existingFollowUpPrompt = !string.IsNullOrWhiteSpace(freshSubmission.FollowUpPrompt)
                ? freshSubmission.FollowUpPrompt!.Trim()
                : (!string.IsNullOrWhiteSpace(submission.FollowUpPrompt) ? submission.FollowUpPrompt!.Trim() : null);

            if (!string.IsNullOrWhiteSpace(existingFollowUpPrompt))
            {
                hasMoreFollowUp = true;
                submission.FollowUpPrompt = existingFollowUpPrompt;
            }
            else if (!gradingResult.IsSuspicious && !string.IsNullOrWhiteSpace(gradingResult.FollowUpPrompt))
            {
                var candidateFollowUp = FollowUpPromptBuilder.ResolveFollowUpPrompt(
                    courseCode,
                    null,
                    gradingResult.FollowUpPrompt,
                    submission.FollowUpIndex + 1,
                    null,
                    null,
                    orderedCriteria.Select(c => c.CriterionName),
                    questionContent,
                    sampleAnswer,
                    submission.Transcript,
                    originalAnswerTranscript,
                    previousFollowUpPrompts);

                if (!string.IsNullOrWhiteSpace(candidateFollowUp))
                {
                    hasMoreFollowUp = true;
                    submission.FollowUpPrompt = candidateFollowUp;
                }
            }
        }

        var participant = await dbContext.InClassShiftParticipants
            .FirstOrDefaultAsync(
                p => p.ShiftId == submission.ShiftId && p.StudentId == submission.StudentId,
                cancellationToken);

        if (participant != null)
        {
            var otherSubs = await dbContext.InClassExamSubmissions
                .AsNoTracking()
                .Where(s => s.ShiftId == submission.ShiftId && s.StudentId == submission.StudentId && s.Id != submission.Id)
                .ToListAsync(cancellationToken);

            var allSubs = otherSubs.Append(submission).ToList();
            var questionGroups = allSubs.GroupBy(s => s.QuestionId).ToList();

            var effectiveAiScores = new List<decimal>();
            var effectiveFinalScores = new List<decimal>();

            foreach (var group in questionGroups)
            {
                var mainSub = group.FirstOrDefault(s => !s.IsFollowUp) ?? group.First();
                var followUps = group.Where(s => s.IsFollowUp && s.Id != mainSub.Id).ToList();

                // Điểm AI: Nếu trả lời câu phụ tốt hơn thì kéo điểm cao hơn; nếu câu phụ thấp hơn thì bảo toàn điểm câu chính
                if (mainSub.AiScore.HasValue)
                {
                    decimal bestAi = mainSub.AiScore.Value;
                    foreach (var fu in followUps)
                    {
                        if (fu.AiScore.HasValue && fu.AiScore.Value > bestAi)
                        {
                            bestAi = fu.AiScore.Value;
                        }
                    }
                    effectiveAiScores.Add(bestAi);
                }
                else
                {
                    var anyFuAi = followUps.Where(f => f.AiScore.HasValue).Select(f => f.AiScore!.Value).ToList();
                    if (anyFuAi.Count > 0)
                    {
                        effectiveAiScores.Add(anyFuAi.Max());
                    }
                }

                // Điểm cuối (Giảng viên chấm lại hoặc điểm AI)
                decimal? mainFinal = mainSub.FinalScore ?? mainSub.AiScore;
                if (mainFinal.HasValue)
                {
                    decimal bestFinal = mainFinal.Value;
                    foreach (var fu in followUps)
                    {
                        var fuFinal = fu.FinalScore ?? fu.AiScore;
                        if (fuFinal.HasValue && fuFinal.Value > bestFinal)
                        {
                            bestFinal = fuFinal.Value;
                        }
                    }
                    effectiveFinalScores.Add(bestFinal);
                }
                else
                {
                    var anyFuFinal = followUps
                        .Select(f => f.FinalScore ?? f.AiScore)
                        .Where(sc => sc.HasValue)
                        .Select(sc => sc!.Value)
                        .ToList();
                    if (anyFuFinal.Count > 0)
                    {
                        effectiveFinalScores.Add(anyFuFinal.Max());
                    }
                }
            }

            if (effectiveAiScores.Count > 0)
            {
                participant.TotalAiScore = Math.Round(effectiveAiScores.Average(), 2, MidpointRounding.AwayFromZero);
            }

            if (effectiveFinalScores.Count > 0)
            {
                participant.TotalFinalScore = Math.Round(effectiveFinalScores.Average(), 2, MidpointRounding.AwayFromZero);
            }

            if (string.Equals(participant.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase))
            {
                var assignedIds = OralExamination.Application.Features.InClassShifts.InClassShiftPoolConfig
                    .ParseGuidList(participant.AssignedQuestionIdsJson)
                    .ToList();
                if (assignedIds.Count == 0 && participant.AssignedQuestionId.HasValue)
                {
                    assignedIds.Add(participant.AssignedQuestionId.Value);
                }

                var mainSubmittedIds = allSubs
                    .Where(s => !s.IsFollowUp)
                    .Select(s => s.QuestionId)
                    .ToHashSet();
                var allMainSubmitted = assignedIds.Count == 0 || assignedIds.All(mainSubmittedIds.Contains);
                var allGraded = allSubs.Count > 0 && allSubs.All(s =>
                    !string.Equals(s.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) &&
                    (s.FinalScore.HasValue || s.AiScore.HasValue));

                if (allMainSubmitted && allGraded)
                {
                    participant.Status = "GRADED";
                }
            }
        }

        var nowForDlq = DateTime.UtcNow;
        var pendingDlqEntries = await dbContext.DeadLetterQueues
            .Where(q => q.CorrelationId == submissionId && q.Status == "pending")
            .ToListAsync(cancellationToken);
        foreach (var dlq in pendingDlqEntries)
        {
            dlq.Status = "resolved";
            dlq.ResolvedAt = nowForDlq;
            dlq.NextRetryAt = null;
            dlq.ResolutionNotes ??= "Auto-resolved after successful in-class submission grading.";
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogInformation("Phát hiện xung đột đồng thời (DbUpdateConcurrencyException) cho InClassExamSubmission {SubmissionId}. Bỏ qua cập nhật vì bản ghi đã bị thay đổi (giảng viên đã hậu kiểm AUDITED hoặc ca thi đã chốt sổ).", submissionId);
            return;
        }

        // Publish điểm song song theo thời gian thực (Parallel Real-time Grade Publishing) qua SignalR Group của Ca thi
        try
        {
            var inClassEventDto = new
            {
                shiftId = submission.ShiftId,
                submissionId = submission.Id,
                studentId = submission.StudentId,
                questionId = submission.QuestionId,
                aiScore = score,
                aiFeedback = gradingResult.Feedback,
                status = "AI_GRADED",
                isFollowUp = submission.IsFollowUp,
                parentSubmissionId = submission.ParentSubmissionId,
                followUpIndex = submission.FollowUpIndex,
                needsFollowUp = hasMoreFollowUp,
                followUpPrompt = submission.FollowUpPrompt,
                followUpQuestion = hasMoreFollowUp && !string.IsNullOrWhiteSpace(submission.FollowUpPrompt)
                    ? new
                    {
                        prompt = submission.FollowUpPrompt,
                        followUpIndex = submission.FollowUpIndex + 1
                    }
                    : null
            };

            if (_inClassHubContext is not null)
            {
                await _inClassHubContext.Clients
                    .Group(InClassShiftHub.StaffGroupName(submission.ShiftId))
                    .InClassSubmissionGraded(inClassEventDto);

                await _inClassHubContext.Clients
                    .Group(InClassShiftHub.StudentGroupName(submission.ShiftId, submission.StudentId))
                    .InClassSubmissionGraded(new
                    {
                        shiftId = submission.ShiftId,
                        submissionId = submission.Id,
                        questionId = submission.QuestionId,
                        status = "AI_GRADED",
                        isFollowUp = submission.IsFollowUp,
                        parentSubmissionId = submission.ParentSubmissionId,
                        followUpIndex = submission.FollowUpIndex,
                        needsFollowUp = hasMoreFollowUp,
                        followUpPrompt = hasMoreFollowUp ? submission.FollowUpPrompt : null
                    });
            }
        }
        catch (Exception signalrEx)
        {
            _logger.LogWarning(signalrEx, "Không thể phát sự kiện SignalR ReceiveGradingResult cho InClassShift {ShiftId}, Submission {SubmissionId}", submission.ShiftId, submission.Id);
        }

        _logger.LogInformation("InClassExamSubmission {SubmissionId} đã được chấm AI thành công: {Score}đ", submissionId, score);
    }

    private static async Task ResolvePendingDlqEntriesAsync(
        IApplicationDbContext dbContext,
        Guid correlationId,
        string resolutionNote,
        CancellationToken cancellationToken)
    {
        var pendingDlqEntries = await dbContext.DeadLetterQueues
            .Where(q => q.CorrelationId == correlationId && q.Status == "pending")
            .ToListAsync(cancellationToken);

        if (pendingDlqEntries.Count == 0)
        {
            return;
        }

        var resolvedNow = DateTime.UtcNow;
        foreach (var dlq in pendingDlqEntries)
        {
            dlq.Status = "resolved";
            dlq.ResolvedAt = resolvedNow;
            dlq.NextRetryAt = null;
            dlq.ResolutionNotes ??= resolutionNote;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

