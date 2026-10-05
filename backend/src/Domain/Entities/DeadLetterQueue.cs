using System;

namespace OralExamination.Domain.Entities;

public partial class DeadLetterQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TaskType { get; set; } = null!;
    public string PayloadJson { get; set; } = "{}";
    public string ErrorMessage { get; set; } = null!;
    public int RetryCount { get; set; } = 0;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
