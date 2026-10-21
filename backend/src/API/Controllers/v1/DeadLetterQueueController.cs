using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;

namespace API.Controllers.v1;

/// <summary>
/// Controller giám sát và xử lý hàng đợi sự cố (Dead-Letter Queue - DLQ).
/// Thẩm quyền thuộc về Trưởng Bộ Môn (Department Head) và Quản trị viên (Admin).
/// Hỗ trợ truy vấn danh sách bài chấm lỗi và kích hoạt Replay thủ công.
/// </summary>
[ApiController]
[Route("api/v1/dlq")]
[Route("api/v1/department-head/dlq")]
[Authorize(Roles = "department_head,admin")]
public class DeadLetterQueueController : ControllerBase
{
    private readonly ISender _sender;

    public DeadLetterQueueController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy danh sách các tác vụ chấm điểm AI gặp lỗi trong hàng đợi Dead-Letter Queue.
    /// </summary>
    /// <param name="status">Trạng thái lọc: pending, resolved, abandoned (tùy chọn)</param>
    [HttpGet("tasks")]
    public async Task<IActionResult> GetDlqTasks([FromQuery] string? status = null)
    {
        var query = new GetDlqTasksQuery(status);
        var result = await _sender.Send(query);

        return Ok(result.Value);
    }

    /// <summary>
    /// Kích hoạt chấm bù (Replay) tác vụ DLQ đã thất bại bằng cách đẩy ngược lại vào IGradingQueueChannel.
    /// </summary>
    /// <param name="id">Mã định danh duy nhất của bản ghi DLQ</param>
    [HttpPost("tasks/{id:guid}/replay")]
    public async Task<IActionResult> ReplayDlqTask(Guid id)
    {
        var command = new ReplayDlqTaskCommand(id);
        var result = await _sender.Send(command);

        if (!result.IsSuccess)
        {
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
}
