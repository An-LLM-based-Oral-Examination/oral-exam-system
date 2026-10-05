using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class ExamQuestion
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
    public Guid? ApprovedBy { get; set; }
    public string Source { get; set; } = "manual";
    public string ApprovalStatus { get; set; } = "DRAFT";
    public Guid? SubmittedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
    public virtual Rubric Rubric { get; set; } = null!;
    public virtual User? ApprovedByUser { get; set; }
    public virtual User? SubmittedByUser { get; set; }
    public virtual ICollection<ExamSetQuestion> ExamSetQuestions { get; set; } = new List<ExamSetQuestion>();
    public virtual ICollection<ExamQuestionSubmission> Submissions { get; set; } = new List<ExamQuestionSubmission>();
}
