using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Admin.Commands.CreateAdminNotification;
using OralExamination.Application.Features.Admin.Commands.CreateUser;
using OralExamination.Application.Features.Admin.Commands.DeleteAdminNotification;
using OralExamination.Application.Features.Admin.Commands.ResetUserPassword;
using OralExamination.Application.Features.Admin.Commands.UpdateSystemConfig;
using OralExamination.Application.Features.Admin.Commands.UpdateUserRole;
using OralExamination.Application.Features.Admin.Commands.UpdateUserStatus;
using OralExamination.Application.Features.Admin.Queries.GetAdminNotifications;
using OralExamination.Application.Features.Admin.Queries.GetAuditLogs;
using OralExamination.Application.Features.Admin.Queries.GetRuntimeDiagnostics;
using OralExamination.Application.Features.Admin.Queries.GetSystemConfigs;
using OralExamination.Application.Features.Admin.Queries.GetUsers;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản trị hệ thống dành cho Admin (BE-V05 / FE-09 User Management, BE-V08 System Configs, BE-V09b Audit Logs).
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "admin")]
public class AdminController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    private Guid GetCurrentUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var id) && id != Guid.Empty)
        {
            return id;
        }

        return Guid.Empty;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetUsersQuery(search, role, isActive, page, pageSize), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var result = await _sender.Send(new CreateUserCommand(
            adminId,
            request.Email,
            request.FullName,
            request.Password,
            request.Role,
            request.StudentCode,
            request.Department), cancellationToken);

        if (!result.IsSuccess)
        {
            return AdminProblem(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("users/{id:guid}/status")]
    [HttpPatch("users/{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var result = await _sender.Send(new UpdateUserStatusCommand(adminId, id, request.IsActive, request.Reason), cancellationToken);
        if (!result.IsSuccess)
        {
            return AdminProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPut("users/{id:guid}/role")]
    [HttpPatch("users/{id:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var result = await _sender.Send(new UpdateUserRoleCommand(adminId, id, request.Role), cancellationToken);
        if (!result.IsSuccess)
        {
            return AdminProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("users/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(
        [FromRoute] Guid id,
        [FromBody] ResetUserPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var result = await _sender.Send(
            new ResetUserPasswordCommand(adminId, id, request.TemporaryPassword, request.Reason),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return AdminProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? isRead = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetAdminNotificationsQuery(search, type, role, isRead, page, pageSize),
            cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("notifications")]
    public async Task<IActionResult> CreateNotification(
        [FromBody] CreateAdminNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var result = await _sender.Send(
            new CreateAdminNotificationCommand(
                adminId,
                request.TargetScope,
                request.Title,
                request.Message,
                request.Type,
                request.TargetRole,
                request.TargetUserId),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return AdminProblem(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpDelete("notifications/{id:guid}")]
    public async Task<IActionResult> DeleteNotification(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var result = await _sender.Send(new DeleteAdminNotificationCommand(adminId, id), cancellationToken);
        if (!result.IsSuccess)
        {
            return AdminProblem(result.Error);
        }

        return NoContent();
    }

    [HttpGet("system-configs")]
    [HttpGet("configs")]
    [HttpGet("/api/v1/system-configs")]
    public async Task<IActionResult> GetSystemConfigs(CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetSystemConfigsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("system-configs/{key}")]
    [HttpPut("configs/{key}")]
    [HttpPut("/api/v1/system-configs/{key}")]
    public async Task<IActionResult> UpdateSystemConfig(
        [FromRoute] string key,
        [FromBody] UpdateSystemConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        var result = await _sender.Send(new UpdateSystemConfigCommand(adminId, key, request.Value, request.Description), cancellationToken);
        if (!result.IsSuccess)
        {
            return AdminProblem(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityName = null,
        [FromQuery] string? action = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetAuditLogsQuery(entityName, action, userId, page, pageSize), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("diagnostics/runtime")]
    public async Task<IActionResult> GetRuntimeDiagnostics(CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetRuntimeDiagnosticsQuery(), cancellationToken);
        return Ok(result);
    }

    private IActionResult AdminProblem(string error)
    {
        var statusCode = error switch
        {
            var err when err.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var err when err.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var err when err.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Không đủ thẩm quyền quản trị.",
                StatusCodes.Status404NotFound => "Không tìm thấy tài nguyên.",
                StatusCodes.Status409Conflict => "Xung đột nghiệp vụ quản trị.",
                _ => "Dữ liệu cấu hình không hợp lệ."
            },
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/admin-{statusCode}"
        });
    }
}

public sealed record UpdateUserStatusRequest
{
    public bool IsActive { get; init; }
    public string? Reason { get; init; }
}

public sealed record UpdateUserRoleRequest
{
    public string Role { get; init; } = string.Empty;
}

public sealed record UpdateSystemConfigRequest
{
    public string Value { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    string Role,
    string? StudentCode = null,
    string? Department = null);

public sealed record ResetUserPasswordRequest
{
    public string TemporaryPassword { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public sealed record CreateAdminNotificationRequest
{
    public string TargetScope { get; init; } = "ALL_ACTIVE";
    public string? TargetRole { get; init; }
    public Guid? TargetUserId { get; init; }
    public string Type { get; init; } = "SYSTEM_ANNOUNCEMENT";
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

