using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RealExamAnswer
{
    public Guid RealAnswerId { get; set; }

    public Guid ResultId { get; set; }

    public Guid QuestionId { get; set; }

    public Guid? FollowupQuestionId { get; set; }

    public Guid? ParentAnswerId { get; set; }

    public int RoundIndex { get; set; }

    public string? AudioUrl { get; set; }

    public string? Transcript { get; set; }

    public string? TimestampsJson { get; set; }

    public string? RubricSnapshot { get; set; }

    public string? AiEvidence { get; set; }

    public decimal? AiScore { get; set; }

    public decimal? FinalScore { get; set; }

    public virtual RealExamFollowupQuestion? FollowupQuestion { get; set; }

    public virtual ICollection<RealExamAnswer> InverseParentAnswer { get; set; } = new List<RealExamAnswer>();

    public virtual RealExamAnswer? ParentAnswer { get; set; }

    public virtual ExamQuestion Question { get; set; } = null!;

    public virtual ICollection<RealExamFollowupQuestion> RealExamFollowupQuestions { get; set; } = new List<RealExamFollowupQuestion>();

    public virtual RealExamResult Result { get; set; } = null!;
}
