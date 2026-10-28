using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OralExamination.Application.Common.Models;

/// <summary>
/// Kết quả bóc băng giọng nói (Speech-to-Text) chi tiết, bao gồm thông tin nhà cung cấp (Option 1 hoặc Option 2 Fallback),
/// văn bản đã chuẩn hóa thuật ngữ SE (Code-Switching) và chuỗi JSON mốc thời gian từ ngữ (nếu có).
/// </summary>
public sealed record SpeechTranscriptionResult(
    string Text,
    string RawText,
    string Provider,
    bool UsedFallback,
    string? TimestampsJson = null,
    double? ConfidenceScore = null
);

/// <summary>
/// Kết quả tổng hợp giọng đọc AI (Text-to-Speech) từ Backend.
/// Hỗ trợ Option 1 (Edge Neural TTS) và tự động chuyển đổi sang Option 2 (Gemini / Google Neural TTS) khi gặp lỗi.
/// </summary>
public sealed record SpeechSynthesisResult(
    byte[] AudioBytes,
    string ContentType,
    string Provider,
    string VoiceName,
    double SpeakingRate,
    bool UsedFallback,
    bool FromCache,
    string CacheKeySha256
);

/// <summary>
/// Cấu hình giọng đọc AI giám khảo hỗ trợ trên hệ thống.
/// </summary>
public sealed record TtsVoiceOptionDto(
    string VoiceId,
    string DisplayName,
    string Gender,
    string Locale,
    bool IsDefault
);

/// <summary>
/// Tiện ích hỗ trợ chuẩn hóa transcript, lọc bỏ ảo giác tiếng ồn Whisper / Gemini và ngăn chặn phục hồi ảo giác.
/// </summary>
public static class TranscriptHelper
{
    private static readonly System.Text.RegularExpressions.Regex NoiseRegex = new(
        @"^(?:thank\s+you(?:\s+very\s+much|\s+so\s+much|\s+for\s+watching|\s+for\s+listening)?|thanks(?:\s+for\s+watching|\s+for\s+listening)?|subtitles?\s+(?:by|created\s+by).*|captions?\s+by.*|amara\.org.*|please\s+subscribe.*|like\s+and\s+subscribe.*|don't\s+forget\s+to\s+subscribe.*|(?:(?:các\s+bạn|mọi\s+người|quý\s+vị(?:\s+và\s+các\s+bạn)?)\s*,?\s*)?(?:hãy\s+|nhớ\s+|đừng\s+quên\s+|nhấn\s+|bấm\s+|vui\s+lòng\s+)?(?:subscribe|đăng\s+k[ýíy]|theo\s+dõi|like\s+và\s+(?:subscribe|đăng\s+k[ýíy]))(?:\s+cho)?(?:\s+kênh)?.*|bấm\s+chuông.*|ghiền\s*mì\s*gõ.*|la\s*la\s*school.*|fap\s*tv.*|kem\s*xôi\s*tv.*|trắng\s*tv.*|để\s+không\s+bỏ\s+lỡ\s+những\s+video.*|(?:cảm\s+ơn|cám\s+ơn)\s+(?:các\s+bạn|mọi\s+người|quý\s+vị(?:\s+và\s+các\s+bạn)?)\s+đã\s+(?:theo\s+dõi|xem|lắng\s+nghe|đón\s+xem).*|hẹn\s+gặp\s+lại\s+(?:các\s+bạn|mọi\s+người|quý\s+vị).*|(?:phụ\s+đề|biên\s+tập\s+phụ\s+đề|dịch\s+phụ\s+đề)\s*(?:bởi|được\s+thực\s+hiện)?.*|dạ\s+thưa\s+thầy\s+cô\s*,?\s*em\s+xin\s+trả\s+lời\s+câu\s+hỏi\s+thi\s+vấn\s+đáp(?:\s+bằng\s+tiếng\s+việt\s+kết\s+hợp\s+thuật\s+ngữ\s+tiếng\s+anh\s+tại\s+đại\s+học\s+fpt)?\s*[:.,]?|you(?:\s+you)*|bye(?:\s+bye)*|mbc\s+news.*)$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));

    private static readonly System.Text.RegularExpressions.Regex RepeatedPhraseRegex = new(
        @"\b(\w+(?:\s+\w+){1,16})(?:[\s,.!?]+\1){1,}\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));

    private static readonly System.Text.RegularExpressions.Regex RepeatedWordLoopRegex = new(
        @"\b(\w+)(?:[\s,.!?]+\1){3,}\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));

    private static readonly System.Text.RegularExpressions.Regex SentenceSplitRegex = new(
        @"(?<=[.!?。])\s+|[\r\n]+|(?<=[\p{Ll}][.!?。])(?=[\p{Lu}])",
        System.Text.RegularExpressions.RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    private static readonly System.Text.RegularExpressions.Regex PunctuationTrimRegex = new(
        @"^[\s\p{P}]+|[\s\p{P}]+$",
        System.Text.RegularExpressions.RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    private static readonly char[] WordSplitSeparators =
    [
        ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '/', '\\', '-', '_', '“', '”', '‘', '’', '…'
    ];

    private static string CollapseRepetitions(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var collapsed = text;
        try
        {
            collapsed = RepeatedWordLoopRegex.Replace(collapsed, "$1");
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            // Giữ nguyên khi chuỗi quá dài vượt ngưỡng timeout của regex
        }

        try
        {
            if (collapsed.Length <= 2_500)
            {
                collapsed = RepeatedPhraseRegex.Replace(collapsed, "$1");
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            // Giữ nguyên khi chuỗi quá dài vượt ngưỡng timeout của regex
        }

        return collapsed;
    }

    private static bool SafeIsNoiseMatch(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 600)
        {
            return false;
        }

        try
        {
            return NoiseRegex.IsMatch(input);
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public static bool IsNoiseHallucination(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(?:(?:các\s+bạn|mọi\s+người|quý\s+vị(?:\s+và\s+các\s+bạn)?)\s*,?\s*)?(?:hãy\s+|nhớ\s+|đừng\s+quên\s+|nhấn\s+|bấm\s+|vui\s+lòng\s+)(?:subscribe|đăng\s+k[ýíy])(?:\s+cho)?(?:\s+kênh)?(?:\s+(?:ghiền\s*mì\s*gõ|la\s*la\s*school|fap\s*tv|kem\s*xôi\s*tv|trắng\s*tv))?(?:\s+và\s+bấm\s+chuông(?:\s+theo\s+dõi)?)?(?:\s+để\s+không\s+bỏ\s+lỡ\s+những\s+video(?:\s+hấp\s+dẫn|\s+mới\s+nhất)?)?|để\s+không\s+bỏ\s+lỡ\s+những\s+video(?:\s+hấp\s+dẫn|\s+mới\s+nhất)?|kênh\s+(?:ghiền\s*mì\s*gõ|la\s*la\s*school|fap\s*tv|kem\s*xôi\s*tv|trắng\s*tv)|ghiền\s*mì\s*gõ|la\s*la\s*school|fap\s*tv|kem\s*xôi\s*tv|trắng\s*tv|subtitles\s+by\s+the\s+amara\.org\s+community|amara\.org|dạ\s+thưa\s+thầy\s+cô\s*,?\s*em\s+xin\s+trả\s+lời\s+câu\s+hỏi\s+thi\s+vấn\s+đáp\s+bằng\s+tiếng\s+việt\s+kết\s+hợp\s+thuật\s+ngữ\s+tiếng\s+anh\s+tại\s+đại\s+học\s+fpt\s*:?",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim();
        // 1. Kiểm tra nhanh khi cả văn bản sau khi khử lặp trùng khớp với mẫu ảo giác tiếng ồn
        var collapsedAll = CollapseRepetitions(trimmed);
        var coreAll = PunctuationTrimRegex.Replace(collapsedAll, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(coreAll) || SafeIsNoiseMatch(coreAll))
        {
            return true;
        }

        // 2. Tách thành các câu đơn để kiểm tra từng câu
        string[] sentences;
        try
        {
            sentences = SentenceSplitRegex.Split(trimmed);
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            sentences = [trimmed];
        }

        var hasValidSentence = false;
        foreach (var s in sentences)
        {
            var collapsedSentence = CollapseRepetitions(s);
            var core = PunctuationTrimRegex.Replace(collapsedSentence, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(core))
            {
                continue;
            }

            if (!SafeIsNoiseMatch(core))
            {
                hasValidSentence = true;
                break;
            }
        }

        return !hasValidSentence;
    }

    /// <summary>
    /// Kiểm tra xem bản bóc băng của thí sinh có phải chỉ đọc nhại lại nguyên văn câu hỏi đề bài (Question Echo)
    /// mà không đưa ra câu trả lời thực chất hay không.
    /// </summary>
    public static bool LooksLikeQuestionEcho(string? transcript, string? questionText)
    {
        if (string.IsNullOrWhiteSpace(transcript) || string.IsNullOrWhiteSpace(questionText))
        {
            return false;
        }

        var questionWords = questionText
            .ToLowerInvariant()
            .Split(WordSplitSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (questionWords.Length < 6)
        {
            return false;
        }

        var transcriptWords = transcript
            .ToLowerInvariant()
            .Split(WordSplitSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (transcriptWords.Length == 0)
        {
            return false;
        }

        var minWords = (int)Math.Floor(questionWords.Length * 0.60);
        var maxWords = (int)Math.Ceiling(questionWords.Length * 1.35);
        if (transcriptWords.Length < minWords || transcriptWords.Length > maxWords)
        {
            return false;
        }

        var questionWordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in questionWords)
        {
            questionWordCounts[w] = questionWordCounts.TryGetValue(w, out var c) ? c + 1 : 1;
        }

        int matchedWords = 0;
        foreach (var tw in transcriptWords)
        {
            if (questionWordCounts.TryGetValue(tw, out var remaining) && remaining > 0)
            {
                matchedWords++;
                questionWordCounts[tw] = remaining - 1;
            }
        }

        double overlapRatio = (double)matchedWords / transcriptWords.Length;
        return overlapRatio >= 0.85;
    }

    public static string NormalizeOrFallback(string? rawText, string? normalized)
    {
        if (normalized != null)
        {
            if (string.IsNullOrWhiteSpace(normalized) || IsNoiseHallucination(normalized))
            {
                return string.Empty;
            }

            return normalized.Trim();
        }

        if (string.IsNullOrWhiteSpace(rawText) || IsNoiseHallucination(rawText))
        {
            return string.Empty;
        }

        return rawText.Trim();
    }

    public static bool IsServerConfirmedSilence(SpeechTranscriptionResult? result, Exception? sttException)
    {
        if (sttException is not null || result is null)
        {
            return false;
        }

        var isRealCloudSttProvider =
            string.Equals(result.Provider, "CloudflareWhisper", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result.Provider, "GeminiMultimodalStt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result.Provider, "GeminiDedicatedTranscribe", StringComparison.OrdinalIgnoreCase);

        return isRealCloudSttProvider && string.IsNullOrWhiteSpace(result.Text);
    }

    private static readonly ConcurrentDictionary<string, DateTime> VerifiedServerTranscripts = new(StringComparer.Ordinal);
    private static readonly TimeSpan VerifiedTranscriptTtl = TimeSpan.FromMinutes(15);
    private static readonly System.Text.RegularExpressions.Regex WhitespaceNormalizeRegex = new(
        @"\s+",
        System.Text.RegularExpressions.RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static string NormalizeWhitespace(string text)
        => WhitespaceNormalizeRegex.Replace(text.Trim(), " ");

    public static void RegisterVerifiedTranscript(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        var normalized = NormalizeWhitespace(transcript);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (VerifiedServerTranscripts.Count > 500)
        {
            foreach (var kvp in VerifiedServerTranscripts)
            {
                if (now - kvp.Value > VerifiedTranscriptTtl)
                {
                    VerifiedServerTranscripts.TryRemove(kvp.Key, out _);
                }
            }
        }

        VerifiedServerTranscripts[normalized] = now;
    }

    public static bool IsVerifiedServerTranscript(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return false;
        }

        var normalized = NormalizeWhitespace(transcript);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (VerifiedServerTranscripts.TryGetValue(normalized, out var registeredAt))
        {
            if (now - registeredAt <= VerifiedTranscriptTtl)
            {
                return true;
            }

            VerifiedServerTranscripts.TryRemove(normalized, out _);
        }

        var activeSegments = VerifiedServerTranscripts
            .Where(kvp => now - kvp.Value <= VerifiedTranscriptTtl &&
                          kvp.Key.Length >= 4 &&
                          normalized.Contains(kvp.Key, StringComparison.Ordinal))
            .Select(kvp => kvp.Key)
            .OrderByDescending(k => k.Length)
            .ToList();

        if (activeSegments.Count == 0)
        {
            return false;
        }

        var covered = new bool[normalized.Length];
        foreach (var seg in activeSegments)
        {
            var searchIndex = 0;
            while (searchIndex <= normalized.Length - seg.Length)
            {
                var foundIndex = normalized.IndexOf(seg, searchIndex, StringComparison.Ordinal);
                if (foundIndex < 0)
                {
                    break;
                }

                for (int i = foundIndex; i < foundIndex + seg.Length; i++)
                {
                    covered[i] = true;
                }

                searchIndex = foundIndex + seg.Length;
            }
        }

        var coveredCount = covered.Count(c => c);
        return coveredCount >= (int)Math.Ceiling(normalized.Length * 0.8);
    }

    public static bool HasRealAudioContainerHeader(ReadOnlySpan<byte> headerBytes)
    {
        if (headerBytes.Length < 256)
        {
            return false;
        }

        // 1. WebM / Matroska EBML header: 1A 45 DF A3
        if (headerBytes[0] == 0x1A && headerBytes[1] == 0x45 && headerBytes[2] == 0xDF && headerBytes[3] == 0xA3)
        {
            return true;
        }

        // 2. RIFF / WAV header: "RIFF" (52 49 46 46)
        if (headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46)
        {
            return true;
        }

        // 3. Ogg container header: "OggS" (4F 67 67 53)
        if (headerBytes[0] == 0x4F && headerBytes[1] == 0x67 && headerBytes[2] == 0x67 && headerBytes[3] == 0x53)
        {
            return true;
        }

        // 4. MP4 / M4A ISO Base Media File Format: "ftyp" at offset 4..8
        if (headerBytes[4] == (byte)'f' && headerBytes[5] == (byte)'t' && headerBytes[6] == (byte)'y' && headerBytes[7] == (byte)'p')
        {
            return true;
        }

        return false;
    }
}
