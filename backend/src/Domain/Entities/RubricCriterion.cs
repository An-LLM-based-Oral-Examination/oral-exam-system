using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RubricCriterion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RubricId { get; set; }
    public string CriterionName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal MaxScore { get; set; }
    public decimal Weight { get; set; }
    public string? BloomLevel { get; set; }
    public int OrderIndex { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Rubric Rubric { get; set; } = null!;
    public virtual ICollection<AiEvaluationDetail> AiEvaluationDetails { get; set; } = new List<AiEvaluationDetail>();
    public virtual ICollection<LecturerAuditDetail> LecturerAuditDetails { get; set; } = new List<LecturerAuditDetail>();
}
