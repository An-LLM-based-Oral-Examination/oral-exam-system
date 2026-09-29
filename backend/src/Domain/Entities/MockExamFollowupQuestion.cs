using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class MockExamFollowupQuestion
{
    public Guid FollowupId { get; set; }

    public Guid MockAnswerId { get; set; }

    public Guid MockSessionId { get; set; }

    public int RoundIndex { get; set; }

    public string QuestionText { get; set; } = null!;

    public string? ContextTranscript { get; set; }

    public Guid? ParentPracticeQuestionId { get; set; }

    public string? ModelVersion { get; set; }

    public string? PromptVersion { get; set; }

    public string Status { get; set; } = null!;

    public int? TimeoutSec { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual MockExamAnswer MockAnswer { get; set; } = null!;

    public virtual ICollection<MockExamAnswer> MockExamAnswers { get; set; } = new List<MockExamAnswer>();

    public virtual MockExamSession MockSession { get; set; } = null!;

    public virtual PracticeQuestion? ParentPracticeQuestion { get; set; }
}
