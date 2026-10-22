using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Infrastructure.Services;

/// <summary>
/// Bộ điều phối nhận diện giọng nói (Speech-to-Text Orchestrator) hỗ trợ cơ chế định tuyến thông minh &amp; tự động chuyển đổi dự phòng hai chiều (Smart Adaptive Auto-Fallback):
/// - Chế độ STT-A (PrimarySttProvider = "Gemini"): Ưu tiên Gemini 3.5 Transcribe (customVocabulary SE + SMART mode) làm Primary, tự động fallback sang Cloudflare Whisper Large-v3-Turbo.
/// - Chế độ Mặc định / Whisper-First: Ưu tiên Cloudflare Workers AI Whisper (@cf/openai/whisper-large-v3-turbo) làm Primary, tự động fallback sang Gemini 3.5 Transcribe / 3.8 Flash.
/// </summary>
public class ResilientSpeechToTextService : ISpeechToTextService
{
    private readonly CloudflareWhisperService _primaryWhisperService;
    private readonly GeminiSpeechToTextService _fallbackGeminiService;
    private readonly ILogger<ResilientSpeechToTextService>? _logger;
    private readonly IConfiguration? _configuration;

    public ResilientSpeechToTextService(
        CloudflareWhisperService primaryWhisperService,
        GeminiSpeechToTextService fallbackGeminiService,
        ILogger<ResilientSpeechToTextService>? logger = null,
        IConfiguration? configuration = null)
    {
        _primaryWhisperService = primaryWhisperService ?? throw new ArgumentNullException(nameof(primaryWhisperService));
        _fallbackGeminiService = fallbackGeminiService ?? throw new ArgumentNullException(nameof(fallbackGeminiService));
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<string> TranscribeAudioAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var detailedResult = await TranscribeDetailedAsync(audioStream, fileName, null, cancellationToken);
        return detailedResult.Text;
    }

    public string NormalizeTranscript(string? rawTranscript) => SeTerminologyNormalizer.Normalize(rawTranscript);

    public async Task<SpeechTranscriptionResult> TranscribeDetailedAsync(
        Stream audioStream,
        string fileName,
        string? vocabularyHint = null,
        CancellationToken cancellationToken = default)
    {
        var preferGeminiFirst = ShouldPreferGeminiPrimary();
        var defaultProviderName = preferGeminiFirst ? "GeminiMultimodalStt" : "CloudflareWhisper";

        if (audioStream == null)
        {
            return new SpeechTranscriptionResult(
                Text: string.Empty,
                RawText: string.Empty,
                Provider: defaultProviderName,
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
                    Provider: defaultProviderName,
                    UsedFallback: false,
                    TimestampsJson: null,
                    ConfidenceScore: 0.0);
            }

            if (audioStream.Position != 0)
            {
                audioStream.Position = 0;
            }
        }

        using var bufferedStream = new MemoryStream();
        await audioStream.CopyToAsync(bufferedStream, cancellationToken);
        var audioBytes = bufferedStream.ToArray();

        if (audioBytes.Length == 0)
        {
            return new SpeechTranscriptionResult(
                Text: string.Empty,
                RawText: string.Empty,
                Provider: defaultProviderName,
                UsedFallback: false,
                TimestampsJson: null,
                ConfidenceScore: 0.0);
        }

        if (preferGeminiFirst)
        {
            return await ExecuteGeminiFirstThenWhisperAsync(audioBytes, fileName, vocabularyHint, cancellationToken);
        }

        return await ExecuteWhisperFirstThenGeminiAsync(audioBytes, fileName, vocabularyHint, cancellationToken);
    }

    private bool ShouldPreferGeminiPrimary()
    {
        var configuredPrimary = _configuration?["Speech:PrimarySttProvider"]
            ?? _configuration?["Speech__PrimarySttProvider"];

        if (string.Equals(configuredPrimary, "Gemini", StringComparison.OrdinalIgnoreCase) &&
            _fallbackGeminiService.HasCredentials)
        {
            return true;
        }

        if (string.Equals(configuredPrimary, "Cloudflare", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !_primaryWhisperService.HasCredentials && _fallbackGeminiService.HasCredentials;
    }

    private async Task<SpeechTranscriptionResult> ExecuteGeminiFirstThenWhisperAsync(
        byte[] audioBytes,
        string fileName,
        string? vocabularyHint,
        CancellationToken cancellationToken)
    {
        Exception? geminiException = null;
        SpeechTranscriptionResult? geminiResult = null;

        // Bước 1 (Primary STT-A): Gemini 3.5 Transcribe (customVocabulary SE + SMART mode) / Gemini 3.8 Flash
        try
        {
            using var primaryStream = new MemoryStream(audioBytes, writable: false);
            geminiResult = await _fallbackGeminiService.TranscribeDetailedAsync(
                primaryStream,
                fileName,
                vocabularyHint,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(geminiResult.Text))
            {
                var normalized = SeTerminologyNormalizer.NormalizeWithContext(geminiResult.Text, vocabularyHint);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return geminiResult with
                    {
                        Text = normalized,
                        Provider = "GeminiMultimodalStt",
                        UsedFallback = false
                    };
                }
            }

            _logger?.LogWarning(
                "Phương án 1 STT-A (GeminiMultimodalStt) trả về kết quả rỗng cho tệp '{FileName}' ({ByteLength} bytes). Chuyển sang Phương án 2 (CloudflareWhisper).",
                fileName,
                audioBytes.Length);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            geminiException = ex;
            _logger?.LogWarning(
                ex,
                "Phương án 1 STT-A (GeminiMultimodalStt) gặp sự cố khi bóc băng '{FileName}'. Tự động chuyển sang Phương án 2 (CloudflareWhisper).",
                fileName);
        }

        // Bước 2 (Auto-Fallback STT-A): Cloudflare Whisper Large-v3-Turbo
        try
        {
            using var fallbackStream = new MemoryStream(audioBytes, writable: false);
            var whisperResult = await _primaryWhisperService.TranscribeDetailedAsync(
                fallbackStream,
                fileName,
                vocabularyHint,
                cancellationToken);

            var normalized = SeTerminologyNormalizer.NormalizeWithContext(whisperResult.Text, vocabularyHint);
            return whisperResult with
            {
                Text = normalized,
                Provider = "CloudflareWhisper",
                UsedFallback = true
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception whisperException)
        {
            _logger?.LogError(
                whisperException,
                "Phương án 2 STT-A (CloudflareWhisper) cũng gặp lỗi khi bóc băng '{FileName}'.",
                fileName);

            if (geminiException is null && geminiResult is not null)
            {
                return geminiResult with { UsedFallback = false };
            }

            throw new InvalidOperationException(
                $"Cả Phương án 1 (GeminiMultimodalStt: {geminiException?.Message}) và Phương án 2 (CloudflareWhisper: {whisperException.Message}) đều thất bại khi bóc băng giọng nói.",
                whisperException);
        }
    }

    private async Task<SpeechTranscriptionResult> ExecuteWhisperFirstThenGeminiAsync(
        byte[] audioBytes,
        string fileName,
        string? vocabularyHint,
        CancellationToken cancellationToken)
    {
        Exception? primaryException = null;
        SpeechTranscriptionResult? primaryResult = null;

        // Bước 1: Phương án 1 (Primary) — Cloudflare Whisper AI
        try
        {
            using var primaryStream = new MemoryStream(audioBytes, writable: false);
            primaryResult = await _primaryWhisperService.TranscribeDetailedAsync(
                primaryStream,
                fileName,
                vocabularyHint,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(primaryResult.Text))
            {
                var normalized = SeTerminologyNormalizer.NormalizeWithContext(primaryResult.Text, vocabularyHint);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return primaryResult with
                    {
                        Text = normalized,
                        Provider = "CloudflareWhisper",
                        UsedFallback = false
                    };
                }
            }

            _logger?.LogWarning(
                "Phương án 1 (CloudflareWhisper) trả về kết quả rỗng cho tệp '{FileName}' ({ByteLength} bytes). Chuyển sang Phương án 2 (GeminiMultimodalStt).",
                fileName,
                audioBytes.Length);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            primaryException = ex;
            _logger?.LogWarning(
                ex,
                "Phương án 1 (CloudflareWhisper) gặp sự cố khi bóc băng '{FileName}'. Tự động chuyển sang Phương án 2 (GeminiMultimodalStt).",
                fileName);
        }

        // Bước 2: Phương án 2 (Auto-Fallback) — Gemini Multimodal Audio STT
        try
        {
            using var fallbackStream = new MemoryStream(audioBytes, writable: false);
            var fallbackResult = await _fallbackGeminiService.TranscribeDetailedAsync(
                fallbackStream,
                fileName,
                vocabularyHint,
                cancellationToken);

            var normalized = SeTerminologyNormalizer.NormalizeWithContext(fallbackResult.Text, vocabularyHint);
            return fallbackResult with
            {
                Text = normalized,
                Provider = "GeminiMultimodalStt",
                UsedFallback = true
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception fallbackException)
        {
            _logger?.LogError(
                fallbackException,
                "Phương án 2 (GeminiMultimodalStt) cũng gặp lỗi khi bóc băng '{FileName}'.",
                fileName);

            if (primaryException is null && primaryResult is not null)
            {
                return primaryResult;
            }

            throw new InvalidOperationException(
                $"Cả Phương án 1 (CloudflareWhisper: {primaryException?.Message}) và Phương án 2 (GeminiMultimodalStt: {fallbackException.Message}) đều thất bại khi bóc băng giọng nói.",
                fallbackException);
        }
    }
}
