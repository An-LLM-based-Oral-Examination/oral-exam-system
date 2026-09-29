using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class MockExamAnswer
{
    public Guid MockAnswerId { get; set; }

    public Guid MockSessionId { get; set; }

    public Guid QuestionId { get; set; }

    public Guid? FollowupQuestionId { get; set; }

    public Guid? ParentAnswerId { get; set; }

    public int RoundIndex { get; set; }

    public string? UserTranscript { get; set; }

    public decimal? AiScore { get; set; }

    public string? AiFeedback { get; set; }

    public string Status { get; set; } = null!;

    public virtual MockExamFollowupQuestion? FollowupQuestion { get; set; }

    public virtual ICollection<MockExamAnswer> InverseParentAnswer { get; set; } = new List<MockExamAnswer>();

    public virtual ICollection<MockExamFollowupQuestion> MockExamFollowupQuestions { get; set; } = new List<MockExamFollowupQuestion>();

    public virtual MockExamSession MockSession { get; set; } = null!;

    public virtual MockExamAnswer? ParentAnswer { get; set; }

    public virtual PracticeQuestion Question { get; set; } = null!;
}
