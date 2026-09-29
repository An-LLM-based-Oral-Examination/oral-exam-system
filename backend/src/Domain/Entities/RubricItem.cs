using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RubricItem
{
    public Guid RubricId { get; set; }

    public Guid? PracticeQuestionId { get; set; }

    public Guid? ExamQuestionId { get; set; }

    public string CriteriaDescription { get; set; } = null!;

    public decimal MaxScore { get; set; }

    public virtual ExamQuestion? ExamQuestion { get; set; }

    public virtual PracticeQuestion? PracticeQuestion { get; set; }
}
