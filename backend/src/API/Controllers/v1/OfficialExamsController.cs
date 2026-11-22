using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OralExamination.Application.Common.Interfaces;
using API.Common;
using OralExamination.Application.Features.OfficialExams.Commands.UnlockExamTicketDevice;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShiftStatus;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.DistributeShiftTickets;
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
using OralExamination.Application.Features.OfficialExams.Commands.ImportExamRoster;
using OralExamination.Application.Features.OfficialExams.Commands.ImportSessionCandidates;
using OralExamination.Application.Features.OfficialExams.Commands.BatchCreateOfficialExamSessions;
using OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;
using OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftExcel;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;
using OralExamination.Application.Features.OfficialExams.Queries.GetAntiCheatLogs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamShifts;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamEnrollmentEstimate;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamTicketState;
using OralExamination.Application.Features.OfficialExams.Queries.GetSemesterEligibleExamCourses;
using OralExamination.Application.Features.OfficialExams.Queries.GetSessionCandidates;
using OralExamination.Domain.Enums;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý phân hệ Thi Thật Phòng Lab, Hậu kiểm & Công bố điểm (MF-04).
/// Chuẩn RESTful RFC 7807 Problem Details.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/official-exams")]
[Authorize]
public class OfficialExamsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IExamRosterFileParser? _rosterParser;
    private readonly ICandidateRosterFileParser? _candidateRosterParser;

    public OfficialExamsController(
        ISender sender,
        IExamRosterFileParser? rosterParser = null,
        ICandidateRosterFileParser? candidateRosterParser = null)
    {
        _sender = sender;
        _rosterParser = rosterParser;
        _candidateRosterParser = candidateRosterParser;
    }

    private Guid GetCurrentUserId() => GetActorId() ?? Guid.Empty;
    private Guid GetAuthenticatedUserId() => GetCurrentUserId();

    /// <summary>
    /// Lấy danh sách kỳ thi thật (OfficialExamSession). Hỗ trợ lọc theo môn học, học kỳ, đợt FE/RE và trạng thái.
    /// </summary>
    [HttpGet("sessions")]
    [HttpGet("seasons")]
    [Authorize(Roles = "lecturer,department_head,examination_officer,proctor")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? courseId = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? semesterId = null,
        [FromQuery] string? examAttemptType = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetOfficialExamSessionsQuery(courseId, status, semesterId, examAttemptType),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Lấy danh sách ca thi phòng Lab (RealExamSessionShift). Hỗ trợ lọc theo sessionId, proctorUserId, examinerUserId, status.
    /// </summary>
    [HttpGet("shifts")]
    [HttpGet("seasons/{sessionId:guid}/shifts")]
    [Authorize(Roles = "lecturer,department_head,examination_officer,proctor")]
    public async Task<IActionResult> GetShifts(
        [FromRoute] Guid? sessionId = null,
        [FromQuery(Name = "sessionId")] Guid? querySessionId = null,
        [FromQuery] Guid? proctorUserId = null,
        [FromQuery] Guid? examinerUserId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedSessionId = sessionId ?? querySessionId;
        var result = await _sender.Send(
            new GetExamShiftsQuery(resolvedSessionId, proctorUserId, status, examinerUserId),
            cancellationToken);
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
    [Authorize(Roles = "lecturer,department_head,examination_officer")]
    public async Task<IActionResult> GetAuditEvidence(Guid shiftId, CancellationToken cancellationToken = default)
    {
        var query = new GetAuditEvidenceQuery(shiftId, GetCurrentUserId());
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase)
                || result.Error == OfficialExamErrors.Forbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Type = "https://oralexam.fpt.edu.vn/errors/official-exam-workflow-403",
                    Title = "Thao tác bị từ chối.",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = result.Error
                });
            }

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
    [Authorize(Roles = "lecturer,department_head,examination_officer")]
    public Task<IActionResult> GetAuditEvidenceByQuery([FromQuery] Guid shiftId, CancellationToken cancellationToken = default)
    {
        return GetAuditEvidence(shiftId, cancellationToken);
    }

    /// <summary>
    /// Giảng viên hậu kiểm và ghi đè điểm bài thi thật Kiosk trên Evidence Panel (MF-04 / BE-4.4b).
    /// Bắt buộc nhập lý do chỉnh sửa (override_reason >= 10 ký tự) và chỉ cho phép trước khi công bố điểm (is_locked == false).
    /// </summary>
    [HttpPut("tickets/{ticketId:guid}/override-score")]
    [HttpPost("tickets/{ticketId:guid}/override-score")]
    [HttpPut("/api/v1/audit/override")]
    [HttpPost("/api/v1/audit/submissions/{ticketId:guid}/override-score")]
    [Authorize(Roles = "lecturer,department_head")]
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
    /// Giảng viên hoặc Trưởng Bộ Môn khóa một chiều điểm của một vé thi cụ thể trên Evidence Panel (MF-04 / R2.1).
    /// </summary>
    [HttpPost("/api/v1/audit/lock")]
    [HttpPost("tickets/{ticketId:guid}/lock")]
    [Authorize(Roles = "lecturer,department_head")]
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
    /// Cán bộ Khảo thí thực hiện Công Bố Điểm (Publish Grades) cho ca thi phòng Lab (MF-04).
    /// BẮT BUỘC: 100% sinh viên trong ca thi phải có điểm hoàn chỉnh.
    /// Kích hoạt cơ chế Khóa Một Chiều (One-Way Lock is_locked = true), ngăn chặn mọi hành vi chỉnh sửa sau đó.
    /// </summary>
    [HttpPost("shifts/{shiftId:guid}/publish-grades")]
    [Authorize(Roles = "examination_officer")]
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
    [Authorize(Roles = "lecturer,department_head,examination_officer")]
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

        var result = await _sender.Send(new ExportShiftExcelQuery(resolvedShiftId, GetCurrentUserId()), cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase)
                || result.Error == OfficialExamErrors.Forbidden
                ? StatusCodes.Status403Forbidden
                : result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status409Conflict;
            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    StatusCodes.Status403Forbidden => "Thao tác bị từ chối.",
                    StatusCodes.Status404NotFound => "Không tìm thấy.",
                    _ => "Chưa thể xuất báo cáo."
                },
                Detail = result.Error,
                Type = $"https://oralexam.fpt.edu.vn/errors/exam-report-{statusCode}"
            });
        }

        Response?.Headers.Append("X-Certificate-Hash", result.Value.CertificateHash);
        if (!string.IsNullOrWhiteSpace(result.Value.CertificateThumbprint))
        {
            Response?.Headers.Append("X-Signature-Thumbprint", result.Value.CertificateThumbprint);
        }
        return File(result.Value.FileContents, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Xuất biên bản bảng điểm khảo thí ca thi ra tệp PDF có mã niêm phong SHA-256 (BE-V01).
    /// </summary>
    [HttpGet("shifts/{shiftId:guid}/export-pdf")]
    [HttpGet("/api/v1/audit/export-pdf")]
    [Authorize(Roles = "lecturer,department_head,examination_officer")]
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

        var result = await _sender.Send(new ExportShiftPdfQuery(resolvedShiftId, GetCurrentUserId()), cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase)
                || result.Error == OfficialExamErrors.Forbidden
                ? StatusCodes.Status403Forbidden
                : result.Error.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status409Conflict;
            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    StatusCodes.Status403Forbidden => "Thao tác bị từ chối.",
                    StatusCodes.Status404NotFound => "Không tìm thấy.",
                    _ => "Chưa thể xuất báo cáo."
                },
                Detail = result.Error,
                Type = $"https://oralexam.fpt.edu.vn/errors/exam-report-{statusCode}"
            });
        }

        Response?.Headers.Append("X-Certificate-Hash", result.Value.CertificateHash);
        if (!string.IsNullOrWhiteSpace(result.Value.CertificateThumbprint))
        {
            Response?.Headers.Append("X-Signature-Thumbprint", result.Value.CertificateThumbprint);
        }
        return File(result.Value.FileContents, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Bước 0.1: Lấy danh sách các môn học mở trong học kỳ kèm sĩ số sinh viên enrolled thực tế và đề xuất số phòng thi (chuẩn tối đa 24 SV/phòng).
    /// </summary>
    [HttpGet("semesters/{semesterId:guid}/eligible-courses")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(IReadOnlyList<SemesterEligibleExamCourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSemesterEligibleCourses(
        Guid semesterId,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new GetSemesterEligibleExamCoursesQuery(actorId.Value, semesterId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Bước 0.2: Khởi tạo hàng loạt kỳ thi chính thức (OfficialExamSession) cho các môn được chọn trong học kỳ, tự động kế thừa cấu trúc đề và Follow-up từ Trưởng Bộ Môn.
    /// </summary>
    [HttpPost("semesters/{semesterId:guid}/sessions/batch")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(BatchCreateOfficialExamSessionsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BatchCreateSessions(
        Guid semesterId,
        [FromBody] BatchCreateOfficialExamSessionsRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return OfficialExamWorkflowProblem("BAD_REQUEST: Nội dung yêu cầu khởi tạo hàng loạt kỳ thi không được để trống.");
        }

        var result = await _sender.Send(new BatchCreateOfficialExamSessionsCommand(
            actorId.Value,
            semesterId,
            request.ExamDate,
            request.ExamInputMode,
            request.TranscriptBufferSeconds,
            request.SkipExisting,
            request.Courses,
            request.ExamAttemptType), cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Bước 2.0: Tải tệp mẫu (.xlsx hoặc .csv) chỉ yêu cầu cột MSSV (studentCode) để nhập danh sách thí sinh dự thi FE/RE.
    /// </summary>
    [HttpGet("candidates/import/template")]
    [HttpGet("sessions/{sessionId:guid}/candidates/import/template")]
    [HttpGet("seasons/{sessionId:guid}/candidates/import/template")]
    [Authorize(Roles = "examination_officer")]
    public IActionResult DownloadCandidateImportTemplate(
        [FromQuery] string format = "xlsx")
    {
        if (_candidateRosterParser is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Dịch vụ đọc danh sách thí sinh chưa được cấu hình."
            });
        }

        var normalizedFormat = string.IsNullOrWhiteSpace(format) ? "xlsx" : format.Trim().ToLowerInvariant();
        if (normalizedFormat is not ("xlsx" or "csv"))
        {
            return OfficialExamWorkflowProblem("BAD_REQUEST: Định dạng mẫu tải về (format) chỉ hỗ trợ xlsx hoặc csv.");
        }

        var template = _candidateRosterParser.GenerateTemplate(normalizedFormat);
        return File(template.Contents, template.ContentType, template.FileName);
    }

    /// <summary>
    /// Bước 2.1: Xem trước kết quả kiểm tra tệp danh sách thí sinh (Excel/CSV, tối đa 2MB, tối đa 2.000 dòng) cho đợt FE/RE.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/candidates/import/preview")]
    [HttpPost("seasons/{sessionId:guid}/candidates/import/preview")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(CandidateRosterImportPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PreviewSessionCandidatesImport(
        Guid sessionId,
        [FromForm] IFormFile? file,
        [FromForm] string? mode = null,
        [FromQuery(Name = "mode")] string? queryMode = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var (rows, parseError) = await ParseCandidateRosterFileAsync(file, cancellationToken);
        if (parseError is not null)
        {
            return parseError;
        }

        var resolvedMode = !string.IsNullOrWhiteSpace(mode) ? mode : queryMode;
        var result = await _sender.Send(
            new PreviewSessionCandidateImportQuery(actorId.Value, sessionId, resolvedMode, rows!),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Bước 2.2: Xác nhận nhập danh sách thí sinh dự thi vào bảng 3NF official_exam_candidates (REPLACE hoặc APPEND).
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/candidates/import/commit")]
    [HttpPost("seasons/{sessionId:guid}/candidates/import/commit")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(CandidateRosterImportCommitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CommitSessionCandidatesImport(
        Guid sessionId,
        [FromForm] IFormFile? file,
        [FromForm] string? mode = null,
        [FromQuery(Name = "mode")] string? queryMode = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var (rows, parseError) = await ParseCandidateRosterFileAsync(file, cancellationToken);
        if (parseError is not null)
        {
            return parseError;
        }

        var resolvedMode = !string.IsNullOrWhiteSpace(mode) ? mode : queryMode;
        var result = await _sender.Send(
            new CommitSessionCandidateImportCommand(actorId.Value, sessionId, resolvedMode, rows!),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Bước 2.3: Truy vấn danh sách thí sinh đã nhập cho đợt thi (có tìm kiếm, phân trang và trạng thái phân bổ phòng/ghế).
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/candidates")]
    [HttpGet("seasons/{sessionId:guid}/candidates")]
    [Authorize(Roles = "examination_officer,department_head")]
    [ProducesResponseType(typeof(SessionCandidatesPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionCandidates(
        Guid sessionId,
        [FromQuery] string? search = null,
        [FromQuery] bool? allocatedOnly = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new GetSessionCandidatesQuery(actorId.Value, sessionId, search, allocatedOnly, pageNumber, pageSize),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Bước 2.4: Xóa một thí sinh khỏi danh sách dự thi (chặn 409 nếu thí sinh đã có vé phòng thi).
    /// </summary>
    [HttpDelete("sessions/{sessionId:guid}/candidates/{studentId:guid}")]
    [HttpDelete("seasons/{sessionId:guid}/candidates/{studentId:guid}")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveSessionCandidate(
        Guid sessionId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new RemoveSessionCandidateCommand(actorId.Value, sessionId, studentId),
            cancellationToken);

        return result.IsSuccess ? NoContent() : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Bước 0.3: Ước tính sĩ số sinh viên enrolled hợp lệ của môn trong kỳ và đề xuất số phòng thi theo trần chuẩn 24 SV/phòng.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/enrollment-estimate")]
    [HttpGet("seasons/{sessionId:guid}/enrollment-estimate")]
    [Authorize(Roles = "examination_officer,department_head")]
    [ProducesResponseType(typeof(OfficialExamEnrollmentEstimateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrollmentEstimate(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new GetOfficialExamEnrollmentEstimateQuery(actorId.Value, sessionId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Bước 0.4: Tạo ca thi và sinh nguyên tử K phòng thi Lab (maxSeats &lt;= 24) trong cùng khung giờ, chặn đụng lịch phòng/Giám thị/Giám khảo.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/shifts/batch")]
    [HttpPost("seasons/{sessionId:guid}/shifts/batch")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(BatchOpenExamShiftsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BatchOpenShifts(
        Guid sessionId,
        [FromBody] BatchOpenExamShiftsRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return OfficialExamWorkflowProblem("BAD_REQUEST: Nội dung yêu cầu mở hàng loạt phòng thi không được để trống.");
        }

        var result = await _sender.Send(new BatchOpenExamShiftsCommand(
            actorId.Value,
            sessionId,
            request.ShiftGroupName,
            request.StartTime,
            request.EndTime,
            request.Rooms), cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Bước 0.5: Phân bổ ngẫu nhiên (CSPRNG) sinh viên enrolled chưa có vé vào các phòng thi và tự động cân bằng đều sĩ số ở ca thi cuối cùng (chênh lệch &lt;= 1 SV/phòng).
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/auto-allocate-students")]
    [HttpPost("seasons/{sessionId:guid}/auto-allocate-students")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(AutoAllocateSessionStudentsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AutoAllocateSessionStudents(
        Guid sessionId,
        [FromBody] AutoAllocateSessionStudentsRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var effectiveRequest = request ?? new AutoAllocateSessionStudentsRequest();
        var result = await _sender.Send(new AutoAllocateSessionStudentsCommand(
            actorId.Value,
            sessionId,
            effectiveRequest.ShiftIds,
            effectiveRequest.AssignHandedOverExamSets,
            effectiveRequest.DryRun), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Khảo thí tạo kỳ thi thật với cấu hình học thuật mặc định an toàn.
    /// </summary>
    [HttpPost("sessions")]
    [HttpPost("seasons")]
    [Authorize(Roles = "examination_officer")]
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
            request.TranscriptBufferSeconds,
            request.ExamAttemptType), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Khảo thí mở một ca thi trong kỳ, gắn phòng lab và người coi thi.
    /// </summary>
    [HttpPut("shifts/{id:guid}")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateShift(Guid id, [FromBody] UpdateExamShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null) return Unauthorized();
        var result = await _sender.Send(new UpdateExamShiftCommand(actorId.Value, GetActorRole(), id,
            request.ShiftName, request.RoomLab, request.StartTime, request.EndTime,
            request.ProctorUserId, request.ExaminerUserId, request.MaxSeats), cancellationToken);
        return result.IsSuccess ? NoContent() : ShiftMutationProblem.Create(result.Error);
    }

    [HttpDelete("shifts/{id:guid}")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteShift(Guid id, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null) return Unauthorized();
        var result = await _sender.Send(new DeleteExamShiftCommand(actorId.Value, GetActorRole(), id), cancellationToken);
        return result.IsSuccess ? NoContent() : ShiftMutationProblem.Create(result.Error);
    }

    [HttpPost("shifts")]
    [HttpPost("seasons/{sessionId:guid}/shifts")]
    [Authorize(Roles = "examination_officer")]
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
            request.ExaminerUserId,
            request.MaxSeats), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Chỉ Trưởng bộ môn phụ trách môn cấu hình Follow-up cho kỳ thi thật (MF-04).
    /// </summary>
    [HttpPut("sessions/{id:guid}/follow-up-config")]
    [Authorize(Roles = "department_head")]
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
    /// Khảo thí gán sinh viên vào ghế 1-40 và địa chỉ IP máy trạm của ca thi.
    /// </summary>
    [HttpPost("tickets")]
    [Authorize(Roles = "examination_officer")]
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
    /// Giám thị hoặc Sinh viên điểm danh mở vé thi tại máy trạm/BYOD (Check-in Ticket -> IN_PROGRESS).
    /// Khóa vé theo X-Device-Id và cấp 1-lần X-Kiosk-Session-Token; ip_address chỉ ghi nhận kiểm toán.
    /// </summary>
    [HttpPost("tickets/{ticketId:guid}/check-in")]
    [Authorize(Roles = "student,proctor,lecturer,department_head")]
    public async Task<IActionResult> CheckInTicket(
        [FromRoute] Guid ticketId,
        CancellationToken cancellationToken = default,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CheckInExamTicketRequest? request = null)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var remoteIp = ResolveRemoteIpAddress();
        var deviceId = ResolveDeviceId(request?.DeviceId);
        var kioskVersion = ResolveKioskVersion(request?.KioskVersion);
        var result = await _sender.Send(new OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket.CheckInExamTicketCommand(
            actorId.Value,
            ticketId,
            remoteIp,
            DateTime.UtcNow,
            deviceId,
            kioskVersion), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Cán bộ Khảo thí mở khóa thiết bị BYOD cho vé thi khi máy sinh viên gặp sự cố (chỉ examination_officer).
    /// Bắt buộc nhập lý do 10–500 ký tự, ghi AuditLog và cho phép check-in lại trên máy thay thế.
    /// </summary>
    [HttpPost("tickets/{ticketId:guid}/unlock-device")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(UnlockExamTicketDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UnlockTicketDevice(
        [FromRoute] Guid ticketId,
        [FromBody] UnlockExamTicketDeviceRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return OfficialExamWorkflowProblem("UNPROCESSABLE: Lý do mở khóa thiết bị không được để trống (tối thiểu 10 ký tự).");
        }

        var result = await _sender.Send(
            new UnlockExamTicketDeviceCommand(actorId.Value, ticketId, request.Reason ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Đọc trước danh sách sinh viên từ CSV/XLSX. Preview không ghi dữ liệu; tệp cần có studentCode, seatNumber, ipAddress.
    /// </summary>
    [HttpPost("shifts/{shiftId:guid}/tickets/import/preview")]
    [Authorize(Roles = "examination_officer")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> PreviewTicketImport(
        Guid shiftId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null) return Unauthorized();
        var parsed = await ParseRosterFileAsync(file, cancellationToken);
        if (parsed.Error is not null) return parsed.Error;

        var result = await _sender.Send(new PreviewExamRosterImportQuery(actorId.Value, shiftId, parsed.Rows!), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BulkImportProblem(result.Error);
    }

    /// <summary>
    /// Xác nhận nhập cả khối. Hệ thống kiểm tra lại dữ liệu và chỉ tạo vé khi mọi dòng cùng có hiệu lực.
    /// </summary>
    [HttpPost("shifts/{shiftId:guid}/tickets/import/commit")]
    [Authorize(Roles = "examination_officer")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> CommitTicketImport(
        Guid shiftId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null) return Unauthorized();
        var parsed = await ParseRosterFileAsync(file, cancellationToken);
        if (parsed.Error is not null) return parsed.Error;

        var result = await _sender.Send(new CommitExamRosterImportCommand(actorId.Value, shiftId, parsed.Rows!), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BulkImportProblem(result.Error);
    }

    /// <summary>
    /// Sinh viên tại máy trạm/BYOD Kiosk lấy thông tin vé thi hiện tại (xác thực X-Device-Id & X-Kiosk-Session-Token; ip_address chỉ ghi kiểm toán).
    /// </summary>
    [HttpGet("tickets/current")]
    [HttpGet("tickets/my-workstation")]
    [HttpGet("workstation/ticket")]
    [Authorize(Roles = "student,proctor,lecturer,department_head")]
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
            DateTime.UtcNow,
            ResolveDeviceId(),
            ResolveKioskSessionToken()), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Sinh viên sở hữu vé thi truy xuất trạng thái khôi phục Kiosk sau F5 hoặc mất mạng (0% lộ điểm).
    /// </summary>
    [HttpGet("tickets/{ticketId:guid}")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(OfficialExamTicketStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetTicketState(
        [FromRoute] Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new GetOfficialExamTicketStateQuery(
                actorId.Value,
                ticketId,
                ResolveDeviceId(),
                ResolveKioskSessionToken()),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Cán bộ Khảo thí công bố đề thi cho ca thi phòng Lab (is_exam_published = true).
    /// </summary>
    [HttpPost("shifts/{id:guid}/publish")]
    [Authorize(Roles = "examination_officer")]
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
    /// Cán bộ Khảo thí điều khiển chuyển đổi trạng thái ca thi (scheduled -> in_progress -> completed, hoặc cancelled).
    /// </summary>
    [HttpPut("shifts/{id:guid}/status")]
    [HttpPatch("shifts/{id:guid}/status")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(ExamShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateShiftStatus(
        Guid id,
        [FromBody] UpdateExamShiftStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null) return Unauthorized();
        var actorRole = GetActorRole();
        var result = await _sender.Send(new UpdateExamShiftStatusCommand(
            actorId.Value, actorRole, id, request?.Status ?? string.Empty), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Cán bộ Khảo thí phân bổ bộ đề thi HANDED_OVER không trùng lặp (uq_tickets_shift_exam_set) cho các vé chưa có đề trong ca thi.
    /// </summary>
    [HttpPost("shifts/{id:guid}/distribute-tickets")]
    [Authorize(Roles = "examination_officer")]
    [ProducesResponseType(typeof(DistributeShiftTicketsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DistributeShiftTickets(
        Guid id,
        [FromBody] DistributeShiftTicketsRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null) return Unauthorized();
        var actorRole = GetActorRole();
        var effectiveRequest = request ?? new DistributeShiftTicketsRequest();
        var result = await _sender.Send(new DistributeShiftTicketsCommand(
            actorId.Value, actorRole, id, effectiveRequest.AllowRandomShuffle), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Ghi nhận sự kiện vi phạm an ninh phòng thi từ Kiosk Desktop App.
    /// </summary>
    [HttpPost("anti-cheat/log")]
    [Authorize(Roles = "student,proctor,examination_officer,department_head")]
    public async Task<IActionResult> LogAntiCheat([FromBody] LogAntiCheatEventRequest request, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var resolvedStudentId = request.StudentId is { } requestedStudentId && requestedStudentId != Guid.Empty
            ? requestedStudentId
            : (User.IsInRole(UserRole.Student) ? actorId.Value : Guid.Empty);

        if (resolvedStudentId == Guid.Empty)
        {
            return OfficialExamWorkflowProblem("BAD_REQUEST: StudentId không được để trống.");
        }

        var command = new LogAntiCheatEventCommand(
            actorId.Value,
            request.TicketId,
            request.ShiftId,
            resolvedStudentId,
            request.EventType,
            request.Severity,
            request.Details,
            request.ClientEventId,
            ResolveDeviceId(),
            ResolveKioskSessionToken());

        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : OfficialExamWorkflowProblem(result.Error);
    }

    /// <summary>
    /// Lấy danh sách sự kiện vi phạm an ninh của ca thi (hoặc theo vé thi).
    /// </summary>
    [HttpGet("shifts/{shiftId:guid}/anti-cheat/logs")]
    [HttpGet("shifts/{shiftId:guid}/anti-cheat-logs")]
    [Authorize(Roles = "proctor,examination_officer,department_head,lecturer")]
    public async Task<IActionResult> GetAntiCheatLogs(Guid shiftId, [FromQuery] Guid? ticketId, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var query = new GetAntiCheatLogsQuery(shiftId, ticketId, actorId.Value);
        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : OfficialExamProblem(result.Error);
    }

    /// <summary>
    /// Lấy danh sách bộ đề thi thật theo môn học.
    /// </summary>
    [HttpGet("exam-sets")]
    [Authorize(Roles = "lecturer,department_head,examination_officer")]
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
    [Authorize(Roles = "lecturer,department_head")]
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
            if (result.Error == OfficialExamErrors.Forbidden ||
                result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return OfficialExamProblem(result.Error);
            }

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
    [Authorize(Roles = "lecturer,department_head")]
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
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> ApproveExamSet(Guid id, [FromBody] ApproveExamSetRequest? request, CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId() ?? Guid.Empty;
        var actorRole = GetActorRole();
        var command = new ApproveExamSetCommand(id, request?.ReviewNotes, actorId, actorRole);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error == OfficialExamErrors.Forbidden)
            {
                return OfficialExamProblem(result.Error);
            }

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
    /// Trưởng bộ môn phụ trách môn bàn giao bộ đề chính thức sang phòng Khảo thí (HANDED_OVER).
    /// </summary>
    [HttpPost("exam-sets/{id:guid}/hand-over")]
    [Authorize(Roles = "department_head")]
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

    private string? ResolveDeviceId(string? bodyDeviceId = null)
    {
        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-Device-Id", out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        return string.IsNullOrWhiteSpace(bodyDeviceId) ? null : bodyDeviceId.Trim();
    }

    private string? ResolveKioskSessionToken()
    {
        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-Kiosk-Session-Token", out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        return null;
    }

    private string? ResolveKioskVersion(string? bodyVersion = null)
    {
        if (Request?.Headers != null &&
            Request.Headers.TryGetValue("X-Kiosk-Version", out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        return string.IsNullOrWhiteSpace(bodyVersion) ? null : bodyVersion.Trim();
    }

    private Guid? GetActorId()
    {
        var claim = User?.FindFirst("sub")?.Value
            ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private async Task<(IReadOnlyList<BulkExamTicketImportRow>? Rows, IActionResult? Error)> ParseRosterFileAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return (null, BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Tệp danh sách không hợp lệ.",
                Detail = "Cần tải lên tệp .csv hoặc .xlsx không rỗng."
            }));
        }

        if (_rosterParser is null)
        {
            return (null, StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Dịch vụ đọc danh sách chưa được cấu hình."
            }));
        }

        await using var stream = file.OpenReadStream();
        var parsed = await _rosterParser.ParseAsync(stream, file.FileName, cancellationToken);
        if (parsed.Errors.Count == 0) return (parsed.Rows, null);
        return (null, BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Không thể đọc tệp danh sách.",
            Detail = string.Join(" ", parsed.Errors)
        }));
    }

    private async Task<(IReadOnlyList<CandidateRosterImportRow>? Rows, IActionResult? Error)> ParseCandidateRosterFileAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return (null, BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Tệp danh sách thí sinh không hợp lệ.",
                Detail = "Cần tải lên tệp .xlsx hoặc .csv không rỗng.",
                Type = "https://oralexam.fpt.edu.vn/errors/candidate-roster-400"
            }));
        }

        if (file.Length > OfficialExamRules.MaxCandidateRosterBytes)
        {
            return (null, StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Tệp vượt quá dung lượng cho phép.",
                Detail = $"Tệp danh sách thí sinh không được vượt quá {OfficialExamRules.MaxCandidateRosterBytes / (1024 * 1024)} MB.",
                Type = "https://oralexam.fpt.edu.vn/errors/candidate-roster-413"
            }));
        }

        if (_candidateRosterParser is null)
        {
            return (null, StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Dịch vụ đọc danh sách thí sinh chưa được cấu hình."
            }));
        }

        await using var stream = file.OpenReadStream();
        var parsed = await _candidateRosterParser.ParseAsync(stream, file.FileName, file.Length, cancellationToken);
        if (parsed.IsSuccess)
        {
            return (parsed.Rows, null);
        }

        var statusCode = parsed.ErrorKind switch
        {
            CandidateRosterFileErrorKind.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
            CandidateRosterFileErrorKind.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
            CandidateRosterFileErrorKind.TooManyRows => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest
        };

        return (null, StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status413PayloadTooLarge => "Tệp vượt quá dung lượng cho phép.",
                StatusCodes.Status415UnsupportedMediaType => "Định dạng tệp không được hỗ trợ.",
                StatusCodes.Status422UnprocessableEntity => "Danh sách thí sinh vượt quá giới hạn xử lý.",
                _ => "Không thể đọc tệp danh sách thí sinh."
            },
            Detail = string.Join(" ", parsed.Errors),
            Type = $"https://oralexam.fpt.edu.vn/errors/candidate-roster-{statusCode}"
        }));
    }

    private IActionResult BulkImportProblem(string error) => OfficialExamWorkflowProblem(error);
    private IActionResult OfficialExamProblem(string error) => OfficialExamWorkflowProblem(error);

    private IActionResult OfficialExamWorkflowProblem(string error)
    {
        var (statusCode, detail) = error switch
        {
            _ when error.StartsWith("LOCKED:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status403Forbidden, error["LOCKED:".Length..].Trim()),
            _ when error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status403Forbidden, error["FORBIDDEN:".Length..].Trim()),
            _ when error is OfficialExamErrors.Forbidden or OfficialExamErrors.WrongWorkstation or PublishGradesErrors.Forbidden =>
                (StatusCodes.Status403Forbidden, error),
            _ when error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status404NotFound, error["NOT_FOUND:".Length..].Trim()),
            _ when error is OfficialExamErrors.CourseNotFound
                or OfficialExamErrors.StructureNotFound
                or OfficialExamErrors.SessionNotFound
                or OfficialExamErrors.ShiftNotFound
                or OfficialExamErrors.TicketNotFound
                or OfficialExamErrors.WorkstationTicketNotFound
                or OfficialExamErrors.StudentNotFound
                or PublishGradesErrors.ActorNotFound
                or PublishGradesErrors.ShiftNotFound =>
                (StatusCodes.Status404NotFound, error),
            _ when error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status409Conflict, error["CONFLICT:".Length..].Trim()),
            _ when error is OfficialExamErrors.ShiftAlreadyPublished
                or PublishGradesErrors.AlreadyPublished
                or PublishGradesErrors.IncompleteGrades
                or PublishGradesErrors.ShiftNotEnded =>
                (StatusCodes.Status409Conflict, error),
            _ when error.StartsWith("UNPROCESSABLE:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status422UnprocessableEntity, error["UNPROCESSABLE:".Length..].Trim()),
            _ when error.StartsWith("BAD_REQUEST:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status400BadRequest, error["BAD_REQUEST:".Length..].Trim()),
            _ => (StatusCodes.Status400BadRequest, error)
        };

        var errorType = error switch
        {
            _ when error.Contains("DEVICE_MISMATCH", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/device-mismatch",
            _ when error.Contains("ACTIVE_SESSION_CONFLICT", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/active-session-conflict",
            _ when error.Contains("DEVICE_IN_USE_OTHER_TICKET", StringComparison.OrdinalIgnoreCase) =>
                "https://oralexam.fpt.edu.vn/errors/device-in-use-other-ticket",
            _ => $"https://oralexam.fpt.edu.vn/errors/official-exam-workflow-{statusCode}"
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Thao tác bị từ chối.",
                StatusCodes.Status404NotFound => "Không tìm thấy tài nguyên.",
                StatusCodes.Status409Conflict => "Xung đột dữ liệu hoặc trạng thái tài nguyên.",
                StatusCodes.Status422UnprocessableEntity => "Không thể xử lý yêu cầu nghiệp vụ.",
                _ => "Dữ liệu đầu vào không hợp lệ."
            },
            Detail = string.IsNullOrWhiteSpace(detail) ? error : detail,
            Type = errorType
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
