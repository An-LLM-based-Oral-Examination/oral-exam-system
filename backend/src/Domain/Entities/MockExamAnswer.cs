using System;

namespace OralExamination.Domain.Entities;

public partial class MockExamAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public string? AnswerText { get; set; }
    public string? AudioUrl { get; set; }
    public int TimeTakenSeconds { get; set; } = 0;
    public string Status { get; set; } = "answered";
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    public virtual MockExamSession Session { get; set; } = null!;
    public virtual PracticeQuestion Question { get; set; } = null!;
}
