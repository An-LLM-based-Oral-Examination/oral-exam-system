using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.QuestionBank.Queries.GetFlmSyllabus;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/flm")]
[Authorize(Roles = "lecturer,department_head,admin")]
public class FlmController : ControllerBase
{
    private readonly ISender _sender;

    public FlmController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Tra cứu đề cương học phần (Syllabus), CLOs và danh sách chủ đề từ FLM phục vụ sinh đề AI (MF-03)
    /// </summary>
    [HttpGet("courses/{courseId:guid}/syllabus")]
    public async Task<IActionResult> GetCourseSyllabus(
        Guid courseId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTopic = null,
        [FromQuery] string? cloCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFlmSyllabusQuery(courseId, pageNumber, pageSize, searchTopic, cloCode);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/flm-syllabus-not-found",
                    title = "Không tìm thấy đề cương FLM",
                    status = StatusCodes.Status404NotFound,
                    detail = result.Error
                });
            }

            return BadRequest(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/flm-syllabus-error",
                title = "Lỗi truy xuất đề cương FLM",
                status = StatusCodes.Status400BadRequest,
                detail = result.Error
            });
        }

        return Ok(result.Value);
    }
}
