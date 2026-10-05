using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý phân hệ Thi Thử Vấn Đáp Bấm Giờ (MF-02).
/// Kiểm soát hạn ngạch Daily Quota Guard (K = 3 lượt/ngày/môn).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class MockExamsController : ControllerBase
{
    private readonly ISender _sender;

    public MockExamsController(ISender sender)
    {
        _sender = sender;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        if (Request?.Headers != null && Request.Headers.TryGetValue("X-User-Id", out var headerVal) && Guid.TryParse(headerVal, out var headerId))
        {
            return headerId;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Tra cứu hạn ngạch thi thử trong ngày của sinh viên cho một môn học cụ thể (MF-02).
    /// Hạn ngạch tối đa K = 3 lượt/ngày/môn.
    /// </summary>
    [HttpGet("quota")]
    public async Task<IActionResult> GetQuota([FromQuery] Guid courseId, [FromQuery] Guid? studentId)
    {
        var currentUserId = GetCurrentUserId();
        var effectiveStudentId = currentUserId != Guid.Empty ? currentUserId : (studentId ?? Guid.Empty);

        var query = new GetMockExamQuotaQuery(courseId, effectiveStudentId);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/query-failed",
                title = "Bad Request",
                status = 400,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }
}
