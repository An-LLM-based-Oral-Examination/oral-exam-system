using System;
using System.Text.RegularExpressions;

namespace OralExamination.Infrastructure.Services;

/// <summary>
/// Bộ chuẩn hóa thuật ngữ chuyên ngành Kỹ thuật Phần mềm (SE Code-Switching Normalizer)
/// cho đầu ra Speech-to-Text và tiền xử lý văn bản đầu vào cho Text-to-Speech.
/// </summary>
public static class SeTerminologyNormalizer
{
    /// <summary>
    /// Chuỗi gợi ý từ vựng chuyên ngành Kỹ thuật Phần mềm (SE Vocabulary Hint) cho Whisper / Gemini STT.
    /// </summary>
    public const string DefaultSeWhisperPrompt =
        "Kỳ thi vấn đáp Kỹ thuật Phần mềm FPT University: Clean Architecture, Dependency Inversion, SOLID, Single Responsibility, Open-Closed, Liskov Substitution, Interface Segregation, Dependency Injection, CQRS, MediatR, FluentValidation, Entity Framework Core, DbContext, AsNoTracking, BoundedChannel, BackgroundService, IHostedService, Polly Retry, Circuit Breaker, Dead-Letter Queue, Microservices, API Gateway, Saga Pattern, CAP Theorem, Use Case, User Story, SRS, Non-Functional Requirements, SHA-256, Cloudflare R2, JWT, OAuth 2.0, PKCE, SignalR, WebSocket.";

    /// <summary>
    /// Danh sách từ vựng chuyên ngành Kỹ thuật Phần mềm chuẩn hóa dùng cho customVocabulary của gemini-3.5-transcribe (tối ưu <= 100 từ khóa).
    /// </summary>
    public static readonly System.Collections.Generic.IReadOnlyList<string> DefaultSeCustomVocabulary =
    [
        "Clean Architecture",
        "Dependency Inversion",
        "Dependency Injection",
        "SOLID",
        "Single Responsibility",
        "Open-Closed",
        "Liskov Substitution",
        "Interface Segregation",
        "CQRS",
        "MediatR",
        "FluentValidation",
        "Entity Framework Core",
        "DbContext",
        "AsNoTracking",
        "BoundedChannel",
        "BackgroundService",
        "IHostedService",
        "Polly Retry",
        "Circuit Breaker",
        "Dead-Letter Queue",
        "Microservices",
        "API Gateway",
        "Saga Pattern",
        "CAP Theorem",
        "Repository Pattern",
        "Unit of Work",
        "Domain-Driven Design",
        "Idempotent",
        "Idempotency",
        "OneWayLockInterceptor",
        "GradingQueueWorker",
        "OAuth 2.0",
        "PKCE",
        "JWT",
        "Refresh Token",
        "Access Token",
        "SignalR",
        "WebSocket",
        "PostgreSQL",
        "Cloudflare R2",
        "SHA-256",
        "Middleware",
        "Controller",
        "Migration",
        "Transaction",
        "Caching",
        "In-Memory",
        "ORM",
        "async/await",
        ".NET",
        "C#"
    ];

    /// <summary>
    /// Xây dựng danh sách từ vựng chuyên ngành (customVocabulary) kết hợp động giữa từ khóa trích xuất từ câu hỏi hiện tại
    /// và bộ từ điển SE cốt lõi, giới hạn tối đa theo khuyến nghị của Google Gemini 3.5 Transcribe (mặc định 100 từ).
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<string> BuildCustomVocabularyList(string? vocabularyHint, int maxTerms = 100)
    {
        var limit = Math.Clamp(maxTerms, 1, 1000);
        var result = new System.Collections.Generic.List<string>(Math.Min(limit, 100));
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(vocabularyHint))
        {
            var matches = TechnicalContextTokenRegex.Matches(vocabularyHint);
            foreach (Match match in matches)
            {
                var token = match.Value.Trim();
                if (token.Length >= 2 && seen.Add(token))
                {
                    result.Add(token);
                    if (result.Count >= limit)
                    {
                        return result;
                    }
                }
            }
        }

        foreach (var term in DefaultSeCustomVocabulary)
        {
            if (seen.Add(term))
            {
                result.Add(term);
                if (result.Count >= limit)
                {
                    break;
                }
            }
        }

        return result;
    }

    private static readonly (Regex Pattern, string Replacement)[] NormalizationRules =
    [
        (CreatePhraseRegex(@"cờ\s*lin\s*a\s*ki\s*téc\s*chơ|clean\s*a\s*ki\s*téc\s*chơ|linh\s*a\s*ki\s*téc(\s*trở)?|linh\s+akitek(\s+trở)?|clean\s+architecture"), "Clean Architecture"),
        (CreatePhraseRegex(@"đì\s*pen\s*đần\s*xi\s*in\s*vơ\s*sần|dependency\s*in\s*vơ\s*sần|dependency\s+inversion"), "Dependency Inversion"),
        (CreatePhraseRegex(@"đì\s*pen\s*đần\s*xi\s*in\s*giéc\s*sần|dependency\s*in\s*giéc\s*sần|dependency\s+injection"), "Dependency Injection"),
        (CreatePhraseRegex(@"si\s*quy\s*a\s*ét|si\s*quy\s*ai\s*ét|cờ\s*quy\s*a\s*ét|c\s*q\s*r\s*s|cqis|cqrs"), "CQRS"),
        (CreatePhraseRegex(@"mê\s*đi\s*a\s*tờ|mê\s*đi\s*át\s*rờ|mê\s*đi\s*a\s*trơ|media\s+trơ|mediatr"), "MediatR"),
        (CreatePhraseRegex(@"flu\s*ần\s*va\s*li\s*đây\s*sần|fluent\s+validation|fluentvalidation"), "FluentValidation"),
        (CreatePhraseRegex(@"bao\s*đựt\s*chan\s*nồ|bounded\s+channel|boundedchannel"), "BoundedChannel"),
        (CreatePhraseRegex(@"bách\s*grao\s*sơ\s*vít|background\s+service|backgroundservice"), "BackgroundService"),
        (CreatePhraseRegex(@"ai\s*hốt\s*tựt\s*sơ\s*vít|i\s+hosted\s+service|ihostedservice"), "IHostedService"),
        (CreatePhraseRegex(@"đốt\s*nét|dot\s+net"), ".NET"),
        (CreatePhraseRegex(@"si\s*sáp|c\s+sharp"), "C#"),
        (CreatePhraseRegex(@"pô\s*li\s*rì\s*trai|pô\s*li\s+retry|polly\s+retry"), "Polly Retry"),
        (CreatePhraseRegex(@"sơ\s*kít\s*bờ\s*rếch\s*cơ|circuit\s+breaker"), "Circuit Breaker"),
        (CreatePhraseRegex(@"đét\s*lét\s*tơ\s*kiu|dead\s+letter\s+queue|dead-letter\s+queue"), "Dead-Letter Queue"),
        (CreatePhraseRegex(@"mai\s*crô\s*sơ\s*vít|micro\s+services|microservices"), "Microservices"),
        (CreatePhraseRegex(@"en\s*ti\s*ti\s*phờ\s*rêm\s*guốc(\s*co)?|entity\s+framework\s+core"), "Entity Framework Core"),
        (CreatePhraseRegex(@"đi\s*bi\s*con\s*tếch|db\s+context|dbcontext"), "DbContext"),
        (CreatePhraseRegex(@"ác\s*nô\s*trắc\s*kinh|as\s+no\s+tracking|asnotracking"), "AsNoTracking"),
        (CreatePhraseRegex(@"xích\s*nồ\s*a|signal\s+r|signalr"), "SignalR"),
        (CreatePhraseRegex(@"uép\s*xóc\s*két|web\s+socket|websocket"), "WebSocket"),
        (CreatePhraseRegex(@"ây\s*pi\s*ai"), "API"),
        (CreatePhraseRegex(@"giây\s*đắp\s*bồ\s*iu\s*ti"), "JWT"),
        (CreatePhraseRegex(@"rê\s*pô\s*xi\s*tô\s*ri(\s*pát\s*tần)?|ri\s*pô\s*si\s*tô\s*ri(\s*pát\s*tần)?|repository\s+pattern"), "Repository Pattern"),
        (CreatePhraseRegex(@"iu\s*nít\s*óp\s*guốc|unit\s+of\s+work"), "Unit of Work"),
        (CreatePhraseRegex(@"ô\s*oắt\s*hai(\s*chấm\s*không)?|oauth\s*2(\.0)?"), "OAuth 2.0"),
        (CreatePhraseRegex(@"pi\s*cây\s*si\s*i|p\s*k\s*c\s*e|pkce"), "PKCE"),
        (CreatePhraseRegex(@"in\s*một\s*đơ|in\s*mê\s*mô\s*ri|in\s+memory|in-memory"), "In-Memory"),
        (CreatePhraseRegex(@"o\s*a\s*em|o\s*r\s*m|orm"), "ORM"),
        (CreatePhraseRegex(@"ây\s*xích(\s*ờ\s*uây)?|async\s+await|async/await"), "async/await"),
        (CreatePhraseRegex(@"đô\s*mên\s*đờ\s*rai\s*vần\s*đì\s*zai|domain\s+driven\s+design|domain-driven\s+design"), "Domain-Driven Design"),
        (CreatePhraseRegex(@"ai\s*đờm\s*pô\s*tần|idempotent"), "Idempotent"),
        (CreatePhraseRegex(@"cáp\s*thê\s*ô\s*rầm|cap\s+theorem"), "CAP Theorem"),
        (CreatePhraseRegex(@"sa\s*ga\s*pát\s*tần|saga\s+pattern"), "Saga Pattern"),
        (CreatePhraseRegex(@"ăn\s*đơ\s*xờ\s*ten"), "Understand"),
        (CreatePhraseRegex(@"áp\s*lai"), "Apply"),
        (CreatePhraseRegex(@"ơ\s*na\s*lai"), "Analyze"),
        (CreatePhraseRegex(@"xô\s*lít|sô\s*lít"), "SOLID"),
        (CreatePhraseRegex(@"xin\s*gồ\s*rì\s*xờ\s*pon\s*xi\s*bi\s*li\s*ti|single\s+responsibility"), "Single Responsibility"),
        (CreatePhraseRegex(@"ô\s*phần\s*cờ\s*lâu|open\s+closed|open-closed"), "Open-Closed"),
        (CreatePhraseRegex(@"lít\s*cốp(\s*sắp\s*ti\s*tiu\s*sần)?|liskov\s+substitution"), "Liskov Substitution"),
        (CreatePhraseRegex(@"in\s*tơ\s*phây\s*xê\s*gờ\s*rê\s*gây\s*sần|interface\s+segregation"), "Interface Segregation"),
        (CreatePhraseRegex(@"in\s*tơ\s*phây"), "Interface"),
        (CreatePhraseRegex(@"mít\s*đồ\s*que|mít\s*đồ\s*uê|middleware"), "Middleware"),
        (CreatePhraseRegex(@"con\s*trô\s*lơ|controller"), "Controller"),
        (CreatePhraseRegex(@"mây\s*gờ\s*rây\s*sần|mai\s*gờ\s*rây\s*sần|migration"), "Migration"),
        (CreatePhraseRegex(@"trần\s*sắc\s*sần|tran\s*xắc\s*sần|transaction"), "Transaction"),
        (CreatePhraseRegex(@"rì\s*phờ\s*rét\s*tô\s*cần|refresh\s+token"), "Refresh Token"),
        (CreatePhraseRegex(@"ác\s*xét\s*tô\s*cần|access\s+token"), "Access Token"),
        (CreatePhraseRegex(@"cách\s*sinh|két\s*sinh|caching"), "Caching"),
        (CreatePhraseRegex(@"pốt\s*gờ\s*re(\s*ét\s*quy\s*eo)?|postgres(\s*ql)?|postgresql"), "PostgreSQL"),
        (CreatePhraseRegex(@"cờ\s*lao\s*phờ\s*le|cloudflare"), "Cloudflare"),
        (CreatePhraseRegex(@"kiến\s+chúc"), "kiến trúc"),
        (CreatePhraseRegex(@"chấm\s+thị\s+vấn\s+đáp|chấm\s+thi\s+v\?n\s+dạp"), "chấm thi vấn đáp")
    ];

    /// <summary>
    /// Các mẫu câu ảo giác điển hình của Whisper khi thu phải tiếng ồn nền hoặc khoảng lặng.
    /// </summary>
    private static readonly Regex NoiseHallucinationPhraseRegex = new(
        @"^(?:thank\s+you(?:\s+very\s+much|\s+so\s+much|\s+for\s+watching)?|thanks(?:\s+for\s+watching)?|subtitles\s+by.*|amara\.org.*|please\s+subscribe.*|like\s+and\s+subscribe.*|hãy\s+đăng\s+ký\s+kênh.*|cảm\s+ơn\s+các\s+bạn\s+đã\s+theo\s+dõi.*|hẹn\s+gặp\s+lại\s+các\s+bạn.*|phụ\s+đề\s+bởi.*|you|bye(?:\s+bye)?|mbc\s+news.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));

    private static readonly Regex RepeatedPhraseRegex = new(
        @"\b(\w+(?:\s+\w+){1,6})(?:[\s,.!?]+\1){1,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));
    private static readonly Regex RepeatedWordLoopRegex = new(
        @"\b(\w+)(?:[\s,.!?]+\1){2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));
    private static readonly Regex SentenceSplitRegex = new(
        @"(?<=[.!?。])\s+|[\r\n]+|(?<=[\p{Ll}][.!?。])(?=[\p{Lu}])",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));
    private static readonly Regex PunctuationTrimRegex = new(
        @"^[\s\p{P}]+|[\s\p{P}]+$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));
    private static readonly Regex MultiWhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));
    private static readonly Regex MarkdownSymbolsRegex = new(
        @"[*_`#~]+",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));
    private static readonly Regex TechnicalContextTokenRegex = new(
        @"\b(?:[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]*)+|[A-Z]{2,}(?:-\d+)?|\.[A-Za-z0-9]+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(500));

    /// <summary>
    /// Thu gọn các chuỗi lặp lại do ảo giác STT: các cụm từ lặp (2-7 từ) và các từ đơn lặp liên tiếp từ 3 lần trở lên.
    /// </summary>
    public static string CollapseRepetitions(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var collapsed = RepeatedWordLoopRegex.Replace(text, "$1");
        return RepeatedPhraseRegex.Replace(collapsed, "$1");
    }

    /// <summary>
    /// Tạo chuỗi initial_prompt tối ưu cho Whisper (< 224 tokens), đặt ngữ cảnh câu hỏi hiện tại ở cuối
    /// để bộ giải mã Whisper ưu tiên dự đoán chính xác các từ vựng thuộc câu hỏi đang trả lời.
    /// </summary>
    public static string BuildContextualWhisperPrompt(string? vocabularyHint)
    {
        if (string.IsNullOrWhiteSpace(vocabularyHint))
        {
            return DefaultSeWhisperPrompt;
        }

        var cleanedHint = MultiWhitespaceRegex.Replace(vocabularyHint.Trim(), " ");
        if (cleanedHint.Length > 360)
        {
            cleanedHint = cleanedHint[..360].Trim();
        }

        return $"{DefaultSeWhisperPrompt} Câu hỏi hiện tại: {cleanedHint}";
    }

    /// <summary>
    /// Kiểm tra xem văn bản bóc băng có phải là ảo giác tiếng ồn (Whisper Hallucination Loop) hay không.
    /// </summary>
    public static bool IsHallucinationOrNoise(string? rawTranscript)
    {
        if (string.IsNullOrWhiteSpace(rawTranscript))
        {
            return true;
        }

        var sanitized = SanitizeHallucinations(rawTranscript);
        return string.IsNullOrWhiteSpace(sanitized);
    }

    /// <summary>
    /// Lọc bỏ các câu ảo giác do tiếng ồn nền (ví dụ: "Thank you very much. Thank you very much...")
    /// và khử các vòng lặp câu liên tiếp của Whisper.
    /// </summary>
    public static string SanitizeHallucinations(string? rawTranscript)
    {
        if (string.IsNullOrWhiteSpace(rawTranscript))
        {
            return string.Empty;
        }

        var trimmed = rawTranscript.Trim();

        // 1. Kiểm tra nhanh khi cả văn bản sau khi khử lặp trùng khớp với mẫu ảo giác tiếng ồn
        var collapsedAll = CollapseRepetitions(trimmed);
        var coreAll = PunctuationTrimRegex.Replace(collapsedAll, string.Empty);
        coreAll = MultiWhitespaceRegex.Replace(coreAll, " ").Trim();
        if (string.IsNullOrWhiteSpace(coreAll) || NoiseHallucinationPhraseRegex.IsMatch(coreAll))
        {
            return string.Empty;
        }

        // 2. Tách thành các câu đơn để kiểm tra từng câu
        var rawSentences = SentenceSplitRegex.Split(trimmed);
        var keptSentences = new System.Collections.Generic.List<string>(rawSentences.Length);
        string? lastCoreSentence = null;
        int consecutiveDuplicateCount = 0;

        foreach (var sentence in rawSentences)
        {
            var candidate = sentence.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var collapsedCandidate = CollapseRepetitions(candidate);
            var core = PunctuationTrimRegex.Replace(collapsedCandidate, string.Empty);
            core = MultiWhitespaceRegex.Replace(core, " ").Trim();
            if (string.IsNullOrWhiteSpace(core))
            {
                continue;
            }

            // Bỏ qua ngay nếu câu nằm trong danh sách ảo giác tiếng ồn của Whisper
            if (NoiseHallucinationPhraseRegex.IsMatch(core))
            {
                continue;
            }

            // Khử lặp câu liên tiếp (Whisper loop)
            if (lastCoreSentence != null &&
                string.Equals(core, lastCoreSentence, StringComparison.OrdinalIgnoreCase))
            {
                consecutiveDuplicateCount++;
                continue;
            }

            lastCoreSentence = core;
            consecutiveDuplicateCount = 0;
            keptSentences.Add(collapsedCandidate);
        }

        if (keptSentences.Count == 0)
        {
            return string.Empty;
        }

        var joined = string.Join(" ", keptSentences);
        joined = CollapseRepetitions(joined);
        joined = MultiWhitespaceRegex.Replace(joined, " ").Trim();

        var finalCore = PunctuationTrimRegex.Replace(joined, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(finalCore) || NoiseHallucinationPhraseRegex.IsMatch(finalCore))
        {
            return string.Empty;
        }

        return joined;
    }

    /// <summary>
    /// Chuẩn hóa văn bản bóc băng giọng nói, tự động lọc ảo giác tiếng ồn và sửa các cụm từ tiếng Việt phiên âm sai
    /// sang thuật ngữ chuẩn Kỹ thuật Phần mềm (Code-Switching).
    /// </summary>
    public static string Normalize(string? rawTranscript)
    {
        return NormalizeWithContext(rawTranscript, null);
    }

    /// <summary>
    /// Chuẩn hóa văn bản bóc băng kết hợp ngữ cảnh câu hỏi hiện tại (vocabularyHint) để tự động khớp
    /// và viết hoa chuẩn xác các thuật ngữ chuyên ngành xuất hiện trong đề bài.
    /// </summary>
    public static string NormalizeWithContext(string? rawTranscript, string? vocabularyHint)
    {
        var sanitized = SanitizeHallucinations(rawTranscript);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return string.Empty;
        }

        var normalized = sanitized;

        foreach (var (pattern, replacement) in NormalizationRules)
        {
            normalized = pattern.Replace(normalized, replacement);
        }

        if (!string.IsNullOrWhiteSpace(vocabularyHint))
        {
            var matches = TechnicalContextTokenRegex.Matches(vocabularyHint);
            var seenTokens = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in matches)
            {
                var token = match.Value;
                if (token.Length >= 3 && seenTokens.Add(token))
                {
                    var escaped = Regex.Escape(token);
                    normalized = Regex.Replace(
                        normalized,
                        $@"(?<![\p{{L}}\p{{N}}]){escaped}(?![\p{{L}}\p{{N}}])",
                        token,
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
            }
        }

        normalized = MultiWhitespaceRegex.Replace(normalized, " ").Trim();
        return normalized;
    }

    /// <summary>
    /// Làm sạch ký hiệu Markdown và chuyển đổi các ký hiệu kỹ thuật sang dạng phát âm tự nhiên cho bộ đọc TTS.
    /// </summary>
    public static string PrepareTextForTts(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Trim();

        // Chuyển đổi C# và .NET trước khi xóa ký hiệu '#' của Markdown
        cleaned = Regex.Replace(cleaned, @"(?i)C#", " C sharp ");
        cleaned = Regex.Replace(cleaned, @"(?i)\.NET\b", " dot NET ");

        // Chuyển đổi ký hiệu toán học & tiền tệ/điểm số
        cleaned = cleaned.Replace(">=", " lớn hơn hoặc bằng ", StringComparison.Ordinal)
                         .Replace("<=", " nhỏ hơn hoặc bằng ", StringComparison.Ordinal)
                         .Replace("10.0đ", "10 điểm", StringComparison.OrdinalIgnoreCase)
                         .Replace("10đ", "10 điểm", StringComparison.OrdinalIgnoreCase);

        // Loại bỏ các ký hiệu định dạng Markdown (**, *, `, #, ~)
        cleaned = MarkdownSymbolsRegex.Replace(cleaned, " ");

        cleaned = MultiWhitespaceRegex.Replace(cleaned, " ").Trim();
        return cleaned;
    }

    private static Regex CreatePhraseRegex(string phrasePattern)
    {
        return new Regex(
            $@"(?<![\p{{L}}\p{{N}}])(?:{phrasePattern})(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }
}
