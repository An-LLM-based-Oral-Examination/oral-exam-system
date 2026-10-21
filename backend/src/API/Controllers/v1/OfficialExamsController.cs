using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftExcel;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamShifts;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý phân hệ Thi Thật Phòng Lab, Hậu kiểm & Công bố điểm (MF-04).
/// Chuẩn RESTful RFC 7807 Problem Details.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/official-exams")]
[Authorize(Roles = "lecturer,department_head,proctor,admin")]
public class OfficialExamsController : ControllerBase
{
    private readonly ISender _sender;

    public OfficialExamsController(ISender sender)
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

    private Guid GetAuthenticatedUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Lấy danh sách kỳ thi thật (OfficialExamSession). Hỗ trợ alias seasons & sessions.
    /// </summary>
    [HttpGet("sessions")]
    [HttpGet("seasons")]
    [Authorize(Roles = "lecturer,department_head,proctor,admin")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? courseId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetOfficialExamSessionsQuery(courseId, status), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy danh sách ca thi phòng Lab (RealExamSessionShift). Hỗ trợ lọc theo sessionId, proctorUserId, status.
    /// </summary>
    [HttpGet("shifts")]
    [HttpGet("seasons/{sessionId:guid}/shifts")]
    [Authorize(Roles = "lecturer,department_head,proctor,admin")]
    public async Task<IActionResult> GetShifts(
        [FromRoute] Guid? sessionId = null,
        [FromQuery(Name = "sessionId")] Guid? querySessionId = null,
        [FromQuery] Guid? proctorUserId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedSessionId = sessionId ?? querySessionId;
        var result = await _sender.Send(new GetExamShiftsQuery(resolvedSessionId, proctorUserId, status), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên truy xuất toàn bộ bằng chứng hậu kiểm (Evidence Panel) của một ca thi phòng Lab (MF-04).
    /// Bao gồm Audio URL trên Cloudflare R2, mã băm SHA-256, bản bóc băng Whisper,
    /// điểm AI, độ tin cậy, cờ phân loại nghi ngờ (AI Doubt Guard) và chuỗi suy luận CoT.
    /// </summary>
    [HttpGet("shifts/{shiftId:guid}/audit-evidence")]
    [HttpGet("shifts/{shiftId:guid}/audit-submissions")]
    [HttpGet("/api/v1/audit/shifts/{shiftId:guid}/submissions")]
    [Authorize(Roles = "lecturer,department_head")]
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
    /// Alias truy xuất bằng chứng hậu kiểm theo query parameter shiftId.
    /// </summary>
    [HttpGet("/api/v1/audit/submissions")]
    [Authorize(Roles = "lecturer,department_head")]
    public Task<IActionResult> GetAuditEvidenceByQuery([FromQuery] Guid shiftId)
    {
        return GetAuditEvidence(shiftId);
    }

    /// <summary>
    /// Giảng viên hậu kiểm và ghi đè điểm bài thi thật Kiosk trên Evidence Panel (MF-04 / BE-4.4b).
    /// Bắt buộc nhập lý do chỉnh sửa (override_reason >= 10 ký tự) và chỉ cho phép trước khi công bố điểm (is_locked == false).
    /// </summary>
    [HttpPut("tickets/{ticketId:guid}/override-score")]
    [HttpPost("tickets/{ticketId:guid}/override-score")]
    [HttpPut("/api/v1/audit/override")]
    [HttpPost("/api/v1/audit/submissions/{ticketId:guid}/override-score")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> OverrideScore(
        [FromRoute] Guid? ticketId,
        [FromBody] OverrideExamScoreRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetCurrentUserId();
        if (actorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var resolvedTicketId = ticketId ?? request.TicketId ?? Guid.Empty;
        if (resolvedTicketId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "Mã vé thi (TicketId) không được để trống.",
                Type = "https://oralexam.fpt.edu.vn/errors/invalid-ticket-id"
            });
        }

        var command = new OverrideExamScoreCommand(
            actorId,
            resolvedTicketId,
            request.AuditedScore,
            request.OverrideReason ?? string.Empty,
            request.CriterionDetails ?? request.CriteriaScores);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.Error switch
            {
                var err when err.StartsWith("LOCKED:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
                var err when err.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
                var err when err.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };

            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    StatusCodes.Status403Forbidden => "Thao tác bị từ chối.",
                    StatusCodes.Status404NotFound => "Không tìm thấy vé thi.",
                    _ => "Dữ liệu hậu kiểm không hợp lệ."
                },
                Detail = result.Error,
                Type = $"https://oralexam.fpt.edu.vn/errors/override-score-{statusCode}"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên, Trưởng Bộ Môn hoặc Quản trị viên khóa một chiều điểm của một vé thi cụ thể trên Evidence Panel (MF-04 / R2.1).
    /// </summary>
    [HttpPost("/api/v1/audit/lock")]
    [HttpPost("tickets/{ticketId:guid}/lock")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> LockTicketGrade(
        [FromRoute] Guid? ticketId = null,
        [FromBody] LockExamTicketRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTicketId = ticketId ?? request?.TicketId ?? Guid.Empty;
        if (resolvedTicketId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "Mã vé thi (TicketId) không được để trống.",
                Type = "https://oralexam.fpt.edu.vn/errors/invalid-ticket-id"
            });
        }

        var result = await _sender.Send(new LockExamTicketCommand(GetCurrentUserId(), resolvedTicketId), cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.Error switch
            {
                var err when err.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
                var err when err.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
                var err when err.StartsWith("UNPROCESSABLE:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status400BadRequest
            };

            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    StatusCodes.Status403Forbidden => "Thao tác bị từ chối.",
                    StatusCodes.Status404NotFound => "Không tìm thấy vé thi.",
                    StatusCodes.Status422UnprocessableEntity => "Không thể khóa điểm bài thi.",
                    _ => "Dữ liệu đầu vào không hợp lệ."
                },
                Detail = result.Error,
                Type = $"https://oralexam.fpt.edu.vn/errors/lock-ticket-{statusCode}"
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
    [Authorize(Roles = "lecturer,department_head")]
    public async Task<IActionResult> PublishGrades(Guid shiftId, CancellationToken cancellationToken = default)
    {
        var lecturerId = GetAuthenticatedUserId();
        if (lecturerId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new PublishGradesCommand(shiftId, lecturerId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var statusCode = result.Error switch
            {
                PublishGradesErrors.ActorNotFound or PublishGradesErrors.ShiftNotFound => StatusCodes.Status404NotFound,
                PublishGradesErrors.Forbidden => StatusCodes.Status403Forbidden,
                PublishGradesErrors.AlreadyPublished or PublishGradesErrors.IncompleteGrades or PublishGradesErrors.ShiftNotEnded => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status422UnprocessableEntity
            };

            return StatusCode(statusCode, new ProblemDetails
            {
                Type = $"https://oralexam.fpt.edu.vn/errors/publish-grades-{statusCode}",
                Title = statusCode switch
                {
                    StatusCodes.Status403Forbidden => "Không được phép.",
                    StatusCodes.Status404NotFound => "Không tìm thấy.",
                    StatusCodes.Status409Conflict => "Xung đột trạng thái tài nguyên.",
                    _ => "Dữ liệu không thể xử lý."
                },
                Status = statusCode,
                Detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Xuất biên bản bảng điểm khảo thí ca thi ra tệp Excel (.xlsx) kèm mã niêm phong SHA-256 (BE-V01).
    /// </summary>
    [HttpGet("shifts/{shiftId:guid}/export-excel")]
    [HttpGet("/api/v1/audit/export-excel")]
    [Authorize(Roles = "lecturer,department_head")]
    public async Task<IActionResult> ExportShiftExcel([FromRoute] Guid? shiftId, [FromQuery(Name = "shiftId")] Guid? queryShiftId, CancellationToken cancellationToken = default)
    {
        var resolvedShiftId = shiftId ?? queryShiftId ?? Guid.Empty;
        if (resolvedShiftId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "ShiftId không được để trống.",
                Type = "https://oralexam.fpt.edu.vn/errors/invalid-shift-id"
            });
        }

        var result = await _sender.Send(new ExportShiftExcelQuery(resolvedShiftId), cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status409Conflict;
            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode == StatusCodes.Status404NotFound ? "Không tìm thấy." : "Chưa thể xuất báo cáo.",
                Detail = result.Error,
                Type = $"https://oralexam.fpt.edu.vn/errors/exam-report-{statusCode}"
            });
        }

        Response?.Headers.Append("X-Certificate-Hash", result.Value.CertificateHash);
        return File(result.Value.FileContents, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Xuất biên bản bảng điểm khảo thí ca thi ra tệp PDF có mã niêm phong SHA-256 (BE-V01).
    /// </summary>
    [HttpGet("shifts/{shiftId:guid}/export-pdf")]
    [HttpGet("/api/v1/audit/export-pdf")]
    [Authorize(Roles = "lecturer,department_head")]
    public async Task<IActionResult> ExportShiftPdf([FromRoute] Guid? shiftId, [FromQuery(Name = "shiftId")] Guid? queryShiftId, CancellationToken cancellationToken = default)
    {
        var resolvedShiftId = shiftId ?? queryShiftId ?? Guid.Empty;
        if (resolvedShiftId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "ShiftId không được để trống.",
                Type = "https://oralexam.fpt.edu.vn/errors/invalid-shift-id"
            });
        }

        var result = await _sender.Send(new ExportShiftPdfQuery(resolvedShiftId), cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status409Conflict;
            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode == StatusCodes.Status404NotFound ? "Không tìm thấy." : "Chưa thể xuất báo cáo.",
                Detail = result.Error,
                Type = $"https://oralexam.fpt.edu.vn/errors/exam-report-{statusCode}"
            });
        }

        Response?.Headers.Append("X-Certificate-Hash", result.Value.CertificateHash);
        return File(result.Value.FileContents, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Trưởng bộ môn tạo kỳ thi thật. Role lấy từ tài khoản trong database.
    /// </summary>
    [HttpPost("sessions")]
    [HttpPost("seasons")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> CreateSession([FromBody] CreateOfficialExamSessionRequest request, CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new CreateOfficialExamSessionCommand(
            actorId.Value,
            request.CourseId,
            request.ExamStructureId,
            request.Title,
            request.ExamDate,
            request.HasFollowUp,
            request.MaxFollowUpQuestions,
            request.ExamInputMode,
            request.TranscriptBufferSeconds), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Trưởng bộ môn mở một ca thi trong kỳ, gắn phòng lab và người coi thi.
    /// </summary>
    [HttpPost("shifts")]
    [HttpPost("seasons/{sessionId:guid}/shifts")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> OpenShift(
        [FromBody] OpenExamShiftRequest request,
        CancellationToken cancellationToken = default,
        [FromRoute] Guid? sessionId = null)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var resolvedSessionId = sessionId.HasValue && sessionId.Value != Guid.Empty
            ? sessionId.Value
            : request.SessionId;

        var result = await _sender.Send(new OpenExamShiftCommand(
            actorId.Value,
            resolvedSessionId,
            request.ShiftName,
            request.RoomLab,
            request.StartTime,
            request.EndTime,
            request.ProctorUserId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Trưởng bộ môn gán sinh viên vào ghế 1-40 và địa chỉ IP máy trạm của ca thi.
    /// </summary>
    [HttpPost("tickets")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> AssignTicket([FromBody] AssignExamTicketRequest request, CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new AssignExamTicketCommand(
            actorId.Value,
            request.ShiftId,
            request.StudentId,
            request.SeatNumber,
            request.IpAddress), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Giám thị hoặc Sinh viên điểm danh mở vé thi tại máy trạm phòng Lab (Check-in Ticket -> IN_PROGRESS).
    /// Đối chiếu địa chỉ IP máy trạm (ip_address) và khung giờ ca thi.
    /// </summary>
    [HttpPost("tickets/{ticketId:guid}/check-in")]
    [Authorize(Roles = "student,proctor,lecturer,department_head,admin")]
    public async Task<IActionResult> CheckInTicket([FromRoute] Guid ticketId, CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var remoteIp = ResolveRemoteIpAddress();
        var result = await _sender.Send(new OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket.CheckInExamTicketCommand(
            actorId.Value,
            ticketId,
            remoteIp,
            DateTime.UtcNow), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Sinh viên tại máy trạm Kiosk lấy thông tin vé thi hiện tại khớp với địa chỉ IP máy trạm (ip_address).
    /// </summary>
    [HttpGet("tickets/current")]
    [HttpGet("tickets/my-workstation")]
    [HttpGet("workstation/ticket")]
    [Authorize(Roles = "student,proctor,lecturer,department_head,admin")]
    public async Task<IActionResult> GetMyWorkstationTicket(CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var remoteIp = ResolveRemoteIpAddress();
        var result = await _sender.Send(new OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket.GetMyWorkstationTicketQuery(
            actorId.Value,
            remoteIp,
            DateTime.UtcNow), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    private string? ResolveRemoteIpAddress()
    {
        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) &&
            !string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.ToString().Split(',')[0].Trim();
        }

        return HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private Guid? GetActorId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private IActionResult OfficialExamProblem(string error)
    {
        var statusCode = error switch
        {
            OfficialExamErrors.Forbidden or OfficialExamErrors.WrongWorkstation => StatusCodes.Status403Forbidden,
            OfficialExamErrors.CourseNotFound
                or OfficialExamErrors.StructureNotFound
                or OfficialExamErrors.SessionNotFound
                or OfficialExamErrors.ShiftNotFound
                or OfficialExamErrors.TicketNotFound
                or OfficialExamErrors.WorkstationTicketNotFound
                or OfficialExamErrors.StudentNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Không được phép.",
                StatusCodes.Status404NotFound => "Không tìm thấy.",
                _ => "Dữ liệu đầu vào không hợp lệ."
            },
            Detail = error,
            Type = "https://tools.ietf.org/html/rfc7231"
        });
    }
}

public sealed record OverrideExamScoreRequest
{
    public Guid? TicketId { get; init; }
    public decimal AuditedScore { get; init; }
    public string OverrideReason { get; init; } = string.Empty;
    public IReadOnlyList<OverrideCriterionScoreDto>? CriterionDetails { get; init; }
    public IReadOnlyList<OverrideCriterionScoreDto>? CriteriaScores { get; init; }
}

public sealed record LockExamTicketRequest
{
    public Guid? TicketId { get; init; }
}
