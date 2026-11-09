using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AddClassStudent;
using OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.RemoveClassStudent;
using OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetClassDetail;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerClasses;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetStudentMyClasses;
using OralExamination.Application.Features.AcademicHierarchy.Queries.LookupClassStudent;
using OralExamination.Application.Features.AcademicHierarchy.Queries.SearchAvailableClasses;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản trị lớp học phần, tìm kiếm/hiển thị lớp học và tính năng sinh viên tham gia lớp học (R5 Academic Hierarchy).
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
    [HttpPost("classes")]
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
            effectiveActorRole,
            request.CourseOfferingId,
            currentUserId);

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

            if (result.Error.Contains("Không tìm thấy", StringComparison.OrdinalIgnoreCase) ||
                result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
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

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("lecturer/course-offerings/{courseOfferingId:guid}/classes")]
    [Authorize(Roles = "lecturer,admin,department_head")]
    public async Task<IActionResult> CreateCourseOfferingClass(
        [FromRoute] Guid courseOfferingId,
        [FromBody] CreateCourseOfferingClassRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = GetActorRole();

        if (string.Equals(actorRole, "lecturer", StringComparison.OrdinalIgnoreCase) &&
            request.LecturerId is { } requestedLecturerId &&
            requestedLecturerId != Guid.Empty &&
            requestedLecturerId != currentUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "FORBIDDEN: Giảng viên không được tạo lớp mạo danh giảng viên khác.",
                Type = "https://oralexam.fpt.edu.vn/errors/class-forbidden"
            });
        }

        var lecturerId = string.Equals(actorRole, "lecturer", StringComparison.OrdinalIgnoreCase)
            ? currentUserId
            : request.LecturerId ?? currentUserId;
        var effectiveActorRole = string.Equals(actorRole, "department_head", StringComparison.OrdinalIgnoreCase)
            ? $"department_head:{currentUserId}"
            : actorRole;

        var result = await _sender.Send(new CreateClassCommand(
            request.Code,
            request.Name,
            Guid.Empty,
            Guid.Empty,
            lecturerId,
            request.JoinCode,
            request.Password,
            request.MaxStudents,
            request.AllowSelfEnrollment,
            request.IsJoinEnabled,
            effectiveActorRole,
            courseOfferingId,
            currentUserId), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                var error when error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) =>
                    StatusCode(StatusCodes.Status403Forbidden, ClassProblem(403, "Forbidden", error)),
                var error when error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) =>
                    NotFound(ClassProblem(404, "Not Found", error)),
                var error when error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) =>
                    Conflict(ClassProblem(409, "Conflict", error)),
                _ => UnprocessableEntity(ClassProblem(422, "Unprocessable Entity", result.Error))
            };
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Lấy danh sách các lớp học phần do Giảng viên phụ trách (hoặc toàn bộ lớp theo bộ lọc đối với Trưởng BM / Admin).
    /// </summary>
    [HttpGet("lecturer/classes")]
    [HttpGet("classes")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> GetLecturerClasses(
        [FromQuery] Guid? courseId = null,
        [FromQuery] Guid? semesterId = null,
        [FromQuery] Guid? courseOfferingId = null,
        [FromQuery] Guid? lecturerId = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = GetActorRole();

        var effectiveLecturerId = string.Equals(actorRole, "lecturer", StringComparison.OrdinalIgnoreCase)
            ? currentUserId
            : lecturerId;

        var query = new GetLecturerClassesQuery(
            effectiveLecturerId,
            courseId,
            semesterId,
            courseOfferingId,
            keyword);

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

    /// <summary>
    /// Sinh viên tìm kiếm & duyệt danh sách các lớp học phần đang mở để xem trạng thái và bấm Tham gia (Join).
    /// </summary>
    [HttpGet("student/classes/search")]
    [HttpGet("student/classes/available")]
    [HttpGet("classes/available")]
    [HttpGet("classes/search")]
    [Authorize(Roles = "student,lecturer,department_head,admin")]
    public async Task<IActionResult> SearchAvailableClasses(
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? courseId = null,
        [FromQuery] Guid? semesterId = null,
        [FromQuery] Guid? courseOfferingId = null,
        [FromQuery] Guid? lecturerId = null,
        [FromQuery] bool onlyAvailableToJoin = false,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();

        var query = new SearchAvailableClassesQuery(
            CurrentStudentId: currentUserId,
            Keyword: keyword,
            CourseId: courseId,
            SemesterId: semesterId,
            CourseOfferingId: courseOfferingId,
            OnlyAvailableToJoin: onlyAvailableToJoin,
            LecturerId: lecturerId);

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

    /// <summary>
    /// Xem chi tiết một lớp học phần (thông tin lớp, danh sách ca thi trong lớp MF-05, và danh sách sinh viên nếu là Giảng viên/TBM/Admin).
    /// </summary>
    [HttpGet("classes/{id:guid}")]
    [HttpGet("lecturer/classes/{id:guid}")]
    [HttpGet("student/classes/{id:guid}")]
    [Authorize(Roles = "lecturer,student,department_head,admin")]
    public async Task<IActionResult> GetClassDetail(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = GetActorRole();

        var query = new GetClassDetailQuery(id, currentUserId, actorRole);
        var result = await _sender.Send(query, cancellationToken);

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
    /// Sinh viên tham gia lớp học phần bằng ClassId (chọn từ danh sách tìm kiếm) hoặc mã JoinCode kèm mật khẩu lớp (Authorize: student, admin).
    /// </summary>
    [NonAction]
    public Task<IActionResult> JoinClass(
        JoinClassRequest request,
        CancellationToken cancellationToken)
    {
        return JoinClass(request, null, cancellationToken);
    }

    [HttpPost("student/classes/join")]
    [HttpPost("classes/join")]
    [HttpPost("student/classes/{classId:guid}/join")]
    [HttpPost("classes/{classId:guid}/join")]
    [Authorize(Roles = "student,admin")]
    public async Task<IActionResult> JoinClass(
        [FromBody] JoinClassRequest request,
        [FromRoute] Guid? classId = null,
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

        var effectiveClassId = classId.HasValue && classId.Value != Guid.Empty
            ? classId
            : request.ClassId;

        var command = new JoinClassCommand(
            currentStudentId,
            request.JoinCode,
            request.Password,
            effectiveClassId);
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
    [HttpGet("classes/my-classes")]
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

    /// <summary>
    /// Giảng viên tra đúng một sinh viên theo mã hoặc email trước khi thêm vào lớp.
    /// </summary>
    [HttpGet("lecturer/classes/students/lookup")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> LookupClassStudent(
        [FromQuery] string? q,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new LookupClassStudentQuery(q), cancellationToken);
        if (!result.IsSuccess)
        {
            return RosterFailure(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên thêm sinh viên vào lớp học phần do mình phụ trách (hỗ trợ MSSV, Email, StudentId hoặc chuỗi định danh).
    /// </summary>
    [HttpPost("lecturer/classes/{classId:guid}/students")]
    [HttpPost("classes/{classId:guid}/students")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> AddStudentToClass(
        [FromRoute] Guid classId,
        [FromBody] AddClassStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = GetActorRole();

        var command = new AddClassStudentCommand(
            ClassId: classId,
            ActorUserId: currentUserId,
            ActorRole: actorRole,
            StudentId: request.StudentId,
            StudentCode: request.StudentCode,
            Email: request.Email,
            Identifier: request.Identifier);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return RosterFailure(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Giảng viên xóa sinh viên khỏi lớp học phần theo StudentId.
    /// </summary>
    [HttpDelete("lecturer/classes/{classId:guid}/students/{studentId:guid}")]
    [HttpDelete("classes/{classId:guid}/students/{studentId:guid}")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> RemoveStudentFromClass(
        [FromRoute] Guid classId,
        [FromRoute] Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = GetActorRole();

        var command = new RemoveClassStudentCommand(
            ClassId: classId,
            ActorUserId: currentUserId,
            ActorRole: actorRole,
            StudentId: studentId);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return RosterFailure(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên xóa sinh viên khỏi lớp học phần bằng thông tin định danh linh hoạt (Query param hoặc Body).
    /// </summary>
    [HttpDelete("lecturer/classes/{classId:guid}/students")]
    [HttpDelete("classes/{classId:guid}/students")]
    [HttpPost("lecturer/classes/{classId:guid}/students/remove")]
    [HttpPost("classes/{classId:guid}/students/remove")]
    [Authorize(Roles = "lecturer,department_head,admin")]
    public async Task<IActionResult> RemoveStudentFlexible(
        [FromRoute] Guid classId,
        [FromBody] RemoveClassStudentRequest? bodyRequest = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] string? studentCode = null,
        [FromQuery] string? email = null,
        [FromQuery] string? identifier = null,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = GetActorRole();

        var effectiveStudentId = bodyRequest?.StudentId ?? studentId;
        var effectiveStudentCode = bodyRequest?.StudentCode ?? studentCode;
        var effectiveEmail = bodyRequest?.Email ?? email;
        var effectiveIdentifier = bodyRequest?.Identifier ?? identifier;

        var command = new RemoveClassStudentCommand(
            ClassId: classId,
            ActorUserId: currentUserId,
            ActorRole: actorRole,
            StudentId: effectiveStudentId,
            StudentCode: effectiveStudentCode,
            Email: effectiveEmail,
            Identifier: effectiveIdentifier);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return RosterFailure(result.Error);
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

    private IActionResult RosterFailure(string error)
    {
        var detail = error;
        if (error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ClassProblem(403, "Forbidden", detail));
        }

        if (error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ClassProblem(404, "Not Found", detail));
        }

        if (error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(ClassProblem(409, "Conflict", detail));
        }

        return BadRequest(ClassProblem(400, "Bad Request", detail));
    }

    private static ProblemDetails ClassProblem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = "https://tools.ietf.org/html/rfc7807"
    };
}

