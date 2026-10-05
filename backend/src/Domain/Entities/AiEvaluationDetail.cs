using System;

namespace OralExamination.Domain.Entities;

public partial class AiEvaluationDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EvaluationId { get; set; }
    public Guid CriterionId { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }

    public virtual AiEvaluation Evaluation { get; set; } = null!;
    public virtual RubricCriterion Criterion { get; set; } = null!;
}
