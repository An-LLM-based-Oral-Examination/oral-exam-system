using System;

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
    public string? CotTrace { get; set; }
    public decimal? FinalScore { get; set; }
    public bool IsLocked { get; set; }
}

/// <summary>
/// DTO yêu cầu công bố điểm toàn bộ ca thi từ Giảng viên.
/// </summary>
public sealed class PublishGradesRequestDto
{
    public Guid? LecturerId { get; set; }
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
