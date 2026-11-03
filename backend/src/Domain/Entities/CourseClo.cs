using System;
using OralExamination.Domain.Enums;

namespace OralExamination.Domain.Entities;

public partial class CourseClo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public string CloCode { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string BloomLevel { get; set; } = Enums.BloomLevel.Understand;
    public int OrderIndex { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
}
