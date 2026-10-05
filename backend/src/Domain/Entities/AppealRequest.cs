using System;
using OralExamination.Domain.Common;

namespace OralExamination.Domain.Entities;

public partial class AppealRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SessionId { get; set; }
    public Guid? SubmissionId { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = "PENDING";
    public Guid AssignedTo { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? Decision { get; set; }
    public decimal? OriginalScore { get; set; }
    public decimal? ProposedScore { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public virtual StudentExamTicket Ticket { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual OfficialExamSession Session { get; set; } = null!;
    public virtual ExamQuestionSubmission? Submission { get; set; }
    public virtual User AssignedToUser { get; set; } = null!;
    public virtual User? ReviewedByUser { get; set; }
}
