using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AssignDepartmentHead;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AssignLecturersToCourse;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseLecturers;
using OralExamination.Application.Features.Courses.Commands.CreateCourse;
using OralExamination.Application.Features.Courses.Commands.UpdateCourse;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.Courses.Queries.GetCourseConfiguration;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản trị môn học và cấu hình môn học (Dynamic Configurations) do Trưởng Bộ Môn / Admin thiết lập.
/// </summary>
[ApiController]
[Route("api/v1/courses")]
[Route("api/v1/academic/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ISender _sender;

    public CoursesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các môn học đang hoạt động (phục vụ Student Portal & chọn môn luyện tập MF-01).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCourses()
    {
        var query = new OralExamination.Application.Features.Courses.Queries.GetCourses.GetCoursesQuery();
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/courses-query-failed",
                title = "Bad Request",
                status = 400,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Quản trị viên (Admin) tạo mới môn học trong chuỗi phân cấp học thuật R5.
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost]
    [HttpPost("/api/v1/admin/courses")]
    public async Task<IActionResult> CreateCourse(
        [FromBody] CreateCourseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateCourseCommand(
            request.Code,
            request.Name,
            request.Credits,
            request.Description,
            request.DepartmentHeadUserId,
            request.MaxMockExamsPerDay,
            request.MockExamDurationMinutes,
            request.MockExamTotalQuestions,
            request.IsActive,
            request.SemesterId);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapCourseProblem(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Quản trị viên (Admin) cập nhật thông tin môn học trong chuỗi phân cấp học thuật R5.
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPut("{id:guid}")]
    [HttpPut("/api/v1/admin/courses/{id:guid}")]
    public async Task<IActionResult> UpdateCourse(
        Guid id,
        [FromBody] UpdateCourseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateCourseCommand(
            id,
            request.Code,
            request.Name,
            request.Credits,
            request.Description,
            request.DepartmentHeadUserId,
            request.MaxMockExamsPerDay,
            request.MockExamDurationMinutes,
            request.MockExamTotalQuestions,
            request.IsActive,
            request.SemesterId);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapCourseProblem(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Cập nhật cấu hình động của môn học (Mở quyền cho Trưởng Bộ Môn / Admin).
    /// Trưởng Bộ Môn cấu hình tính năng hỏi chuyên sâu, thời gian đệm và cấu trúc đề.
    /// Riêng hạn ngạch thi thử (MaxMockExamsPerDay) chỉ được phép cấu hình bởi Quản trị viên (Admin).
    /// </summary>
    [Authorize(Roles = "department_head,admin")]
    [HttpPut("{id:guid}/configurations")]
    [HttpPut("{id:guid}/exam-config")]
    public async Task<IActionResult> UpdateConfiguration(Guid id, [FromBody] UpdateCourseConfigRequestDto request)
    {
        if (User?.IsInRole("lecturer") == true && User?.IsInRole("admin") != true && User?.IsInRole("department_head") != true)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "Giảng viên không có quyền cấu hình tham số môn học.",
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        // R2 Check: Hạn ngạch thi thử (MaxMockExamsPerDay) chỉ được phép cấu hình bởi Admin
        if (request.MaxMockExamsPerDay.HasValue && (User?.IsInRole("admin") != true))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "Chỉ có Quản trị viên (Admin) mới có quyền cấu hình hạn ngạch thi thử (MaxMockExamsPerDay).",
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        var actorRole = GetActorRoleWithId();

        var command = new UpdateCourseConfigurationCommand(
            id,
            request.TranscriptBufferSeconds,
            request.MaxFollowUpQuestions,
            request.HasFollowUp,
            request.ExamInputMode
        )
        {
            MaxMockExamsPerDay = request.MaxMockExamsPerDay,
            MockExamDurationMinutes = request.MockExamDurationMinutes,
            MockExamTotalQuestions = request.MockExamTotalQuestions,
            MockExamBloomDistribution = request.MockExamBloomDistribution,
            ActorRole = actorRole
        };

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error != null && result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7807"
                });
            }

            return UnprocessableEntity(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/config-failed",
                title = "Unprocessable Entity",
                status = 422,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy thông tin cấu hình động hiện tại của môn học.
    /// </summary>
    [HttpGet("{id:guid}/configurations")]
    [HttpGet("{id:guid}/exam-config")]
    public async Task<IActionResult> GetConfiguration(Guid id)
    {
        var query = new GetCourseConfigurationQuery(id);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            return NotFound(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/course-not-found",
                title = "Not Found",
                status = 404,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy danh sách Chuẩn đầu ra (CLOs) của môn học.
    /// </summary>
    [HttpGet("{id:guid}/clos")]
    public async Task<IActionResult> GetCourseClos(Guid id, [FromQuery] bool? activeOnly)
    {
        var query = new OralExamination.Application.Features.Courses.Queries.GetCourseClos.GetCourseClosQuery(id, activeOnly);
        var result = await _sender.Send(query);

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
    /// Tạo mới Chuẩn đầu ra (CLO) cho môn học (Trưởng bộ môn / Admin).
    /// </summary>
    [Authorize(Roles = "department_head,admin")]
    [HttpPost("{id:guid}/clos")]
    public async Task<IActionResult> CreateCourseClo(Guid id, [FromBody] CreateCourseCloRequest request)
    {
        var actorRole = GetActorRoleWithId();
        var command = new OralExamination.Application.Features.Courses.Commands.CreateCourseClo.CreateCourseCloCommand(
            id,
            request.CloCode,
            request.Description,
            request.BloomLevel,
            request.OrderIndex,
            actorRole);

        var result = await _sender.Send(command);
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

            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Unprocessable Entity",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return CreatedAtAction(nameof(GetCourseClos), new { id }, result.Value);
    }

    /// <summary>
    /// Cập nhật Chuẩn đầu ra (CLO) cho môn học (Trưởng bộ môn / Admin).
    /// </summary>
    [Authorize(Roles = "department_head,admin")]
    [HttpPut("{id:guid}/clos/{cloId:guid}")]
    public async Task<IActionResult> UpdateCourseClo(Guid id, Guid cloId, [FromBody] UpdateCourseCloRequest request)
    {
        var actorRole = GetActorRoleWithId();
        var command = new OralExamination.Application.Features.Courses.Commands.UpdateCourseClo.UpdateCourseCloCommand(
            id,
            cloId,
            request.CloCode,
            request.Description,
            request.BloomLevel,
            request.OrderIndex,
            request.IsActive,
            actorRole);

        var result = await _sender.Send(command);
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
    /// Xóa Chuẩn đầu ra (CLO) của môn học (Trưởng bộ môn / Admin).
    /// </summary>
    [Authorize(Roles = "department_head,admin")]
    [HttpDelete("{id:guid}/clos/{cloId:guid}")]
    public async Task<IActionResult> DeleteCourseClo(Guid id, Guid cloId)
    {
        var actorRole = GetActorRoleWithId();
        var command = new OralExamination.Application.Features.Courses.Commands.DeleteCourseClo.DeleteCourseCloCommand(id, cloId, actorRole);
        var result = await _sender.Send(command);

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

            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Unprocessable Entity",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Gán Trưởng bộ môn phụ trách môn học (Admin).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost("/api/v1/admin/courses/{id:guid}/assign-department-head")]
    [HttpPost("{id:guid}/assign-department-head")]
    public async Task<IActionResult> AssignDepartmentHead(Guid id, [FromBody] AssignDepartmentHeadRequest request)
    {
        var actorRole = GetActorRole();
        var command = new AssignDepartmentHeadCommand(id, request.DepartmentHeadUserId, actorRole);
        var result = await _sender.Send(command);

        if (!result.IsSuccess)
        {
            if (result.Error.Contains("Không tìm thấy môn học", StringComparison.OrdinalIgnoreCase))
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

        return Ok(result.Value);
    }

    /// <summary>
    /// Phân công danh sách giảng viên phụ trách môn học (Trưởng bộ môn / Admin).
    /// </summary>
    [Authorize(Roles = "department_head,admin")]
    [HttpPost("/api/v1/department-head/courses/{id:guid}/lecturers")]
    [HttpPost("{id:guid}/lecturers")]
    public async Task<IActionResult> AssignLecturers(Guid id, [FromBody] AssignLecturersToCourseRequest request)
    {
        var actorRole = GetActorRole();
        var currentUserId = GetCurrentUserId();

        var command = new AssignLecturersToCourseCommand(
            id,
            request.LecturerUserIds,
            currentUserId,
            request.SemesterId,
            actorRole);

        var result = await _sender.Send(command);

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

        return Ok(result.Value);
    }

    /// <summary>
    /// Lấy danh sách giảng viên phụ trách môn học (Mọi role đã đăng nhập).
    /// </summary>
    [HttpGet("{id:guid}/lecturers")]
    public async Task<IActionResult> GetLecturers(Guid id)
    {
        var query = new GetCourseLecturersQuery(id);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7807"
            });
        }

        return Ok(result.Value);
    }

    private IActionResult MapCourseProblem(string error)
    {
        var statusCode = error switch
        {
            var err when err.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var err when err.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var err when err.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not Found",
                StatusCodes.Status409Conflict => "Conflict",
                _ => "Unprocessable Entity"
            },
            Detail = error,
            Type = "https://tools.ietf.org/html/rfc7807"
        });
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

    private string? GetActorRoleWithId()
    {
        var role = GetActorRole();
        var userId = GetCurrentUserId();
        if (string.Equals(role, "department_head", StringComparison.OrdinalIgnoreCase) && userId != Guid.Empty)
        {
            return $"department_head:{userId}";
        }

        return role;
    }
}
