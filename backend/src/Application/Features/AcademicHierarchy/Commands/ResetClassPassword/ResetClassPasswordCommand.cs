using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;

/// <summary>
/// DTO yêu cầu đặt lại / thay đổi mật khẩu tham gia lớp học phần (Giảng viên / Trưởng BM / Admin).
/// </summary>
public sealed record ResetClassPasswordRequest(
    string? NewPassword = null,
    string? NewJoinCode = null,
    bool? IsJoinEnabled = null);

/// <summary>
/// DTO phản hồi khi đặt lại mật khẩu hoặc mã tham gia lớp học phần.
/// </summary>
public sealed record ResetClassPasswordResponseDto(
    Guid ClassId,
    string Code,
    string Name,
    string JoinCode,
    bool HasPassword,
    bool IsJoinEnabled,
    DateTime UpdatedAt);

/// <summary>
/// CQRS Command cho phép Giảng viên phụ trách lớp, Trưởng bộ môn của môn học hoặc Admin đổi mật khẩu lớp học.
/// </summary>
public sealed record ResetClassPasswordCommand(
    Guid ClassId,
    Guid ActorUserId,
    string? NewPassword = null,
    string? NewJoinCode = null,
    bool? IsJoinEnabled = null,
    string? ActorRole = null) : IRequest<Result<ResetClassPasswordResponseDto>>;
