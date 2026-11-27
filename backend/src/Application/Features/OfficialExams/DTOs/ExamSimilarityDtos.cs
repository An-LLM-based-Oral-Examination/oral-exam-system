namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed record QuestionSimilarityMatch(Guid QuestionId, Guid ExamSetId, string SetCode, string SessionTitle,
    string SemesterCode, string Title, string Content, decimal Percent, bool SameSource, int OrderIndex = 0);
public sealed record PaperQuestionSimilarity(Guid QuestionId, int OrderIndex, decimal Percent, string Band,
    List<QuestionSimilarityMatch> Matches);
public sealed record ComparedPaperSummary(Guid ExamSetId, string SetCode, Guid SessionId, string SessionTitle,
    string SemesterCode, int QuestionCount);
public sealed record PaperSimilarityReport(Guid ExamSetId, decimal SimilarityPercent, List<PaperQuestionSimilarity> Questions,
    ComparedPaperSummary? ComparedPaper = null);
public sealed record BankPromotionCandidate(Guid QuestionId, Guid SourceQuestionId, string Title, decimal SimilarityPercent,
    string Band, string Recommendation, Guid? ComparedQuestionId, string? ComparedTitle, string? ComparedContent);
public sealed record PromotePaperQuestions(List<Guid> QuestionIds);
public sealed record UpdateExamReusePolicy(int ReusePercent);
internal sealed record DifficultyCapacity(string Difficulty, long RequiredSlots, int AvailableQuestions, long RemainingCapacity,
    long MissingSlots, long AdditionalQuestionsRequired);
public sealed record ExamCapacityReport(int RequiredSetCount, int ReusePercent, int MaxUses, string Band,
    long AdditionalQuestionsRequired, int? SuggestedReusePercent)
{
    public string? Warning => AdditionalQuestionsRequired > 0
        ? $"Ngân hàng hiện thiếu ít nhất {AdditionalQuestionsRequired} câu hỏi để đáp ứng cấu hình. Có thể soạn mới hoặc tạo bằng AI để bổ sung."
        : null;
}
