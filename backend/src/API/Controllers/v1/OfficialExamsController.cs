using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý phân hệ Thi Thật Phòng Lab, Hậu kiểm & Công bố điểm (MF-04).
/// Chuẩn RESTful RFC 7807 Problem Details.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class OfficialExamsController : ControllerBase
{
    private readonly ISender _sender;

    public OfficialExamsController(ISender sender)
    {
        _sender = sender;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        if (Request.Headers.TryGetValue("X-User-Id", out var headerVal) && Guid.TryParse(headerVal, out var headerId))
        {
            return headerId;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Giảng viên truy xuất toàn bộ bằng chứng hậu kiểm (Evidence Panel) của một ca thi phòng Lab (MF-04).
    /// Bao gồm Audio URL trên Cloudflare R2, mã băm SHA-256, bản bóc băng Whisper,
    /// điểm AI, độ tin cậy, cờ phân loại nghi ngờ (AI Doubt Guard) và chuỗi suy luận CoT.
    /// </summary>
    [HttpGet("shifts/{shiftId:guid}/audit-evidence")]
    [HttpGet("shifts/{shiftId:guid}/audit-submissions")]
    public async Task<IActionResult> GetAuditEvidence(Guid shiftId)
    {
        var query = new GetAuditEvidenceQuery(shiftId);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            return NotFound(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/shift-not-found",
                title = "Not Found",
                status = 404,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên thực hiện Công Bố Điểm (Publish Grades) cho ca thi phòng Lab (MF-04).
    /// BẮT BUỘC: 100% sinh viên trong ca thi phải có điểm hoàn chỉnh.
    /// Kích hoạt cơ chế Khóa Một Chiều (One-Way Lock is_locked = true), ngăn chặn mọi hành vi chỉnh sửa sau đó.
    /// </summary>
    [HttpPost("shifts/{shiftId:guid}/publish-grades")]
    public async Task<IActionResult> PublishGrades(Guid shiftId, [FromBody] PublishGradesRequestDto? request)
    {
        var currentUserId = GetCurrentUserId();
        var lecturerId = currentUserId != Guid.Empty ? currentUserId : (request?.LecturerId ?? Guid.Empty);

        var command = new PublishGradesCommand(shiftId, lecturerId);
        var result = await _sender.Send(command);

        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/publish-failed",
                title = "Unprocessable Entity",
                status = 422,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }
}
