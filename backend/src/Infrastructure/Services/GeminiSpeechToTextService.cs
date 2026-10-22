using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Infrastructure.Services;

/// <summary>
/// Dịch vụ bóc băng âm thanh dự phòng (Phương án 2 — Automatic Fallback STT) sử dụng Gemini Multimodal Audio.
/// Tự động kích hoạt khi Cloudflare Whisper AI (Phương án 1) gặp sự cố mạng, quá tải hoặc thiếu thông tin xác thực.
/// </summary>
public class GeminiSpeechToTextService
{
    private const string DefaultGeminiSttUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-transcribe:generateContent";

    private static readonly string[] FallbackSttUrls =
    [
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-transcribe:generateContent",
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent",
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.7-flash:generateContent",
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent",
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent",
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent",
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiUrl;
    private readonly IReadOnlyList<string> _candidateSttUrls;
    private readonly ILogger<GeminiSpeechToTextService>? _logger;

    public bool HasCredentials => !string.IsNullOrWhiteSpace(_apiKey);

    public GeminiSpeechToTextService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiSpeechToTextService>? logger = null)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"]
            ?? configuration["Gemini__ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? string.Empty;

        var configuredSttUrl = configuration["Gemini:SttApiUrl"]
            ?? configuration["Gemini__SttApiUrl"]
            ?? Environment.GetEnvironmentVariable("GEMINI_STT_API_URL");

        if (string.IsNullOrWhiteSpace(configuredSttUrl))
        {
            var configuredUrl = configuration["Gemini:ApiUrl"]
                ?? configuration["Gemini__ApiUrl"];

            if (!string.IsNullOrWhiteSpace(configuredUrl) &&
                configuredUrl.Contains("transcribe", StringComparison.OrdinalIgnoreCase))
            {
                configuredSttUrl = configuredUrl;
            }
        }

        _apiUrl = string.IsNullOrWhiteSpace(configuredSttUrl)
            ? DefaultGeminiSttUrl
            : configuredSttUrl.Trim();

        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredSttUrl))
        {
            urls.Add(configuredSttUrl.Trim());
        }

        foreach (var fallbackUrl in FallbackSttUrls)
        {
            if (!urls.Contains(fallbackUrl, StringComparer.OrdinalIgnoreCase))
            {
                urls.Add(fallbackUrl);
            }
        }

        _candidateSttUrls = urls;
        _logger = logger;
    }

    public async Task<string> TranscribeAudioAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var result = await TranscribeDetailedAsync(audioStream, fileName, null, cancellationToken);
        return result.Text;
    }

    public async Task<SpeechTranscriptionResult> TranscribeDetailedAsync(
        Stream audioStream,
        string fileName,
        string? vocabularyHint = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Gemini API Key for fallback STT is missing.");
        }

        if (audioStream == null)
        {
            return new SpeechTranscriptionResult(
                Text: string.Empty,
                RawText: string.Empty,
                Provider: "GeminiMultimodalStt",
                UsedFallback: true,
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
                    Provider: "GeminiMultimodalStt",
                    UsedFallback: true,
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
                Provider: "GeminiMultimodalStt",
                UsedFallback: true,
                TimestampsJson: null,
                ConfidenceScore: 0.0);
        }

        var mimeType = ResolveMimeType(fileName);
        var base64Audio = Convert.ToBase64String(audioBytes);
        var hint = string.IsNullOrWhiteSpace(vocabularyHint)
            ? SeTerminologyNormalizer.DefaultSeWhisperPrompt
            : vocabularyHint.Trim();

        var customVocabulary = SeTerminologyNormalizer.BuildCustomVocabularyList(vocabularyHint, maxTerms: 100);

        // Payload chuẩn Dedicated ASR cho gemini-3.5-transcribe (Google Gemini API Docs: generate-content/transcribe.md)
        var dedicatedTranscribePayload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Audio
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                audioTranscriptionConfig = new
                {
                    languageCodes = new[] { "vi-VN", "en-US" },
                    customVocabulary = customVocabulary,
                    mode = "SMART"
                }
            }
        };

        // Payload chuẩn Multimodal Audio cho gemini-3.8-flash / gemini-3.6-flash (không dùng temperature theo chuẩn Gemini 3.8)
        var multimodalPrompt =
            $"Bạn là bộ bóc băng âm thanh (Speech-to-Text) đa ngôn ngữ Việt - Anh chuyên biệt cho kỳ thi vấn đáp Kỹ thuật Phần mềm và Tiếng Anh tại Đại học FPT (hỗ trợ cả tiếng Việt kết hợp thuật ngữ tiếng Anh Code-Switching lẫn câu trả lời bằng tiếng Anh).\n" +
            $"Từ vựng chuyên ngành ưu tiên nhận diện chính xác: {hint}\n" +
            $"Yêu cầu: Chỉ trả về nguyên văn lời nói của sinh viên trong đoạn âm thanh dưới dạng chuỗi văn bản thuần túy (hoặc JSON {{\"text\": \"...\"}}). Tuyệt đối không tự giải thích, không thêm lời bình luận và không bịa thêm ý.";

        var multimodalPayload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Audio
                            }
                        },
                        new
                        {
                            text = multimodalPrompt
                        }
                    }
                }
            }
        };

        var serializedTranscribePayload = JsonSerializer.Serialize(dedicatedTranscribePayload, JsonOptions);
        var serializedMultimodalPayload = JsonSerializer.Serialize(multimodalPayload, JsonOptions);
        HttpRequestException? lastHttpException = null;

        foreach (var candidateUrl in _candidateSttUrls)
        {
            var isDedicatedTranscribeModel = candidateUrl.Contains("gemini-3.5-transcribe", StringComparison.OrdinalIgnoreCase);
            var payloadJson = isDedicatedTranscribeModel ? serializedTranscribePayload : serializedMultimodalPayload;

            using var request = new HttpRequestMessage(HttpMethod.Post, candidateUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning(
                    "Gemini Multimodal STT ({Url}) trả về lỗi HTTP {StatusCode}: {Body}",
                    candidateUrl,
                    response.StatusCode,
                    responseBody);
                lastHttpException = new HttpRequestException(
                    $"Gemini Multimodal STT failed: {response.StatusCode} - {responseBody}",
                    null,
                    response.StatusCode);
                continue;
            }

            var rawText = ExtractCandidateText(responseBody);
            var sanitizedRaw = SeTerminologyNormalizer.SanitizeHallucinations(rawText);
            var normalizedText = SeTerminologyNormalizer.NormalizeWithContext(sanitizedRaw, vocabularyHint);

            var isEmptyOrHallucinated = string.IsNullOrWhiteSpace(sanitizedRaw) || string.IsNullOrWhiteSpace(normalizedText);
            var finalRaw = isEmptyOrHallucinated ? string.Empty : sanitizedRaw;
            var finalText = isEmptyOrHallucinated ? string.Empty : normalizedText;
            var finalConfidence = isEmptyOrHallucinated
                ? 0.0
                : (isDedicatedTranscribeModel ? 0.96 : 0.92);

            return new SpeechTranscriptionResult(
                Text: finalText,
                RawText: finalRaw,
                Provider: "GeminiMultimodalStt",
                UsedFallback: true,
                TimestampsJson: null,
                ConfidenceScore: finalConfidence);
        }

        throw lastHttpException ?? new HttpRequestException("Gemini Multimodal STT failed across all fallback models.");
    }

    private static string ResolveMimeType(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "audio/webm";
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mp3",
            ".ogg" => "audio/ogg",
            ".m4a" or ".mp4" => "audio/mp4",
            _ => "audio/webm"
        };
    }

    private static string ExtractCandidateText(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var firstCandidate = candidates[0];
        if (!firstCandidate.TryGetProperty("content", out var contentElement) ||
            !contentElement.TryGetProperty("parts", out var partsElement) ||
            partsElement.ValueKind != JsonValueKind.Array ||
            partsElement.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var candidateText = (partsElement[0].TryGetProperty("text", out var textProp)
            ? textProp.GetString()
            : string.Empty)?.Trim() ?? string.Empty;

        if (candidateText.StartsWith('{') && candidateText.EndsWith('}'))
        {
            try
            {
                using var innerDoc = JsonDocument.Parse(candidateText);
                if (innerDoc.RootElement.TryGetProperty("text", out var innerTextProp) &&
                    innerTextProp.ValueKind == JsonValueKind.String)
                {
                    return (innerTextProp.GetString() ?? string.Empty).Trim();
                }
            }
            catch (JsonException)
            {
                // Nếu không phải JSON hợp lệ thì dùng trực tiếp văn bản thuần
            }
        }

        return candidateText;
    }
}
