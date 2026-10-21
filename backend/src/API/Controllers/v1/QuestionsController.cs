using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromFlm;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.Commands.UploadQuestionImage;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "lecturer,department_head,admin")]
public class QuestionsController : ControllerBase
{
    private readonly ISender _sender;

    public QuestionsController(ISender sender)
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

    /// <summary>
    /// Giảng viên tải hình ảnh minh họa đính kèm cho câu hỏi tạo thủ công (MF-03)
    /// </summary>
    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadQuestionImage(
        IFormFile? file,
        [FromForm] string? description = null)
    {
        IFormFile? targetFile = file;
        if (targetFile == null && HttpContext != null && Request != null && Request.HasFormContentType && Request.Form.Files.Count > 0)
        {
            targetFile = Request.Form.Files[0];
        }

        var resolvedDescription = description;
        if (string.IsNullOrWhiteSpace(resolvedDescription) && HttpContext != null && Request?.HasFormContentType == true)
        {
            resolvedDescription = Request.Form["description"].FirstOrDefault()
                ?? Request.Form["imageDescription"].FirstOrDefault();
        }

        if (targetFile == null || targetFile.Length == 0)
        {
            return BadRequest(new { error = "BAD_REQUEST: Không tìm thấy tệp hình ảnh hoặc tệp rỗng." });
        }

        string contentType = "application/octet-stream";
        try
        {
            contentType = !string.IsNullOrWhiteSpace(targetFile.ContentType) ? targetFile.ContentType : "application/octet-stream";
        }
        catch
        {
            contentType = "application/octet-stream";
        }

        using var stream = targetFile.OpenReadStream();
        var command = new UploadQuestionImageCommand(
            stream,
            targetFile.FileName ?? "image.png",
            contentType,
            targetFile.Length,
            resolvedDescription);

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("PAYLOAD_TOO_LARGE:", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = result.Error });
            }

            if (result.Error.StartsWith("UNSUPPORTED_MEDIA_TYPE:", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên hoặc Trưởng Bộ Môn kích hoạt AI sinh câu hỏi từ đề cương FLM theo Barem riêng
    /// </summary>
    [HttpPost("generate-from-flm")]
    public async Task<IActionResult> GenerateFromFlm([FromBody] GenerateQuestionsFromFlmRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var requesterId = currentUserId != Guid.Empty ? currentUserId : (request.RequesterUserId ?? Guid.Empty);

        var command = new GenerateQuestionsFromFlmCommand(
            request.CourseId,
            requesterId,
            request.SelectedCloCodes,
            request.Topics,
            request.NumberOfQuestions,
            request.UsageScope,
            request.TargetBloomLevels,
            request.DifficultyDistribution
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { type = "https://oralexam.fpt.edu.vn/errors/generation-failed", detail = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên lưu câu hỏi dự thảo kèm Barem riêng
    /// </summary>
    [HttpPost]
    [HttpPost("draft")]
    public async Task<IActionResult> CreateDraftQuestion([FromBody] CreateDraftQuestionRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var lecturerId = currentUserId != Guid.Empty ? currentUserId : (request.LecturerId ?? Guid.Empty);

        var command = new CreateDraftQuestionCommand(
            request.CourseId,
            lecturerId,
            request.Title,
            request.Content,
            request.SampleAnswer,
            request.KeyPoints,
            request.Difficulty,
            request.BloomLevel,
            request.Source ?? "manual",
            request.UsageScope ?? "exam",
            request.HasFollowUp,
            request.FollowUpPrompt,
            request.Rubric!,
            request.RubricId,
            request.ApprovedBy,
            request.ImageUrl,
            request.ImageDescription
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new { type = "https://oralexam.fpt.edu.vn/errors/validation-failed", detail = result.Error });
        }

        return CreatedAtAction(nameof(GetQuestionById), new { id = result.Value }, new { questionId = result.Value });
    }

    /// <summary>
    /// Tạo câu hỏi trực tiếp vào kho Luyện tập &amp; Thi thử (practice_questions)
    /// </summary>
    [HttpPost("/api/v1/practice/questions")]
    public async Task<IActionResult> CreatePracticeQuestion([FromBody] CreateDraftQuestionRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var lecturerId = currentUserId != Guid.Empty ? currentUserId : (request.LecturerId ?? Guid.Empty);

        var command = new CreateDraftQuestionCommand(
            request.CourseId,
            lecturerId,
            request.Title,
            request.Content,
            request.SampleAnswer,
            request.KeyPoints,
            request.Difficulty,
            request.BloomLevel,
            request.Source ?? "manual",
            "practice",
            request.HasFollowUp,
            request.FollowUpPrompt,
            request.Rubric!,
            request.RubricId,
            request.ApprovedBy,
            request.ImageUrl,
            request.ImageDescription
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new { type = "https://oralexam.fpt.edu.vn/errors/validation-failed", detail = result.Error });
        }

        return CreatedAtAction(nameof(GetQuestionById), new { id = result.Value }, new { questionId = result.Value });
    }

    /// <summary>
    /// Trưởng Bộ Môn hoặc Quản trị viên tạo câu hỏi trực tiếp vào kho Thi thật (exam_questions)
    /// </summary>
    [HttpPost("/api/v1/official-exams/questions")]
    [Authorize(Roles = "department_head,admin")]
    public async Task<IActionResult> CreateOfficialExamQuestion([FromBody] CreateDraftQuestionRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var lecturerId = currentUserId != Guid.Empty ? currentUserId : (request.LecturerId ?? Guid.Empty);
        var approvedBy = currentUserId != Guid.Empty ? currentUserId : request.ApprovedBy;

        var command = new CreateDraftQuestionCommand(
            request.CourseId,
            lecturerId,
            request.Title,
            request.Content,
            request.SampleAnswer,
            request.KeyPoints,
            request.Difficulty,
            request.BloomLevel,
            request.Source ?? "manual",
            "exam",
            request.HasFollowUp,
            request.FollowUpPrompt,
            request.Rubric!,
            request.RubricId,
            approvedBy,
            request.ImageUrl,
            request.ImageDescription
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new { type = "https://oralexam.fpt.edu.vn/errors/validation-failed", detail = result.Error });
        }

        return CreatedAtAction(nameof(GetQuestionById), new { id = result.Value }, new { questionId = result.Value });
    }

    /// <summary>
    /// Trưởng Bộ Môn phê duyệt và lưu hàng loạt câu hỏi đã sinh từ AI vào ngân hàng câu hỏi (MF-03)
    /// </summary>
    [HttpPost("batch-approve")]
    [Authorize(Roles = "department_head,admin")]
    public async Task<IActionResult> BatchApprove([FromBody] BatchApproveQuestionsRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var approverId = currentUserId != Guid.Empty ? currentUserId : (request.ApproverId ?? Guid.Empty);

        var command = new BatchApproveQuestionsCommand(
            request.CourseId,
            approverId,
            request.Questions
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/forbidden",
                    title = "Không đủ quyền phê duyệt",
                    status = StatusCodes.Status403Forbidden,
                    detail = result.Error
                });
            }

            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/course-not-found",
                    title = "Không tìm thấy môn học",
                    status = StatusCodes.Status404NotFound,
                    detail = result.Error
                });
            }

            if (result.Error.StartsWith("INVARIANT:", StringComparison.OrdinalIgnoreCase))
            {
                return UnprocessableEntity(new
                {
                    type = "https://oralexam.fpt.edu.vn/errors/batch-approve-invariant-violation",
                    title = "Vi phạm ràng buộc Barem 10.00 điểm hoặc Model Answer",
                    status = StatusCodes.Status422UnprocessableEntity,
                    detail = result.Error
                });
            }

            return BadRequest(new
            {
                type = "https://oralexam.fpt.edu.vn/errors/batch-approve-failed",
                title = "Phê duyệt hàng loạt thất bại",
                status = StatusCodes.Status400BadRequest,
                detail = result.Error
            });
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Giảng viên gửi mẻ câu hỏi lên Bộ Môn thẩm định (SUBMITTED_FOR_REVIEW)
    /// </summary>
    [HttpPost("batch-submit-review")]
    public async Task<IActionResult> BatchSubmitReview([FromBody] BatchSubmitQuestionsForReviewRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var lecturerId = currentUserId != Guid.Empty ? currentUserId : (request.LecturerId ?? Guid.Empty);

        var command = new BatchSubmitQuestionsForReviewCommand(
            request.CourseId,
            lecturerId,
            request.QuestionIds,
            request.SubmissionNotes
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new { type = "https://oralexam.fpt.edu.vn/errors/batch-submit-failed", detail = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Trưởng Bộ Môn ra quyết định thẩm định (APPROVED, NEEDS_REVISION, REJECTED)
    /// </summary>
    [HttpPost("{id}/review-decision")]
    [HttpPut("{id}/review-decision")]
    public async Task<IActionResult> ReviewDecision(Guid id, [FromBody] QuestionReviewDecisionRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        var reviewerId = currentUserId != Guid.Empty ? currentUserId : (request.ReviewerId ?? Guid.Empty);

        var command = new ReviewQuestionDecisionCommand(
            id,
            reviewerId,
            request.Decision,
            request.ReviewNotes,
            request.TargetUsageScope
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.Contains("Chỉ Trưởng Bộ Môn", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://oralexam.fpt.edu.vn/errors/forbidden", detail = result.Error });
            }

            return BadRequest(new { type = "https://oralexam.fpt.edu.vn/errors/review-decision-failed", detail = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tra cứu danh mục câu hỏi theo môn học, trạng thái phê duyệt, độ khó, Bloom
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQuestions([FromQuery] GetQuestionsQuery query)
    {
        var result = await _sender.Send(query);
        if (!result.IsSuccess)
        {
            return BadRequest(new { detail = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Xem chi tiết câu hỏi kèm Barem và các tiêu chí con
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetQuestionById(Guid id)
    {
        var result = await _sender.Send(new GetQuestionByIdQuery(id));
        if (!result.IsSuccess)
        {
            return NotFound(new { detail = result.Error });
        }

        return Ok(result.Value);
    }
}
