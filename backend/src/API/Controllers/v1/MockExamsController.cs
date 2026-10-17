using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý phân hệ Thi Thử Vấn Đáp Bấm Giờ (MF-02).
/// Kiểm soát hạn ngạch Daily Quota Guard do Quản trị viên (Admin) cấu hình.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/mock-exams")]
[Authorize(Roles = "student,lecturer,department_head,admin")]
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
    /// Hạn ngạch tối đa do Quản trị viên (Admin) cấu hình trong Course.MaxMockExamsPerDay (mặc định K = 3).
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

    /// <summary>
    /// Khởi tạo ca thi thử bấm giờ có hạn ngạch (MF-02).
    /// Hạn ngạch K lượt/ngày do Quản trị viên (Admin) cấu hình.
    /// Bốc đề từ kho practice_questions theo ma trận Bloom (cô lập 100% với exam_questions).
    /// </summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession([FromBody] StartMockExamRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/unauthorized",
                title = "Unauthorized",
                status = 401,
                detail = "Không tìm thấy thông tin định danh sinh viên."
            });
        }

        var command = new StartMockExamCommand(
            CourseId: request.CourseId,
            StudentId: currentUserId,
            HasFollowUp: request.HasFollowUp
        );

        var result = await _sender.Send(command);

        if (!result.IsSuccess)
        {
            if (result.Error?.StartsWith("QUOTA_EXCEEDED:") == true)
            {
                var detail = result.Error.Substring("QUOTA_EXCEEDED:".Length);
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/quota-exceeded",
                    title = "Too Many Requests",
                    status = 429,
                    detail = detail
                });
            }

            if (result.Error?.Contains("không tồn tại") == true)
            {
                return NotFound(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/not-found",
                    title = "Not Found",
                    status = 404,
                    detail = result.Error
                });
            }

            return BadRequest(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/command-failed",
                title = "Bad Request",
                status = 400,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Nộp câu trả lời Voice-First cho một câu hỏi trong ca thi thử (MF-02).
    /// Stream âm thanh qua Whisper STT trong bộ nhớ RAM, tuyệt đối không lưu file lên Cloudflare R2.
    /// </summary>
    [HttpPost("sessions/{id:guid}/answers")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] Guid id,
        [FromForm] SubmitMockExamAnswerFormRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/unauthorized",
                title = "Unauthorized",
                status = 401,
                detail = "Không tìm thấy thông tin định danh sinh viên."
            });
        }

        Stream? audioStream = null;
        string? fileName = null;
        string? contentType = null;

        if (request.File != null && request.File.Length > 0)
        {
            audioStream = request.File.OpenReadStream();
            fileName = request.File.FileName;
            contentType = request.File.ContentType;
        }

        try
        {
            var command = new SubmitMockExamAnswerCommand(
                SessionId: id,
                StudentId: currentUserId,
                QuestionId: request.QuestionId,
                AudioStream: audioStream,
                FileName: fileName,
                ContentType: contentType,
                TimeTakenSeconds: request.TimeTakenSeconds,
                DirectTranscript: request.Transcript
            );

            var result = await _sender.Send(command);

            if (!result.IsSuccess)
            {
                if (result.Error?.Contains("không tồn tại") == true)
                {
                    return NotFound(new
                    {
                        type = "https://oralexam.fpt.edu.vn/errors/not-found",
                        title = "Not Found",
                        status = 404,
                        detail = result.Error
                    });
                }

                if (result.Error?.Contains("hết thời gian") == true)
                {
                    return StatusCode(StatusCodes.Status410Gone, new
                    {
                        type = "https://oralexam.fpt.edu.vn/errors/session-expired",
                        title = "Gone",
                        status = 410,
                        detail = result.Error
                    });
                }

                return BadRequest(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/command-failed",
                    title = "Bad Request",
                    status = 400,
                    detail = result.Error
                });
            }

            return Ok(result.Value);
        }
        finally
        {
            if (audioStream != null)
            {
                await audioStream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Nộp bài thi thử chính thức và nhận Bảng điểm Instant Scorecard tức thì (MF-02).
    /// Kiểm soát Server Master Timer, tự động khóa ca thi và chấm các câu đã nộp nếu quá hạn.
    /// </summary>
    [HttpPost("sessions/{id:guid}/submit")]
    public async Task<IActionResult> SubmitSession([FromRoute] Guid id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/unauthorized",
                title = "Unauthorized",
                status = 401,
                detail = "Không tìm thấy thông tin định danh sinh viên."
            });
        }

        var command = new SubmitMockExamSessionCommand(
            SessionId: id,
            StudentId: currentUserId
        );

        var result = await _sender.Send(command);

        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("không tồn tại") == true)
            {
                return NotFound(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/not-found",
                    title = "Not Found",
                    status = 404,
                    detail = result.Error
                });
            }

            return BadRequest(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/command-failed",
                title = "Bad Request",
                status = 400,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tra cứu danh sách lịch sử thi thử của sinh viên có phân trang và lọc theo môn học (MF-02 - FE-02).
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid? courseId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/unauthorized",
                title = "Unauthorized",
                status = 401,
                detail = "Không tìm thấy thông tin định danh sinh viên."
            });
        }

        var query = new GetMockExamHistoryQuery(
            StudentId: currentUserId,
            CourseId: courseId,
            PageIndex: pageIndex,
            PageSize: pageSize
        );

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

    /// <summary>
    /// Tra cứu chi tiết một bài thi thử trong quá khứ kèm Bảng điểm Scorecard (MF-02 - FE-02).
    /// </summary>
    [HttpGet("history/{sessionId:guid}")]
    public async Task<IActionResult> GetHistoryDetail([FromRoute] Guid sessionId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/unauthorized",
                title = "Unauthorized",
                status = 401,
                detail = "Không tìm thấy thông tin định danh sinh viên."
            });
        }

        var query = new GetMockExamSessionDetailQuery(
            SessionId: sessionId,
            StudentId: currentUserId
        );

        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("Không tìm thấy") == true)
            {
                return NotFound(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/not-found",
                    title = "Not Found",
                    status = 404,
                    detail = result.Error
                });
            }

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

/// <summary>
/// Model bind dữ liệu nộp câu trả lời Voice-First từ Client (multipart/form-data).
/// </summary>
public class SubmitMockExamAnswerFormRequest
{
    public Guid QuestionId { get; set; }
    public IFormFile? File { get; set; }
    public int TimeTakenSeconds { get; set; }
    public string? Transcript { get; set; }
}

