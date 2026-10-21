using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;

public sealed record OverrideCriterionScoreDto(
    Guid CriterionId,
    decimal AuditedScore,
    string? LecturerComment = null,
    decimal? AiScore = null,
    string? Comment = null);

public sealed record OverrideExamScoreResponseDto
{
    public Guid AuditId { get; init; }
    public Guid TicketId { get; init; }
    public Guid LecturerId { get; init; }
    public decimal OriginalAiScore { get; init; }
    public decimal AuditedScore { get; init; }
    public string OverrideReason { get; init; } = string.Empty;
    public string TicketStatus { get; init; } = string.Empty;
    public bool IsLocked { get; init; }
    public DateTime AuditedAt { get; init; }
}

/// <summary>
/// Command giảng viên hậu kiểm và ghi đè điểm bài thi thật Kiosk (MF-04 / BE-4.4b).
/// Bắt buộc cung cấp lý do ghi đè (override_reason >= 10 ký tự) và chỉ cho phép khi vé thi chưa bị khóa một chiều (is_locked == false).
/// </summary>
public sealed record OverrideExamScoreCommand(
    Guid ActorUserId,
    Guid TicketId,
    decimal AuditedScore,
    string OverrideReason,
    IReadOnlyList<OverrideCriterionScoreDto>? CriterionDetails = null) : IRequest<Result<OverrideExamScoreResponseDto>>;
