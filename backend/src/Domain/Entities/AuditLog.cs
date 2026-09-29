using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class AuditLog
{
    public Guid LogId { get; set; }

    public Guid UserId { get; set; }

    public string Action { get; set; } = null!;

    public string TargetTable { get; set; } = null!;

    public Guid? TargetId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
