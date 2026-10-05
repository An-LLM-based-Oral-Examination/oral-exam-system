using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class PracticeAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid StudentId { get; set; }
    public string AnswerText { get; set; } = null!;
    public string? AudioUrl { get; set; }
    public bool IsFollowUp { get; set; } = false;
    public Guid? ParentAnswerId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public virtual PracticeSession Session { get; set; } = null!;
    public virtual PracticeQuestion Question { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual PracticeAnswer? ParentAnswer { get; set; }
    public virtual ICollection<PracticeAnswer> FollowUpAnswers { get; set; } = new List<PracticeAnswer>();
    public virtual ICollection<AiEvaluation> AiEvaluations { get; set; } = new List<AiEvaluation>();
}
