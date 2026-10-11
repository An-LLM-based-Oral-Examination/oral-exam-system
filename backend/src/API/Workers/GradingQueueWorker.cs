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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng khi chấm điểm AI cho Answer {AnswerId}. Đẩy vào Dead-Letter Queue (DLQ).", task.AnswerId);

                // 1. Thực thi logic lưu DLQ và cập nhật answer.Status = "failed" TRƯỚC TIÊN (bảo đảm bền vững dữ liệu)
                try
                {
                    using var errorScope = _serviceProvider.CreateScope();
                    var errorDbContext = errorScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                    var dlqEntry = new DeadLetterQueue
                    {
                        TaskType = "PracticeGrading",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(task),
                        ErrorMessage = ex.Message,
                        RetryCount = 3,
                        Status = "pending",
                        CreatedAt = DateTime.UtcNow
                    };
                    errorDbContext.DeadLetterQueues.Add(dlqEntry);

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

    private async Task ProcessGradingTaskAsync(GradingTask gradingTask, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var aiGradingService = scope.ServiceProvider.GetRequiredService<IAiGradingService>();

        var question = await dbContext.PracticeQuestions
            .Include(q => q.Rubric)
            .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(q => q.Id == gradingTask.QuestionId, cancellationToken);

        if (question == null || question.Rubric == null || !question.Rubric.Criteria.Any())
        {
            throw new Exception("Không tìm thấy câu hỏi hoặc barem rubric.");
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
            Transcript = gradingTask.AnswerText,
            Criteria = criteriaContext
        };

        var gradingResult = await aiGradingService.GradeAnswerAsync(request, cancellationToken);

        var evaluation = new AiEvaluation
        {
            AnswerType = "practice",
            PracticeAnswerId = gradingTask.AnswerId,
            TotalScore = gradingResult.Score,
            ConfidenceScore = gradingResult.ConfidenceScore,
            Feedback = gradingResult.Feedback,
            CotTrace = gradingResult.CotTrace,
            Details = gradingResult.Details.Select(d => new AiEvaluationDetail
            {
                CriterionId = Guid.TryParse(d.CriterionId, out var parsedGuid) ? parsedGuid : question.Rubric.Criteria.FirstOrDefault()?.Id ?? Guid.Empty,
                Score = d.Score,
                Comment = d.Comment
            }).ToList()
        };

        dbContext.AiEvaluations.Add(evaluation);

        // Cập nhật câu trả lời: DÙNG 'graded' THAY VÌ 'AI_GRADED' (Tuân thủ ck_practice_answers_status)
        var answer = await dbContext.PracticeAnswers.FindAsync(new object[] { gradingTask.AnswerId }, cancellationToken);
        if (answer != null)
        {
            answer.Status = "graded";
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var detailDto = new PracticeAnswerDetailDto
        {
            AnswerId = gradingTask.AnswerId,
            QuestionId = gradingTask.QuestionId,
            AnswerText = gradingTask.AnswerText,
            IsFollowUp = gradingTask.IsFollowUp,
            ParentAnswerId = gradingTask.ParentAnswerId,
            Status = "graded",
            TotalScore = evaluation.TotalScore,
            Feedback = evaluation.Feedback,
            IsSuspicious = gradingResult.IsSuspicious,
            ConfidenceScore = evaluation.ConfidenceScore,
            CriteriaScores = gradingResult.Details.Select(d => 
            {
                var critId = Guid.TryParse(d.CriterionId, out var parsedGuid) ? parsedGuid : Guid.Empty;
                var critName = question.Rubric.Criteria.FirstOrDefault(c => c.Id == critId)?.CriterionName ?? string.Empty;
                return new PracticeEvaluationDetailDto
                {
                    CriterionId = critId,
                    CriterionName = critName,
                    Score = d.Score,
                    Comment = d.Comment
                };
            }).ToList()
        };

        // KÍCH HOẠT FOLLOW-UP ENGINE MF-01: Điểm ranh giới 4.0 <= Score <= 8.0 ở chế độ Per-Question
        bool isBorderlineScore = evaluation.TotalScore >= 4.0m && evaluation.TotalScore <= 8.0m;
        if (!gradingTask.IsFullSession && isBorderlineScore)
        {
            // Đọc cấu hình số câu hỏi phụ tối đa từ bảng system_configs (1-5 câu, mặc định 2 câu)
            var maxFollowUpConfig = await dbContext.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Key == "MaxPracticeFollowUpQuestions", cancellationToken);
            int maxAllowedFollowUps = (maxFollowUpConfig != null && int.TryParse(maxFollowUpConfig.Value, out var parsedMaxFollowUps)) ? parsedMaxFollowUps : 2;

            // Đếm số lượng câu trả lời phụ đã có của câu hỏi này trong phiên luyện tập
            var existingFollowUpsCount = await dbContext.PracticeAnswers
                .AsNoTracking()
                .CountAsync(a => a.SessionId == gradingTask.SessionId &&
                                 a.QuestionId == gradingTask.QuestionId &&
                                 a.IsFollowUp, cancellationToken);

            if (existingFollowUpsCount < maxAllowedFollowUps)
            {
                detailDto.NeedsFollowUp = true;
                detailDto.FollowUpPrompt = !string.IsNullOrWhiteSpace(question.FollowUpPrompt)
                    ? question.FollowUpPrompt
                    : "Hãy làm rõ hơn luận điểm kỹ thuật và ví dụ thực tế liên quan đến câu trả lời của bạn.";
            }
        }

        // Bắn SignalR RIÊNG cho nhóm Session (Bảo vệ tính riêng tư, không dùng Clients.All)
        await _hubContext.Clients.Group($"session_{gradingTask.SessionId}").ReceiveGradingResult(detailDto);
    }
}
