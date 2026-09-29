using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class ExamQuestion
{
    public Guid QuestionId { get; set; }

    public int CourseId { get; set; }

    public string Content { get; set; } = null!;

    public string Difficulty { get; set; } = null!;

    public string ModelAnswer { get; set; } = null!;

    public bool IsActive { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<RealExamAnswer> RealExamAnswers { get; set; } = new List<RealExamAnswer>();

    public virtual ICollection<RubricItem> RubricItems { get; set; } = new List<RubricItem>();
}
