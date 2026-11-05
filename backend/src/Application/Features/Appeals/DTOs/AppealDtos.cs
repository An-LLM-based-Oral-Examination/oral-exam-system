using System;
using System.Collections.Generic;

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
/// DTO thẩm định và ra quyết định phúc khảo từ phía Cán bộ Khảo thí / Admin.
/// Hỗ trợ đồng thời ProposedScore / FinalScore và ReviewNotes / DecisionReason.
/// </summary>
public sealed class ReviewAppealRequestDto
{
    public string Decision { get; set; } = string.Empty;

    private decimal? _proposedScore;
    public decimal? ProposedScore
    {
        get => _proposedScore ?? _finalScore;
        set => _proposedScore = value;
    }

    private decimal? _finalScore;
    public decimal? FinalScore
    {
        get => _finalScore ?? _proposedScore;
        set => _finalScore = value;
    }

    private string _reviewNotes = string.Empty;
    public string ReviewNotes
    {
        get => !string.IsNullOrWhiteSpace(_reviewNotes) ? _reviewNotes : (_decisionReason ?? string.Empty);
        set => _reviewNotes = value ?? string.Empty;
    }

    private string? _decisionReason;
    public string? DecisionReason
    {
        get => _decisionReason ?? (!string.IsNullOrWhiteSpace(_reviewNotes) ? _reviewNotes : null);
        set => _decisionReason = value;
    }

    public Guid? ReviewerId { get; set; }
}

/// <summary>
/// Yêu cầu Cán bộ Khảo thí phân công một giảng viên đang hoạt động chấm lại đơn phúc khảo.
/// Hỗ trợ đồng thời LecturerId và RegradeLecturerId.
/// </summary>
public sealed class AssignAppealLecturerRequestDto
{
    private Guid _lecturerId;
    public Guid LecturerId
    {
        get => _lecturerId != Guid.Empty ? _lecturerId : (_regradeLecturerId ?? Guid.Empty);
        set => _lecturerId = value;
    }

    private Guid? _regradeLecturerId;
    public Guid? RegradeLecturerId
    {
        get => _regradeLecturerId ?? (_lecturerId != Guid.Empty ? _lecturerId : null);
        set => _regradeLecturerId = value;
    }
}

/// <summary>
/// Kết quả chấm lại do chính giảng viên được phân công gửi lên.
/// Hỗ trợ đồng thời NewScore / ProposedScore và ReviewNotes / LecturerNotes.
/// </summary>
public sealed class RegradeAppealRequestDto
{
    private decimal? _newScore;
    public decimal NewScore
    {
        get => _newScore ?? _proposedScore ?? 0m;
        set => _newScore = value;
    }

    private decimal? _proposedScore;
    public decimal? ProposedScore
    {
        get => _proposedScore ?? _newScore;
        set => _proposedScore = value;
    }

    private string _reviewNotes = string.Empty;
    public string ReviewNotes
    {
        get => !string.IsNullOrWhiteSpace(_reviewNotes) ? _reviewNotes : (_lecturerNotes ?? string.Empty);
        set => _reviewNotes = value ?? string.Empty;
    }

    private string? _lecturerNotes;
    public string? LecturerNotes
    {
        get => _lecturerNotes ?? (!string.IsNullOrWhiteSpace(_reviewNotes) ? _reviewNotes : null);
        set => _lecturerNotes = value;
    }
}

/// <summary>
/// Đề xuất phân bổ chấm lại đơn phúc khảo tự động (định mức tối đa 24 bài/GV, cân bằng tải).
/// </summary>
public sealed class AppealAllocationProposalDto
{
    public int TotalAppeals { get; set; }
    public int TotalLecturers { get; set; }
    public bool ExceedsStandardQuota { get; set; }
    public List<LecturerAllocationSummaryDto> LecturerSummaries { get; set; } = new();
    public List<AppealAssignmentProposalItemDto> Items { get; set; } = new();
}

public sealed class LecturerAllocationSummaryDto
{
    public Guid LecturerId { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int AssignedCount { get; set; }
}

public sealed class AppealAssignmentProposalItemDto
{
    public Guid AppealId { get; set; }
    public Guid TicketId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public Guid? InitialExaminerUserId { get; set; }
    public string? InitialExaminerName { get; set; }
    public Guid? ProposedLecturerId { get; set; }
    public string? ProposedLecturerName { get; set; }
    public decimal? OriginalScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO yêu cầu phân công đồng loạt sau khi Khảo thí đã điều chỉnh thủ công.
/// </summary>
public sealed class BatchAssignAppealsRequestDto
{
    public List<AppealAssignmentItemDto> Assignments { get; set; } = new();
}

public sealed class AppealAssignmentItemDto
{
    public Guid AppealId { get; set; }

    private Guid _lecturerId;
    public Guid LecturerId
    {
        get => _lecturerId != Guid.Empty ? _lecturerId : (_regradeLecturerId ?? Guid.Empty);
        set => _lecturerId = value;
    }

    private Guid? _regradeLecturerId;
    public Guid? RegradeLecturerId
    {
        get => _regradeLecturerId ?? (_lecturerId != Guid.Empty ? _lecturerId : null);
        set => _regradeLecturerId = value;
    }
}

public sealed class BatchAssignAppealsResponseDto
{
    public int TotalAssigned { get; set; }
    public int AssignedCount => TotalAssigned;
    public int FailedCount => 0;
    public List<AppealResponseDto> AssignedAppeals { get; set; } = new();
}
