using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Student.Commands.AcknowledgeOfficialGrade;
using OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;
using OralExamination.Application.Features.Student.Queries.GetStudentDashboard;

namespace API.Controllers.v1;

/// <summary>
/// Controller phục vụ Student Portal (FE-10 / BE-V03):
/// - Tổng quan Dashboard học tập, hạn ngạch thi thử và lịch thi thật của sinh viên
/// - Xem chi tiết điểm thi thật và Scorecard sau khi công bố (MF-04)
/// - Xác nhận đồng ý kết quả điểm thi thật sau khi công bố (MF-04)
/// </summary>
[ApiController]
[Route("api/v1/student")]
[Authorize(Roles = "student,lecturer,department_head,admin")]
public class StudentController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    private Guid GetCurrentStudentId(Guid? fallbackStudentId = null)
    {
        var claimValue = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User?.FindFirstValue("sub");

        var hasClaimId = Guid.TryParse(claimValue, out var claimId) && claimId != Guid.Empty;
        var isElevatedRole = User?.IsInRole("admin") == true
                             || User?.IsInRole("department_head") == true
                             || User?.IsInRole("lecturer") == true;

        // Nếu là Admin / Trưởng BM / Giảng viên và có truyền ?studentId=... thì ưu tiên lấy studentId được truyền vào
        if (isElevatedRole && fallbackStudentId.HasValue && fallbackStudentId.Value != Guid.Empty)
        {
            return fallbackStudentId.Value;
        }

        return hasClaimId ? claimId : Guid.Empty;
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
    /// Sinh viên xem chi tiết điểm thi thật và Scorecard theo tiêu chí Rubric sau khi giảng viên đã công bố điểm (MF-04).
    /// </summary>
    [HttpGet("official-exams/{ticketId:guid}/grade")]
    [HttpGet("/api/v1/official-exams/tickets/{ticketId:guid}/grade")]
    public async Task<IActionResult> GetOfficialExamGrade(
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
            new GetOfficialExamGradeQuery(ticketId, resolvedStudentId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy vé thi.",
                    Detail = result.Error,
                    Type = "https://oralexam.fpt.edu.vn/errors/ticket-not-found"
                });
            }

            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
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
                Title = "Chưa thể xem điểm thi.",
                Detail = result.Error,
                Type = "https://oralexam.fpt.edu.vn/errors/grade-not-published"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên xác nhận đồng ý kết quả điểm thi thật đã công bố (ACKNOWLEDGED) trên Student Portal (MF-04).
    /// </summary>
    [Authorize(Roles = "student,admin")]
    [HttpPost("official-exams/{ticketId:guid}/acknowledge-grade")]
    [HttpPost("/api/v1/official-exams/tickets/{ticketId:guid}/acknowledge")]
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
