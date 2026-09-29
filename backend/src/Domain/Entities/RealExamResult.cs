using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RealExamResult
{
    public Guid ResultId { get; set; }

    public Guid AssignmentId { get; set; }

    public string Status { get; set; } = null!;

    public bool MicCheckPassed { get; set; }

    public int ViolationCount { get; set; }

    public bool IsFlagged { get; set; }

    public decimal? AiScore { get; set; }

    public decimal? FinalScore { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public bool IsOverridden { get; set; }

    public string? OverrideReason { get; set; }

    public bool IsLocked { get; set; }

    public string? Sha256Hash { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? LockedAt { get; set; }

    public virtual RealExamAssignment Assignment { get; set; } = null!;

    public virtual ICollection<RealExamAnswer> RealExamAnswers { get; set; } = new List<RealExamAnswer>();

    public virtual ICollection<RealExamFollowupQuestion> RealExamFollowupQuestions { get; set; } = new List<RealExamFollowupQuestion>();

    public virtual User? ReviewedByNavigation { get; set; }
}
