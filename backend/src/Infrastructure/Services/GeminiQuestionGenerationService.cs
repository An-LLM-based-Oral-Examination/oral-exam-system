using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.QuestionBank.DTOs;
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
            ?? _configuration["Gemini__ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? string.Empty;

        var hasUsableGeminiKey = !string.IsNullOrWhiteSpace(apiKey)
            && !apiKey.Contains("Placeholder", StringComparison.OrdinalIgnoreCase)
            && !apiKey.Contains("YourActual", StringComparison.OrdinalIgnoreCase)
            && !apiKey.StartsWith("AQ.", StringComparison.OrdinalIgnoreCase);

        string lastError = string.Empty;

        if (hasUsableGeminiKey)
        {
            try
            {
                var geminiGenerated = await CallGeminiApiAsync(apiKey, parameters, cancellationToken);
                if (geminiGenerated.Count > 0)
                {
                    return geminiGenerated;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "Gọi Gemini API sinh câu hỏi thất bại, chuyển sang Cloudflare Workers AI (Llama 3.1).");
            }
        }

        var cloudflareGenerated = await TryCallCloudflareLlamaAsync(parameters, cancellationToken);
        if (cloudflareGenerated.Count > 0)
        {
            return cloudflareGenerated;
        }

        throw new HttpRequestException(
            $"Không thể sinh câu hỏi tự động từ AI (cả Gemini API và Cloudflare Workers AI đều không phản hồi hợp lệ). Chi tiết: {lastError}",
            null,
            HttpStatusCode.ServiceUnavailable);
    }

    private async Task<List<GeneratedQuestionDraftDto>> CallGeminiApiAsync(
        string apiKey,
        AiQuestionGenerationParams parameters,
        CancellationToken cancellationToken)
    {
        string[] candidateModels =
        {
            "gemini-3.8-flash",
            "gemini-3.6-flash",
            "gemini-3.5-flash-lite",
            "gemini-flash-latest",
            "gemini-flash-lite-latest"
        };
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
                response_mime_type = "application/json"
            }
        };

        string lastHttpError = string.Empty;

        foreach (var modelName in candidateModels)
        {
            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";

            try
            {
                var response = await _resiliencePipeline.ExecuteAsync(async state =>
                {
                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                    {
                        Content = JsonContent.Create(payload)
                    };
                    requestMessage.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
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
                        var parsed = ParseAiResponse(candidateText, parameters);
                        if (parsed.Count > 0)
                        {
                            return parsed;
                        }
                    }
                }
                else
                {
                    lastHttpError = $"HTTP {(int)response.StatusCode} ({modelName})";
                    if ((int)response.StatusCode == 404)
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                lastHttpError = ex.Message;
                _logger.LogWarning(ex, "Lỗi khi gọi model {ModelName}, thử model tiếp theo.", modelName);
            }
        }

        if (!string.IsNullOrWhiteSpace(lastHttpError))
        {
            throw new HttpRequestException($"Gemini Question Generation failed: {lastHttpError}");
        }

        return new List<GeneratedQuestionDraftDto>();
    }

    private async Task<List<GeneratedQuestionDraftDto>> TryCallCloudflareLlamaAsync(
        AiQuestionGenerationParams parameters,
        CancellationToken cancellationToken)
    {
        var accountId = _configuration["Cloudflare:AccountId"]
            ?? _configuration["Cloudflare__AccountId"]
            ?? Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID")
            ?? string.Empty;
        var apiToken = _configuration["Cloudflare:ApiToken"]
            ?? _configuration["Cloudflare__ApiToken"]
            ?? Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(apiToken))
        {
            return new List<GeneratedQuestionDraftDto>();
        }

        var url = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/ai/run/@cf/meta/llama-3.1-8b-instruct";
        var prompt = BuildPrompt(parameters);

        var payload = new
        {
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "Bạn là chuyên gia khảo thí ĐH FPT. Chỉ trả về duy nhất một mảng JSON hợp lệ theo đúng cấu trúc được yêu cầu, không kèm lời dẫn hay markdown."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            max_tokens = 2048,
            temperature = 0.3
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                return new List<GeneratedQuestionDraftDto>();
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.TryGetProperty("result", out var resultElem) &&
                resultElem.TryGetProperty("response", out var responseProp))
            {
                var rawText = responseProp.GetString();
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    return ParseAiResponse(rawText, parameters);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gọi Cloudflare Workers AI sinh câu hỏi thất bại.");
        }

        return new List<GeneratedQuestionDraftDto>();
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

    private static List<GeneratedQuestionDraftDto> ParseAiResponse(string jsonText, AiQuestionGenerationParams parameters)
    {
        var cleanJson = ExtractJsonArray(jsonText);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var rawList = JsonSerializer.Deserialize<List<GeminiQuestionItem>>(cleanJson, options);
            if (rawList == null) return new List<GeneratedQuestionDraftDto>();

            var result = new List<GeneratedQuestionDraftDto>();
            foreach (var item in rawList)
            {
                if (string.IsNullOrWhiteSpace(item.Content))
                {
                    continue;
                }

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

                var modelAnswer = item.ModelAnswer?.Trim() ?? string.Empty;
                if (modelAnswer.Length < 50)
                {
                    continue;
                }

                result.Add(new GeneratedQuestionDraftDto(
                    Guid.NewGuid().ToString(),
                    item.CloCode ?? parameters.SelectedCloCodes.FirstOrDefault() ?? "CLO1",
                    item.Title ?? $"Câu hỏi môn {parameters.CourseCode}",
                    item.Content.Trim(),
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

    private static string ExtractJsonArray(string raw)
    {
        var trimmed = raw.Trim();
        var startIdx = trimmed.IndexOf('[');
        var endIdx = trimmed.LastIndexOf(']');
        if (startIdx >= 0 && endIdx > startIdx)
        {
            return trimmed.Substring(startIdx, endIdx - startIdx + 1);
        }
        return trimmed;
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
