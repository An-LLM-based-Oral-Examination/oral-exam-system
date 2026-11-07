using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ResolveDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;

namespace API.Controllers.v1;

public class ResolveDlqTaskRequest
{
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// Controller giám sát và xử lý hàng đợi sự cố (Dead-Letter Queue - DLQ).
/// Phân quyền nghiêm ngặt: Quản trị viên (admin) quản lý MF-01/02; Cán bộ Khảo thí (examination_officer) quản lý MF-04.
/// Cấm tuyệt đối Trưởng Bộ Môn (department_head) can thiệp vào DLQ.
/// </summary>
[ApiController]
[Authorize(Roles = "admin,examination_officer,lecturer")]
public class DeadLetterQueueController : ControllerBase
{
    private readonly ISender _sender;

    public DeadLetterQueueController(ISender sender)
    {
        _sender = sender;
    }

    #region Admin Endpoints (/api/v1/admin/dlq)

    /// <summary>
    /// Admin: Lấy danh sách các tác vụ DLQ gặp sự cố thuộc luồng Luyện tập (MF-01), Thi thử (MF-02) và Ca thi trong lớp (MF-05).
    /// </summary>
    [HttpGet]
    [Route("api/v1/admin/dlq")]
    [Route("api/v1/admin/dlq/tasks")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAdminDlqTasks([FromQuery] string? status = null, [FromQuery] string? flowType = null)
    {
        if (!string.IsNullOrWhiteSpace(flowType) &&
            !string.Equals(flowType.Trim(), "PRACTICE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(flowType.Trim(), "MOCK_EXAM", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(flowType.Trim(), "IN_CLASS_EXAM", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "FORBIDDEN: Quản trị viên (Admin) chỉ quản lý DLQ của luồng Luyện tập (PRACTICE), Thi thử (MOCK_EXAM) và Ca thi trong lớp (IN_CLASS_EXAM).",
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        List<string>? flowTypes = null;
        if (string.IsNullOrWhiteSpace(flowType))
        {
            flowTypes = new List<string> { "PRACTICE", "MOCK_EXAM", "IN_CLASS_EXAM" };
        }

        var query = new GetDlqTasksQuery(
            Status: status,
            FlowType: flowType?.Trim().ToUpperInvariant(),
            ManagedByRole: "admin",
            FlowTypes: flowTypes);

        var result = await _sender.Send(query);
        return Ok(result.Value);
    }

    /// <summary>
    /// Admin: Kích hoạt replay tác vụ DLQ của luồng Luyện tập hoặc Thi thử.
    /// </summary>
    [HttpPost]
    [Route("api/v1/admin/dlq/{id:guid}/replay")]
    [Route("api/v1/admin/dlq/tasks/{id:guid}/replay")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ReplayAdminDlqTask(Guid id)
    {
        var command = new ReplayDlqTaskCommand(id, GetCurrentUserId(), "admin");
        return await ExecuteReplayCommandAsync(id, command);
    }

    /// <summary>
    /// Admin: Đánh dấu đã xử lý tác vụ DLQ của luồng Luyện tập hoặc Thi thử kèm ghi chú.
    /// </summary>
    [HttpPost]
    [Route("api/v1/admin/dlq/{id:guid}/resolve")]
    [Route("api/v1/admin/dlq/tasks/{id:guid}/resolve")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ResolveAdminDlqTask(Guid id, [FromBody] ResolveDlqTaskRequest? request = null)
    {
        var command = new ResolveDlqTaskCommand(id, request?.ResolutionNotes, GetCurrentUserId(), "admin");
        return await ExecuteResolveCommandAsync(id, command);
    }

    #endregion

    #region Examination Officer Endpoints (/api/v1/examination-officer/dlq)

    /// <summary>
    /// Khảo thí: Lấy danh sách các tác vụ DLQ gặp sự cố thuộc luồng Thi thật phòng lab (MF-04).
    /// </summary>
    [HttpGet]
    [Route("api/v1/examination-officer/dlq")]
    [Route("api/v1/examination-officer/dlq/tasks")]
    [Authorize(Roles = "examination_officer")]
    public async Task<IActionResult> GetExamOfficerDlqTasks([FromQuery] string? status = null)
    {
        var query = new GetDlqTasksQuery(
            Status: status,
            FlowType: "OFFICIAL_EXAM",
            ManagedByRole: "examination_officer");

        var result = await _sender.Send(query);
        return Ok(result.Value);
    }

    /// <summary>
    /// Khảo thí: Kích hoạt replay tác vụ DLQ của bài thi thật MF-04.
    /// </summary>
    [HttpPost]
    [Route("api/v1/examination-officer/dlq/{id:guid}/replay")]
    [Route("api/v1/examination-officer/dlq/tasks/{id:guid}/replay")]
    [Authorize(Roles = "examination_officer")]
    public async Task<IActionResult> ReplayExamOfficerDlqTask(Guid id)
    {
        var command = new ReplayDlqTaskCommand(id, GetCurrentUserId(), "examination_officer");
        return await ExecuteReplayCommandAsync(id, command);
    }

    /// <summary>
    /// Khảo thí: Đánh dấu đã xử lý tác vụ DLQ của bài thi thật MF-04 kèm ghi chú.
    /// </summary>
    [HttpPost]
    [Route("api/v1/examination-officer/dlq/{id:guid}/resolve")]
    [Route("api/v1/examination-officer/dlq/tasks/{id:guid}/resolve")]
    [Authorize(Roles = "examination_officer")]
    public async Task<IActionResult> ResolveExamOfficerDlqTask(Guid id, [FromBody] ResolveDlqTaskRequest? request = null)
    {
        var command = new ResolveDlqTaskCommand(id, request?.ResolutionNotes, GetCurrentUserId(), "examination_officer");
        return await ExecuteResolveCommandAsync(id, command);
    }

    #endregion

    #region Shared Endpoints (/api/v1/dlq)

    /// <summary>
    /// Cổng dùng chung: Lấy danh sách DLQ tự động phân luồng theo role của caller (Admin thấy MF-01/02, Khảo thí thấy MF-04).
    /// </summary>
    [HttpGet]
    [Route("api/v1/dlq")]
    [Route("api/v1/dlq/tasks")]
    public async Task<IActionResult> GetDlqTasks([FromQuery] string? status = null, [FromQuery] string? flowType = null)
    {
        List<string>? flowTypes = null;
        string? effectiveFlowType = flowType?.Trim().ToUpperInvariant();
        string? managedByRole = null;

        if (User.IsInRole("admin") && !User.IsInRole("examination_officer"))
        {
            managedByRole = "admin";
            if (!string.IsNullOrWhiteSpace(effectiveFlowType) &&
                effectiveFlowType != "PRACTICE" &&
                effectiveFlowType != "MOCK_EXAM" &&
                effectiveFlowType != "IN_CLASS_EXAM")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "FORBIDDEN: Quản trị viên (Admin) chỉ quản lý DLQ của luồng Luyện tập (PRACTICE), Thi thử (MOCK_EXAM) và Ca thi trong lớp (IN_CLASS_EXAM).",
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (string.IsNullOrWhiteSpace(effectiveFlowType))
            {
                flowTypes = new List<string> { "PRACTICE", "MOCK_EXAM", "IN_CLASS_EXAM" };
            }
        }
        else if (User.IsInRole("examination_officer") && !User.IsInRole("admin"))
        {
            managedByRole = "examination_officer";
            if (!string.IsNullOrWhiteSpace(effectiveFlowType) &&
                effectiveFlowType != "OFFICIAL_EXAM")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "FORBIDDEN: Cán bộ Khảo thí chỉ quản lý DLQ của luồng Thi thật (OFFICIAL_EXAM).",
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            effectiveFlowType = "OFFICIAL_EXAM";
        }
        else if (User.IsInRole("lecturer") && !User.IsInRole("admin") && !User.IsInRole("examination_officer"))
        {
            managedByRole = "lecturer";
            if (!string.IsNullOrWhiteSpace(effectiveFlowType) &&
                effectiveFlowType != "IN_CLASS_EXAM")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "FORBIDDEN: Giảng viên chỉ được xem và xử lý DLQ của luồng Ca thi trong lớp (IN_CLASS_EXAM).",
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            effectiveFlowType = "IN_CLASS_EXAM";
        }

        var query = new GetDlqTasksQuery(
            Status: status,
            FlowType: effectiveFlowType,
            ManagedByRole: managedByRole,
            FlowTypes: flowTypes);

        var result = await _sender.Send(query);
        return Ok(result.Value);
    }

    /// <summary>
    /// Cổng dùng chung: Kích hoạt replay tác vụ DLQ.
    /// </summary>
    [HttpPost]
    [Route("api/v1/dlq/{id:guid}/replay")]
    [Route("api/v1/dlq/tasks/{id:guid}/replay")]
    public async Task<IActionResult> ReplayDlqTask(Guid id)
    {
        var callerRole = GetCurrentRole();
        var command = new ReplayDlqTaskCommand(id, GetCurrentUserId(), callerRole);
        return await ExecuteReplayCommandAsync(id, command);
    }

    /// <summary>
    /// Cổng dùng chung: Đánh dấu đã xử lý tác vụ DLQ kèm ghi chú.
    /// </summary>
    [HttpPost]
    [Route("api/v1/dlq/{id:guid}/resolve")]
    [Route("api/v1/dlq/tasks/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveDlqTask(Guid id, [FromBody] ResolveDlqTaskRequest? request = null)
    {
        var callerRole = GetCurrentRole();
        var command = new ResolveDlqTaskCommand(id, request?.ResolutionNotes, GetCurrentUserId(), callerRole);
        return await ExecuteResolveCommandAsync(id, command);
    }

    #endregion

    #region Private Helper Methods

    private async Task<IActionResult> ExecuteReplayCommandAsync(Guid id, ReplayDlqTaskCommand command)
    {
        var result = await _sender.Send(command);

        if (!result.IsSuccess)
        {
            if (result.Error.Contains("FORBIDDEN", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.Contains("không tìm thấy", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "DLQ Task Not Found",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Replay Failed",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(new
        {
            id,
            dlqId = id,
            status = "requeued",
            message = "Tác vụ DLQ đã được đưa vào hàng đợi chấm điểm thành công."
        });
    }

    private async Task<IActionResult> ExecuteResolveCommandAsync(Guid id, ResolveDlqTaskCommand command)
    {
        var result = await _sender.Send(command);

        if (!result.IsSuccess)
        {
            if (result.Error.Contains("FORBIDDEN", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.Contains("không tìm thấy", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "DLQ Task Not Found",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Resolve Failed",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(new
        {
            id,
            dlqId = id,
            status = "resolved",
            message = "Tác vụ DLQ đã được đánh dấu giải quyết thành công."
        });
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User?.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private string? GetCurrentRole()
    {
        if (User == null) return null;
        if (User.IsInRole("admin")) return "admin";
        if (User.IsInRole("examination_officer")) return "examination_officer";
        if (User.IsInRole("lecturer")) return "lecturer";
        if (User.IsInRole("department_head")) return "department_head";
        if (User.IsInRole("student")) return "student";
        if (User.IsInRole("proctor")) return "proctor";
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }

    #endregion
}
