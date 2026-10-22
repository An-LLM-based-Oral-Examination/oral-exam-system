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

    private static readonly char[] WordSplitChars =
    [
        ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '/', '\\', '-'
    ];

    private static readonly HashSet<string> CommonStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "have", "has", "had", "will", "would", "could", "should",
        "can", "about", "into", "then", "there", "here", "what", "where", "when", "who", "how", "why", "which",
        "your", "you", "for", "are", "was", "were", "been", "being", "they", "them", "their", "our", "out",
        "của", "và", "các", "những", "một", "được", "khi", "này", "thì", "là", "hay", "hoặc", "như", "theo",
        "cần", "phải", "trong", "với", "cho", "tôi", "em", "bạn", "thầy", "cô", "câu", "hỏi", "trả", "lời",
        "hãy", "nêu", "trình", "bày", "giải", "thích", "phân", "tích", "sự", "khác", "biệt", "giữa"
    };

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
        IEnumerable<string>? criterionNames = null,
        string? questionContent = null,
        string? sampleAnswer = null,
        string? studentTranscript = null,
        string? originalAnswerTranscript = null,
        IEnumerable<string>? previousFollowUpPrompts = null)
    {
        var isEnglish = IsEnglishCourse(courseCode, criterionNames);
        var cleanAiPrompt = string.IsNullOrWhiteSpace(aiGeneratedFollowUpPrompt) ? null : aiGeneratedFollowUpPrompt.Trim();
        var cleanQuestionContent = string.IsNullOrWhiteSpace(questionContent) ? null : questionContent.Trim();
        var cleanQuestionFollowUp = string.IsNullOrWhiteSpace(questionFollowUpPrompt) ? null : questionFollowUpPrompt.Trim();
        var hasStudentTranscript = !string.IsNullOrWhiteSpace(studentTranscript) || !string.IsNullOrWhiteSpace(originalAnswerTranscript);

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

        if (!string.IsNullOrWhiteSpace(cleanQuestionFollowUp) && followUpSequence >= 2)
        {
            previousSet.Add(cleanQuestionFollowUp);
        }

        // 1. Ưu tiên tuyệt đối câu hỏi phụ do AI sinh động từ ngữ cảnh câu trả lời và chủ đề câu hỏi
        // (miễn là không phải câu mẫu rỗng/chung chung và chưa từng được hỏi ở các lượt trước)
        if (!string.IsNullOrWhiteSpace(cleanAiPrompt) &&
            !cleanAiPrompt.StartsWith('<') &&
            !GenericBoilerplatePrompts.Contains(cleanAiPrompt) &&
            !previousSet.Contains(cleanAiPrompt) &&
            !string.Equals(cleanAiPrompt, cleanQuestionContent, StringComparison.OrdinalIgnoreCase))
        {
            return cleanAiPrompt;
        }

        // 1b. Khi chưa có câu trả lời của sinh viên (ví dụ khởi tạo đề thi thử MF-02 trước khi nói),
        // ưu tiên câu hỏi phụ của ngân hàng câu hỏi để không làm lộ đáp án mẫu (SampleAnswer)
        if (!hasStudentTranscript &&
            followUpSequence <= 1 &&
            !string.IsNullOrWhiteSpace(cleanQuestionFollowUp) &&
            !GenericBoilerplatePrompts.Contains(cleanQuestionFollowUp) &&
            !previousSet.Contains(cleanQuestionFollowUp))
        {
            return cleanQuestionFollowUp;
        }

        // 2. Sinh câu hỏi phụ động trực tiếp từ ngữ cảnh câu trả lời của sinh viên + chủ đề/đáp án mẫu của câu hỏi gốc
        return BuildContextualFollowUpFromTopicAndTranscript(
            isEnglish,
            Math.Max(1, followUpSequence),
            cleanQuestionContent,
            hasStudentTranscript ? sampleAnswer : null,
            studentTranscript,
            originalAnswerTranscript,
            previousSet,
            weakestCriterionName,
            weakestCriterionDescription,
            criterionNames,
            cleanQuestionFollowUp);
    }

    private static string BuildContextualFollowUpFromTopicAndTranscript(
        bool isEnglish,
        int followUpSequence,
        string? questionContent,
        string? sampleAnswer,
        string? studentTranscript,
        string? originalAnswerTranscript,
        HashSet<string> previousSet,
        string? weakestCriterionName = null,
        string? weakestCriterionDescription = null,
        IEnumerable<string>? criterionNames = null,
        string? questionFollowUpPrompt = null)
    {
        var combinedStudentText = $"{originalAnswerTranscript} {studentTranscript}".Trim();
        var studentKeywords = ExtractSignificantTokens(combinedStudentText);
        var studentSnippet = ExtractStudentPointSnippet(studentTranscript)
            ?? ExtractStudentPointSnippet(originalAnswerTranscript);

        var candidateFocusPoints = ExtractTopicFocusCandidates(
            questionContent,
            sampleAnswer,
            weakestCriterionName,
            weakestCriterionDescription,
            criterionNames,
            questionFollowUpPrompt);
        var askedKeywords = ExtractSignificantTokens(string.Join(" ", previousSet));

        string? selectedFocus = null;
        if (candidateFocusPoints.Count > 0)
        {
            // Sắp xếp các ý của chủ đề câu hỏi/đáp án mẫu theo:
            // (1) Chưa bị hỏi trong các lượt follow-up trước
            // (2) Độ phủ từ khóa thấp nhất trong bài trả lời của sinh viên (xoáy đúng ý sinh viên còn thiếu/chưa sâu)
            var rankedCandidates = candidateFocusPoints
                .Select((clause, idx) =>
                {
                    var clauseTokens = ExtractSignificantTokens(clause);
                    var studentOverlap = clauseTokens.Count > 0
                        ? (double)clauseTokens.Count(t => studentKeywords.Contains(t)) / clauseTokens.Count
                        : 1.0;
                    var askedOverlap = clauseTokens.Count > 0
                        ? (double)clauseTokens.Count(t => askedKeywords.Contains(t)) / clauseTokens.Count
                        : 0.0;
                    var isAlreadyAsked = previousSet.Any(p =>
                        p.Contains(clause, StringComparison.OrdinalIgnoreCase) ||
                        clause.Contains(p, StringComparison.OrdinalIgnoreCase));

                    return new
                    {
                        Clause = clause,
                        Index = idx,
                        IsAlreadyAsked = isAlreadyAsked,
                        AskedOverlap = askedOverlap,
                        StudentOverlap = studentOverlap
                    };
                })
                .OrderBy(x => x.IsAlreadyAsked ? 1 : 0)
                .ThenBy(x => x.AskedOverlap >= 0.65 ? 1 : 0)
                .ThenBy(x => x.StudentOverlap)
                .ThenBy(x => x.Index)
                .Select(x => x.Clause)
                .ToList();

            var pickIndex = Math.Min(followUpSequence - 1, rankedCandidates.Count - 1);
            selectedFocus = rankedCandidates[pickIndex];
        }

        if (string.IsNullOrWhiteSpace(selectedFocus))
        {
            selectedFocus = ExtractTopicHeadline(questionContent)
                ?? (isEnglish ? "the core idea of this topic" : "nguyên lý cốt lõi của chủ đề này");
        }

        var focusClean = TrimClauseForQuestion(selectedFocus, 140);
        var isDirectQuestionFocus = focusClean.EndsWith('?');

        string candidatePrompt;
        if (isEnglish)
        {
            if (isDirectQuestionFocus)
            {
                candidatePrompt = !string.IsNullOrWhiteSpace(studentSnippet)
                    ? $"Based on what you shared about \"{studentSnippet}\", {LowercaseFirstIfQuestionWord(focusClean)}"
                    : focusClean;
            }
            else if (followUpSequence == 1)
            {
                candidatePrompt = !string.IsNullOrWhiteSpace(studentSnippet)
                    ? $"You mentioned \"{studentSnippet}\". Connecting this to the topic, how would you explain or illustrate \"{focusClean}\"?"
                    : $"Regarding this topic, how would you explain \"{focusClean}\" with a concrete example?";
            }
            else if (followUpSequence == 2)
            {
                candidatePrompt = !string.IsNullOrWhiteSpace(studentSnippet)
                    ? $"Building on your point about \"{studentSnippet}\", how does \"{focusClean}\" apply in a practical situation?"
                    : $"Going deeper into this topic, how does \"{focusClean}\" affect the outcome in practice?";
            }
            else
            {
                candidatePrompt = !string.IsNullOrWhiteSpace(studentSnippet)
                    ? $"Following up further on \"{studentSnippet}\", what challenges or key lessons arise regarding \"{focusClean}\"?"
                    : $"Considering \"{focusClean}\", what is the most important takeaway for this topic?";
            }
        }
        else
        {
            if (isDirectQuestionFocus)
            {
                candidatePrompt = !string.IsNullOrWhiteSpace(studentSnippet)
                    ? $"Từ ý bạn vừa trình bày về \"{studentSnippet}\", {LowercaseFirstChar(focusClean)}"
                    : focusClean;
            }
            else if (followUpSequence == 1)
            {
                candidatePrompt = !string.IsNullOrWhiteSpace(studentSnippet)
                    ? $"Trong câu trả lời vừa rồi, bạn có đề cập đến \"{studentSnippet}\". Vậy đối với khía cạnh \"{focusClean}\" của chủ đề này, cơ chế hoạt động và lý do kỹ thuật cụ thể là gì?"
                    : $"Liên quan trực tiếp đến chủ đề câu hỏi, bạn hãy phân tích rõ cơ chế của \"{focusClean}\"?";
            }
            else if (followUpSequence == 2)
            {
                candidatePrompt = !string.IsNullOrWhiteSpace(studentSnippet)
                    ? $"Tiếp nối phần trả lời của bạn về \"{studentSnippet}\", khi triển khai \"{focusClean}\" trong thực tế thì cần xử lý những tình huống hoặc ràng buộc kỹ thuật nào?"
                    : $"Đào sâu hơn vào \"{focusClean}\", bạn hãy phân tích cách triển khai và xử lý ngoại lệ trong thực tế?";
            }
            else
            {
                candidatePrompt = !string.IsNullOrWhiteSpace(studentSnippet)
                    ? $"Dựa trên ngữ cảnh \"{studentSnippet}\" bạn đã nêu, sự đánh đổi kỹ thuật (trade-off) quan trọng nhất đối với \"{focusClean}\" là gì?"
                    : $"Đối với \"{focusClean}\", giải pháp tối ưu hiệu năng và khả năng mở rộng trong thực tế là gì?";
            }
        }

        // Bảo đảm tuyệt đối không trùng với bất kỳ câu hỏi phụ nào đã hỏi trước đó
        if (previousSet.Contains(candidatePrompt))
        {
            candidatePrompt = isEnglish
                ? $"Regarding your explanation of \"{studentSnippet ?? focusClean}\", what specific scenario or edge case would challenge your approach to \"{focusClean}\"?"
                : $"Từ góc độ thực tiễn của ý \"{studentSnippet ?? focusClean}\", nếu xảy ra xung đột hoặc lỗi khi áp dụng \"{focusClean}\" thì bạn sẽ khắc phục như thế nào?";
        }

        return candidatePrompt;
    }

    private static List<string> ExtractTopicFocusCandidates(
        string? questionContent,
        string? sampleAnswer,
        string? weakestCriterionName = null,
        string? weakestCriterionDescription = null,
        IEnumerable<string>? criterionNames = null,
        string? questionFollowUpPrompt = null)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var cleaned = CleanBulletPrefix(raw);
            if (cleaned.Length < 12) return;
            if (cleaned.StartsWith("Topic:", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("Description Monologue:", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("Extended Discussion", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("You should", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("Please answer", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (seen.Add(cleaned))
            {
                results.Add(cleaned);
            }
        }

        // 1. Trích xuất các mệnh đề kiến thức cốt lõi từ SampleAnswer (Đáp án mẫu của câu hỏi)
        if (!string.IsNullOrWhiteSpace(sampleAnswer))
        {
            var sampleParts = Regex.Split(
                sampleAnswer,
                @"(?<=[.?!;])\s+|\r?\n|, trong khi |, còn |, nhưng |, tuy nhiên |, đồng thời |, whereas |, while ",
                RegexOptions.IgnoreCase);
            foreach (var part in sampleParts)
            {
                AddCandidate(part.Trim().TrimEnd('.', ';', '!'));
            }
        }

        // 2. Trích xuất các ý hỏi con / tiểu mục từ QuestionContent (Chủ đề câu hỏi gốc)
        if (!string.IsNullOrWhiteSpace(questionContent))
        {
            var questionLines = questionContent
                .Split(new[] { '\n', '\r', '?' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in questionLines)
            {
                var trimmed = line.Trim();
                if (questionContent.Contains(trimmed + "?", StringComparison.Ordinal))
                {
                    trimmed += "?";
                }
                if (!string.Equals(trimmed, questionContent.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    AddCandidate(trimmed);
                }
            }

            if (results.Count == 0)
            {
                var clauses = Regex.Split(questionContent, @"(?<=[.?!;])\s+|\r?\n");
                foreach (var c in clauses)
                {
                    AddCandidate(c.Trim().TrimEnd('.', ';', '!'));
                }
            }
        }

        // 3. Bổ sung tiêu chí yếu nhất (weakestCriterion) hoặc câu hỏi phụ của ngân hàng câu hỏi làm điểm nhấn dự phòng
        if (!string.IsNullOrWhiteSpace(weakestCriterionDescription))
        {
            AddCandidate(weakestCriterionDescription.Trim().TrimEnd('.', ';', '!'));
        }

        if (!string.IsNullOrWhiteSpace(weakestCriterionName))
        {
            AddCandidate(weakestCriterionName.Trim().TrimEnd('.', ';', '!'));
        }

        if (criterionNames != null)
        {
            foreach (var cName in criterionNames)
            {
                AddCandidate(cName?.Trim().TrimEnd('.', ';', '!'));
            }
        }

        if (!string.IsNullOrWhiteSpace(questionFollowUpPrompt) &&
            !GenericBoilerplatePrompts.Contains(questionFollowUpPrompt.Trim()))
        {
            AddCandidate(questionFollowUpPrompt.Trim());
        }

        return results;
    }

    private static string? ExtractStudentPointSnippet(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return null;
        }

        var cleaned = transcript
            .Replace("<student_transcript>", "", StringComparison.OrdinalIgnoreCase)
            .Replace("</student_transcript>", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        var clauses = cleaned
            .Split(new[] { '.', '!', '?', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => c.Length >= 8)
            .ToList();

        var targetClause = clauses.LastOrDefault() ?? cleaned;
        var words = targetClause.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return null;
        }

        var maxWords = Math.Min(words.Length, 12);
        var snippet = string.Join(" ", words.Take(maxWords)).Trim(',', ':', ';', '"', '\'');
        return snippet.Length >= 4 ? snippet : null;
    }

    private static string? ExtractTopicHeadline(string? questionContent)
    {
        if (string.IsNullOrWhiteSpace(questionContent))
        {
            return null;
        }

        var firstSentence = questionContent
            .Split(new[] { '\n', '\r', '.', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanBulletPrefix)
            .FirstOrDefault(s => s.Length >= 8);

        return string.IsNullOrWhiteSpace(firstSentence)
            ? TrimClauseForQuestion(questionContent.Trim(), 100)
            : TrimClauseForQuestion(firstSentence, 100);
    }

    private static string CleanBulletPrefix(string input)
    {
        var s = input.Trim();
        while (s.Length > 0 && (s[0] == '-' || s[0] == '*' || s[0] == '•'))
        {
            s = s[1..].TrimStart();
        }

        if (s.Length >= 3 && char.IsDigit(s[0]) && (s[1] == '.' || s[1] == ')'))
        {
            s = s[2..].TrimStart();
        }

        return s.Trim();
    }

    private static string TrimClauseForQuestion(string clause, int maxChars)
    {
        var cleaned = clause.Trim().TrimEnd('.');
        if (cleaned.Length <= maxChars)
        {
            return cleaned;
        }

        var cut = cleaned[..maxChars];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > maxChars / 2)
        {
            cut = cut[..lastSpace];
        }

        return cut.TrimEnd(',', ';', ':') + "...";
    }

    private static string LowercaseFirstChar(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (text.Length > 1 && char.IsUpper(text[0]) && char.IsUpper(text[1]))
        {
            return text;
        }

        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private static string LowercaseFirstIfQuestionWord(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (text.StartsWith("I ", StringComparison.Ordinal))
        {
            return text;
        }

        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private static HashSet<string> ExtractSignificantTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return text
            .Split(WordSplitChars, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length >= 3 && !CommonStopWords.Contains(w))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
