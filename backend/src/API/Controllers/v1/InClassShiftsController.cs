using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OralExamination.API.Hubs;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.ActivateInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;
using OralExamination.Application.Features.InClassShifts.Commands.CloseInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;
using OralExamination.Application.Features.InClassShifts.Commands.FinishStudentInClassExam;
using OralExamination.Application.Features.InClassShifts.Commands.ResumeInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftById;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftCurrentPassword;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftSubmissions;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftsByClass;
using OralExamination.Application.Features.InClassShifts.Queries.GetMyInClassShifts;
using OralExamination.Application.Features.InClassShifts.Queries.GetNextInClassFollowUp;
using OralExamination.Application.Features.InClassShifts.Queries.GetStudentInClassSubmission;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản lý phân hệ Ca thi trong Lớp & Mật khẩu TOTP xoay vòng 5 phút (MF-05 / R6).
/// Phân quyền RBAC nghiêm ngặt: Giảng viên phụ trách, Trưởng bộ môn, Admin và Sinh viên.
/// </summary>
[ApiController]
[Route("api/v1/in-class-shifts")]
[Authorize]
public class InClassShiftsController(
    ISender sender,
    IHubContext<PracticeHub, IPracticeClient>? hubContext = null) : ControllerBase
{
    private Guid GetAuthenticatedUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Giảng viên tạo ca thi mới cho lớp học phần (MF-05).
    /// </summary>
    [HttpPost]
    [HttpPost("/api/v1/lecturer/in-class-shifts")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    [ProducesResponseType(typeof(InClassExamShiftResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateShift(
        [FromBody] CreateInClassShiftRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new CreateInClassShiftCommand(
            userId,
            request.ClassId,
            request.ShiftName,
            request.StartTime,
            request.EndTime,
            request.Description,
            request.QuestionPoolMode ?? "ALL_COURSE_BANK",
            request.SelectedQuestionIds,
            request.TotalQuestionsPerStudent ?? 1,
            request.HasFollowUp ?? false,
            request.MaxFollowUpQuestions ?? 2,
            request.IsAutoPublishGrades ?? true,
            request.ExamDurationMinutes ?? 20
        );

        var result = await sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.LecturerNotAuthorized)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = result.Error,
                    Status = StatusCodes.Status403Forbidden
                });
            }

            if (result.Error == InClassShiftErrors.InsufficientQuestionPool)
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Title = "Unprocessable Entity",
                    Detail = result.Error,
                    Status = StatusCodes.Status422UnprocessableEntity
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return CreatedAtAction(nameof(GetShiftById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Giảng viên kích hoạt ca kiểm tra để chiếu mã OTP và cho sinh viên vào làm bài.
    /// </summary>
    [HttpPost("{id}/activate")]
    [HttpPost("/api/v1/lecturer/in-class-shifts/{id}/activate")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    [ProducesResponseType(typeof(InClassExamShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateShift(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new ActivateInClassShiftCommand(userId, id), cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.LecturerNotAuthorized)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = result.Error,
                    Status = StatusCodes.Status403Forbidden
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên đóng ca đang chờ hoặc đang mở. Sinh viên không vào ca hoặc nộp bài được nữa.
    /// </summary>
    [HttpPost("{id}/close")]
    [HttpPost("/api/v1/lecturer/in-class-shifts/{id}/close")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    [ProducesResponseType(typeof(CloseInClassShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CloseShift(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new CloseInClassShiftCommand(userId, id), cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.LecturerNotAuthorized)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = result.Error,
                    Status = StatusCodes.Status403Forbidden
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên lấy toàn bộ danh sách các ca kiểm tra trên lớp thuộc các lớp học phần đang tham gia.
    /// </summary>
    [HttpGet("my")]
    [HttpGet("/api/v1/student/in-class-shifts/my")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(IReadOnlyList<InClassExamShiftResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyShifts(CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new GetMyInClassShiftsQuery(studentId), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Xem thông tin chi tiết ca thi kèm danh sách sinh viên tham gia và bài nộp.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HttpGet("/api/v1/lecturer/in-class-shifts/{id:guid}")]
    [HttpGet("/api/v1/student/in-class-shifts/{id:guid}")]
    [Authorize(Roles = "lecturer,department_head,admin,student")]
    [ProducesResponseType(typeof(InClassShiftDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShiftById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new GetInClassShiftByIdQuery(userId, id);
        var result = await sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.ShiftNotFound)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Not Found",
                    Detail = result.Error,
                    Status = StatusCodes.Status404NotFound
                });
            }

            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Forbidden",
                Detail = result.Error,
                Status = StatusCodes.Status403Forbidden
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên lấy mật khẩu OTP xoay vòng 5 phút hiện tại để chiếu lên màn hình lớp học.
    /// </summary>
    [HttpGet("{id}/current-password")]
    [HttpGet("{id}/active-password")]
    [HttpGet("/api/v1/lecturer/in-class-shifts/{id}/current-password")]
    [HttpGet("/api/v1/lecturer/in-class-shifts/{id}/active-password")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    [ProducesResponseType(typeof(ActiveShiftPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentPassword(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new GetInClassShiftCurrentPasswordQuery(userId, id);
        var result = await sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.ShiftNotFound)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Not Found",
                    Detail = result.Error,
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên xem danh sách các bài nộp và điểm số AI của ca thi.
    /// </summary>
    [HttpGet("{id}/submissions")]
    [HttpGet("/api/v1/lecturer/in-class-shifts/{id}/submissions")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    [ProducesResponseType(typeof(IReadOnlyList<InClassExamSubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSubmissions(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new GetInClassShiftSubmissionsQuery(userId, id);
        var result = await sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên hậu kiểm điểm thành phần, nhập nhận xét và điều chỉnh điểm.
    /// </summary>
    [HttpPut("{id}/submissions/{submissionId}/grade")]
    [HttpPut("{id}/submissions/{submissionId}/audit")]
    [HttpPut("submissions/{submissionId}/adjust-score")]
    [HttpPut("/api/v1/lecturer/in-class-shifts/{id}/submissions/{submissionId}/grade")]
    [HttpPut("/api/v1/lecturer/in-class-shifts/{id}/submissions/{submissionId}/audit")]
    [HttpPut("/api/v1/lecturer/in-class-shifts/submissions/{submissionId}/adjust-score")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    [ProducesResponseType(typeof(InClassExamSubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AdjustSubmissionScore(
        [FromRoute] Guid id,
        [FromRoute] Guid submissionId,
        [FromBody] AdjustSubmissionScoreRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new AdjustInClassSubmissionScoreCommand(
            userId,
            submissionId,
            request.EffectiveFinalScore,
            request.EffectiveNotes
        );

        var result = await sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.ShiftLocked)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = result.Error,
                    Status = StatusCodes.Status403Forbidden
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (hubContext is not null)
        {
            try
            {
                await hubContext.Clients.Group($"session_{result.Value.ShiftId:D}").InClassSubmissionGraded(result.Value);
            }
            catch
            {
                // Không chặn phản hồi HTTP nếu kết nối SignalR tạm thời gián đoạn
            }
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên chốt bảng điểm ca thi, kích hoạt One-Way Lock khóa 1 chiều dữ liệu.
    /// </summary>
    [HttpPost("{id}/finalize-grades")]
    [HttpPost("/api/v1/lecturer/in-class-shifts/{id}/finalize-grades")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    [ProducesResponseType(typeof(FinalizeInClassGradesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FinalizeGrades(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new FinalizeInClassShiftGradesCommand(userId, id);
        var result = await sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.ShiftLocked)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = result.Error,
                    Status = StatusCodes.Status403Forbidden
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (hubContext is not null)
        {
            try
            {
                await hubContext.Clients.Group($"session_{id:D}").InClassShiftGradesFinalized(result.Value);
            }
            catch
            {
                // Không chặn phản hồi HTTP nếu kết nối SignalR tạm thời gián đoạn
            }
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy danh sách ca thi của một lớp học phần.
    /// </summary>
    [HttpGet("by-class/{classId}")]
    [HttpGet("/api/v1/lecturer/in-class-shifts/by-class/{classId}")]
    [HttpGet("/api/v1/student/in-class-shifts/by-class/{classId}")]
    [Authorize(Roles = "lecturer,department_head,admin,student")]
    [ProducesResponseType(typeof(IReadOnlyList<InClassExamShiftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetShiftsByClass(
        [FromRoute] Guid classId,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new GetInClassShiftsByClassQuery(userId, classId);
        var result = await sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên nhập mã OTP 6 ký tự để tham gia vào ca thi lớp học.
    /// </summary>
    [HttpPost("{id}/enter")]
    [HttpPost("{id}/join")]
    [HttpPost("/api/v1/student/in-class-shifts/{id}/enter")]
    [HttpPost("/api/v1/student/in-class-shifts/{id}/join")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(JoinInClassShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnterShift(
        [FromRoute] Guid id,
        [FromBody] EnterInClassShiftRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new EnterInClassShiftCommand(studentId, id, request.OtpCode);
        var result = await sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.InvalidOtp)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = result.Error,
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            if (result.Error == InClassShiftErrors.InsufficientQuestionPool)
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Title = "Unprocessable Entity",
                    Detail = result.Error,
                    Status = StatusCodes.Status422UnprocessableEntity
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên vào lại phòng sau sự cố (đóng tab / crash) — không cần OTP; đồng hồ cá nhân vẫn chạy.
    /// </summary>
    [HttpPost("{id}/resume")]
    [HttpPost("/api/v1/student/in-class-shifts/{id}/resume")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(JoinInClassShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResumeShift(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new ResumeInClassShiftCommand(studentId, id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên kết thúc phiên làm bài — khóa quyền vào lại ca.
    /// </summary>
    [HttpPost("{id}/finish")]
    [HttpPost("/api/v1/student/in-class-shifts/{id}/finish")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(FinishStudentInClassExamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FinishExam(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new FinishStudentInClassExamCommand(studentId, id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên nộp bài làm transcript vấn đáp, đẩy vào hàng đợi chấm tự động.
    /// </summary>
    [HttpPost("{id}/submit-answer")]
    [HttpPost("/api/v1/student/in-class-shifts/{id}/submit-answer")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(SubmitInClassAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] Guid id,
        [FromBody] SubmitInClassAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var effectiveTranscript = !string.IsNullOrWhiteSpace(request.TranscriptText)
            ? request.TranscriptText
            : (request.Transcript ?? string.Empty);

        var command = new SubmitInClassShiftAnswerCommand(
            studentId,
            id,
            request.QuestionId,
            effectiveTranscript,
            request.IsFollowUp ?? false,
            request.ParentSubmissionId,
            request.FollowUpIndex ?? 0
        );

        var result = await sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error == InClassShiftErrors.ShiftLocked)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = result.Error,
                    Status = StatusCodes.Status403Forbidden
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh câu hỏi phụ (Follow-up) theo ngữ cảnh câu trả lời chính khi Giảng viên bật HasFollowUp cho ca thi trên lớp (MF-05, Zero-R2).
    /// </summary>
    [HttpPost("{id}/submissions/{submissionId}/next-followup")]
    [HttpPost("/api/v1/student/in-class-shifts/{id}/submissions/{submissionId}/next-followup")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(InClassNextFollowUpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNextFollowUp(
        [FromRoute] Guid id,
        [FromRoute] Guid submissionId,
        CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(
            new GetNextInClassFollowUpQuery(studentId, id, submissionId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên xem lại bài làm của mình trong ca thi (hỗ trợ lọc theo questionId và followUpIndex).
    /// </summary>
    [HttpGet("{id}/my-submission")]
    [HttpGet("/api/v1/student/in-class-shifts/{id}/my-submission")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(InClassExamSubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubmission(
        [FromRoute] Guid id,
        [FromQuery] Guid? questionId = null,
        [FromQuery] int? followUpIndex = null,
        CancellationToken cancellationToken = default)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new GetStudentInClassSubmissionQuery(studentId, id, questionId, followUpIndex);
        var result = await sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên xem toàn bộ danh sách các lượt nộp bài (N câu chính + các lượt follow-up) của chính mình trong ca thi.
    /// </summary>
    [HttpGet("{id}/my-submissions")]
    [HttpGet("{id}/results")]
    [HttpGet("/api/v1/student/in-class-shifts/{id}/my-submissions")]
    [HttpGet("/api/v1/student/in-class-shifts/{id}/results")]
    [Authorize(Roles = "student")]
    [ProducesResponseType(typeof(IReadOnlyList<InClassExamSubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubmissions(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var studentId = GetAuthenticatedUserId();
        if (studentId == Guid.Empty)
        {
            return Unauthorized();
        }

        var shiftDetailResult = await sender.Send(new GetInClassShiftByIdQuery(studentId, id), cancellationToken);
        if (!shiftDetailResult.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = shiftDetailResult.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(shiftDetailResult.Value.Submissions);
    }
}

public sealed record CreateInClassShiftRequest(
    Guid ClassId,
    string ShiftName,
    DateTime StartTime,
    DateTime EndTime,
    string? Description = null,
    string? QuestionPoolMode = "ALL_COURSE_BANK",
    IReadOnlyList<Guid>? SelectedQuestionIds = null,
    int? TotalQuestionsPerStudent = 1,
    bool? HasFollowUp = false,
    int? MaxFollowUpQuestions = 2,
    bool? IsAutoPublishGrades = true,
    int? ExamDurationMinutes = 20
);

public sealed record EnterInClassShiftRequest(string OtpCode);

public sealed record SubmitInClassAnswerRequest(
    Guid QuestionId,
    string TranscriptText = "",
    bool? IsFollowUp = false,
    Guid? ParentSubmissionId = null,
    int? FollowUpIndex = 0,
    string? Transcript = null
);

public sealed record AdjustSubmissionScoreRequest(
    decimal? FinalScore = null,
    string? LecturerNotes = null,
    decimal? AdjustedScore = null,
    decimal? LecturerScore = null,
    string? OverrideReason = null,
    string? LecturerComment = null)
{
    public decimal EffectiveFinalScore => FinalScore ?? AdjustedScore ?? LecturerScore ?? 0m;
    public string? EffectiveNotes => !string.IsNullOrWhiteSpace(LecturerNotes)
        ? LecturerNotes
        : !string.IsNullOrWhiteSpace(OverrideReason)
            ? OverrideReason
            : LecturerComment;
}
