using System;

namespace OralExamination.Domain.Entities;

public partial class OfficialExamCandidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? ImportedBy { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public virtual OfficialExamSession Session { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual User? ImportedByUser { get; set; }
}
