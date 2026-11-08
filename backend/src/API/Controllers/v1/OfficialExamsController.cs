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
using OralExamination.Application.Features.OfficialExams.Commands.ConfigureOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.Commands.PublishExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.HandOverExamSet;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftExcel;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;
using OralExamination.Application.Features.OfficialExams.Queries.GetAntiCheatLogs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
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
[Authorize(Roles = "lecturer,department_head,proctor,admin,examination_officer")]
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
    [Authorize(Roles = "lecturer,department_head,examination_officer,proctor,admin")]
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
    [Authorize(Roles = "lecturer,department_head,examination_officer,proctor,admin")]
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
            request.CriterionDetails ?? request.CriteriaScores,
            request.SubmissionId);

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
    [Authorize(Roles = "lecturer,department_head,examination_officer,admin")]
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
    [Authorize(Roles = "lecturer,department_head,examination_officer,admin")]
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
    [Authorize(Roles = "lecturer,department_head,examination_officer,admin")]
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
    /// Trưởng bộ môn hoặc Khảo thí tạo kỳ thi thật. Role lấy từ tài khoản trong database.
    /// </summary>
    [HttpPost("sessions")]
    [HttpPost("seasons")]
    [Authorize(Roles = "department_head,examination_officer,admin")]
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
    /// Trưởng bộ môn hoặc Khảo thí mở một ca thi trong kỳ, gắn phòng lab và người coi thi.
    /// </summary>
    [HttpPost("shifts")]
    [HttpPost("seasons/{sessionId:guid}/shifts")]
    [Authorize(Roles = "department_head,examination_officer,admin")]
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
            request.ProctorUserId,
            request.ExaminerUserId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Trưởng bộ môn hoặc Quản trị viên cấu hình tính năng Follow-up cho kỳ thi thật (MF-04).
    /// Cán bộ Khảo thí (examination_officer) bị từ chối 403 Forbidden.
    /// </summary>
    [HttpPut("sessions/{id:guid}/follow-up-config")]
    [Authorize(Roles = "department_head,admin")]
    public async Task<IActionResult> ConfigureFollowUp(
        Guid id,
        [FromBody] ConfigureOfficialExamFollowUpRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new ConfigureOfficialExamFollowUpCommand(
            actorId.Value,
            id,
            request.HasFollowUp,
            request.MaxFollowUpQuestions), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Trưởng bộ môn hoặc Khảo thí gán sinh viên vào ghế 1-40 và địa chỉ IP máy trạm của ca thi.
    /// </summary>
    [HttpPost("tickets")]
    [Authorize(Roles = "department_head,examination_officer,admin")]
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

    /// <summary>
    /// Cán bộ khảo thí hoặc Trưởng bộ môn công bố đề thi cho ca thi phòng Lab (is_exam_published = true).
    /// </summary>
    [HttpPost("shifts/{id:guid}/publish")]
    [Authorize(Roles = "examination_officer,department_head,admin")]
    public async Task<IActionResult> PublishShift(Guid id, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var actorRole = GetActorRole();
        var command = new PublishExamShiftCommand(id, actorId.Value, actorRole);
        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(new { success = true, shiftId = id, message = "Ca thi đã được công bố đề thi thành công." }) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Ghi nhận sự kiện vi phạm an ninh phòng thi từ Kiosk Desktop App.
    /// </summary>
    [HttpPost("anti-cheat/log")]
    [Authorize(Roles = "student,proctor,examination_officer,department_head,admin")]
    public async Task<IActionResult> LogAntiCheat([FromBody] LogAntiCheatEventRequest request, CancellationToken cancellationToken = default)
    {
        var command = new LogAntiCheatEventCommand(
            request.TicketId,
            request.ShiftId,
            request.StudentId,
            request.EventType,
            request.Severity,
            request.Details);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Không thể ghi nhận vi phạm.",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy danh sách sự kiện vi phạm an ninh của ca thi (hoặc theo vé thi).
    /// </summary>
    [HttpGet("shifts/{shiftId:guid}/anti-cheat/logs")]
    [HttpGet("shifts/{shiftId:guid}/anti-cheat-logs")]
    [Authorize(Roles = "proctor,examination_officer,department_head,admin")]
    public async Task<IActionResult> GetAntiCheatLogs(Guid shiftId, [FromQuery] Guid? ticketId, CancellationToken cancellationToken = default)
    {
        var query = new GetAntiCheatLogsQuery(shiftId, ticketId);
        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Lấy danh sách bộ đề thi thật theo môn học.
    /// </summary>
    [HttpGet("exam-sets")]
    [Authorize(Roles = "lecturer,department_head,examination_officer,admin")]
    public async Task<IActionResult> GetExamSets([FromQuery] Guid courseId, [FromQuery] Guid? sessionId, [FromQuery] string? status, CancellationToken cancellationToken = default)
    {
        var query = new GetExamSetsQuery(courseId, sessionId, status);
        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Giảng viên hoặc Trưởng bộ môn tạo mới bộ đề thi.
    /// </summary>
    [HttpPost("exam-sets")]
    [Authorize(Roles = "lecturer,department_head,examination_officer,admin")]
    public async Task<IActionResult> CreateExamSet([FromBody] CreateExamSetRequest request, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId() ?? Guid.Empty;
        var actorRole = GetActorRole();
        var command = new CreateExamSetCommand(
            request.StructureId,
            request.CourseId,
            request.SessionId,
            request.SetCode,
            request.QuestionIds,
            actorId,
            actorRole);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Không thể tạo bộ đề.",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên gửi bộ đề lên Trưởng bộ môn phê duyệt (SUBMITTED_FOR_REVIEW).
    /// </summary>
    [HttpPost("exam-sets/{id:guid}/submit-review")]
    [Authorize(Roles = "lecturer,department_head,examination_officer,admin")]
    public async Task<IActionResult> SubmitExamSetReview(Guid id, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId() ?? Guid.Empty;
        var actorRole = GetActorRole();
        var command = new SubmitExamSetForReviewCommand(id, actorId, actorRole);
        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(new { success = true, examSetId = id }) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Trưởng bộ môn phê duyệt bộ đề thành chính thức (OFFICIAL), cascade duyệt toàn bộ câu hỏi và cắm cờ cảnh báo câu hỏi trùng.
    /// </summary>
    [HttpPost("exam-sets/{id:guid}/approve")]
    [Authorize(Roles = "department_head,examination_officer,admin")]
    public async Task<IActionResult> ApproveExamSet(Guid id, [FromBody] ApproveExamSetRequest? request, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId() ?? Guid.Empty;
        var actorRole = GetActorRole();
        var command = new ApproveExamSetCommand(id, request?.ReviewNotes, actorId, actorRole);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Không thể phê duyệt bộ đề.",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Trưởng bộ môn hoặc Khảo thí bàn giao bộ đề chính thức sang phòng Khảo thí (HANDED_OVER).
    /// </summary>
    [HttpPost("exam-sets/{id:guid}/hand-over")]
    [Authorize(Roles = "department_head,examination_officer,admin")]
    public async Task<IActionResult> HandOverExamSet(Guid id, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId() ?? Guid.Empty;
        var actorRole = GetActorRole();
        var command = new HandOverExamSetCommand(id, actorId, actorRole);
        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(new { success = true, examSetId = id, status = "HANDED_OVER" }) : OfficialExamProblem(result.Error);
    }

    private string? GetActorRole()
    {
        if (User?.IsInRole("admin") == true) return "admin";
        if (User?.IsInRole("department_head") == true) return "department_head";
        if (User?.IsInRole("examination_officer") == true) return "examination_officer";
        if (User?.IsInRole("lecturer") == true) return "lecturer";
        if (User?.IsInRole("proctor") == true) return "proctor";
        if (User?.IsInRole("student") == true) return "student";
        return null;
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
    public Guid? SubmissionId { get; init; }
    public decimal AuditedScore { get; init; }
    public string OverrideReason { get; init; } = string.Empty;
    public IReadOnlyList<OverrideCriterionScoreDto>? CriterionDetails { get; init; }
    public IReadOnlyList<OverrideCriterionScoreDto>? CriteriaScores { get; init; }
}

public sealed record LockExamTicketRequest
{
    public Guid? TicketId { get; init; }
}
