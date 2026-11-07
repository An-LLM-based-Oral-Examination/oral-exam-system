using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class InClassExamSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShiftId { get; set; }
    public Guid StudentId { get; set; }
    public Guid QuestionId { get; set; }
    public PracticeQuestion? Question { get; set; }
    public bool IsFollowUp { get; set; } = false;
    public Guid? ParentSubmissionId { get; set; }
    public InClassExamSubmission? ParentSubmission { get; set; }
    public int FollowUpIndex { get; set; } = 0;
    public string? FollowUpPrompt { get; set; }
    public string Transcript { get; set; } = null!;
    public decimal? AiScore { get; set; }
    public decimal? AiConfidenceScore { get; set; }
    public string? CotTrace { get; set; }
    public string? AiFeedback { get; set; }
    public decimal? FinalScore { get; set; }
    public string? LecturerNotes { get; set; }
    public string Status { get; set; } = "SUBMITTED";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? GradedAt { get; set; }

    public virtual InClassExamShift Shift { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual ICollection<AiEvaluation> AiEvaluations { get; set; } = new List<AiEvaluation>();
}
