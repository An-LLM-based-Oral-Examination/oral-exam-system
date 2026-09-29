using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class PracticeFollowupQuestion
{
    public Guid FollowupId { get; set; }

    public Guid PracticeAnswerId { get; set; }

    public Guid PracticeSessionId { get; set; }

    public int RoundIndex { get; set; }

    public string QuestionText { get; set; } = null!;

    public decimal? TriggerScore { get; set; }

    public string? GapSummary { get; set; }

    public string? ModelVersion { get; set; }

    public string? PromptVersion { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual PracticeAnswer PracticeAnswer { get; set; } = null!;

    public virtual ICollection<PracticeAnswer> PracticeAnswers { get; set; } = new List<PracticeAnswer>();

    public virtual PracticeSession PracticeSession { get; set; } = null!;
}
