namespace OralExamination.Domain.Entities;

/// <summary>
/// Durable hand-off between an official-exam submission and the in-memory worker queue.
/// The database row is the source of truth; the channel is only a bounded transport.
/// </summary>
public sealed class GradingJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public string Status { get; set; } = "pending";
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ExamQuestionSubmission Submission { get; set; } = null!;
}
