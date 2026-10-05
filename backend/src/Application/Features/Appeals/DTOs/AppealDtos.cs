using System;

namespace OralExamination.Application.Features.Appeals.DTOs;

/// <summary>
/// DTO phản hồi thông tin chi tiết của Đơn Phúc khảo Nội bộ (Internal Exam Appeal).
/// </summary>
public sealed class AppealResponseDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public Guid? SubmissionId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid AssignedTo { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public string? Decision { get; set; }
    public decimal? OriginalScore { get; set; }
    public decimal? ProposedScore { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// DTO nộp đơn phúc khảo từ phía Sinh viên.
/// </summary>
public sealed class CreateAppealRequestDto
{
    public Guid TicketId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? SubmissionId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO thẩm định và ra quyết định phúc khảo từ phía Trưởng Bộ Môn / Admin.
/// </summary>
public sealed class ReviewAppealRequestDto
{
    public string Decision { get; set; } = string.Empty;
    public decimal? ProposedScore { get; set; }
    public string ReviewNotes { get; set; } = string.Empty;
    public Guid? ReviewerId { get; set; }
}

