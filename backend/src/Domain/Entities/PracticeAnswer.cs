using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class PracticeAnswer
{
    public Guid AnswerId { get; set; }

    public Guid SessionId { get; set; }

    public Guid QuestionId { get; set; }

    public Guid? FollowupQuestionId { get; set; }

    public Guid? ParentAnswerId { get; set; }

    public string? UserTranscript { get; set; }

    public decimal? AiScore { get; set; }

    public string? AiFeedback { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual PracticeFollowupQuestion? FollowupQuestion { get; set; }

    public virtual ICollection<PracticeAnswer> InverseParentAnswer { get; set; } = new List<PracticeAnswer>();

    public virtual PracticeAnswer? ParentAnswer { get; set; }

    public virtual ICollection<PracticeFollowupQuestion> PracticeFollowupQuestions { get; set; } = new List<PracticeFollowupQuestion>();

    public virtual PracticeQuestion Question { get; set; } = null!;

    public virtual PracticeSession Session { get; set; } = null!;
}
