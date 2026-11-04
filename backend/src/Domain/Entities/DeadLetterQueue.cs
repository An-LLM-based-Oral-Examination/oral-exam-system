using System;

namespace OralExamination.Domain.Entities;

public partial class DeadLetterQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TaskType { get; set; } = null!;
    public Guid? CorrelationId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string ErrorMessage { get; set; } = null!;
    public int RetryCount { get; set; } = 0;
    public string Status { get; set; } = "pending";
    public string FlowType { get; set; } = "OFFICIAL_EXAM";
    public string ManagedByRole { get; set; } = "examination_officer";
    public Guid? ResolvedByUserId { get; set; }
    public virtual User? ResolvedByUser { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextRetryAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
