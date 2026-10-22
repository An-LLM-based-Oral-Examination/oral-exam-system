using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Polly;
using Polly.Retry;

namespace OralExamination.Infrastructure.Services;

public class CloudflareWhisperService : ISpeechToTextService
{
    private const string WhisperModel = "@cf/openai/whisper-large-v3-turbo";
    private const string TranscriptionLanguage = "vi";

    private readonly HttpClient _httpClient;
    private readonly string _accountId;
    private readonly string _apiToken;
    private readonly ResiliencePipeline _resiliencePipeline;

    public bool HasCredentials => !string.IsNullOrWhiteSpace(_accountId) && !string.IsNullOrWhiteSpace(_apiToken);

    public CloudflareWhisperService(
        HttpClient httpClient,
        IConfiguration configuration,
        ResiliencePipeline? resiliencePipeline = null)
    {
        _httpClient = httpClient;
        _accountId = configuration["Cloudflare:AccountId"]
            ?? configuration["Cloudflare__AccountId"]
            ?? string.Empty;
        _apiToken = configuration["Cloudflare:ApiToken"]
            ?? configuration["Cloudflare__ApiToken"]
            ?? string.Empty;

        // Cấu hình Polly Resilience Pipeline: Chỉ retry với lỗi mạng hoặc HTTP 5xx/429/408 (không retry vô ích với 400 BadRequest)
        _resiliencePipeline = resiliencePipeline ?? new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(400),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode is null
                        or >= System.Net.HttpStatusCode.InternalServerError
                        or System.Net.HttpStatusCode.TooManyRequests
                        or System.Net.HttpStatusCode.RequestTimeout)
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
            })
            .Build();
    }

    public async Task<string> TranscribeAudioAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_accountId) || string.IsNullOrEmpty(_apiToken))
        {
            throw new InvalidOperationException("Cloudflare Credentials for Whisper are missing.");
        }

        // Kiểm tra stream rỗng an toàn, tránh Cloudflare trả về 400 Bad Request
        if (audioStream == null)
        {
            return string.Empty;
        }

        if (audioStream.CanSeek)
        {
            if (audioStream.Length == 0)
            {
                return string.Empty;
            }

            if (audioStream.Position != 0)
            {
                audioStream.Position = 0;
            }
        }

        // Convert stream to byte array (Cloudflare AI requires binary payload)
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        var audioBytes = memoryStream.ToArray();

        // Kiểm tra sau khi copy để chặn mọi stream rỗng (kể cả stream non-seekable)
        if (audioBytes.Length == 0)
        {
            return string.Empty;
        }

        var responseString = await SendWhisperTurboWithVadAsync(audioBytes, null, cancellationToken);
        var (rawText, _, _) = ParseWhisperResponse(responseString);
        var sanitized = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
        return SeTerminologyNormalizer.NormalizeWithContext(sanitized, null);
    }

    public async Task<SpeechTranscriptionResult> TranscribeDetailedAsync(
        Stream audioStream,
        string fileName,
        string? vocabularyHint = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_accountId) || string.IsNullOrEmpty(_apiToken))
        {
            throw new InvalidOperationException("Cloudflare Credentials for Whisper are missing.");
        }

        if (audioStream == null)
        {
            return new SpeechTranscriptionResult(
                Text: string.Empty,
                RawText: string.Empty,
                Provider: "CloudflareWhisper",
                UsedFallback: false,
                TimestampsJson: null,
                ConfidenceScore: 0.0);
        }

        if (audioStream.CanSeek)
        {
            if (audioStream.Length == 0)
            {
                return new SpeechTranscriptionResult(
                    Text: string.Empty,
                    RawText: string.Empty,
                    Provider: "CloudflareWhisper",
                    UsedFallback: false,
                    TimestampsJson: null,
                    ConfidenceScore: 0.0);
            }

            if (audioStream.Position != 0)
            {
                audioStream.Position = 0;
            }
        }

        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        var audioBytes = memoryStream.ToArray();

        if (audioBytes.Length == 0)
        {
            return new SpeechTranscriptionResult(
                Text: string.Empty,
                RawText: string.Empty,
                Provider: "CloudflareWhisper",
                UsedFallback: false,
                TimestampsJson: null,
                ConfidenceScore: 0.0);
        }

        var responseString = await SendWhisperTurboWithVadAsync(audioBytes, vocabularyHint, cancellationToken);
        var (rawText, timestampsJson, confidenceScore) = ParseWhisperResponse(responseString);
        var sanitizedRaw = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
        var normalizedText = SeTerminologyNormalizer.NormalizeWithContext(sanitizedRaw, vocabularyHint);

        var finalConfidence = string.IsNullOrWhiteSpace(sanitizedRaw) ? 0.0 : confidenceScore;

        return new SpeechTranscriptionResult(
            Text: normalizedText,
            RawText: sanitizedRaw,
            Provider: "CloudflareWhisper",
            UsedFallback: false,
            TimestampsJson: timestampsJson,
            ConfidenceScore: finalConfidence);
    }

    /// <summary>
    /// Gọi mô hình @cf/openai/whisper-large-v3-turbo kèm bộ lọc tiếng ồn Silero VAD (vad_filter = true),
    /// ngôn ngữ tiếng Việt kết hợp gợi ý thuật ngữ chuyên ngành SE song ngữ Anh - Việt (initial_prompt).
    /// Tự động fallback về @cf/openai/whisper nếu endpoint turbo bị từ chối.
    /// </summary>
    private async Task<string> SendWhisperTurboWithVadAsync(
        byte[] audioBytes,
        string? vocabularyHint,
        CancellationToken cancellationToken)
    {
        var turboUrl = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/{WhisperModel}";
        var prompt = SeTerminologyNormalizer.BuildContextualWhisperPrompt(vocabularyHint);

        var jsonPayload = JsonSerializer.Serialize(new
        {
            audio = Convert.ToBase64String(audioBytes),
            language = TranscriptionLanguage,
            task = "transcribe",
            vad_filter = true,
            initial_prompt = prompt
        });

        try
        {
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, turboUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
                request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Cloudflare Whisper STT failed: {response.StatusCode} - {body}",
                        null,
                        response.StatusCode);
                }

                return body;
            }, cancellationToken);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.BadRequest
            or System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.UnsupportedMediaType)
        {
            return await SendWhisperBinaryRequestAsync(audioBytes, cancellationToken);
        }
    }

    private async Task<string> SendWhisperBinaryRequestAsync(byte[] audioBytes, CancellationToken cancellationToken)
    {
        var url = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/@cf/openai/whisper";

        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

            var content = new ByteArrayContent(audioBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content = content;

            var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Cloudflare Whisper STT failed: {response.StatusCode} - {body}", null, response.StatusCode);
            }

            return body;
        }, cancellationToken);
    }

    private static (string RawText, string? TimestampsJson, double? ConfidenceScore) ParseWhisperResponse(string responseString)
    {
        if (string.IsNullOrWhiteSpace(responseString))
        {
            return (string.Empty, null, null);
        }

        using var document = JsonDocument.Parse(responseString);
        string rawText = string.Empty;
        string? timestampsJson = null;
        double? confidenceScore = null;

        if (document.RootElement.TryGetProperty("result", out var resultElement) &&
            resultElement.ValueKind == JsonValueKind.Object)
        {
            if (resultElement.TryGetProperty("text", out var textElement))
            {
                rawText = (textElement.GetString() ?? string.Empty).Trim();
            }

            // Xử lý và hiệu chuẩn độ tin cậy từ danh sách segments
            if (resultElement.TryGetProperty("segments", out var segmentsElement) &&
                segmentsElement.ValueKind == JsonValueKind.Array)
            {
                const double silenceThreshold = 0.85;
                var validSegments = new List<(string Text, double Confidence, double Duration, JsonElement RawElement)>();
                int totalSegmentsCount = 0;

                foreach (var segment in segmentsElement.EnumerateArray())
                {
                    totalSegmentsCount++;

                    double noSpeechProb = 0.0;
                    if (segment.TryGetProperty("no_speech_prob", out var noSpeechProp) &&
                        noSpeechProp.ValueKind == JsonValueKind.Number &&
                        noSpeechProp.TryGetDouble(out var nspVal) &&
                        !double.IsNaN(nspVal) && !double.IsInfinity(nspVal))
                    {
                        noSpeechProb = nspVal;
                    }

                    // Loại bỏ segment bị chi phối bởi tiếng ồn hoặc im lặng
                    if (noSpeechProb > silenceThreshold)
                    {
                        continue;
                    }

                    double avgLogprob = -0.20; // Giá trị ngầm định hợp lý nếu thiếu
                    if (segment.TryGetProperty("avg_logprob", out var avgLogprobProp) &&
                        avgLogprobProp.ValueKind == JsonValueKind.Number &&
                        avgLogprobProp.TryGetDouble(out var alpVal) &&
                        !double.IsNaN(alpVal) && !double.IsInfinity(alpVal))
                    {
                        avgLogprob = alpVal;
                    }

                    // Công thức hiệu chuẩn độ tin cậy segment: c_i = Clamp(e^(avg_logprob) * (1 - no_speech_prob), 0.0, 1.0)
                    double segmentConfidence = Math.Clamp(Math.Exp(avgLogprob) * (1.0 - noSpeechProb), 0.0, 1.0);

                    double start = 0.0;
                    double end = 0.0;
                    if (segment.TryGetProperty("start", out var startProp) &&
                        startProp.ValueKind == JsonValueKind.Number &&
                        startProp.TryGetDouble(out var sVal) &&
                        !double.IsNaN(sVal) && !double.IsInfinity(sVal))
                    {
                        start = sVal;
                    }
                    if (segment.TryGetProperty("end", out var endProp) &&
                        endProp.ValueKind == JsonValueKind.Number &&
                        endProp.TryGetDouble(out var eVal) &&
                        !double.IsNaN(eVal) && !double.IsInfinity(eVal))
                    {
                        end = eVal;
                    }
                    double duration = Math.Max(0.0, end - start);

                    string segmentText = string.Empty;
                    if (segment.TryGetProperty("text", out var segTextProp))
                    {
                        segmentText = (segTextProp.GetString() ?? string.Empty).Trim();
                    }

                    validSegments.Add((segmentText, segmentConfidence, duration, segment));
                }

                if (totalSegmentsCount > 0 && validSegments.Count == 0)
                {
                    // Toàn bộ audio bị chi phối bởi im lặng hoặc ảo giác (no_speech_prob > 0.85)
                    rawText = string.Empty;
                    confidenceScore = 0.0;
                    timestampsJson = null;
                }
                else if (validSegments.Count > 0)
                {
                    // Nếu có segment bị lọc bỏ, tái tạo rawText từ các segment hợp lệ để khử ảo giác tiếng ồn
                    if (validSegments.Count < totalSegmentsCount)
                    {
                        rawText = string.Join(" ", validSegments
                            .Select(s => s.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t)))
                            .Trim();
                    }

                    // Tính điểm tin cậy tổng thể (ưu tiên trọng số theo thời lượng duration-weighted)
                    double totalDuration = validSegments.Sum(s => s.Duration);
                    if (totalDuration > 0.001)
                    {
                        confidenceScore = validSegments.Sum(s => s.Confidence * s.Duration) / totalDuration;
                    }
                    else
                    {
                        confidenceScore = validSegments.Average(s => s.Confidence);
                    }

                    confidenceScore = Math.Round(Math.Clamp(confidenceScore.Value, 0.0, 1.0), 4);

                    // Trích xuất timestampsJson: giữ nguyên array gốc nếu không có segment bị lọc, hoặc serialize valid elements
                    if (validSegments.Count == totalSegmentsCount)
                    {
                        timestampsJson = segmentsElement.GetRawText();
                    }
                    else
                    {
                        timestampsJson = JsonSerializer.Serialize(validSegments.Select(s => s.RawElement));
                    }
                }
            }

            // Fallback trích xuất words hoặc vtt nếu timestampsJson chưa được gán từ segments
            if (string.IsNullOrEmpty(timestampsJson))
            {
                if (resultElement.TryGetProperty("words", out var wordsElement) &&
                    wordsElement.ValueKind == JsonValueKind.Array)
                {
                    timestampsJson = wordsElement.GetRawText();
                }
                else if (resultElement.TryGetProperty("vtt", out var vttElement) &&
                         vttElement.ValueKind == JsonValueKind.String)
                {
                    timestampsJson = vttElement.GetRawText();
                }
            }

            // Nếu không có segments diagnostics nhưng có text hợp lệ, gán heuristic confidenceScore = 0.90
            if (confidenceScore == null && !string.IsNullOrWhiteSpace(rawText))
            {
                confidenceScore = 0.90;
            }

            return (rawText, timestampsJson, confidenceScore);
        }

        if (document.RootElement.TryGetProperty("text", out var rootTextElement))
        {
            rawText = (rootTextElement.GetString() ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                confidenceScore = 0.90;
            }
        }

        return (rawText, timestampsJson, confidenceScore);
    }
}
