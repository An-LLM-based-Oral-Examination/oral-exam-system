using System;

namespace OralExamination.Application.Features.DeadLetterQueue.DTOs;

public class DeadLetterQueueDto
{
    public Guid Id { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
