using System;
using System.Collections.Generic;

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
        @"^(?:thank\s+you(?:\s+very\s+much|\s+so\s+much|\s+for\s+watching)?|thanks(?:\s+for\s+watching)?|subtitles\s+by.*|amara\.org.*|please\s+subscribe.*|like\s+and\s+subscribe.*|hãy\s+đăng\s+ký\s+kênh.*|cảm\s+ơn\s+các\s+bạn\s+đã\s+theo\s+dõi.*|hẹn\s+gặp\s+lại\s+các\s+bạn.*|phụ\s+đề\s+bởi.*|you|bye(?:\s+bye)?|mbc\s+news.*)$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));

    private static readonly System.Text.RegularExpressions.Regex RepeatedPhraseRegex = new(
        @"\b(\w+(?:\s+\w+){1,6})(?:[\s,.!?]+\1){1,}\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));

    private static readonly System.Text.RegularExpressions.Regex RepeatedWordLoopRegex = new(
        @"\b(\w+)(?:[\s,.!?]+\1){2,}\b",
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

    private static string CollapseRepetitions(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var collapsed = RepeatedWordLoopRegex.Replace(text, "$1");
        return RepeatedPhraseRegex.Replace(collapsed, "$1");
    }

    public static bool IsNoiseHallucination(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        // 1. Kiểm tra nhanh khi cả văn bản sau khi khử lặp trùng khớp với mẫu ảo giác tiếng ồn
        var collapsedAll = CollapseRepetitions(trimmed);
        var coreAll = PunctuationTrimRegex.Replace(collapsedAll, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(coreAll) || NoiseRegex.IsMatch(coreAll))
        {
            return true;
        }

        // 2. Tách thành các câu đơn để kiểm tra từng câu
        var sentences = SentenceSplitRegex.Split(trimmed);
        var hasValidSentence = false;
        foreach (var s in sentences)
        {
            var collapsedSentence = CollapseRepetitions(s);
            var core = PunctuationTrimRegex.Replace(collapsedSentence, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(core))
            {
                continue;
            }

            if (!NoiseRegex.IsMatch(core))
            {
                hasValidSentence = true;
                break;
            }
        }

        return !hasValidSentence;
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
}
