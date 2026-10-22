using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.Courses.Queries.GetCourseConfiguration;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản trị cấu hình môn học (Dynamic Configurations) do Trưởng Bộ Môn / Admin thiết lập.
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

        var actorRole = User?.IsInRole("admin") == true
            ? "admin"
            : (User?.IsInRole("department_head") == true
                ? "department_head"
                : (User?.IsInRole("lecturer") == true ? "lecturer" : null));

        var command = new UpdateCourseConfigurationCommand(
            id,
            request.TranscriptBufferSeconds,
            request.MaxFollowUpQuestions,
            request.HasFollowUp,
            request.ExamInputMode
        )
        {
            MaxMockExamsPerDay = request.MaxMockExamsPerDay,
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
}
