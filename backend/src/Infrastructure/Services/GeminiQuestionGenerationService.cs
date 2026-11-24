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
using OralExamination.Domain.Enums;
using Polly;
using OralExamination.Infrastructure.Resilience;

namespace OralExamination.Infrastructure.Services;

public class GeminiQuestionGenerationService : IAiQuestionGenerationService
{
    private static readonly SemaphoreSlim AiGenerationBulkhead = new(16, 16);

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

        // Shared HTTP pipeline owns retries; do not multiply service retries by provider/model failover.
        _resiliencePipeline = ResiliencePipeline.Empty;
    }

    public async Task<List<GeneratedQuestionDraftDto>> GenerateQuestionsAsync(
        AiQuestionGenerationParams parameters,
        CancellationToken cancellationToken = default)
    {
        ValidateParameters(parameters);
        var candidateKeys = GeminiApiKeyPriority.GetCandidates(_configuration);

        string lastError = string.Empty;

        foreach (var activeKey in candidateKeys)
        {
            try
            {
                var geminiGenerated = await CallGeminiApiAsync(activeKey, parameters, cancellationToken);
                if (geminiGenerated.Count > 0)
                {
                    return geminiGenerated;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.PaymentRequired
                or HttpStatusCode.Forbidden
                or HttpStatusCode.TooManyRequests
                || (ex.StatusCode.HasValue && (int)ex.StatusCode.Value >= 500)
                || ExternalDependencyResilience.IsTransient(ex))
            {
                lastError = ex.Message;
                _logger.LogWarning(
                    ex,
                    "Gọi Gemini API sinh câu hỏi thất bại với mã {StatusCode}; xoay sang key tiếp theo trong pool.",
                    ex.StatusCode);
                continue;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "Gọi Gemini API sinh câu hỏi thất bại, chuyển sang key tiếp theo hoặc Cloudflare Workers AI (Llama).");
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

    private IReadOnlyList<string> ResolveCandidateModels()
    {
        var models = new List<string>();
        void AddIfValid(string? model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return;
            }

            var trimmed = model.Trim();
            if (!models.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                models.Add(trimmed);
            }
        }

        AddIfValid(_configuration["Gemini:Model"]
            ?? _configuration["Gemini__Model"]
            ?? Environment.GetEnvironmentVariable("GEMINI_MODEL"));
        AddIfValid(_configuration["Gemini:FastModel"]
            ?? _configuration["Gemini__FastModel"]
            ?? Environment.GetEnvironmentVariable("GEMINI_FAST_MODEL"));

        AddIfValid("gemini-flash-lite-latest");
        AddIfValid("gemini-3.1-flash-lite");
        AddIfValid("gemini-2.5-flash");
        AddIfValid("gemini-flash-latest");

        return models;
    }

    private async Task<List<GeneratedQuestionDraftDto>> CallGeminiApiAsync(
        string apiKey,
        AiQuestionGenerationParams parameters,
        CancellationToken cancellationToken)
    {
        var candidateModels = ResolveCandidateModels();
        var prompt = QuestionGenerationPrompt.Build(parameters);

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
        HttpStatusCode? lastStatusCode = null;

        foreach (var modelName in candidateModels)
        {
            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";

            try
            {
                using var response = await _resiliencePipeline.ExecuteAsync(async state =>
                {
                    await AiGenerationBulkhead.WaitAsync(state);
                    try
                    {
                        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                        {
                            Content = JsonContent.Create(payload)
                        };
                        requestMessage.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                        return await _httpClient.SendAsync(requestMessage, state);
                    }
                    finally
                    {
                        AiGenerationBulkhead.Release();
                    }
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
                    lastStatusCode = response.StatusCode;
                    if (ExternalDependencyResilience.IsTransientStatus(response.StatusCode))
                    {
                        throw new HttpRequestException(lastHttpError, null, response.StatusCode);
                    }

                    if (response.StatusCode is HttpStatusCode.Unauthorized
                        or HttpStatusCode.PaymentRequired
                        or HttpStatusCode.Forbidden
                        or HttpStatusCode.BadRequest)
                    {
                        break;
                    }

                    if ((int)response.StatusCode == 404 || (int)response.StatusCode == 503)
                    {
                        continue;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastHttpError = ex.Message;
                if (ex is HttpRequestException httpError) lastStatusCode = httpError.StatusCode;
                _logger.LogWarning(ex, "Lỗi khi gọi model {ModelName}, thử model tiếp theo.", modelName);
                if (ExternalDependencyResilience.IsTransient(ex)) throw;
            }
        }

        if (!string.IsNullOrWhiteSpace(lastHttpError))
        {
            throw new HttpRequestException($"Gemini Question Generation failed: {lastHttpError}", null, lastStatusCode);
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

        var configuredLlamaModel = _configuration["Cloudflare:LlamaModel"]
            ?? _configuration["Cloudflare__LlamaModel"];
        var llamaModel = string.IsNullOrWhiteSpace(configuredLlamaModel)
            ? "@cf/meta/llama-3.1-8b-instruct"
            : configuredLlamaModel.Trim();
        var url = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/ai/run/{llamaModel}";
        var prompt = QuestionGenerationPrompt.Build(parameters);

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gọi Cloudflare Workers AI sinh câu hỏi thất bại.");
        }

        return new List<GeneratedQuestionDraftDto>();
    }

    private static void ValidateParameters(AiQuestionGenerationParams p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.SelectedClos is null || p.SelectedCloCodes is null || p.SelectedClos.Count == 0 ||
            p.SelectedClos.Any(c => c is null || string.IsNullOrWhiteSpace(c.Code) || string.IsNullOrWhiteSpace(c.Description)) ||
            p.SelectedCloCodes.Any(string.IsNullOrWhiteSpace) ||
            p.SelectedClos.Select(c => c.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != p.SelectedClos.Count ||
            !p.SelectedCloCodes.Select(c => c.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(p.SelectedClos.Select(c => c.Code.Trim())))
            throw new ArgumentException("AI generation requires complete descriptions for every selected CLO.", nameof(p));

        if (p.NumberOfQuestions is < 1 or > 10 || p.EasyCount < 0 || p.MediumCount < 0 || p.HardCount < 0 ||
            (long)p.EasyCount + p.MediumCount + p.HardCount != p.NumberOfQuestions ||
            !UsageScope.All.Contains(p.UsageScope) || p.TargetBloomLevels is null || p.TargetBloomLevels.Count == 0 ||
            p.TargetBloomLevels.Any(level => !BloomLevel.All.Contains(level)))
            throw new ArgumentException("Invalid question count, scope, Bloom levels or difficulty distribution.", nameof(p));
    }

    private static List<GeneratedQuestionDraftDto> ParseAiResponse(string jsonText, AiQuestionGenerationParams parameters)
    {
        try
        {
            var rawList = JsonSerializer.Deserialize<List<GeminiQuestionItem>>(
                ExtractJsonArray(jsonText), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            // Reject the whole batch: do not return a silent subset with a different distribution.
            if (rawList is null || rawList.Count != parameters.NumberOfQuestions)
                return new();

            var selectedClos = parameters.SelectedClos.ToDictionary(c => c.Code.Trim(), StringComparer.OrdinalIgnoreCase);
            var result = new List<GeneratedQuestionDraftDto>();
            foreach (var item in rawList)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.CloCode) ||
                    !selectedClos.TryGetValue(item.CloCode.Trim(), out var clo) ||
                    string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 300 ||
                    string.IsNullOrWhiteSpace(item.Content) || string.IsNullOrWhiteSpace(item.ModelAnswer) ||
                    item.ModelAnswer.Trim().Length < 50 || item.KeyPoints is null || item.KeyPoints.Count == 0 ||
                    item.KeyPoints.Any(string.IsNullOrWhiteSpace) ||
                    item.HasFollowUp || !string.IsNullOrWhiteSpace(item.FollowUpPrompt))
                    return new();

                var difficulty = item.Difficulty?.Trim().ToLowerInvariant();
                var bloom = BloomLevel.All.FirstOrDefault(b => string.Equals(b, item.BloomLevel?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (difficulty is not ("easy" or "medium" or "hard") || bloom is null ||
                    !parameters.TargetBloomLevels.Contains(bloom))
                    return new();

                var rubric = item.Rubric;
                if (rubric is null || string.IsNullOrWhiteSpace(rubric.Name) || rubric.Name.Trim().Length > 200 ||
                    rubric.TotalMaxScore != 10m || rubric.Criteria is null || rubric.Criteria.Count < 2 ||
                    rubric.Criteria.Any(c => c is null || string.IsNullOrWhiteSpace(c.CriterionName) ||
                        c.CriterionName.Trim().Length > 200 || string.IsNullOrWhiteSpace(c.Description) ||
                        c.MaxScore <= 0m || c.MaxScore > 10m || decimal.Round(c.MaxScore, 2) != c.MaxScore ||
                        decimal.Round(c.MaxScore / 10m, 2) != c.MaxScore / 10m ||
                        !BloomLevel.All.Any(b => string.Equals(b, c.BloomLevel?.Trim(), StringComparison.OrdinalIgnoreCase))) ||
                    rubric.Criteria.Sum(c => c.MaxScore) != 10m ||
                    rubric.Criteria.Select(c => c.CriterionName!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rubric.Criteria.Count)
                    return new();

                // Only derived metadata is normalized. Never invent content, add criteria or rescale scores.
                var criteria = rubric.Criteria.Select((c, index) => new RubricCriterionDraftDto(
                    c.CriterionName!.Trim(), c.MaxScore, c.MaxScore / 10m,
                    BloomLevel.All.First(b => string.Equals(b, c.BloomLevel!.Trim(), StringComparison.OrdinalIgnoreCase)),
                    c.Description!.Trim(), index + 1)).ToList();
                result.Add(new GeneratedQuestionDraftDto(
                    Guid.NewGuid().ToString(), clo.Code.Trim(), item.Title.Trim(), item.Content.Trim(),
                    difficulty, bloom, QuestionSource.AiGenerated, parameters.UsageScope,
                    item.ModelAnswer.Trim(), item.KeyPoints.Select(p => p.Trim()).ToList(), false, null,
                    new RubricDraftDto(rubric.Name.Trim(), rubric.Description?.Trim(), 10m, criteria)));
            }

            if (result.Count(q => q.Difficulty == "easy") != parameters.EasyCount ||
                result.Count(q => q.Difficulty == "medium") != parameters.MediumCount ||
                result.Count(q => q.Difficulty == "hard") != parameters.HardCount ||
                result.Select(q => q.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count ||
                result.Select(q => q.Content).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count)
                return new();
            return result;
        }
        catch (JsonException)
        {
            return new();
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
