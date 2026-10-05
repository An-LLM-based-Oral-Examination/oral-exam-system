using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class PracticeQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid RubricId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? SampleAnswer { get; set; }
    public string KeyPoints { get; set; } = "[]";
    public string Difficulty { get; set; } = "medium";
    public string BloomLevel { get; set; } = "Understand";
    public bool HasFollowUp { get; set; } = false;
    public string? FollowUpPrompt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
    public virtual Rubric Rubric { get; set; } = null!;
    public virtual ICollection<PracticeAnswer> PracticeAnswers { get; set; } = new List<PracticeAnswer>();
    public virtual ICollection<MockExamAnswer> MockExamAnswers { get; set; } = new List<MockExamAnswer>();
}
