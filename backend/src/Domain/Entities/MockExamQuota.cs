using System;

namespace OralExamination.Domain.Entities;

public partial class MockExamQuota
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public DateOnly QuotaDate { get; set; }
    public int UsedCount { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual User Student { get; set; } = null!;
    public virtual Course Course { get; set; } = null!;
}
