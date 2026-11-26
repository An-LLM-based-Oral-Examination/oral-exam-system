using OralExamination.Application.Features.MockExams.Commands;
using System;
using System.IO;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading;
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
using OralExamination.Application.Features.MockExams.Queries.ResumeMockExamSession;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý phân hệ Thi Thử Vấn Đáp Bấm Giờ (MF-02).
/// Kiểm soát hạn ngạch Daily Quota Guard do Quản trị viên (Admin) cấu hình.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/mock-exams")]
[Authorize(Roles = "student")]
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

        return Guid.Empty;
    }

    private bool TryResolveStudentActor(Guid? requestedStudentId, out Guid studentId, out IActionResult? errorResult)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
        {
            studentId = Guid.Empty;
            errorResult = Unauthorized(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/unauthorized",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Không tìm thấy thông tin định danh sinh viên."
            });
            return false;
        }

        if (requestedStudentId is { } requested && requested != Guid.Empty && requested != currentUserId)
        {
            studentId = Guid.Empty;
            errorResult = StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Bạn không có quyền truy cập dữ liệu thi thử của sinh viên khác."
            });
            return false;
        }

        studentId = currentUserId;
        errorResult = null;
        return true;
    }

    /// <summary>
    /// Tra cứu hạn ngạch thi thử trong ngày của sinh viên cho một môn học cụ thể (MF-02).
    /// Hạn ngạch tối đa do Quản trị viên (Admin) cấu hình trong Course.MaxMockExamsPerDay (mặc định K = 3).
    /// </summary>
    [HttpGet("quota")]
    public async Task<IActionResult> GetQuota(
        [FromQuery] Guid courseId,
        [FromQuery] Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveStudentActor(studentId, out var effectiveStudentId, out var authError))
        {
            return authError!;
        }

        var query = new GetMockExamQuotaQuery(courseId, effectiveStudentId);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error?.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = result.Error.Substring("FORBIDDEN:".Length)
                });
            }

            if (result.Error?.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/not-found",
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase)
                        ? result.Error["NOT_FOUND:".Length..].Trim()
                        : result.Error
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

    /// <summary>
    /// Khởi tạo ca thi thử bấm giờ có hạn ngạch (MF-02).
    /// Hạn ngạch K lượt/ngày do Quản trị viên (Admin) cấu hình.
    /// Bốc đề từ kho practice_questions theo ma trận Bloom (cô lập 100% với exam_questions).
    /// </summary>
    [HttpPost("sessions")]
    [Authorize(Roles = "student")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartMockExamRequest request,
        CancellationToken cancellationToken = default)
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

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error?.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = result.Error.Substring("FORBIDDEN:".Length)
                });
            }

            if (result.Error?.StartsWith("QUOTA_EXCEEDED:") == true)
            {
                var detail = result.Error.Substring("QUOTA_EXCEEDED:".Length);
                var quotaProblem = new QuotaExceededProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/quota-exceeded",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = detail
                };
                quotaProblem.Extensions["errorCode"] = "QUOTA_EXCEEDED";
                return StatusCode(StatusCodes.Status429TooManyRequests, quotaProblem);
            }

            if (result.Error?.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Conflict(new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/conflict",
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = result.Error.Substring("CONFLICT:".Length)
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
    [Authorize(Roles = "student")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] Guid id,
        [FromForm] SubmitMockExamAnswerFormRequest request,
        CancellationToken cancellationToken = default)
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
                DirectTranscript: request.Transcript,
                AnswerId: request.AnswerId
            );

            var result = await _sender.Send(command, cancellationToken);

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

                if (result.Error?.Contains("không có quyền", StringComparison.OrdinalIgnoreCase) == true ||
                    result.Error?.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                        title = "Forbidden",
                        status = 403,
                        detail = result.Error
                    });
                }

                if (result.Error?.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) == true ||
                    result.Error?.Contains("đã được nộp", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Conflict(new
                    {
                        type = "https://oralexam.fpt.edu.vn/errors/conflict",
                        title = "Conflict",
                        status = 409,
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
    [Authorize(Roles = "student")]
    public async Task<IActionResult> SubmitSession(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
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

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error?.StartsWith("DEFERRED_GRADING:", StringComparison.OrdinalIgnoreCase) == true)
            {
                var deferred = await _sender.Send(new GetMockExamSessionDetailQuery(
                    SessionId: id,
                    StudentId: currentUserId,
                    IsElevatedRole: false,
                    AttemptLazyGrading: false), cancellationToken);
                if (deferred.IsSuccess)
                {
                    return StatusCode(StatusCodes.Status202Accepted, deferred.Value);
                }
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

            if (result.Error?.Contains("không có quyền", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                    title = "Forbidden",
                    status = 403,
                    detail = result.Error
                });
            }

            if (result.Error?.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.Contains("đã được nộp", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Conflict(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/conflict",
                    title = "Conflict",
                    status = 409,
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
    /// Tạm dừng đồng hồ ca thi thử đang in_progress của sinh viên.
    /// </summary>
    [HttpPost("sessions/{id:guid}/pause")]
    [Authorize(Roles = "student")]
    public async Task<IActionResult> PauseSession([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/unauthorized",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Không tìm thấy thông tin định danh sinh viên."
            });
        }

        var result = await _sender.Send(new PauseMockExamSessionCommand(id, studentId), cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/not-found",
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error
                });
            }

            if (result.Error?.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = result.Error
                });
            }

            if (result.Error?.StartsWith("SESSION_EXPIRED:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/session-expired",
                    Title = "Gone",
                    Status = StatusCodes.Status410Gone,
                    Detail = result.Error
                });
            }

            if (result.Error?.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Conflict(new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/conflict",
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = result.Error
                });
            }

            return BadRequest(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/command-failed",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error
            });
        }

        return Ok(new { remainingSeconds = result.Value });
    }

    /// <summary>
    /// Khôi phục ca thi thử đang in_progress để tiếp tục làm bài (không tạo ca mới).
    /// </summary>
    [HttpPost("sessions/{id:guid}/resume")]
    [Authorize(Roles = "student")]
    public async Task<IActionResult> ResumeSession([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/unauthorized",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Không tìm thấy thông tin định danh sinh viên."
            });
        }

        var result = await _sender.Send(new ResumeMockExamSessionQuery(id, currentUserId), cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("không có quyền", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                    Title = "Forbidden",
                    Detail = result.Error,
                    Status = StatusCodes.Status403Forbidden
                });
            }

            if (result.Error?.Contains("Không tìm thấy") == true)
            {
                return NotFound(new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/not-found",
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error
                });
            }

            if (result.Error?.StartsWith("SESSION_EXPIRED:", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.Contains("hết thời gian", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/session-expired",
                    Title = "Gone",
                    Status = StatusCodes.Status410Gone,
                    Detail = result.Error
                });
            }

            if (result.Error?.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Conflict(new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/conflict",
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = result.Error
                });
            }

            return BadRequest(new ProblemDetails
            {
                Type = "https://oralexam.fpt.edu.vn/errors/query-failed",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tra cứu danh sách lịch sử thi thử của sinh viên có phân trang và lọc theo môn học (MF-02 - FE-02).
    /// </summary>
    [HttpGet("history")]
    [Authorize(Roles = "student")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid? courseId = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveStudentActor(studentId, out var effectiveStudentId, out var authError))
        {
            return authError!;
        }

        var query = new GetMockExamHistoryQuery(
            StudentId: effectiveStudentId,
            CourseId: courseId,
            PageIndex: pageIndex,
            PageSize: pageSize,
            Status: string.IsNullOrWhiteSpace(status) ? null : status.Trim()
        );

        var result = await _sender.Send(query, cancellationToken);

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
    [Authorize(Roles = "student")]
    public async Task<IActionResult> GetHistoryDetail(
        [FromRoute] Guid sessionId,
        [FromQuery] Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveStudentActor(studentId, out var effectiveStudentId, out var authError))
        {
            return authError!;
        }

        var query = new GetMockExamSessionDetailQuery(
            SessionId: sessionId,
            StudentId: effectiveStudentId,
            IsElevatedRole: false
        );

        var result = await _sender.Send(query, cancellationToken);

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

            if (result.Error?.Contains("không có quyền", StringComparison.OrdinalIgnoreCase) == true ||
                result.Error?.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                    title = "Forbidden",
                    status = 403,
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

    private sealed class QuotaExceededProblemDetails : ProblemDetails
    {
        [JsonIgnore]
        public int? status => Status;

        [JsonIgnore]
        public string? detail => Detail;
    }
}

/// <summary>
/// Model bind dữ liệu nộp câu trả lời Voice-First từ Client (multipart/form-data).
/// </summary>
public class SubmitMockExamAnswerFormRequest
{
    public Guid QuestionId { get; set; }
    public Guid? AnswerId { get; set; }
    public IFormFile? File { get; set; }
    public int TimeTakenSeconds { get; set; }
    public string? Transcript { get; set; }
}
