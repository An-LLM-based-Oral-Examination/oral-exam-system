using System;

namespace OralExamination.Domain.Entities;

public partial class LecturerAuditDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuditId { get; set; }
    public Guid CriterionId { get; set; }
    public decimal AiScore { get; set; }
    public decimal AuditedScore { get; set; }
    public string? LecturerComment { get; set; }

    public virtual LecturerAudit Audit { get; set; } = null!;
    public virtual RubricCriterion Criterion { get; set; } = null!;
}
