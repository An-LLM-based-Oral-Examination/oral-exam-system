using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Student.Commands.AcknowledgeOfficialGrade;
using OralExamination.Application.Features.Student.Queries.GetStudentDashboard;

namespace API.Controllers.v1;

/// <summary>
/// Controller phục vụ Student Portal (FE-10 / BE-V03):
/// - Tổng quan Dashboard học tập, hạn ngạch thi thử và lịch thi thật của sinh viên
/// - Xác nhận đồng ý kết quả điểm thi thật sau khi công bố (MF-04)
/// </summary>
[ApiController]
[Route("api/v1/student")]
[Authorize(Roles = "student,admin")]
public class StudentController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    private Guid GetCurrentStudentId(Guid? fallbackStudentId = null)
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var claimId) && claimId != Guid.Empty)
        {
            return claimId;
        }

        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-User-Id", out var headerVal) &&
            Guid.TryParse(headerVal, out var headerId) &&
            headerId != Guid.Empty)
        {
            return headerId;
        }

        return fallbackStudentId ?? Guid.Empty;
    }

    /// <summary>
    /// Lấy dữ liệu tổng hợp Student Dashboard (FE-10 / BE-V03):
    /// Luyện tập, Thi thử, Hạn ngạch trong ngày, Độ thông thạo tiêu chí CLO/Rubric, và Lịch thi thật.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedStudentId = GetCurrentStudentId(studentId);
        if (resolvedStudentId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Chưa xác thực danh tính sinh viên.",
                Detail = "Không tìm thấy thông tin định danh sinh viên trong phiên đăng nhập.",
                Type = "https://oralexam.fpt.edu.vn/errors/unauthorized"
            });
        }

        var result = await _sender.Send(new GetStudentDashboardQuery(resolvedStudentId), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy thông tin sinh viên.",
                Detail = result.Error,
                Type = "https://oralexam.fpt.edu.vn/errors/student-not-found"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên xác nhận đồng ý kết quả điểm thi thật đã công bố (ACKNOWLEDGED) trên Student Portal (MF-04).
    /// </summary>
    [HttpPost("official-exams/{ticketId:guid}/acknowledge-grade")]
    public async Task<IActionResult> AcknowledgeOfficialGrade(
        [FromRoute] Guid ticketId,
        [FromQuery] Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedStudentId = GetCurrentStudentId(studentId);
        if (resolvedStudentId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Chưa xác thực danh tính sinh viên.",
                Detail = "Không tìm thấy thông tin định danh sinh viên trong phiên đăng nhập.",
                Type = "https://oralexam.fpt.edu.vn/errors/unauthorized"
            });
        }

        var result = await _sender.Send(
            new AcknowledgeOfficialGradeCommand(ticketId, resolvedStudentId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy vé thi.",
                    Detail = result.Error,
                    Type = "https://oralexam.fpt.edu.vn/errors/ticket-not-found"
                });
            }

            if (result.Error.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Không có quyền truy cập.",
                    Detail = result.Error,
                    Type = "https://oralexam.fpt.edu.vn/errors/forbidden"
                });
            }

            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Không thể xác nhận điểm.",
                Detail = result.Error,
                Type = "https://oralexam.fpt.edu.vn/errors/acknowledge-grade-failed"
            });
        }

        return Ok(result.Value);
    }
}
