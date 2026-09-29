using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class PracticeQuestion
{
    public Guid QuestionId { get; set; }

    public int CourseId { get; set; }

    public string Content { get; set; } = null!;

    public string Difficulty { get; set; } = null!;

    public string ModelAnswer { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<MockExamAnswer> MockExamAnswers { get; set; } = new List<MockExamAnswer>();

    public virtual ICollection<MockExamFollowupQuestion> MockExamFollowupQuestions { get; set; } = new List<MockExamFollowupQuestion>();

    public virtual ICollection<PracticeAnswer> PracticeAnswers { get; set; } = new List<PracticeAnswer>();

    public virtual ICollection<RubricItem> RubricItems { get; set; } = new List<RubricItem>();
}
