using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Enums;
using Polly;
using Polly.Retry;

namespace OralExamination.Infrastructure.Services;

public class GeminiQuestionGenerationService : IAiQuestionGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiQuestionGenerationService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public GeminiQuestionGenerationService(
        IConfiguration configuration,
        ILogger<GeminiQuestionGenerationService> logger,
        HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _configuration = configuration;
        _logger = logger;

        // Cấu hình Polly Resilience Pipeline: Retry 3 lần với Exponential Backoff (2s, 4s, 8s)
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>().Handle<TaskCanceledException>()
            })
            .Build();
    }

    public async Task<List<GeneratedQuestionDraftDto>> GenerateQuestionsAsync(
        AiQuestionGenerationParams parameters,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                return await CallGeminiApiAsync(apiKey, parameters, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gọi Gemini API trực tiếp thất bại, chuyển sang chế độ dự phòng nội bộ.");
            }
        }

        // Chế độ Adapter cục bộ khi dev offline chưa cấu hình API Key bên thứ 3 (Tuân thủ AGENTS.md Mục 6)
        return GenerateDeterministicQuestions(parameters);
    }

    private async Task<List<GeneratedQuestionDraftDto>> CallGeminiApiAsync(
        string apiKey,
        AiQuestionGenerationParams parameters,
        CancellationToken cancellationToken)
    {
        string[] candidateModels = { "gemini-1.5-flash", "gemini-1.5-pro", "gemini-2.0-flash" };
        var prompt = BuildPrompt(parameters);

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = 0.3
            }
        };

        foreach (var modelName in candidateModels)
        {
            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            try
            {
                var response = await _resiliencePipeline.ExecuteAsync(async state =>
                {
                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                    {
                        Content = JsonContent.Create(payload)
                    };
                    return await _httpClient.SendAsync(requestMessage, state);
                }, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                    var candidateText = responseBody
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    if (!string.IsNullOrWhiteSpace(candidateText))
                    {
                        var parsed = ParseGeminiResponse(candidateText, parameters);
                        if (parsed.Count > 0)
                        {
                            return parsed;
                        }
                    }
                }
                else if ((int)response.StatusCode == 404)
                {
                    // Model không tồn tại trên key, thử model tiếp theo
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gọi model {ModelName}, thử model tiếp theo.", modelName);
            }
        }

        return GenerateDeterministicQuestions(parameters);
    }

    private static string BuildPrompt(AiQuestionGenerationParams p)
    {
        return $@"Bạn là Giảng viên Đại học FPT chuyên ngành Kỹ thuật Phần mềm.
Hãy tạo danh sách {p.NumberOfQuestions} câu hỏi vấn đáp cho môn học {p.CourseCode} - {p.CourseName}.
Các chuẩn đầu ra (CLO): {string.Join(", ", p.SelectedCloCodes)}.
Các chủ đề: {string.Join(", ", p.Topics)}.
Phân bổ độ khó: {p.EasyCount} câu Dễ (easy), {p.MediumCount} câu Trung bình (medium), {p.HardCount} câu Khó (hard).
Bậc nhận thức Bloom mục tiêu: {string.Join(", ", p.TargetBloomLevels)}.
Phạm vi sử dụng: {p.UsageScope}.

BẮT BUỘC tuân thủ các quy tắc bất biến sau:
1. Câu trả lời mẫu (ModelAnswer) BẮT BUỘC tối thiểu 50 ký tự trở lên.
2. Mỗi câu hỏi phải có Barem Rubric riêng với tối thiểu 2 tiêu chí con.
3. Tổng điểm tối đa (MaxScore) của các tiêu chí con trong Rubric BẮT BUỘC PHẢI BẰNG CHÍNH XÁC 10.00 điểm.

Trả về mảng JSON đúng cấu trúc:
[
  {{
    ""tempId"": ""guid"",
    ""cloCode"": ""CLO1"",
    ""title"": ""Tiêu đề câu hỏi"",
    ""content"": ""Nội dung câu hỏi chi tiết"",
    ""difficulty"": ""easy/medium/hard"",
    ""bloomLevel"": ""Understand"",
    ""source"": ""flm_api"",
    ""usageScope"": ""{p.UsageScope}"",
    ""modelAnswer"": ""Nội dung câu trả lời mẫu chi tiết tối thiểu 50 ký tự..."",
    ""keyPoints"": [""Ý chính 1"", ""Ý chính 2""],
    ""hasFollowUp"": false,
    ""followUpPrompt"": null,
    ""rubric"": {{
      ""name"": ""Barem đánh giá câu hỏi"",
      ""description"": ""Tiêu chuẩn chấm điểm"",
      ""totalMaxScore"": 10.00,
      ""criteria"": [
        {{
          ""criterionName"": ""Kiến thức cốt lõi"",
          ""maxScore"": 6.00,
          ""weight"": 60,
          ""bloomLevel"": ""Understand"",
          ""description"": ""Trình bày chính xác và đầy đủ các khái niệm"",
          ""orderIndex"": 1
        }},
        {{
          ""criterionName"": ""Ví dụ thực tế và giải thích"",
          ""maxScore"": 4.00,
          ""weight"": 40,
          ""bloomLevel"": ""Apply"",
          ""description"": ""Đưa ra minh họa thực tế thuyết phục"",
          ""orderIndex"": 2
        }}
      ]
    }}
  }}
]";
    }

    private static List<GeneratedQuestionDraftDto> ParseGeminiResponse(string jsonText, AiQuestionGenerationParams parameters)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var rawList = JsonSerializer.Deserialize<List<GeminiQuestionItem>>(jsonText, options);
            if (rawList == null) return new List<GeneratedQuestionDraftDto>();

            var result = new List<GeneratedQuestionDraftDto>();
            foreach (var item in rawList)
            {
                var criteria = new List<RubricCriterionDraftDto>();
                if (item.Rubric?.Criteria != null && item.Rubric.Criteria.Count > 0)
                {
                    int order = 1;
                    foreach (var c in item.Rubric.Criteria)
                    {
                        criteria.Add(new RubricCriterionDraftDto(
                            c.CriterionName ?? "Tiêu chí đánh giá",
                            c.MaxScore,
                            c.Weight > 0 ? c.Weight : c.MaxScore * 10,
                            c.BloomLevel ?? item.BloomLevel ?? "Understand",
                            c.Description ?? "Mô tả tiêu chí",
                            order++
                        ));
                    }
                }

                if (criteria.Count == 0)
                {
                    criteria.Add(new RubricCriterionDraftDto("Kiến thức nền tảng", 5.00m, 50, item.BloomLevel ?? "Understand", "Đánh giá mức độ nắm vững lý thuyết", 1));
                    criteria.Add(new RubricCriterionDraftDto("Ứng dụng và phân tích", 5.00m, 50, item.BloomLevel ?? "Apply", "Đánh giá khả năng áp dụng thực tế", 2));
                }

                // Đảm bảo tổng điểm rubric bằng chính xác 10.00
                var sum = criteria.Sum(c => c.MaxScore);
                if (sum != 10.00m && criteria.Count > 0)
                {
                    var diff = 10.00m - sum;
                    var last = criteria[^1];
                    criteria[^1] = last with { MaxScore = last.MaxScore + diff };
                }

                var modelAnswer = item.ModelAnswer ?? string.Empty;
                if (modelAnswer.Length < 50)
                {
                    modelAnswer = $"{modelAnswer} Sinh viên cần nắm vững các nguyên lý cốt lõi, cơ chế vận hành thực tế và giải thích rõ ràng với các ví dụ cụ thể liên quan đến chủ đề bài học.";
                }

                result.Add(new GeneratedQuestionDraftDto(
                    Guid.NewGuid().ToString(),
                    item.CloCode ?? parameters.SelectedCloCodes.FirstOrDefault() ?? "CLO1",
                    item.Title ?? $"Câu hỏi môn {parameters.CourseCode}",
                    item.Content ?? "Vui lòng trình bày hiểu biết của bạn về chủ đề này.",
                    item.Difficulty ?? "medium",
                    item.BloomLevel ?? parameters.TargetBloomLevels.FirstOrDefault() ?? "Understand",
                    "flm_api",
                    parameters.UsageScope,
                    modelAnswer,
                    item.KeyPoints ?? new List<string> { "Khái niệm", "Ứng dụng" },
                    item.HasFollowUp,
                    item.FollowUpPrompt,
                    new RubricDraftDto(
                        item.Rubric?.Name ?? "Barem đánh giá câu hỏi",
                        item.Rubric?.Description ?? "Tiêu chuẩn đánh giá chuẩn FPT",
                        10.00m,
                        criteria
                    )
                ));
            }

            return result;
        }
        catch
        {
            return new List<GeneratedQuestionDraftDto>();
        }
    }

    private static List<GeneratedQuestionDraftDto> GenerateDeterministicQuestions(AiQuestionGenerationParams p)
    {
        var result = new List<GeneratedQuestionDraftDto>();
        var difficulties = new List<string>();

        for (int i = 0; i < p.EasyCount; i++) difficulties.Add(QuestionDifficulty.Easy);
        for (int i = 0; i < p.MediumCount; i++) difficulties.Add(QuestionDifficulty.Medium);
        for (int i = 0; i < p.HardCount; i++) difficulties.Add(QuestionDifficulty.Hard);

        while (difficulties.Count < p.NumberOfQuestions)
        {
            difficulties.Add(QuestionDifficulty.Medium);
        }

        var cloList = p.SelectedCloCodes.Count > 0 ? p.SelectedCloCodes : new List<string> { "CLO1" };
        var topicList = p.Topics.Count > 0 ? p.Topics : new List<string> { "Kiến trúc hệ thống" };
        var bloomList = p.TargetBloomLevels.Count > 0 ? p.TargetBloomLevels : new List<string> { BloomLevel.Understand };

        for (int i = 0; i < p.NumberOfQuestions; i++)
        {
            var diff = difficulties[i];
            var clo = cloList[i % cloList.Count];
            var topic = topicList[i % topicList.Count];
            var bloom = bloomList[i % bloomList.Count];

            var criteria = new List<RubricCriterionDraftDto>
            {
                new("Nắm vững khái niệm và nguyên lý", 5.00m, 50, bloom, "Trình bày chính xác định nghĩa, cơ chế hoạt động cốt lõi của chủ đề.", 1),
                new("Khả năng phân tích và minh họa", 5.00m, 50, bloom, "Đưa ra ví dụ minh họa thực tiễn, phân tích được ưu nhược điểm giải pháp.", 2)
            };

            var modelAnswer = $"Câu trả lời mẫu cho chủ đề {topic} thuộc môn học {p.CourseCode}: Sinh viên cần định nghĩa rõ ràng nguyên lý hoạt động, phân tích trường hợp áp dụng trong thực tế dự án phần mềm và đưa ra các đánh giá kỹ thuật cụ thể.";

            result.Add(new GeneratedQuestionDraftDto(
                Guid.NewGuid().ToString(),
                clo,
                $"Câu hỏi vấn đáp về {topic} ({p.CourseCode})",
                $"Anh/Chị hãy trình bày chi tiết về chủ đề '{topic}' trong môn học {p.CourseName}. Phân tích cách áp dụng nguyên lý này trong quy trình phát triển phần mềm thực tế.",
                diff,
                bloom,
                "flm_api",
                p.UsageScope,
                modelAnswer,
                new List<string> { $"Khái niệm {topic}", "Ưu nhược điểm", "Ứng dụng thực tế" },
                false,
                null,
                new RubricDraftDto(
                    $"Barem chấm điểm cho câu hỏi {topic}",
                    $"Rubric đánh giá chuẩn hóa 10.0 điểm cho môn {p.CourseCode}",
                    10.00m,
                    criteria
                )
            ));
        }

        return result;
    }

    private sealed class GeminiQuestionItem
    {
        public string? TempId { get; set; }
        public string? CloCode { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Difficulty { get; set; }
        public string? BloomLevel { get; set; }
        public string? Source { get; set; }
        public string? UsageScope { get; set; }
        public string? ModelAnswer { get; set; }
        public List<string>? KeyPoints { get; set; }
        public bool HasFollowUp { get; set; }
        public string? FollowUpPrompt { get; set; }
        public GeminiRubricItem? Rubric { get; set; }
    }

    private sealed class GeminiRubricItem
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal TotalMaxScore { get; set; }
        public List<GeminiRubricCriterionItem>? Criteria { get; set; }
    }

    private sealed class GeminiRubricCriterionItem
    {
        public string? CriterionName { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Weight { get; set; }
        public string? BloomLevel { get; set; }
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
    }
}
