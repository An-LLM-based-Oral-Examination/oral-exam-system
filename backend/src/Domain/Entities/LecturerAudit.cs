using System;
using System.Collections.Generic;

using OralExamination.Domain.Common;

namespace OralExamination.Domain.Entities;

public partial class LecturerAudit : ILockableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid LecturerId { get; set; }
    public decimal OriginalAiScore { get; set; }
    public decimal AuditedScore { get; set; }
    public string OverrideReason { get; set; } = null!;
    public bool IsLocked { get; set; } = false;
    public DateTime AuditedAt { get; set; } = DateTime.UtcNow;

    public virtual StudentExamTicket Ticket { get; set; } = null!;
    public virtual User Lecturer { get; set; } = null!;
    public virtual ICollection<LecturerAuditDetail> Details { get; set; } = new List<LecturerAuditDetail>();
}
