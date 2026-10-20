using System;
using System.Text.Json.Serialization;

namespace OralExamination.Application.Features.OfficialExams.DTOs;

/// <summary>
/// DTO chứa đầy đủ bằng chứng kiểm toán (Evidence Panel) cho từng thí sinh trong ca thi phòng Lab (MF-04).
/// Bao gồm Audio URL trên R2, mã băm SHA-256, bản bóc băng Whisper, điểm AI, độ tin cậy,
/// cờ phát hiện nghi ngờ AI Doubt Guard, chuỗi suy luận CoT và điểm chốt chính thức.
/// </summary>
public sealed class AuditEvidenceItemDto
{
    public Guid TicketId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AudioR2Url { get; set; }
    public string? AudioHashSha256 { get; set; }
    public string? TranscriptWhisper { get; set; }
    public decimal? AiScore { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspiciousReason { get; set; }
    public string? AiChainOfThought { get; set; }
    [JsonIgnore]
    public string? CotTrace
    {
        get => AiChainOfThought;
        set => AiChainOfThought = value;
    }
    public decimal? FinalScore { get; set; }
    public bool IsLocked { get; set; }
}

/// <summary>
/// DTO phản hồi kết quả công bố điểm toàn bộ ca thi và kích hoạt One-Way Lock.
/// </summary>
public sealed class PublishGradesResponseDto
{
    public Guid ShiftId { get; set; }
    public int TotalPublished { get; set; }
    public DateTime PublishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CreateOfficialExamSessionRequest
{
    public Guid CourseId { get; set; }
    public Guid ExamStructureId { get; set; }
    public string? Title { get; set; }
    public DateOnly ExamDate { get; set; }
    public bool HasFollowUp { get; set; }
    public int MaxFollowUpQuestions { get; set; } = 2;
    public string? ExamInputMode { get; set; }
    public int TranscriptBufferSeconds { get; set; } = 60;
}

public sealed record OfficialExamSessionResponse(
    Guid Id,
    Guid CourseId,
    Guid ExamStructureId,
    string Title,
    DateOnly ExamDate,
    bool HasFollowUp,
    int MaxFollowUpQuestions,
    string ExamInputMode,
    int TranscriptBufferSeconds,
    string Status);

public sealed class OpenExamShiftRequest
{
    public Guid SessionId { get; set; }
    public string? ShiftName { get; set; }
    public string? RoomLab { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid ProctorUserId { get; set; }
}

public sealed record ExamShiftResponse(
    Guid Id,
    Guid SessionId,
    string ShiftName,
    string RoomLab,
    DateTime StartTime,
    DateTime EndTime,
    Guid? ProctorUserId,
    string Status);

public sealed class AssignExamTicketRequest
{
    public Guid ShiftId { get; set; }
    public Guid StudentId { get; set; }
    public int SeatNumber { get; set; }
    public string? IpAddress { get; set; }
}

public sealed record ExamTicketResponse(
    Guid Id,
    Guid ShiftId,
    Guid StudentId,
    int SeatNumber,
    string? IpAddress,
    string Status,
    DateTime CheckedInAt);

public sealed record StudentWorkstationTicketResponse(
    Guid Id,
    Guid ShiftId,
    string ShiftName,
    string RoomLab,
    DateTime StartTime,
    DateTime EndTime,
    string ShiftStatus,
    string SessionTitle,
    Guid StudentId,
    int SeatNumber,
    string Status);
