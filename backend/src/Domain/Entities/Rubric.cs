using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class Rubric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal TotalMaxScore { get; set; } = 10.00m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
    public virtual ICollection<RubricCriterion> Criteria { get; set; } = new List<RubricCriterion>();
    public virtual ICollection<PracticeQuestion> PracticeQuestions { get; set; } = new List<PracticeQuestion>();
    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}
