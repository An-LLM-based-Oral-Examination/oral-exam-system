using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class PracticeSession
{
    public Guid SessionId { get; set; }

    public Guid StudentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<PracticeAnswer> PracticeAnswers { get; set; } = new List<PracticeAnswer>();

    public virtual ICollection<PracticeFollowupQuestion> PracticeFollowupQuestions { get; set; } = new List<PracticeFollowupQuestion>();

    public virtual User Student { get; set; } = null!;
}
