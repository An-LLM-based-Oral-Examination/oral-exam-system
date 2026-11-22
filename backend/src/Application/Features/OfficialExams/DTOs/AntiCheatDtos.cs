using System;

namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed class ExamAntiCheatLogDto
{
    public Guid Id { get; set; }
    public string ClientEventId { get; set; } = string.Empty;
    public Guid TicketId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Details { get; set; }
    public int ViolationCount { get; set; }
    public bool IsLocked { get; set; }
    public int MaxViolations { get; set; } = OfficialExamRules.MaxAllowedWindowBlurs;
    public int LockThreshold { get; set; } = OfficialExamRules.WindowBlurLockThreshold;
    public int RemainingStrikes { get; set; }
    public DateTime OccurredAt { get; set; }
}

public sealed class LogAntiCheatEventRequest
{
    public Guid TicketId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid? StudentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ClientEventId { get; set; }
}
