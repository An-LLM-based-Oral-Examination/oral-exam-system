namespace OralExamination.Domain.Entities;

public sealed class QuestionReviewRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? Notes { get; set; }
    public long Version { get; set; }
    public Course Course { get; set; } = null!;
    public User Lecturer { get; set; } = null!;
    public ICollection<QuestionReviewRequestItem> Items { get; set; } = new List<QuestionReviewRequestItem>();
}

public sealed class QuestionReviewRequestItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public Guid QuestionId { get; set; }
    public string UsageScope { get; set; } = "practice";
    public string? SnapshotJson { get; set; }
    public string Decision { get; set; } = "PENDING";
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public QuestionReviewRequest Request { get; set; } = null!;
}
