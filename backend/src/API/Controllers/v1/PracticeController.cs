using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Application.Features.Practice.DTOs;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
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

        if (Request?.Headers != null && Request.Headers.TryGetValue("X-User-Id", out var headerVal) && Guid.TryParse(headerVal, out var headerId))
        {
            return headerId;
        }

        return Guid.Empty;
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession([FromBody] StartPracticeSessionRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var studentId = (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty)
            ? request.StudentId.Value
            : (currentUserId != Guid.Empty ? currentUserId : Guid.Empty);

        var command = new StartPracticeSessionCommand(
            studentId,
            request.CourseId,
            request.Difficulty,
            request.QuestionCount,
            request.IsFullSession,
            request.Topic
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
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
        var query = new OralExamination.Application.Features.Practice.Queries.GetPracticeSession.GetPracticeSessionQuery(sessionId);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            return NotFound(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/session-not-found",
                title = "Not Found",
                status = 404,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    [HttpPost("sessions/{sessionId}/answers")]
    public async Task<IActionResult> SubmitAnswer(Guid sessionId, [FromBody] SubmitPracticeAnswerRequest request)
    {
        var command = new SubmitPracticeAnswerCommand(
            sessionId,
            request.QuestionId,
            request.StudentId,
            request.AnswerText,
            request.IsFollowUp,
            request.ParentAnswerId
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }
        return Accepted(new { answerId = result.Value }); // HTTP 202
    }

    [HttpPost("sessions/{sessionId}/batch-submit")]
    public async Task<IActionResult> SubmitBatch(Guid sessionId, [FromBody] SubmitPracticeBatchRequest request)
    {
        var command = new OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch.SubmitPracticeBatchCommand(
            sessionId,
            request.StudentId,
            request.Answers
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }
        return Accepted(); // HTTP 202
    }

    [HttpPost("sessions/{sessionId:guid}/complete")]
    public async Task<IActionResult> CompleteSession(Guid sessionId, [FromQuery] Guid? studentId = null)
    {
        var currentUserId = GetCurrentUserId();
        var targetStudentId = (studentId.HasValue && studentId.Value != Guid.Empty)
            ? studentId.Value
            : currentUserId;

        var command = new OralExamination.Application.Features.Practice.Commands.CompletePracticeSession.CompletePracticeSessionCommand(
            sessionId,
            targetStudentId
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { message = "Phiên luyện tập đã kết thúc thành công." });
    }

    [HttpGet("student/history")]
    public async Task<IActionResult> GetStudentHistory([FromQuery] Guid? studentId)
    {
        var currentUserId = GetCurrentUserId();
        var targetStudentId = studentId.HasValue && studentId.Value != Guid.Empty
            ? studentId.Value
            : currentUserId;

        if (targetStudentId == Guid.Empty)
        {
            return BadRequest(new { error = "Không tìm thấy thông tin sinh viên." });
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
