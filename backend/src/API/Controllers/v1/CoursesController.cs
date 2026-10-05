using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.Courses.Queries.GetCourseConfiguration;

namespace API.Controllers.v1;

/// <summary>
/// Controller quản trị cấu hình môn học (Dynamic Configurations) do Giảng viên / Trưởng Bộ Môn / Admin thiết lập.
/// Mở quyền cho Giảng viên phụ trách cấu hình thời gian đệm sửa transcript (TranscriptBufferSeconds: 10–300s)
/// và số lượng câu hỏi phụ chuyên sâu (MaxFollowUpQuestions: 1–2 câu, HasFollowUp).
/// </summary>
[ApiController]
[Route("api/v1/courses")]
[Route("api/v1/academic/courses")]
public class CoursesController : ControllerBase
{
    private readonly ISender _sender;

    public CoursesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Cập nhật cấu hình động của môn học (Mở quyền cho Giảng viên / Trưởng Bộ Môn / Admin).
    /// Giảng viên trực tiếp cấu hình tính năng hỏi chuyên sâu (1–2 câu) và thời gian đệm.
    /// </summary>
    [HttpPut("{id:guid}/configurations")]
    public async Task<IActionResult> UpdateConfiguration(Guid id, [FromBody] UpdateCourseConfigRequestDto request)
    {
        var command = new UpdateCourseConfigurationCommand(
            id,
            request.TranscriptBufferSeconds,
            request.MaxFollowUpQuestions,
            request.HasFollowUp,
            request.ExamInputMode
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
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
