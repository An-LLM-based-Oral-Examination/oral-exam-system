using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Infrastructure.Resilience;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Polly;

namespace OralExamination.Infrastructure.Services;

public class CloudflareWhisperService : ISpeechToTextService
{
    private const string DefaultWhisperModel = "@cf/openai/whisper-large-v3-turbo";
    private const string FallbackBinaryWhisperModel = "@cf/openai/whisper";
    private const string TranscriptionLanguage = "vi";

    private readonly HttpClient _httpClient;
    private readonly string _accountId;
    private readonly string _apiToken;
    private readonly string _whisperModel;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<CloudflareWhisperService> _logger;
    private readonly TimeSpan _defensiveTimeout;

    public bool HasCredentials => !string.IsNullOrWhiteSpace(_accountId) && !string.IsNullOrWhiteSpace(_apiToken);

    public CloudflareWhisperService(
        HttpClient httpClient,
        IConfiguration configuration,
        ResiliencePipeline? resiliencePipeline = null,
        ILogger<CloudflareWhisperService>? logger = null)
    {
        _httpClient = httpClient;
        _accountId = configuration["Cloudflare:AccountId"]
            ?? configuration["Cloudflare__AccountId"]
            ?? string.Empty;
        _apiToken = configuration["Cloudflare:ApiToken"]
            ?? configuration["Cloudflare__ApiToken"]
            ?? string.Empty;
        var configuredModel = configuration["Cloudflare:WhisperModel"]
            ?? configuration["Cloudflare__WhisperModel"];
        _whisperModel = string.IsNullOrWhiteSpace(configuredModel)
            ? DefaultWhisperModel
            : configuredModel.Trim();

        // The shared DI HTTP pipeline owns timeout/retry/circuit state. Keep optional override for focused transport tests.
        _resiliencePipeline = resiliencePipeline ?? ResiliencePipeline.Empty;
        _logger = logger ?? NullLogger<CloudflareWhisperService>.Instance;
        _defensiveTimeout = ExternalDependencyResilience.ResolveOuterDefensiveTimeout(configuration, ExternalDependency.Whisper);
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

        // Tận dụng TryGetBuffer của MemoryStream để tránh cấp phát/copy lặp nhiều lần
        var audioBytes = await ResilientSpeechToTextService.ReadAudioSegmentAsync(audioStream, cancellationToken);

        // Kiểm tra sau khi đọc để chặn mọi stream rỗng (kể cả stream non-seekable)
        if (audioBytes.Array is null || audioBytes.Count == 0)
        {
            return string.Empty;
        }

        var responseString = await SendWhisperTurboWithVadAsync(audioBytes, null, cancellationToken);
        var (rawText, _, _, allSegmentsConfirmedSilence) = ParseWhisperResponse(responseString);
        var sanitized = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
        if (string.IsNullOrWhiteSpace(sanitized) && audioBytes.Count >= 4_096 && audioBytes.Count <= 250_000)
        {
            var retryResponse = await SendWhisperTurboWithVadAsync(audioBytes, null, cancellationToken, useVadFilter: false);
            (rawText, _, _, allSegmentsConfirmedSilence) = ParseWhisperResponse(retryResponse);
            sanitized = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
            if (string.IsNullOrWhiteSpace(sanitized) && !allSegmentsConfirmedSilence)
            {
                var binaryResponse = await SendWhisperBinaryRequestAsync(audioBytes, cancellationToken);
                (rawText, _, _, _) = ParseWhisperResponse(binaryResponse);
                sanitized = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
            }
        }
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

        var audioBytes = await ResilientSpeechToTextService.ReadAudioSegmentAsync(audioStream, cancellationToken);

        if (audioBytes.Array is null || audioBytes.Count == 0)
        {
            return new SpeechTranscriptionResult(
                Text: string.Empty,
                RawText: string.Empty,
                Provider: "CloudflareWhisper",
                UsedFallback: false,
                TimestampsJson: null,
                ConfidenceScore: 0.0);
        }

        var sanitizedHint = SpeechVocabularyHintSanitizer.Sanitize(vocabularyHint);
        var responseString = await SendWhisperTurboWithVadAsync(audioBytes, sanitizedHint, cancellationToken);
        var (rawText, timestampsJson, confidenceScore, allSegmentsConfirmedSilence) = ParseWhisperResponse(responseString);
        var sanitizedRaw = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
        if (string.IsNullOrWhiteSpace(sanitizedRaw) && audioBytes.Count >= 4_096 && audioBytes.Count <= 250_000)
        {
            var retryResponse = await SendWhisperTurboWithVadAsync(audioBytes, sanitizedHint, cancellationToken, useVadFilter: false);
            (rawText, timestampsJson, confidenceScore, allSegmentsConfirmedSilence) = ParseWhisperResponse(retryResponse);
            sanitizedRaw = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
            if (string.IsNullOrWhiteSpace(sanitizedRaw) && !allSegmentsConfirmedSilence)
            {
                var binaryResponse = await SendWhisperBinaryRequestAsync(audioBytes, cancellationToken);
                (rawText, timestampsJson, confidenceScore, _) = ParseWhisperResponse(binaryResponse);
                sanitizedRaw = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
            }
        }
        var normalizedText = SeTerminologyNormalizer.NormalizeWithContext(sanitizedRaw, sanitizedHint);

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
    /// Tự động phát hiện ngôn ngữ: nếu vocabularyHint chứa mã môn tiếng Anh (vd: ENT001, ENT104) thì trả về "en";
    /// ngược lại (kể cả SE và chế độ song ngữ "lang:en") mặc định trả về "vi".
    /// </summary>
    public static string DetectLanguage(string? vocabularyHint)
    {
        if (SeTerminologyNormalizer.IsEnglishCourseHint(vocabularyHint))
        {
            return "en";
        }

        return TranscriptionLanguage;
    }

    /// <summary>
    /// Gọi mô hình @cf/openai/whisper-large-v3-turbo hỗ trợ song ngữ Việt - Anh (Code-Switching) cho mọi môn học (cả ENT và SE).
    /// Đặt language phù hợp ("en" cho ENT, "vi" cho SE), task = "transcribe" và luôn truyền chuỗi initial_prompt sạch khác null (tránh lỗi HTTP 400 code 5006
    /// Type mismatch of '/initial_prompt' trên Cloudflare và chống ảo giác phụ đề khi sinh viên trả lời bằng tiếng Anh ở môn ENT).
    /// Với file âm thanh dài (> 250KB) hoặc khi retry sau lúc VAD lọc nhầm, tự động tắt vad_filter.
    /// </summary>
    private async Task<string> SendWhisperTurboWithVadAsync(
        ArraySegment<byte> audioBytes,
        string? vocabularyHint,
        CancellationToken cancellationToken,
        bool useVadFilter = true)
    {
        var turboUrl = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/{_whisperModel}";
        var language = DetectLanguage(vocabularyHint);
        var contextualPrompt = SeTerminologyNormalizer.BuildContextualWhisperPrompt(vocabularyHint);
        var prompt = !string.IsNullOrWhiteSpace(contextualPrompt)
            ? contextualPrompt
            : (language == "en" ? "English vocabulary: clear pronunciation, academic and technical terms." : SeTerminologyNormalizer.DefaultSeWhisperPrompt);
        var enableVadFilter = useVadFilter && audioBytes.Count <= 250_000;

        var jsonPayload = JsonSerializer.Serialize(new
        {
            audio = Convert.ToBase64String(audioBytes.AsSpan()),
            language,
            task = "transcribe",
            vad_filter = enableVadFilter,
            initial_prompt = prompt
        });

        try
        {
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(_defensiveTimeout);
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, turboUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
                    request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                    using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                    var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Cloudflare Whisper STT ({Model}) returned HTTP {StatusCode}.",
                            _whisperModel,
                            (int)response.StatusCode);
                        throw new HttpRequestException(
                            $"Cloudflare Whisper STT failed: {response.StatusCode} - {body}",
                            null,
                            response.StatusCode);
                    }

                    return body;
                }
                catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Cloudflare Whisper STT ({Model}) timed out after {TimeoutSeconds}s.",
                        _whisperModel,
                        (int)_defensiveTimeout.TotalSeconds);
                    throw new HttpRequestException(
                        $"Cloudflare Whisper STT timed out after {(int)_defensiveTimeout.TotalSeconds}s.",
                        ex,
                        HttpStatusCode.RequestTimeout);
                }
            }, cancellationToken);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.NotFound
            or HttpStatusCode.UnsupportedMediaType)
        {
            _logger.LogWarning(
                ex,
                "Cloudflare Whisper ({Model}) returned {StatusCode}; falling back to binary model {FallbackModel}.",
                _whisperModel,
                ex.StatusCode,
                FallbackBinaryWhisperModel);
            return await SendWhisperBinaryRequestAsync(audioBytes, cancellationToken);
        }
    }

    private async Task<string> SendWhisperBinaryRequestAsync(ArraySegment<byte> audioBytes, CancellationToken cancellationToken)
    {
        var url = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/{FallbackBinaryWhisperModel}";

        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_defensiveTimeout);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

                var content = new ByteArrayContent(audioBytes.Array!, audioBytes.Offset, audioBytes.Count);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                request.Content = content;

                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Cloudflare Whisper binary fallback ({Model}) returned HTTP {StatusCode}.",
                        FallbackBinaryWhisperModel,
                        (int)response.StatusCode);
                    throw new HttpRequestException($"Cloudflare Whisper STT failed: {response.StatusCode} - {body}", null, response.StatusCode);
                }

                return body;
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Cloudflare Whisper binary fallback ({Model}) timed out after {TimeoutSeconds}s.",
                    FallbackBinaryWhisperModel,
                    (int)_defensiveTimeout.TotalSeconds);
                throw new HttpRequestException(
                    $"Cloudflare Whisper STT timed out after {(int)_defensiveTimeout.TotalSeconds}s.",
                    ex,
                    HttpStatusCode.RequestTimeout);
            }
        }, cancellationToken);
    }

    private static (string RawText, string? TimestampsJson, double? ConfidenceScore, bool AllSegmentsConfirmedSilence) ParseWhisperResponse(string responseString)
    {
        if (string.IsNullOrWhiteSpace(responseString))
        {
            return (string.Empty, null, null, false);
        }

        using var document = JsonDocument.Parse(responseString);
        string rawText = string.Empty;
        string? timestampsJson = null;
        double? confidenceScore = null;
        bool allSegmentsConfirmedSilence = false;

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

                    if (!string.IsNullOrWhiteSpace(segmentText) && SeTerminologyNormalizer.IsHallucinationOrNoise(segmentText))
                    {
                        continue;
                    }

                    validSegments.Add((segmentText, segmentConfidence, duration, segment));
                }

                if ((totalSegmentsCount > 0 && validSegments.Count == 0) ||
                    (totalSegmentsCount == 0 && string.IsNullOrWhiteSpace(rawText)))
                {
                    // Toàn bộ audio bị chi phối bởi im lặng hoặc ảo giác (no_speech_prob > 0.85 hoặc VAD trả về segments rỗng)
                    rawText = string.Empty;
                    confidenceScore = 0.0;
                    timestampsJson = null;
                    allSegmentsConfirmedSilence = totalSegmentsCount > 0;
                }
                else if (validSegments.Count > 0)
                {
                    // Nếu có segment bị lọc bỏ, tái tạo rawText từ các segment hợp lệ để khử ảo giác tiếng ồn
                    if (validSegments.Count < totalSegmentsCount)
                    {
                        var reconstructed = string.Join(" ", validSegments
                            .Select(s => s.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t)))
                            .Trim();
                        if (!string.IsNullOrWhiteSpace(reconstructed))
                        {
                            rawText = reconstructed;
                        }
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

            return (rawText, timestampsJson, confidenceScore, allSegmentsConfirmedSilence);
        }

        if (document.RootElement.TryGetProperty("text", out var rootTextElement))
        {
            rawText = (rootTextElement.GetString() ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                confidenceScore = 0.90;
            }
        }

        return (rawText, timestampsJson, confidenceScore, allSegmentsConfirmedSilence);
    }
}
