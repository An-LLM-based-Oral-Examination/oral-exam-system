using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Application.Features.Practice.DTOs;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "student")]
public class PracticeController : ControllerBase
{
    private readonly ISender _sender;

    public PracticeController(ISender sender)
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

    private IActionResult UnauthorizedProblem() =>
        StatusCode(StatusCodes.Status401Unauthorized, new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Chưa xác thực danh tính sinh viên.",
            Type = "https://tools.ietf.org/html/rfc7807"
        });

    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession([FromBody] StartPracticeSessionRequest request)
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty)
        {
            return UnauthorizedProblem();
        }

        var isFullSession = request.IsFullSession
            || string.Equals(request.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.PracticeMode, "progressive", StringComparison.OrdinalIgnoreCase);

        var resolvedDifficulties = request.Difficulties is { Count: > 0 }
            ? request.Difficulties
            : request.SelectedDifficulties;

        var resolvedQuestionCount = request.QuestionCount ?? request.TotalQuestions;

        var resolvedDifficulty = !string.IsNullOrWhiteSpace(request.Difficulty)
            ? request.Difficulty
            : (resolvedDifficulties is { Count: > 0 } ? resolvedDifficulties[0] : null);

        var command = new StartPracticeSessionCommand(
            studentId,
            request.CourseId,
            resolvedDifficulty,
            resolvedQuestionCount,
            isFullSession,
            request.Topic,
            resolvedDifficulties
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error.Substring("FORBIDDEN:".Length),
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy tài nguyên.",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            });
        }
        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy chi tiết phiên luyện tập và danh sách câu trả lời đã nộp (kèm điểm số nếu đã chấm).
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty)
        {
            return UnauthorizedProblem();
        }

        var query = new OralExamination.Application.Features.Practice.Queries.GetPracticeSession.GetPracticeSessionQuery(
            sessionId,
            currentUserId,
            false
        );
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Session Not Found",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy câu hỏi tiếp theo cho chế độ [Per-Question] theo yêu cầu (On-Demand).
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/next-question")]
    public async Task<IActionResult> GetNextQuestion(Guid sessionId)
    {
        var targetStudentId = GetCurrentUserId();
        if (targetStudentId == Guid.Empty)
        {
            return UnauthorizedProblem();
        }

        var command = new NextQuestionCommand(sessionId, targetStudentId);
        var result = await _sender.Send(command);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        if (result.Error.StartsWith("SESSION_TIMEOUT_410:", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Contains("không có tương tác trong hơn", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
            {
                Status = StatusCodes.Status410Gone,
                Title = "Session Timed Out",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Session Not Found",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Yêu cầu không hợp lệ",
            Detail = result.Error,
            Type = "https://tools.ietf.org/html/rfc7807"
        });
    }

    [HttpPost("sessions/{sessionId}/answers")]
    public async Task<IActionResult> SubmitAnswer(Guid sessionId, [FromBody] SubmitPracticeAnswerRequest request)
    {
        var targetStudentId = GetCurrentUserId();
        if (targetStudentId == Guid.Empty)
        {
            return UnauthorizedProblem();
        }

        var command = new SubmitPracticeAnswerCommand(
            sessionId,
            request.QuestionId,
            targetStudentId,
            request.AnswerText,
            request.IsFollowUp,
            request.ParentAnswerId
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("SESSION_TIMEOUT_410:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không có tương tác trong hơn", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
                {
                    Status = StatusCodes.Status410Gone,
                    Title = "Session Timed Out",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("đã kết thúc", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("đã được nộp", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status409Conflict, new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }
        return Accepted(new { answerId = result.Value }); // HTTP 202
    }

    [HttpPost("sessions/{sessionId}/batch-submit")]
    [HttpPost("sessions/{sessionId}/batch-answers")]
    public async Task<IActionResult> SubmitBatch(Guid sessionId, [FromBody] SubmitPracticeBatchRequest request)
    {
        var targetStudentId = GetCurrentUserId();
        if (targetStudentId == Guid.Empty)
        {
            return UnauthorizedProblem();
        }

        var command = new OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch.SubmitPracticeBatchCommand(
            sessionId,
            targetStudentId,
            request.Answers
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("SESSION_TIMEOUT_410:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không có tương tác trong hơn", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
                {
                    Status = StatusCodes.Status410Gone,
                    Title = "Session Timed Out",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }
        return Accepted(); // HTTP 202
    }

    [HttpPost("sessions/{sessionId:guid}/complete")]
    public async Task<IActionResult> CompleteSession(Guid sessionId)
    {
        var targetStudentId = GetCurrentUserId();
        if (targetStudentId == Guid.Empty)
        {
            return UnauthorizedProblem();
        }

        var command = new OralExamination.Application.Features.Practice.Commands.CompletePracticeSession.CompletePracticeSessionCommand(
            sessionId,
            targetStudentId
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(new { message = "Phiên luyện tập đã kết thúc thành công." });
    }

    [HttpGet("student/history")]
    public async Task<IActionResult> GetStudentHistory()
    {
        var targetStudentId = GetCurrentUserId();
        if (targetStudentId == Guid.Empty)
        {
            return UnauthorizedProblem();
        }

        var query = new OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory.GetStudentPracticeHistoryQuery(targetStudentId);
        var result = await _sender.Send(query);

        return Ok(result.Value);
    }
}

public record SubmitPracticeBatchRequest(
    Guid StudentId,
    System.Collections.Generic.List<OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch.BatchAnswerDto> Answers
);

public record SubmitPracticeAnswerRequest(
    Guid QuestionId,
    Guid StudentId,
    string AnswerText,
    bool IsFollowUp,
    Guid? ParentAnswerId
);
