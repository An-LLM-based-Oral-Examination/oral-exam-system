using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RealExamFollowupQuestion
{
    public Guid FollowupId { get; set; }

    public Guid RealAnswerId { get; set; }

    public Guid ResultId { get; set; }

    public int RoundIndex { get; set; }

    public string QuestionText { get; set; } = null!;

    public string? ContextTranscript { get; set; }

    public string PromptSnapshot { get; set; } = null!;

    public string? ModelVersion { get; set; }

    public string? PromptVersion { get; set; }

    public string Status { get; set; } = null!;

    public int? TimeoutSec { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual RealExamAnswer RealAnswer { get; set; } = null!;

    public virtual ICollection<RealExamAnswer> RealExamAnswers { get; set; } = new List<RealExamAnswer>();

    public virtual RealExamResult Result { get; set; } = null!;
}
