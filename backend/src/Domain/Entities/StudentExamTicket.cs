using System;
using System.Collections.Generic;

using OralExamination.Domain.Common;
using OralExamination.Domain.Enums;

namespace OralExamination.Domain.Entities;

public partial class StudentExamTicket : ILockableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShiftId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? ExamSetId { get; set; }
    public int SeatNumber { get; set; }
    public string? IpAddress { get; set; }
    public string Status { get; set; } = ExamTicketStatus.Scheduled;
    public bool IsLocked { get; set; } = false;
    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }

    public virtual RealExamSessionShift Shift { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual ExamSet? ExamSet { get; set; }
    public virtual ICollection<ExamQuestionSubmission> Submissions { get; set; } = new List<ExamQuestionSubmission>();
    public virtual LecturerAudit? LecturerAudit { get; set; }
}
