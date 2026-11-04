using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetStudentMyClasses;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản trị lớp học phần và tính năng sinh viên tham gia lớp học (R5 Academic Hierarchy).
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ClassesController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    /// <summary>
    /// Giảng viên tạo lớp học phần mới (Authorize: lecturer, admin, department_head).
    /// </summary>
    [HttpPost("lecturer/classes")]
    [Authorize(Roles = "lecturer,admin,department_head")]
    public async Task<IActionResult> CreateClass(
        [FromBody] CreateClassRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = GetActorRole();

        // Chặn Giảng viên mạo danh Giảng viên khác qua request.LecturerId
        if (string.Equals(actorRole, "lecturer", StringComparison.OrdinalIgnoreCase) &&
            currentUserId != Guid.Empty &&
            request.LecturerId.HasValue &&
            request.LecturerId.Value != Guid.Empty &&
            request.LecturerId.Value != currentUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "FORBIDDEN: Giảng viên không được phép tạo lớp học phần mạo danh giảng viên khác.",
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        var lecturerId = string.Equals(actorRole, "lecturer", StringComparison.OrdinalIgnoreCase) && currentUserId != Guid.Empty
            ? currentUserId
            : (request.LecturerId.HasValue && request.LecturerId.Value != Guid.Empty ? request.LecturerId.Value : currentUserId);

        var effectiveActorRole = string.Equals(actorRole, "department_head", StringComparison.OrdinalIgnoreCase) && currentUserId != Guid.Empty
            ? $"department_head:{currentUserId}"
            : actorRole;

        var command = new CreateClassCommand(
            request.Code,
            request.Name,
            request.CourseId,
            request.SemesterId,
            lecturerId,
            request.JoinCode,
            request.Password,
            request.MaxStudents,
            request.AllowSelfEnrollment,
            request.IsJoinEnabled,
            effectiveActorRole);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.Contains("Không tìm thấy", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Unprocessable Entity",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Giảng viên phụ trách lớp / Trưởng bộ môn / Admin đặt lại hoặc đổi mật khẩu tham gia lớp học phần.
    /// </summary>
    [HttpPut("/api/v1/lecturer/classes/{id:guid}/password")]
    [HttpPost("/api/v1/classes/{id:guid}/reset-password")]
    [HttpPost("lecturer/classes/{id:guid}/reset-password")]
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> ResetClassPassword(
        [FromRoute] Guid id,
        [FromBody] ResetClassPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = GetActorRole();

        var command = new ResetClassPasswordCommand(
            id,
            currentUserId,
            request.NewPassword,
            request.NewJoinCode,
            request.IsJoinEnabled,
            actorRole);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Unprocessable Entity",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sinh viên tham gia lớp học phần bằng mã JoinCode và mật khẩu (Authorize: student, admin).
    /// </summary>
    [HttpPost("student/classes/join")]
    [Authorize(Roles = "student,admin")]
    public async Task<IActionResult> JoinClass(
        [FromBody] JoinClassRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentStudentId = GetCurrentUserId();
        if (currentStudentId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Không xác định được danh tính sinh viên.",
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        var command = new JoinClassCommand(currentStudentId, request.JoinCode, request.Password);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = result.Error["NOT_FOUND:".Length..].Trim(),
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("INVALID_PASSWORD:", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = result.Error["INVALID_PASSWORD:".Length..].Trim(),
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            if (result.Error.StartsWith("JOIN_DISABLED:", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = result.Error["JOIN_DISABLED:".Length..].Trim(),
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

        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy danh sách các lớp học phần mà sinh viên đã ghi danh (Authorize: student, admin).
    /// </summary>
    [HttpGet("student/classes/my-classes")]
    [Authorize(Roles = "student,admin")]
    public async Task<IActionResult> GetMyClasses(
        [FromQuery] Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        var targetStudentId = (User?.IsInRole("admin") == true && studentId.HasValue && studentId.Value != Guid.Empty)
            ? studentId.Value
            : GetCurrentUserId();

        if (targetStudentId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Không xác định được danh tính sinh viên.",
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        var query = new GetStudentMyClassesQuery(targetStudentId);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        var claimValue = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User?.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var claimId) ? claimId : Guid.Empty;
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
}
