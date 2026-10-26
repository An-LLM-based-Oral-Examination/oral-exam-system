using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Infrastructure.Services;

/// <summary>
/// Bộ điều phối tổng hợp giọng nói AI giám khảo (Text-to-Speech Orchestrator):
/// - Hỗ trợ 2 giọng chuẩn khảo thí: Cô Hoài My (vi-VN-HoaiMyNeural) và Thầy Nam Minh (vi-VN-NamMinhNeural).
/// - Lưu bộ nhớ đệm SHA-256 trong RAM (tối đa 250 câu hỏi, TTL 6 giờ) giúp phát lại tức thì (&lt; 1ms).
/// - Phương án 1 (Primary): Microsoft Edge Neural Read-Aloud TTS (WebSocket MP3 24kHz).
/// - Phương án 2 (Auto-Fallback): Google Neural Translate HTTP TTS (và nhánh dự phòng Gemini Audio) khi Phương án 1 gặp lỗi.
/// </summary>
public class ResilientTextToSpeechService : ITextToSpeechService
{
    public const string DefaultFemaleVoice = "vi-VN-HoaiMyNeural";
    public const string DefaultMaleVoice = "vi-VN-NamMinhNeural";
    public const string DefaultEnglishFemaleVoice = "en-US-JennyNeural";
    public const string DefaultEnglishMaleVoice = "en-US-GuyNeural";

    private const string EdgeTrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const int MaxCacheEntries = 250;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private static readonly IReadOnlyList<TtsVoiceOptionDto> SupportedVoices =
    [
        new TtsVoiceOptionDto(
            VoiceId: DefaultFemaleVoice,
            DisplayName: "Cô Hoài My (Giọng Nữ miền Bắc - Chuẩn Khảo thí)",
            Gender: "Female",
            Locale: "vi-VN",
            IsDefault: true),
        new TtsVoiceOptionDto(
            VoiceId: DefaultMaleVoice,
            DisplayName: "Thầy Nam Minh (Giọng Nam miền Bắc - Trầm ấm)",
            Gender: "Male",
            Locale: "vi-VN",
            IsDefault: false)
    ];

    private static readonly ConcurrentDictionary<string, CachedTtsEntry> SharedProductionCache = new(StringComparer.Ordinal);
    private static long _edgeCooldownUntilUtcTicks;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResilientTextToSpeechService>? _logger;
    private readonly Func<string, string, double, CancellationToken, Task<byte[]>> _primaryEdgeSynthesizer;
    private readonly bool _useCustomPrimarySynthesizer;
    private readonly bool _trackCooldown;
    private readonly ConcurrentDictionary<string, CachedTtsEntry> _cache;

    [ActivatorUtilitiesConstructor]
    public ResilientTextToSpeechService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ResilientTextToSpeechService>? logger = null)
        : this(httpClient, configuration, logger, null, enableCooldownForCustom: false)
    {
    }

    public ResilientTextToSpeechService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ResilientTextToSpeechService>? logger,
        Func<string, string, double, CancellationToken, Task<byte[]>>? primaryEdgeSynthOverride)
        : this(httpClient, configuration, logger, primaryEdgeSynthOverride, enableCooldownForCustom: false)
    {
    }

    public ResilientTextToSpeechService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ResilientTextToSpeechService>? logger,
        Func<string, string, double, CancellationToken, Task<byte[]>>? primaryEdgeSynthOverride,
        bool enableCooldownForCustom)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _useCustomPrimarySynthesizer = primaryEdgeSynthOverride is not null;
        _primaryEdgeSynthesizer = primaryEdgeSynthOverride ?? SynthesizeWithEdgeNeuralWebSocketAsync;
        _trackCooldown = primaryEdgeSynthOverride is null || enableCooldownForCustom;
        _cache = primaryEdgeSynthOverride is null
            ? SharedProductionCache
            : new ConcurrentDictionary<string, CachedTtsEntry>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Đặt lại thời gian đóng băng (cooldown) của Edge Neural TTS về 0 (cho phép gọi lại ngay lập tức).
    /// </summary>
    public static void ResetEdgeCooldown()
    {
        Interlocked.Exchange(ref _edgeCooldownUntilUtcTicks, 0);
    }

    /// <summary>
    /// Kiểm tra xem Edge Neural TTS có đang trong thời gian đóng băng (cooldown) do lỗi trước đó hay không.
    /// </summary>
    public static bool IsEdgeInCooldown =>
        Interlocked.Read(ref _edgeCooldownUntilUtcTicks) > DateTimeOffset.UtcNow.UtcTicks;

    /// <summary>
    /// Thiết lập thủ công thời gian đóng băng Edge Neural TTS (phục vụ viết unit test).
    /// </summary>
    public static void SetEdgeCooldown(TimeSpan duration)
    {
        Interlocked.Exchange(
            ref _edgeCooldownUntilUtcTicks,
            DateTimeOffset.UtcNow.Add(duration).UtcTicks);
    }

    /// <summary>
    /// Lấy thời điểm kết thúc thời gian đóng băng Edge Neural TTS theo UTC.
    /// </summary>
    public static DateTimeOffset? EdgeCooldownUntilUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _edgeCooldownUntilUtcTicks);
            return ticks > DateTimeOffset.UtcNow.UtcTicks
                ? new DateTimeOffset(ticks, TimeSpan.Zero)
                : null;
        }
    }

    public IReadOnlyList<TtsVoiceOptionDto> GetAvailableVoices() => SupportedVoices;

    public async Task<SpeechSynthesisResult> SynthesizeSpeechAsync(
        string text,
        string? voiceName = null,
        double speakingRate = 1.0,
        CancellationToken cancellationToken = default)
    {
        var cleanedText = SeTerminologyNormalizer.PrepareTextForTts(text);
        if (string.IsNullOrWhiteSpace(cleanedText))
        {
            throw new ArgumentException("Nội dung văn bản cần tổng hợp giọng nói không được để trống.", nameof(text));
        }

        var resolvedVoice = ResolveVoiceName(voiceName);
        var clampedRate = Math.Clamp(speakingRate, 0.75, 1.35);
        var cacheKey = ComputeCacheKeySha256(resolvedVoice, clampedRate, cleanedText);

        // Kiểm tra bộ nhớ đệm SHA-256
        if (_cache.TryGetValue(cacheKey, out var cachedEntry))
        {
            if (DateTimeOffset.UtcNow - cachedEntry.CreatedAtUtc <= CacheTtl && cachedEntry.AudioBytes.Length > 0)
            {
                return new SpeechSynthesisResult(
                    AudioBytes: cachedEntry.AudioBytes,
                    ContentType: cachedEntry.ContentType,
                    Provider: cachedEntry.Provider,
                    VoiceName: resolvedVoice,
                    SpeakingRate: clampedRate,
                    UsedFallback: cachedEntry.UsedFallback,
                    FromCache: true,
                    CacheKeySha256: cacheKey);
            }

            _cache.TryRemove(cacheKey, out _);
        }

        Exception? primaryException = null;

        // Bước 1: Phương án 1 (Primary) — Edge Neural TTS (Hoài My & Nam Minh chuẩn Neural)
        var disablePrimary = string.Equals(
            _configuration["Speech:DisableEdgePrimary"],
            "true",
            StringComparison.OrdinalIgnoreCase);
        var isEdgeInCooldown = _trackCooldown &&
            Interlocked.Read(ref _edgeCooldownUntilUtcTicks) > DateTimeOffset.UtcNow.UtcTicks;

        if (!disablePrimary && !isEdgeInCooldown)
        {
            try
            {
                var edgeBytes = await _primaryEdgeSynthesizer(cleanedText, resolvedVoice, clampedRate, cancellationToken);
                if (edgeBytes is { Length: > 0 })
                {
                    if (_trackCooldown && Interlocked.Read(ref _edgeCooldownUntilUtcTicks) > 0)
                    {
                        Interlocked.Exchange(ref _edgeCooldownUntilUtcTicks, 0);
                    }

                    StoreInCache(cacheKey, edgeBytes, "EdgeNeuralTts", usedFallback: false, contentType: "audio/mpeg");
                    return new SpeechSynthesisResult(
                        AudioBytes: edgeBytes,
                        ContentType: "audio/mpeg",
                        Provider: "EdgeNeuralTts",
                        VoiceName: resolvedVoice,
                        SpeakingRate: clampedRate,
                        UsedFallback: false,
                        FromCache: false,
                        CacheKeySha256: cacheKey);
                }

                primaryException = new InvalidOperationException("EdgeNeuralTts trả về dữ liệu âm thanh rỗng.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                primaryException = ex;
                if (_trackCooldown)
                {
                    var cooldownSeconds = int.TryParse(_configuration["Speech:EdgeCooldownSeconds"], CultureInfo.InvariantCulture, out var parsedCooldown) && parsedCooldown > 0
                        ? parsedCooldown
                        : 60;

                    Interlocked.Exchange(
                        ref _edgeCooldownUntilUtcTicks,
                        DateTimeOffset.UtcNow.AddSeconds(cooldownSeconds).UtcTicks);
                }
                _logger?.LogWarning(
                    ex,
                    "Phương án 1 (EdgeNeuralTts) gặp lỗi cho giọng '{Voice}'. Tự động chuyển sang nhánh dự phòng.",
                    resolvedVoice);
            }
        }

        var geminiApiKey = _configuration["Gemini:ApiKey"] ?? _configuration["Gemini__ApiKey"];
        var hasUsableGeminiKey = !string.IsNullOrWhiteSpace(geminiApiKey) &&
            !geminiApiKey.StartsWith("AQ.", StringComparison.OrdinalIgnoreCase);
        var preferGeminiFallback = string.Equals(
            _configuration["Speech:PreferGeminiTtsFallback"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        // Nếu cấu hình ưu tiên Gemini 3.8 TTS làm Fallback số 1 khi Edge Neural lỗi
        if (preferGeminiFallback && hasUsableGeminiKey)
        {
            try
            {
                var (geminiBytes, geminiContentType) = await SynthesizeWithGeminiAudioFallbackAsync(
                    cleanedText,
                    resolvedVoice,
                    geminiApiKey!,
                    cancellationToken);
                if (geminiBytes is { Length: > 0 })
                {
                    var isFallback = !disablePrimary;
                    StoreInCache(cacheKey, geminiBytes, "GeminiAudioTts", usedFallback: isFallback, contentType: geminiContentType);
                    return new SpeechSynthesisResult(
                        AudioBytes: geminiBytes,
                        ContentType: geminiContentType,
                        Provider: "GeminiAudioTts",
                        VoiceName: resolvedVoice,
                        SpeakingRate: clampedRate,
                        UsedFallback: isFallback,
                        FromCache: false,
                        CacheKeySha256: cacheKey);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception geminiFirstEx)
            {
                _logger?.LogWarning(geminiFirstEx, "Nhánh Gemini 3.8 TTS ưu tiên gặp lỗi, chuyển sang nhánh tiếp theo.");
            }
        }

        // Nhánh Giọng Nam Nội Bộ Windows (Microsoft An OneCore vi-VN) khi Edge Neural bị tắt hoặc lỗi
        // Đặt trước GoogleNeuralTranslateTts vì Google Translate TTS chỉ có giọng Nữ
        if (!_useCustomPrimarySynthesizer &&
            string.Equals(resolvedVoice, DefaultMaleVoice, StringComparison.OrdinalIgnoreCase) &&
            OperatingSystem.IsWindows())
        {
            var maleWavBytes = TrySynthesizeWindowsOneCoreMaleVoice(cleanedText, clampedRate);
            if (maleWavBytes is { Length: > 0 })
            {
                var isFallback = primaryException is not null;
                StoreInCache(cacheKey, maleWavBytes, "WindowsOneCoreMaleTts", usedFallback: isFallback, contentType: "audio/wav");
                return new SpeechSynthesisResult(
                    AudioBytes: maleWavBytes,
                    ContentType: "audio/wav",
                    Provider: "WindowsOneCoreMaleTts",
                    VoiceName: resolvedVoice,
                    SpeakingRate: clampedRate,
                    UsedFallback: isFallback,
                    FromCache: false,
                    CacheKeySha256: cacheKey);
            }
        }

        // Bước 2: Phương án 2 (Auto-Fallback) — Google Neural HTTP TTS
        try
        {
            var fallbackBytes = await SynthesizeWithGoogleTranslateTtsAsync(cleanedText, resolvedVoice, clampedRate, cancellationToken);
            if (fallbackBytes is { Length: > 0 })
            {
                StoreInCache(cacheKey, fallbackBytes, "GoogleNeuralTranslateTts", usedFallback: true, contentType: "audio/mpeg");
                return new SpeechSynthesisResult(
                    AudioBytes: fallbackBytes,
                    ContentType: "audio/mpeg",
                    Provider: "GoogleNeuralTranslateTts",
                    VoiceName: resolvedVoice,
                    SpeakingRate: clampedRate,
                    UsedFallback: true,
                    FromCache: false,
                    CacheKeySha256: cacheKey);
            }

            throw new InvalidOperationException("GoogleNeuralTranslateTts trả về mảng byte âm thanh rỗng.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception googleEx)
        {
            _logger?.LogWarning(
                googleEx,
                "Phương án 2A (GoogleNeuralTranslateTts) gặp sự cố. Kiểm tra nhánh Gemini 3.8 TTS dự phòng.");

            if (hasUsableGeminiKey)
            {
                try
                {
                    var (geminiBytes, geminiContentType) = await SynthesizeWithGeminiAudioFallbackAsync(
                        cleanedText,
                        resolvedVoice,
                        geminiApiKey!,
                        cancellationToken);
                    if (geminiBytes is { Length: > 0 })
                    {
                        StoreInCache(cacheKey, geminiBytes, "GeminiAudioTts", usedFallback: true, contentType: geminiContentType);
                        return new SpeechSynthesisResult(
                            AudioBytes: geminiBytes,
                            ContentType: geminiContentType,
                            Provider: "GeminiAudioTts",
                            VoiceName: resolvedVoice,
                            SpeakingRate: clampedRate,
                            UsedFallback: true,
                            FromCache: false,
                            CacheKeySha256: cacheKey);
                    }
                }
                catch (Exception geminiEx)
                {
                    _logger?.LogError(geminiEx, "Nhánh Gemini 3.8 TTS dự phòng cũng thất bại.");
                }
            }

            throw new InvalidOperationException(
                $"Cả Phương án 1 (EdgeNeuralTts: {primaryException?.Message ?? "Disabled"}) và Phương án 2 (GoogleNeuralTranslateTts: {googleEx.Message}) đều thất bại khi tổng hợp giọng đọc.",
                googleEx);
        }
    }

    public static bool ContainsVietnameseDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            if ("ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵĂÂĐÊÔƠƯÁÀẢÃẠẤẦẨẪẬẮẰẲẴẶÉÈẺẼẸẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌỐỒỔỖỘỚỜỞỠỢÚÙỦŨỤỨỪỬỮỰÝỲỶỸỴ".IndexOf(ch) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public static string ResolveVoiceName(string? voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName))
        {
            return DefaultFemaleVoice;
        }

        var normalized = voiceName.Trim();
        if (normalized.Contains("Guy", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("en-male", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultEnglishMaleVoice;
        }

        if (normalized.StartsWith("en-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Jenny", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Aria", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("english", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultEnglishFemaleVoice;
        }
        if (normalized.Contains("NamMinh", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("male", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("nam", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultMaleVoice;
        }

        return DefaultFemaleVoice;
    }

    public static string ComputeCacheKeySha256(string resolvedVoice, double clampedRate, string cleanedText)
    {
        var rawKey = $"{resolvedVoice}|{clampedRate.ToString("F2", CultureInfo.InvariantCulture)}|{cleanedText}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private void StoreInCache(string cacheKey, byte[] audioBytes, string provider, bool usedFallback, string contentType = "audio/mpeg")
    {
        if (_cache.Count >= MaxCacheEntries)
        {
            var oldestKey = _cache
                .OrderBy(kvp => kvp.Value.CreatedAtUtc)
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            if (oldestKey is not null)
            {
                _cache.TryRemove(oldestKey, out _);
            }
        }

        _cache[cacheKey] = new CachedTtsEntry(
            AudioBytes: audioBytes,
            Provider: provider,
            UsedFallback: usedFallback,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ContentType: contentType);
    }

    private static async Task<byte[]> SynthesizeWithEdgeNeuralWebSocketAsync(
        string cleanedText,
        string resolvedVoice,
        double clampedRate,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(6));
        var ct = timeoutCts.Token;

        var connectionId = Guid.NewGuid().ToString("N");
        var secMsGec = GenerateSecMsGec();
        var wsUri = new Uri(
            $"wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
            $"?TrustedClientToken={EdgeTrustedClientToken}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version=1-130.0.2849.68&ConnectionId={connectionId}");

        using var webSocket = new ClientWebSocket();
        webSocket.Options.SetRequestHeader("Pragma", "no-cache");
        webSocket.Options.SetRequestHeader("Cache-Control", "no-cache");
        webSocket.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        webSocket.Options.SetRequestHeader(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");

        await webSocket.ConnectAsync(wsUri, ct);

        var timestamp = DateTimeOffset.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);
        var configMessage =
            $"X-Timestamp:{timestamp}\r\n" +
            "Content-Type:application/json; charset=utf-8\r\n" +
            "Path:speech.config\r\n\r\n" +
            "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";

        await SendWebSocketTextAsync(webSocket, configMessage, ct);

        var rateDeltaPercent = (int)Math.Round((clampedRate - 1.0) * 100.0);
        var ratePercent = rateDeltaPercent >= 0 ? $"+{rateDeltaPercent}%" : $"{rateDeltaPercent}%";
        var requestId = Guid.NewGuid().ToString("N");
        var escapedText = SecurityElement.Escape(cleanedText) ?? cleanedText;

        var isEnglishVoice = !string.IsNullOrWhiteSpace(resolvedVoice) && resolvedVoice.StartsWith("en-", StringComparison.OrdinalIgnoreCase);
        var effectiveEdgeVoice = isEnglishVoice && ContainsVietnameseDiacritics(cleanedText)
            ? (resolvedVoice.Contains("Guy", StringComparison.OrdinalIgnoreCase) ? DefaultMaleVoice : DefaultFemaleVoice)
            : resolvedVoice;
        var xmlLang = !string.IsNullOrWhiteSpace(effectiveEdgeVoice) && effectiveEdgeVoice.StartsWith("en-", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "vi-VN";

        var ssml =
            $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{xmlLang}'>" +
            $"<voice name='{effectiveEdgeVoice}'>" +
            $"<prosody pitch='+0Hz' rate='{ratePercent}' volume='+0%'>{escapedText}</prosody>" +
            $"</voice></speak>";

        var ssmlMessage =
            $"X-RequestId:{requestId}\r\n" +
            "Content-Type:application/ssml+xml\r\n" +
            $"X-Timestamp:{timestamp}Z\r\n" +
            "Path:ssml\r\n\r\n" +
            ssml;

        await SendWebSocketTextAsync(webSocket, ssmlMessage, ct);

        using var audioBuffer = new MemoryStream();
        var receiveBuffer = new byte[16 * 1024];
        var pathAudioMarker = Encoding.ASCII.GetBytes("Path:audio\r\n");

        while (webSocket.State == WebSocketState.Open)
        {
            using var frameStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                frameStream.Write(receiveBuffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            var frameBytes = frameStream.ToArray();
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var textFrame = Encoding.UTF8.GetString(frameBytes);
                if (textFrame.Contains("Path:turn.end", StringComparison.Ordinal))
                {
                    break;
                }
            }
            else if (result.MessageType == WebSocketMessageType.Binary && frameBytes.Length > 2)
            {
                var audioStartIndex = FindAudioPayloadOffset(frameBytes, pathAudioMarker);
                if (audioStartIndex >= 0 && audioStartIndex < frameBytes.Length)
                {
                    audioBuffer.Write(frameBytes, audioStartIndex, frameBytes.Length - audioStartIndex);
                }
            }
        }

        return audioBuffer.ToArray();
    }

    private static int FindAudioPayloadOffset(byte[] frameBytes, byte[] marker)
    {
        if (frameBytes.Length >= 2)
        {
            int headerLength = (frameBytes[0] << 8) | frameBytes[1];
            int candidateOffset = 2 + headerLength;
            if (candidateOffset <= frameBytes.Length)
            {
                return candidateOffset;
            }
        }

        for (int i = 0; i <= frameBytes.Length - marker.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < marker.Length; j++)
            {
                if (frameBytes[i + j] != marker[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i + marker.Length;
            }
        }

        return -1;
    }

    private static Task SendWebSocketTextAsync(ClientWebSocket webSocket, string message, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static string GenerateSecMsGec()
    {
        long unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long ticks = unixSeconds + 11644473600L;
        ticks -= ticks % 300;
        ticks *= 10_000_000L;

        var strToHash = $"{ticks}{EdgeTrustedClientToken}";
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(strToHash));
        return Convert.ToHexString(hash);
    }

    private async Task<byte[]> SynthesizeWithGoogleTranslateTtsAsync(
        string cleanedText,
        string resolvedVoice,
        double clampedRate,
        CancellationToken cancellationToken)
    {
        var tl = !string.IsNullOrWhiteSpace(resolvedVoice) && resolvedVoice.StartsWith("en-", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
        var ttsSpeed = clampedRate <= 0.85
            ? "0.5"
            : (Math.Abs(clampedRate - 1.0) <= 0.03
                ? "1.0"
                : clampedRate.ToString("0.0#", CultureInfo.InvariantCulture));

        var chunks = SplitIntoSentenceChunks(cleanedText, maxChunkLength: 180);

        using var combinedAudio = new MemoryStream();

        foreach (var chunk in chunks)
        {
            var chunkTl = ContainsVietnameseDiacritics(chunk)
                ? "vi"
                : (!string.IsNullOrWhiteSpace(resolvedVoice) && resolvedVoice.StartsWith("en-", StringComparison.OrdinalIgnoreCase) ? "en" : tl);
            var url = $"https://translate.googleapis.com/translate_tts?ie=UTF-8&tl={chunkTl}&client=tw-ob&ttsspeed={ttsSpeed}&q={Uri.EscapeDataString(chunk)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Google Neural Translate TTS failed: {response.StatusCode} - {errorBody}",
                    null,
                    response.StatusCode);
            }

            var chunkBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (chunkBytes.Length > 0)
            {
                await combinedAudio.WriteAsync(chunkBytes, cancellationToken);
            }
        }

        return combinedAudio.ToArray();
    }

    private static readonly object WindowsSapiSyncLock = new();

    /// <summary>
    /// Tổng hợp giọng Nam tiếng Việt thật 100% (Microsoft An - MSTTS_V110_viVN_An) qua Windows Speech OneCore COM
    /// với tốc độ phản hồi cực nhanh (&lt; 80ms) và nhịp đọc dứt khoát chuẩn giám khảo vấn đáp.
    /// </summary>
    private byte[] TrySynthesizeWindowsOneCoreMaleVoice(string cleanedText, double clampedRate)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var tempWavPath = Path.Combine(Path.GetTempPath(), $"oral_exam_male_{Guid.NewGuid():N}.wav");
        try
        {
            lock (WindowsSapiSyncLock)
            {
                var voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                var catType = Type.GetTypeFromProgID("SAPI.SpObjectTokenCategory");
                var streamType = Type.GetTypeFromProgID("SAPI.SpFileStream");
                if (voiceType is null || catType is null || streamType is null)
                {
                    return [];
                }

                var spVoice = Activator.CreateInstance(voiceType);
                var spCategory = Activator.CreateInstance(catType);
                var spFileStream = Activator.CreateInstance(streamType);
                if (spVoice is null || spCategory is null || spFileStream is null)
                {
                    return [];
                }

                const System.Reflection.BindingFlags invokeMethod = System.Reflection.BindingFlags.InvokeMethod;
                const System.Reflection.BindingFlags setProperty = System.Reflection.BindingFlags.SetProperty;
                const System.Reflection.BindingFlags getProperty = System.Reflection.BindingFlags.GetProperty;

                catType.InvokeMember(
                    "SetId",
                    invokeMethod,
                    null,
                    spCategory,
                    [@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech_OneCore\Voices", false]);

                var tokens = catType.InvokeMember("EnumerateTokens", invokeMethod, null, spCategory, null);
                if (tokens is null)
                {
                    return [];
                }

                var tokensType = tokens.GetType();
                var countObj = tokensType.InvokeMember("Count", getProperty, null, tokens, null);
                int count = countObj is int c ? c : Convert.ToInt32(countObj, CultureInfo.InvariantCulture);

                object? maleToken = null;
                for (int i = 0; i < count; i++)
                {
                    var token = tokensType.InvokeMember("Item", invokeMethod, null, tokens, [i]);
                    if (token is null)
                    {
                        continue;
                    }

                    var tokenId = Convert.ToString(
                        token.GetType().InvokeMember("Id", getProperty, null, token, null),
                        CultureInfo.InvariantCulture) ?? string.Empty;

                    if (tokenId.Contains("viVN_An", StringComparison.OrdinalIgnoreCase) ||
                        tokenId.Contains("vi-VN", StringComparison.OrdinalIgnoreCase))
                    {
                        maleToken = token;
                        if (tokenId.Contains("viVN_An", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                }

                if (maleToken is null)
                {
                    return [];
                }

                // Tốc độ đọc SAPI (-10..10): 0.85x -> 1, 1.0x -> 3 (nhanh dứt khoát), 1.15x -> 5
                int sapiRate = Math.Clamp((int)Math.Round(3 + (clampedRate - 1.0) * 14.0), -2, 8);

                voiceType.InvokeMember("Voice", setProperty, null, spVoice, [maleToken]);
                voiceType.InvokeMember("Rate", setProperty, null, spVoice, [sapiRate]);
                voiceType.InvokeMember("Volume", setProperty, null, spVoice, [100]);

                // SSFMCreateForWrite = 3
                streamType.InvokeMember("Open", invokeMethod, null, spFileStream, [tempWavPath, 3, false]);
                try
                {
                    voiceType.InvokeMember("AudioOutputStream", setProperty, null, spVoice, [spFileStream]);
                    voiceType.InvokeMember("Speak", invokeMethod, null, spVoice, [cleanedText, 0]);
                }
                finally
                {
                    streamType.InvokeMember("Close", invokeMethod, null, spFileStream, null);
                }
            }

            if (File.Exists(tempWavPath))
            {
                var wavBytes = File.ReadAllBytes(tempWavPath);
                if (wavBytes.Length > 44)
                {
                    return wavBytes;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Không thể tổng hợp giọng Nam qua Windows OneCore Speech (Microsoft An).");
        }
        finally
        {
            try
            {
                if (File.Exists(tempWavPath))
                {
                    File.Delete(tempWavPath);
                }
            }
            catch
            {
                // Bỏ qua lỗi xóa file tạm.
            }
        }

        return [];
    }

    private async Task<(byte[] AudioBytes, string ContentType)> SynthesizeWithGeminiAudioFallbackAsync(
        string cleanedText,
        string resolvedVoice,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var candidateUrls = new List<string>();
        var configuredUrl = _configuration["Gemini:TtsApiUrl"];
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            candidateUrls.Add(configuredUrl.Trim());
        }

        string[] defaultGeminiTtsUrls =
        [
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash-lite-tts:generateContent",
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash-tts:generateContent"
        ];

        foreach (var url in defaultGeminiTtsUrls)
        {
            if (!candidateUrls.Contains(url, StringComparer.OrdinalIgnoreCase))
            {
                candidateUrls.Add(url);
            }
        }

        var geminiVoiceName = string.Equals(resolvedVoice, DefaultMaleVoice, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(resolvedVoice, DefaultEnglishMaleVoice, StringComparison.OrdinalIgnoreCase)
            ? "Puck"
            : "Kore";

        // Chuẩn Gemini 3.8 TTS: parts[].text CHỈ chứa nguyên văn nội dung cần đọc (verbatim transcript),
        // phong cách trình bày đặt trong speech_metadata.style để tránh bị đọc thành tiếng.
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = cleanedText }
                    }
                }
            },
            speech_metadata = new
            {
                style = "clear, calm, professional bilingual Vietnamese-English academic examiner"
            },
            generationConfig = new
            {
                responseModalities = new[] { "AUDIO" },
                speechConfig = new
                {
                    voiceConfig = new
                    {
                        voice = geminiVoiceName,
                        prebuiltVoiceConfig = new
                        {
                            voiceName = geminiVoiceName
                        }
                    }
                }
            }
        };

        var serializedPayload = JsonSerializer.Serialize(payload);
        HttpRequestException? lastException = null;

        foreach (var apiUrl in candidateUrls)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                lastException = new HttpRequestException($"Gemini Audio TTS failed ({apiUrl}): {response.StatusCode} - {body}", null, response.StatusCode);
                continue;
            }

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];
                if (first.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array &&
                    parts.GetArrayLength() > 0)
                {
                    var part = parts[0];
                    if (part.TryGetProperty("inlineData", out var inlineData) &&
                        inlineData.TryGetProperty("data", out var dataProp) &&
                        dataProp.ValueKind == JsonValueKind.String)
                    {
                        var base64 = dataProp.GetString();
                        if (!string.IsNullOrWhiteSpace(base64))
                        {
                            var rawAudioBytes = Convert.FromBase64String(base64);
                            var inlineMimeType = inlineData.TryGetProperty("mimeType", out var mimeProp) &&
                                                 mimeProp.ValueKind == JsonValueKind.String
                                ? mimeProp.GetString()
                                : null;

                            return NormalizeGeminiAudioOutput(rawAudioBytes, inlineMimeType);
                        }
                    }
                }
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }

        return ([], "audio/wav");
    }

    public static (byte[] AudioBytes, string ContentType) NormalizeGeminiAudioOutput(byte[] rawAudioBytes, string? inlineMimeType)
    {
        if (rawAudioBytes.Length == 0)
        {
            return ([], "audio/wav");
        }

        // Nếu đã có RIFF WAV header chuẩn ("RIFF".... "WAVE")
        if (rawAudioBytes.Length >= 12 &&
            rawAudioBytes[0] == (byte)'R' &&
            rawAudioBytes[1] == (byte)'I' &&
            rawAudioBytes[2] == (byte)'F' &&
            rawAudioBytes[3] == (byte)'F')
        {
            return (rawAudioBytes, "audio/wav");
        }

        // Nếu Gemini trả về audio/L16 hoặc audio/pcm (raw 24kHz 16-bit mono PCM), đóng gói vào 44-byte RIFF WAV header
        if (!string.IsNullOrWhiteSpace(inlineMimeType) &&
            (inlineMimeType.Contains("l16", StringComparison.OrdinalIgnoreCase) ||
             inlineMimeType.Contains("pcm", StringComparison.OrdinalIgnoreCase)))
        {
            return (WrapPcm16InWavHeader(rawAudioBytes, sampleRate: 24000, channels: 1, bitsPerSample: 16), "audio/wav");
        }

        // Nếu là MP3 ("ID3" hoặc MPEG sync frame 0xFF 0xFB / 0xFF 0xF3)
        if (!string.IsNullOrWhiteSpace(inlineMimeType) &&
            (inlineMimeType.Contains("mpeg", StringComparison.OrdinalIgnoreCase) ||
             inlineMimeType.Contains("mp3", StringComparison.OrdinalIgnoreCase)))
        {
            return (rawAudioBytes, "audio/mpeg");
        }

        if (rawAudioBytes.Length >= 3 &&
            ((rawAudioBytes[0] == (byte)'I' && rawAudioBytes[1] == (byte)'D' && rawAudioBytes[2] == (byte)'3') ||
             (rawAudioBytes[0] == 0xFF && (rawAudioBytes[1] & 0xE0) == 0xE0)))
        {
            return (rawAudioBytes, "audio/mpeg");
        }

        // Mặc định Gemini 3.8 TTS trả về PCM/WAV 24kHz 16-bit mono -> bọc RIFF WAV header để trình duyệt phát trực tiếp
        return (WrapPcm16InWavHeader(rawAudioBytes, sampleRate: 24000, channels: 1, bitsPerSample: 16), "audio/wav");
    }

    public static byte[] WrapPcm16InWavHeader(byte[] pcmBytes, int sampleRate = 24000, short channels = 1, short bitsPerSample = 16)
    {
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var dataSize = pcmBytes.Length;
        var riffChunkSize = 36 + dataSize;

        var wavBytes = new byte[44 + dataSize];
        using var ms = new MemoryStream(wavBytes);
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(riffChunkSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Subchunk1Size for PCM
        writer.Write((short)1); // AudioFormat = 1 (PCM)
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        writer.Flush();

        Buffer.BlockCopy(pcmBytes, 0, wavBytes, 44, dataSize);
        return wavBytes;
    }

    public static IReadOnlyList<string> SplitIntoSentenceChunks(string text, int maxChunkLength = 180)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = text.Trim();
        if (normalized.Length <= maxChunkLength)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        int currentIndex = 0;

        while (currentIndex < normalized.Length)
        {
            int remaining = normalized.Length - currentIndex;
            if (remaining <= maxChunkLength)
            {
                var tail = normalized.Substring(currentIndex).Trim();
                if (tail.Length > 0)
                {
                    chunks.Add(tail);
                }
                break;
            }

            int searchEnd = currentIndex + maxChunkLength;
            int splitPos = -1;

            char[] punctuationSeparators = ['.', ';', '?', '!', ',', ' '];
            foreach (var sep in punctuationSeparators)
            {
                int found = normalized.LastIndexOf(sep, searchEnd - 1, maxChunkLength);
                if (found > currentIndex)
                {
                    splitPos = found + 1;
                    break;
                }
            }

            if (splitPos <= currentIndex)
            {
                splitPos = searchEnd;
            }

            var piece = normalized.Substring(currentIndex, splitPos - currentIndex).Trim();
            if (piece.Length > 0)
            {
                chunks.Add(piece);
            }

            currentIndex = splitPos;
        }

        return chunks;
    }

    private sealed record CachedTtsEntry(
        byte[] AudioBytes,
        string Provider,
        bool UsedFallback,
        DateTimeOffset CreatedAtUtc,
        string ContentType = "audio/mpeg"
    );
}
