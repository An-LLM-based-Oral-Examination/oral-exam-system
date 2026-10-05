using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class MockExamSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid ExamSetId { get; set; }
    public Guid CourseId { get; set; }
    public bool HasFollowUp { get; set; } = false;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string Status { get; set; } = "in_progress";
    public decimal? TotalScore { get; set; }

    public virtual User Student { get; set; } = null!;
    public virtual ExamSet ExamSet { get; set; } = null!;
    public virtual Course Course { get; set; } = null!;
    public virtual ICollection<MockExamAnswer> Answers { get; set; } = new List<MockExamAnswer>();
}
