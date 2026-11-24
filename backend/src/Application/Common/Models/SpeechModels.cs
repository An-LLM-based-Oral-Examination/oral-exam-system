using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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

    public const int MaxVerifiedTranscriptEntries = 2000;
    public const int MaxVerifiedTranscriptLength = 16_384;
    private const int MaxVerifiedSentenceSegments = 50;
    private const int MinVerifiedSegmentLength = 20;

    private sealed class VerifiedTranscriptEntry
    {
        public VerifiedTranscriptEntry(string hashKey, DateTime expiresAtUtc)
        {
            HashKey = hashKey;
            ExpiresAtUtc = expiresAtUtc;
        }

        public string HashKey { get; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    private static readonly object VerifiedTranscriptLock = new();
    private static readonly Dictionary<string, LinkedListNode<VerifiedTranscriptEntry>> VerifiedServerTranscripts =
        new(StringComparer.Ordinal);
    private static readonly LinkedList<VerifiedTranscriptEntry> VerifiedTranscriptLru = new();
    private static readonly TimeSpan VerifiedTranscriptTtl = TimeSpan.FromMinutes(15);
    private static readonly Regex WhitespaceNormalizeRegex = new(
        @"\s+",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    public static int VerifiedTranscriptCacheCount
    {
        get
        {
            lock (VerifiedTranscriptLock)
            {
                return VerifiedServerTranscripts.Count;
            }
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        try
        {
            return WhitespaceNormalizeRegex.Replace(text.Trim(), " ");
        }
        catch (RegexMatchTimeoutException)
        {
            return text.Trim();
        }
    }

    private static string ComputeScopedTranscriptHash(Guid? userId, string normalizedTranscript)
    {
        var scopeKey = userId is Guid uid && uid != Guid.Empty
            ? uid.ToString("N")
            : "anon";
        var payload = $"{scopeKey}\n{normalizedTranscript}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes);
    }

    private static List<string> SplitBoundedSentences(string normalized)
    {
        string[] rawParts;
        try
        {
            rawParts = SentenceSplitRegex.Split(normalized);
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }

        var result = new List<string>(Math.Min(rawParts.Length, MaxVerifiedSentenceSegments + 1));
        foreach (var part in rawParts)
        {
            var cleaned = NormalizeWhitespace(part);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            result.Add(cleaned);
            if (result.Count > MaxVerifiedSentenceSegments)
            {
                return [];
            }
        }

        return result;
    }

    private static void UpsertVerifiedHashLocked(string hashKey, DateTime now)
    {
        var expiresAt = now.Add(VerifiedTranscriptTtl);

        if (VerifiedServerTranscripts.TryGetValue(hashKey, out var existingNode))
        {
            existingNode.Value.ExpiresAtUtc = expiresAt;
            VerifiedTranscriptLru.Remove(existingNode);
            VerifiedTranscriptLru.AddFirst(existingNode);
            return;
        }

        while (VerifiedTranscriptLru.Last is { } tail && tail.Value.ExpiresAtUtc <= now)
        {
            VerifiedTranscriptLru.RemoveLast();
            VerifiedServerTranscripts.Remove(tail.Value.HashKey);
        }

        while (VerifiedServerTranscripts.Count >= MaxVerifiedTranscriptEntries &&
               VerifiedTranscriptLru.Last is { } oldest)
        {
            VerifiedTranscriptLru.RemoveLast();
            VerifiedServerTranscripts.Remove(oldest.Value.HashKey);
        }

        var node = new LinkedListNode<VerifiedTranscriptEntry>(new VerifiedTranscriptEntry(hashKey, expiresAt));
        VerifiedTranscriptLru.AddFirst(node);
        VerifiedServerTranscripts[hashKey] = node;
    }

    private static bool TryMatchVerifiedHashLocked(string hashKey, DateTime now)
    {
        if (!VerifiedServerTranscripts.TryGetValue(hashKey, out var node))
        {
            return false;
        }

        if (node.Value.ExpiresAtUtc <= now)
        {
            VerifiedTranscriptLru.Remove(node);
            VerifiedServerTranscripts.Remove(hashKey);
            return false;
        }

        VerifiedTranscriptLru.Remove(node);
        VerifiedTranscriptLru.AddFirst(node);
        return true;
    }

    public static void RegisterVerifiedTranscript(string? transcript, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(transcript) || transcript.Length > MaxVerifiedTranscriptLength)
        {
            return;
        }

        var normalized = NormalizeWhitespace(transcript);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaxVerifiedTranscriptLength)
        {
            return;
        }

        var fullHash = ComputeScopedTranscriptHash(userId, normalized);
        List<string>? sentenceHashes = null;

        var sentences = SplitBoundedSentences(normalized);
        if (sentences.Count is >= 2 and <= MaxVerifiedSentenceSegments)
        {
            sentenceHashes = new List<string>(sentences.Count);
            foreach (var sentence in sentences)
            {
                if (sentence.Length >= MinVerifiedSegmentLength)
                {
                    sentenceHashes.Add(ComputeScopedTranscriptHash(userId, sentence));
                }
            }
        }

        var now = DateTime.UtcNow;
        lock (VerifiedTranscriptLock)
        {
            if (sentenceHashes is not null)
            {
                foreach (var segHash in sentenceHashes)
                {
                    UpsertVerifiedHashLocked(segHash, now);
                }
            }

            UpsertVerifiedHashLocked(fullHash, now);
        }
    }

    public static bool IsVerifiedServerTranscript(string? transcript, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(transcript) || transcript.Length > MaxVerifiedTranscriptLength)
        {
            return false;
        }

        var normalized = NormalizeWhitespace(transcript);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaxVerifiedTranscriptLength)
        {
            return false;
        }

        var fullHash = ComputeScopedTranscriptHash(userId, normalized);
        var now = DateTime.UtcNow;

        lock (VerifiedTranscriptLock)
        {
            if (TryMatchVerifiedHashLocked(fullHash, now))
            {
                return true;
            }
        }

        var sentences = SplitBoundedSentences(normalized);
        if (sentences.Count is < 2 or > MaxVerifiedSentenceSegments)
        {
            return false;
        }

        var sentenceChecks = new List<(int Length, string Hash)>(sentences.Count);
        var totalChars = 0;
        foreach (var sentence in sentences)
        {
            totalChars += sentence.Length;
            if (sentence.Length >= MinVerifiedSegmentLength)
            {
                sentenceChecks.Add((sentence.Length, ComputeScopedTranscriptHash(userId, sentence)));
            }
        }

        if (totalChars == 0 || sentenceChecks.Count == 0)
        {
            return false;
        }

        var coveredChars = 0;
        lock (VerifiedTranscriptLock)
        {
            foreach (var (length, hash) in sentenceChecks)
            {
                if (TryMatchVerifiedHashLocked(hash, now))
                {
                    coveredChars += length;
                }
            }
        }

        return coveredChars >= (int)Math.Ceiling(totalChars * 0.8);
    }

    public static bool HasRealAudioContainerHeader(ReadOnlySpan<byte> headerBytes)
        => AudioHeaderValidator.HasValidAudioMagicBytes(headerBytes);
}

/// <summary>
/// Chuẩn hóa và lọc chặt tham số vocabularyHint từ client nhằm chặn LLM Prompt Injection trước khi đưa vào STT.
/// </summary>
public static class SpeechVocabularyHintSanitizer
{
    public const int MaxLength = 80;
    private const int MaxRawInspectLength = 512;

    private static readonly Regex SafeHintAllowlistRegex = new(
        @"^[A-Za-z0-9 ,._:/-]{1,80}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex InjectionKeywordRegex = new(
        @"(?<![A-Za-z])(?:ignore|previous|instructions?|system|prompt|assistant|return|output|translate|answer|explain|write)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Kiểm tra nhanh chuỗi có chứa từ khóa tiêm lệnh (prompt injection) đáng ngờ hay không.
    /// Cắt bỏ ký tự điều khiển trước khi so khớp để chặn bypass dạng "ig\0nore previous".
    /// </summary>
    public static bool ContainsInjectionKeywords(string? rawHint)
    {
        if (string.IsNullOrWhiteSpace(rawHint))
        {
            return false;
        }

        if (rawHint.Length > MaxRawInspectLength)
        {
            return true;
        }

        var stripped = StripControlCharacters(rawHint, MaxRawInspectLength);
        if (stripped.Length == 0)
        {
            return false;
        }

        try
        {
            return InjectionKeywordRegex.IsMatch(stripped);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    /// <summary>
    /// Chuẩn hóa ngữ cảnh kỹ thuật an toàn cho trích xuất từ vựng ASR (BuildCustomVocabularyList / NormalizeWithContext).
    /// Tuyệt đối không dùng chuỗi này cho prompt ngôn ngữ tự nhiên; từ chối mọi payload dài hoặc chứa mẫu tiêm lệnh.
    /// </summary>
    public static string? SanitizeTechnicalContext(string? rawHint)
    {
        if (string.IsNullOrWhiteSpace(rawHint) || rawHint.Length > MaxRawInspectLength || ContainsInjectionKeywords(rawHint))
        {
            return null;
        }

        var stripped = StripControlCharacters(rawHint, 200);
        return stripped.Length == 0 ? null : stripped;
    }

    private static string StripControlCharacters(string input, int maxLength)
    {
        var builder = new StringBuilder(Math.Min(input.Length, maxLength));
        var previousWasSpace = false;

        foreach (var ch in input)
        {
            if (ch is '\r' or '\n' or '\t' or ' ')
            {
                if (builder.Length > 0 && !previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            if (char.IsControl(ch))
            {
                continue;
            }

            if (builder.Length >= maxLength)
            {
                break;
            }

            builder.Append(ch);
            previousWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Chuẩn hóa vocabularyHint: cắt bỏ ký tự điều khiển/xuống dòng, giới hạn tối đa 80 ký tự,
    /// chỉ chấp nhận tập ký tự an toàn và từ chối (trả về null) mọi mẫu chứa từ khóa tiêm lệnh hoặc ký tự lạ.
    /// </summary>
    public static string? Sanitize(string? rawHint)
    {
        if (string.IsNullOrWhiteSpace(rawHint) || rawHint.Length > MaxRawInspectLength)
        {
            return null;
        }

        var builder = new StringBuilder(Math.Min(rawHint.Length, MaxLength + 1));
        var previousWasSpace = false;

        foreach (var ch in rawHint)
        {
            if (ch is '\r' or '\n' or '\t' or ' ')
            {
                if (builder.Length > 0 && !previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            if (char.IsControl(ch))
            {
                continue;
            }

            builder.Append(ch);
            previousWasSpace = false;

            if (builder.Length > MaxLength + 1)
            {
                return null;
            }
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length == 0 || cleaned.Length > MaxLength)
        {
            return null;
        }

        try
        {
            if (!SafeHintAllowlistRegex.IsMatch(cleaned))
            {
                return null;
            }

            if (InjectionKeywordRegex.IsMatch(cleaned))
            {
                return null;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        return cleaned;
    }
}

/// <summary>
/// Kiểm tra magic-bytes header của các định dạng file âm thanh hợp lệ trước khi cấp phát RAM hoặc gọi STT.
/// </summary>
public static class AudioHeaderValidator
{
    public const int HeaderProbeLength = 16;

    /// <summary>
    /// Hỗ trợ kiểm tra header container âm thanh thật:
    /// WebM/EBML (1A 45 DF A3), WAV (RIFF....WAVE), OGG (OggS),
    /// MP3 (ID3 hoặc MPEG frame sync FF FB / FF F3 / FF F2), MP4/M4A (ftyp), FLAC (fLaC).
    /// </summary>
    public static bool HasValidAudioMagicBytes(ReadOnlySpan<byte> headerBytes)
    {
        if (headerBytes.Length < 4)
        {
            return false;
        }

        // 1. WebM / Matroska EBML header: 1A 45 DF A3
        if (headerBytes[0] == 0x1A && headerBytes[1] == 0x45 && headerBytes[2] == 0xDF && headerBytes[3] == 0xA3)
        {
            return true;
        }

        // 2. WAV / RIFF header: "RIFF" (0..3) + "WAVE" (8..11)
        if (headerBytes.Length >= 12 &&
            headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46 &&
            headerBytes[8] == 0x57 && headerBytes[9] == 0x41 && headerBytes[10] == 0x56 && headerBytes[11] == 0x45)
        {
            return true;
        }

        // 3. Ogg container header: "OggS" (4F 67 67 53)
        if (headerBytes[0] == 0x4F && headerBytes[1] == 0x67 && headerBytes[2] == 0x67 && headerBytes[3] == 0x53)
        {
            return true;
        }

        // 4. MP3: ID3 tag ("ID3" = 49 44 33) hoặc MPEG frame sync (FF FB / FF F3 / FF F2)
        if ((headerBytes[0] == 0x49 && headerBytes[1] == 0x44 && headerBytes[2] == 0x33) ||
            (headerBytes[0] == 0xFF && (headerBytes[1] == 0xFB || headerBytes[1] == 0xF3 || headerBytes[1] == 0xF2)))
        {
            return true;
        }

        // 5. MP4 / M4A ISO Base Media File Format: boxSize (0..3, big-endian 16..1024) + "ftyp" (4..7) + major_brand âm thanh/media hợp lệ (8..11)
        if (headerBytes.Length >= 12 &&
            headerBytes[4] == (byte)'f' && headerBytes[5] == (byte)'t' && headerBytes[6] == (byte)'y' && headerBytes[7] == (byte)'p')
        {
            var boxSize = ((uint)headerBytes[0] << 24)
                | ((uint)headerBytes[1] << 16)
                | ((uint)headerBytes[2] << 8)
                | headerBytes[3];
            if (boxSize >= 16 && boxSize <= 1024 && IsAllowedMp4MajorBrand(headerBytes.Slice(8, 4)))
            {
                return true;
            }
        }

        // 6. FLAC header: "fLaC" (66 4C 61 43)
        if (headerBytes[0] == 0x66 && headerBytes[1] == 0x4C && headerBytes[2] == 0x61 && headerBytes[3] == 0x43)
        {
            return true;
        }

        return false;
    }

    private static bool IsAllowedMp4MajorBrand(ReadOnlySpan<byte> brand)
    {
        return brand.SequenceEqual("M4A "u8)
            || brand.SequenceEqual("M4B "u8)
            || brand.SequenceEqual("M4P "u8)
            || brand.SequenceEqual("mp41"u8)
            || brand.SequenceEqual("mp42"u8)
            || brand.SequenceEqual("isom"u8)
            || brand.SequenceEqual("iso2"u8)
            || brand.SequenceEqual("iso5"u8)
            || brand.SequenceEqual("iso6"u8)
            || brand.SequenceEqual("dash"u8)
            || brand.SequenceEqual("qt  "u8)
            || brand.SequenceEqual("3gp4"u8)
            || brand.SequenceEqual("3gp5"u8)
            || brand.SequenceEqual("3gp6"u8)
            || brand.SequenceEqual("f4a "u8)
            || brand.SequenceEqual("MSNV"u8);
    }
}
