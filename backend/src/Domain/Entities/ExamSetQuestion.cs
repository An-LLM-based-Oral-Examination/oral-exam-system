using System;

namespace OralExamination.Domain.Entities;

public partial class ExamSetQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamSetId { get; set; }
    public Guid ExamQuestionId { get; set; }
    public int OrderIndex { get; set; } = 1;

    public virtual ExamSet ExamSet { get; set; } = null!;
    public virtual ExamQuestion ExamQuestion { get; set; } = null!;
}
