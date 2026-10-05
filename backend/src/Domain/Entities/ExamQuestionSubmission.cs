using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class ExamQuestionSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid ExamQuestionId { get; set; }
    public string? AudioR2Url { get; set; }
    public string? AudioHashSha256 { get; set; }
    public string? TranscriptWhisper { get; set; }
    public string? TimestampsWhisper { get; set; }
    public int TimeSpentSeconds { get; set; } = 0;
    public decimal? AiScore { get; set; }
    public decimal? FinalScore { get; set; }
    public string GradingStatus { get; set; } = "pending";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public virtual StudentExamTicket Ticket { get; set; } = null!;
    public virtual ExamQuestion ExamQuestion { get; set; } = null!;
    public virtual ICollection<AiEvaluation> AiEvaluations { get; set; } = new List<AiEvaluation>();
}
