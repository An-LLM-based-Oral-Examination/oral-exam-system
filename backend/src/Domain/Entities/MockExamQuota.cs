using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class MockExamQuota
{
    public Guid QuotaId { get; set; }

    public Guid StudentId { get; set; }

    public int CourseId { get; set; }

    public DateOnly QuotaDate { get; set; }

    public int UsedCount { get; set; }

    public int MaxCount { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
