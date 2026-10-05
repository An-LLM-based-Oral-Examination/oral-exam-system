using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class AiEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AnswerType { get; set; } = null!; // "practice" | "exam"
    public Guid? PracticeAnswerId { get; set; }
    public Guid? ExamSubmissionId { get; set; }
    public decimal TotalScore { get; set; }
    public string? Feedback { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? CotTrace { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    public virtual PracticeAnswer? PracticeAnswer { get; set; }
    public virtual ExamQuestionSubmission? ExamSubmission { get; set; }
    public virtual ICollection<AiEvaluationDetail> Details { get; set; } = new List<AiEvaluationDetail>();
}
