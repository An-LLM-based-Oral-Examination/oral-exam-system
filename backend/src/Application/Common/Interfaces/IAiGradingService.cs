using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Common.Interfaces;

public class AiGradingCriterion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal Weight { get; set; }
}

public class AiGradingRequest
{
    public string Transcript { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public string SampleAnswer { get; set; } = string.Empty;
    public string? CourseCode { get; set; }
    public string? QuestionImageUrl { get; set; }
    public string? QuestionImageDescription { get; set; }
    public string? QuestionImageBase64 { get; set; }
    public string? QuestionImageMimeType { get; set; }
    public bool IsFollowUp { get; set; }
    public string? FollowUpPrompt { get; set; }
    public int FollowUpIndex { get; set; }
    public string? OriginalAnswerTranscript { get; set; }
    public List<string> PreviousFollowUpPrompts { get; set; } = new();
    public List<AiGradingCriterion> Criteria { get; set; } = new();
    public bool ThrowOnTransientFailure { get; set; } = true;
}

public class AiGradingResult
{
    public decimal Score { get; set; }
    public decimal ConfidenceScore { get; set; }
    public bool IsSuspicious { get; set; }
    public string SuspiciousReason { get; set; } = string.Empty;
    public string CotTrace { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
    public string? FollowUpPrompt { get; set; }
    public List<GradingDetailDto> Details { get; set; } = new();
}

public class GradingDetailDto
{
    public string CriterionId { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public interface IAiGradingService
{
    Task<AiGradingResult> GradeAnswerAsync(AiGradingRequest request, CancellationToken cancellationToken);
}

public static class FollowUpPromptBuilder
{
    public const string CotFollowUpTag = "[FOLLOW_UP_PROMPT]:";

    private static readonly HashSet<string> GenericBoilerplatePrompts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Could you share a specific real-life example to further explain your point?",
        "Could you elaborate more on your main point and give a specific real-life example to support your answer?",
        "Could you elaborate further and provide a concrete real-life example to support your answer?",
        "Hãy phân tích rõ hơn cơ chế hoạt động và tình huống ngoại lệ thực tế cho luận điểm vừa nêu?",
        "Hãy làm rõ hơn luận điểm kỹ thuật và ví dụ thực tế liên quan đến câu trả lời của bạn.",
        "Hãy phân tích sâu hơn cơ chế hoạt động và đưa ra một ví dụ thực tế cho câu trả lời của bạn.",
        "Câu mẫu tĩnh không được ưu tiên"
    };

    public static bool ContainsVietnameseDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        const string vietnameseChars = "ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵĂÂĐÊÔƠƯÁÀẢÃẠẤẦẨẪẬẮẰẲẴẶÉÈẺẼẸẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌỐỒỔỖỘỚỜỞỠỢÚÙỦŨỤỨỪỬỮỰÝỲỶỸỴ";
        foreach (var ch in text)
        {
            if (vietnameseChars.IndexOf(ch) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLatinLetters(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsEnglishCourse(
        string? courseCode,
        IEnumerable<string>? criterionNames = null,
        string? questionContent = null)
    {
        if (!string.IsNullOrWhiteSpace(courseCode))
        {
            var code = courseCode.Trim().ToUpperInvariant();
            return code.StartsWith("ENT", StringComparison.Ordinal) || code.StartsWith("ENG", StringComparison.Ordinal);
        }

        if (criterionNames != null)
        {
            var names = criterionNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim().ToLowerInvariant())
                .ToList();

            if (names.Count > 0)
            {
                if (names.Any(n => n.Contains("pronunciation") || n.Contains("phát âm")) ||
                    names.Any(n => n.Contains("fluency") || n.Contains("lưu loát") || n.Contains("trôi chảy")) ||
                    (names.Any(n => n.Contains("grammar") || n.Contains("ngữ pháp")) &&
                     names.Any(n => n.Contains("vocabulary") || n.Contains("từ vựng"))))
                {
                    return true;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(questionContent))
        {
            if (ContainsVietnameseDiacritics(questionContent))
            {
                return false;
            }

            if (ContainsLatinLetters(questionContent))
            {
                return true;
            }
        }

        return false;
    }

    public static string ResolveFollowUpPrompt(
        string? courseCode,
        string? questionFollowUpPrompt,
        string? aiGeneratedFollowUpPrompt,
        int followUpSequence,
        string? weakestCriterionName = null,
        string? weakestCriterionDescription = null,
        IEnumerable<string>? criterionNames = null,
        string? questionContent = null,
        string? sampleAnswer = null,
        string? studentTranscript = null,
        string? originalAnswerTranscript = null,
        IEnumerable<string>? previousFollowUpPrompts = null)
    {
        var cleanQuestionContent = string.IsNullOrWhiteSpace(questionContent) ? null : questionContent.Trim();
        var isEnglish = IsEnglishCourse(courseCode, criterionNames, cleanQuestionContent);
        var cleanAiPrompt = string.IsNullOrWhiteSpace(aiGeneratedFollowUpPrompt) ? null : aiGeneratedFollowUpPrompt.Trim();

        // Nếu sinh viên vừa báo "không biết" / "I don't know" ở lượt hiện tại, trả về rỗng ngay lập tức
        if (!string.IsNullOrWhiteSpace(studentTranscript) && IsRefusalClause(studentTranscript))
        {
            return string.Empty;
        }

        var previousSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (previousFollowUpPrompts != null)
        {
            foreach (var prev in previousFollowUpPrompts)
            {
                if (!string.IsNullOrWhiteSpace(prev))
                {
                    previousSet.Add(prev.Trim());
                }
            }
        }

        // 1. Chỉ chấp nhận câu hỏi phụ do AI sinh thực tế từ ngữ cảnh câu trả lời và chủ đề câu hỏi
        // (Tuyệt đối không tự cắt ghép chuỗi cơ học từ câu trả lời của sinh viên khi AI không trả về câu hỏi phụ)
        // Đối với môn Tiếng Anh (ENT/ENG), câu hỏi phụ bắt buộc phải bằng Tiếng Anh (không chứa dấu Tiếng Việt).
        if (!string.IsNullOrWhiteSpace(cleanAiPrompt) &&
            !cleanAiPrompt.StartsWith('<') &&
            !GenericBoilerplatePrompts.Contains(cleanAiPrompt) &&
            !previousSet.Contains(cleanAiPrompt) &&
            !string.Equals(cleanAiPrompt, cleanQuestionContent, StringComparison.OrdinalIgnoreCase) &&
            (!isEnglish || !ContainsVietnameseDiacritics(cleanAiPrompt)))
        {
            return cleanAiPrompt;
        }

        return string.Empty;
    }

    private static readonly string[] StudentRefusalPhrases =
    [
        "không biết",
        "ko biết",
        "k biết",
        "chưa biết",
        "bỏ qua",
        "em chịu",
        "i don't know",
        "i dont know",
        "don't know",
        "dont know",
        "no idea",
        "not sure",
        "skip",
        "pass"
    ];

    private static readonly string[] TechnicalBypassContextTokens =
    [
        "changetracker",
        "tracking",
        "asnotracking",
        "cache",
        "middleware",
        "validation",
        "domain",
        "infrastructure",
        "entity",
        "ef core",
        "transaction"
    ];

    private static bool IsRefusalClause(string clause)
    {
        var normalized = clause.Trim('.', '!', '?', ',', ';', ':', '"', '\'', ' ');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var normalizedSpacing = Regex.Replace(normalized, @"\s+", " ").Trim();

        foreach (var phrase in StudentRefusalPhrases)
        {
            if (string.Equals(normalizedSpacing, phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (phrase.Equals("skip", StringComparison.OrdinalIgnoreCase) ||
                phrase.Equals("pass", StringComparison.OrdinalIgnoreCase))
            {
                if (Regex.IsMatch(normalizedSpacing, $@"\b{Regex.Escape(phrase)}\b", RegexOptions.IgnoreCase) &&
                    wordCount <= 8)
                {
                    return true;
                }
            }
            else if (normalizedSpacing.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                if (TechnicalBypassContextTokens.Any(t => normalizedSpacing.Contains(t, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (wordCount <= 20)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static string AppendFollowUpTagToCot(string? cotTrace, string? followUpPrompt)
    {
        if (string.IsNullOrWhiteSpace(followUpPrompt))
        {
            return cotTrace ?? string.Empty;
        }

        var singleLinePrompt = followUpPrompt.Replace("\r", " ").Replace("\n", " ").Trim();
        if (string.IsNullOrWhiteSpace(cotTrace))
        {
            return $"{CotFollowUpTag} {singleLinePrompt}";
        }

        if (cotTrace.Contains(CotFollowUpTag, StringComparison.OrdinalIgnoreCase))
        {
            return cotTrace;
        }

        return $"{cotTrace}\n{CotFollowUpTag} {singleLinePrompt}";
    }

    public static string? TryExtractFollowUpFromCot(string? cotTrace)
    {
        if (string.IsNullOrWhiteSpace(cotTrace))
        {
            return null;
        }

        var idx = cotTrace.IndexOf(CotFollowUpTag, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + CotFollowUpTag.Length;
        var remainder = cotTrace[start..];
        var newlineIdx = remainder.IndexOfAny(new[] { '\r', '\n' });
        var extracted = (newlineIdx >= 0 ? remainder[..newlineIdx] : remainder).Trim();
        return string.IsNullOrWhiteSpace(extracted) ? null : extracted;
    }
}
