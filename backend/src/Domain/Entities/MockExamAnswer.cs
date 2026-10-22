using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class MockExamAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public int OrderIndex { get; set; } = 1;
    public string? AnswerText { get; set; }
    public int TimeTakenSeconds { get; set; } = 0;
    public string Status { get; set; } = "answered";
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Câu hỏi phụ (follow-up) gắn với câu trả lời chính.</summary>
    public bool IsFollowUp { get; set; } = false;

    /// <summary>Id câu trả lời chính khi đây là follow-up.</summary>
    public Guid? ParentAnswerId { get; set; }

    /// <summary>Nội dung câu hỏi phụ hiển thị cho sinh viên (null với câu chính).</summary>
    public string? PromptText { get; set; }

    public virtual MockExamSession Session { get; set; } = null!;
    public virtual PracticeQuestion Question { get; set; } = null!;
    public virtual MockExamAnswer? ParentAnswer { get; set; }
    public virtual ICollection<MockExamAnswer> FollowUpAnswers { get; set; } = new List<MockExamAnswer>();
    public virtual ICollection<AiEvaluation> AiEvaluations { get; set; } = new List<AiEvaluation>();
}
