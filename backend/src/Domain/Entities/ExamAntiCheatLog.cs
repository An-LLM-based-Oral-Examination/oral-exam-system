using System;
using OralExamination.Domain.Enums;

namespace OralExamination.Domain.Entities;

public partial class ExamAntiCheatLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid StudentId { get; set; }
    public string EventType { get; set; } = null!;
    public string Severity { get; set; } = AntiCheatSeverity.Medium;
    public string? Details { get; set; }
    public int ViolationCount { get; set; } = 1;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public virtual StudentExamTicket Ticket { get; set; } = null!;
    public virtual RealExamSessionShift Shift { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
}
