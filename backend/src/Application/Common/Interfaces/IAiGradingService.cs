using System;
using System.Collections.Generic;
using System.Linq;
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
    public List<AiGradingCriterion> Criteria { get; set; } = new();
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

    public static bool IsEnglishCourse(string? courseCode, IEnumerable<string>? criterionNames = null)
    {
        if (!string.IsNullOrWhiteSpace(courseCode))
        {
            var code = courseCode.Trim().ToUpperInvariant();
            if (code.StartsWith("ENT", StringComparison.Ordinal) || code.StartsWith("ENG", StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (criterionNames != null)
        {
            var names = criterionNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim().ToLowerInvariant())
                .ToList();

            if (names.Any(n => n.Contains("pronunciation") || n.Contains("phát âm")) ||
                names.Any(n => n.Contains("fluency") || n.Contains("lưu loát") || n.Contains("trôi chảy")) ||
                (names.Any(n => n.Contains("grammar") || n.Contains("ngữ pháp")) &&
                 names.Any(n => n.Contains("vocabulary") || n.Contains("từ vựng"))))
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
        IEnumerable<string>? criterionNames = null)
    {
        var isEnglish = IsEnglishCourse(courseCode, criterionNames);
        var cleanQuestionPrompt = string.IsNullOrWhiteSpace(questionFollowUpPrompt) ? null : questionFollowUpPrompt.Trim();
        var cleanAiPrompt = string.IsNullOrWhiteSpace(aiGeneratedFollowUpPrompt) ? null : aiGeneratedFollowUpPrompt.Trim();

        if (followUpSequence <= 1)
        {
            if (!string.IsNullOrWhiteSpace(cleanQuestionPrompt))
            {
                return cleanQuestionPrompt;
            }

            if (!string.IsNullOrWhiteSpace(cleanAiPrompt))
            {
                return cleanAiPrompt;
            }

            if (isEnglish)
            {
                return !string.IsNullOrWhiteSpace(weakestCriterionName)
                    ? $"Could you elaborate further and provide a concrete example to strengthen your '{weakestCriterionName.Trim()}' in this response?"
                    : "Could you elaborate more on your main point and give a specific real-life example to support your answer?";
            }

            return !string.IsNullOrWhiteSpace(weakestCriterionName)
                ? $"Hãy làm rõ hơn luận điểm kỹ thuật về '{weakestCriterionName.Trim()}' và đưa ra ví dụ thực tế liên quan đến câu trả lời của bạn."
                : "Hãy làm rõ hơn luận điểm kỹ thuật và ví dụ thực tế liên quan đến câu trả lời của bạn.";
        }

        // Lượt hỏi phụ thứ 2 trở đi (followUpSequence >= 2): Tuyệt đối không lặp lại nguyên văn câu hỏi phụ số 1
        if (!string.IsNullOrWhiteSpace(cleanAiPrompt) &&
            !string.Equals(cleanAiPrompt, cleanQuestionPrompt, StringComparison.OrdinalIgnoreCase))
        {
            return cleanAiPrompt;
        }

        if (isEnglish)
        {
            var critName = !string.IsNullOrWhiteSpace(weakestCriterionName)
                ? weakestCriterionName.Trim()
                : "Ideas & Content Development";

            return followUpSequence == 2
                ? $"Follow-up #2: To further develop your '{critName}', could you share a specific real-life situation or personal experience and explain its impact in detail?"
                : $"Follow-up #{followUpSequence}: Looking deeper at '{critName}', how would you compare different viewpoints on this topic and summarize your key takeaway?";
        }
        else
        {
            var critName = !string.IsNullOrWhiteSpace(weakestCriterionName)
                ? weakestCriterionName.Trim()
                : "Kiến thức cốt lõi & Thực tiễn";
            var critDesc = !string.IsNullOrWhiteSpace(weakestCriterionDescription)
                ? $" ({weakestCriterionDescription.Trim()})"
                : string.Empty;

            return followUpSequence == 2
                ? $"Câu hỏi đào sâu #2: Dựa trên phần trả lời bổ sung của bạn, tiêu chí '{critName}'{critDesc} vẫn cần làm rõ hơn. Bạn hãy phân tích một tình huống ngoại lệ (edge-case) hoặc sự đánh đổi kỹ thuật (trade-off) khi áp dụng vào dự án thực tế?"
                : $"Câu hỏi đào sâu #{followUpSequence}: Tiếp tục đào sâu vào tiêu chí '{critName}', bạn hãy đề xuất giải pháp tối ưu hiệu năng hoặc xử lý lỗi cụ thể khi vận hành trong môi trường Production?";
        }
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
