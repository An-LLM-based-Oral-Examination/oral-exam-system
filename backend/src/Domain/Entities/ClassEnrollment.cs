using System;

namespace OralExamination.Domain.Entities;

public partial class ClassEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "enrolled";

    public virtual Class Class { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
}
