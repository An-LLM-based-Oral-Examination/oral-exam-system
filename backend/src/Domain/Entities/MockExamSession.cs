using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class MockExamSession
{
    public Guid MockSessionId { get; set; }

    public Guid StudentId { get; set; }

    public int StructureId { get; set; }

    public string ExamMode { get; set; } = null!;

    public decimal? OverallScore { get; set; }

    public string? OverallFeedback { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<MockExamAnswer> MockExamAnswers { get; set; } = new List<MockExamAnswer>();

    public virtual ICollection<MockExamFollowupQuestion> MockExamFollowupQuestions { get; set; } = new List<MockExamFollowupQuestion>();

    public virtual ExamStructure Structure { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
