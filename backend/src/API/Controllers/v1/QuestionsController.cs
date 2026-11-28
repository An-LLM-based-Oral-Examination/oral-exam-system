using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.API.Configuration;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromClo;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.Commands.UploadQuestionImage;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "lecturer,department_head")]
public class QuestionsController : ControllerBase
{
    private readonly ISender _sender;

    public QuestionsController(ISender sender)
    {
        _sender = sender;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User?.FindFirstValue("sub");

        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Giảng viên tải hình ảnh minh họa đính kèm cho câu hỏi tạo thủ công (MF-03)
    /// </summary>
    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadQuestionImage(
        IFormFile? file,
        [FromForm] string? description = null)
    {
        if (file == null && HttpContext != null && Request != null &&
            !string.IsNullOrWhiteSpace(Request.ContentType) && !Request.HasFormContentType)
        {
            return QuestionProblem(StatusCodes.Status415UnsupportedMediaType,
                "UNSUPPORTED_MEDIA_TYPE: Yêu cầu định dạng multipart/form-data khi tải lên hình ảnh câu hỏi.");
        }

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
            return QuestionProblem(400, "Không tìm thấy tệp hình ảnh hoặc tệp rỗng.");
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
                return QuestionProblem(413, result.Error);
            }

            if (result.Error.StartsWith("UNSUPPORTED_MEDIA_TYPE:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(415, result.Error);
            }

            return QuestionProblem(400, result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Giảng viên hoặc Trưởng Bộ Môn kích hoạt AI sinh câu hỏi từ Chuẩn đầu ra môn học (course_clos 3NF) (MF-03).
    /// Quét phát hiện cờ trùng lặp isFlagged tự động và chuẩn hóa Barem Rubric 10.0đ.
    /// </summary>
    [HttpPost("generate-from-clo")]
    [HttpPost("generate-from-flm")] // Route tương thích ngược cho client cũ
    [Authorize(Roles = "lecturer,department_head")]
    public async Task<IActionResult> GenerateFromClo([FromBody] GenerateQuestionsFromCloRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty) return Unauthorized();
        var requesterId = currentUserId;

        var command = new GenerateQuestionsFromCloCommand(
            request.CourseId,
            requesterId,
            request.SelectedCloCodes,
            request.Topics ?? [],
            request.NumberOfQuestions,
            request.UsageScope,
            request.TargetBloomLevels,
            request.DifficultyDistribution
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(403, result.Error);
            }

            return QuestionProblem(400, result.Error);
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
        if (currentUserId == Guid.Empty) return Unauthorized();

        var command = new CreateDraftQuestionCommand(
            request.CourseId,
            currentUserId,
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
            null,
            request.ImageUrl,
            request.ImageDescription,
            request.CloId,
            request.StructureId,
            currentUserId,
            request.IsPaperPrivate
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(403, result.Error);
            }

            return QuestionProblem(422, result.Error);
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
        if (currentUserId == Guid.Empty) return Unauthorized();

        var command = new CreateDraftQuestionCommand(
            request.CourseId,
            currentUserId,
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
            null,
            request.ImageUrl,
            request.ImageDescription,
            request.CloId,
            request.StructureId,
            currentUserId,
            request.IsPaperPrivate
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(403, result.Error);
            }

            return QuestionProblem(422, result.Error);
        }

        return CreatedAtAction(nameof(GetQuestionById), new { id = result.Value }, new { questionId = result.Value });
    }

    /// <summary>
    /// Trưởng Bộ Môn tạo câu hỏi trực tiếp vào kho Thi thật (exam_questions)
    /// </summary>
    [HttpPost("/api/v1/official-exams/questions")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> CreateOfficialExamQuestion([FromBody] CreateDraftQuestionRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty) return Unauthorized();

        var command = new CreateDraftQuestionCommand(
            request.CourseId,
            currentUserId,
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
            null,
            request.ImageUrl,
            request.ImageDescription,
            request.CloId,
            request.StructureId,
            currentUserId,
            request.IsPaperPrivate
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(403, result.Error);
            }

            return QuestionProblem(422, result.Error);
        }

        return CreatedAtAction(nameof(GetQuestionById), new { id = result.Value }, new { questionId = result.Value });
    }

    /// <summary>
    /// Trưởng Bộ Môn phê duyệt và lưu hàng loạt câu hỏi đã sinh từ AI vào ngân hàng câu hỏi (MF-03)
    /// </summary>
    [HttpPost("batch-approve")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> BatchApprove([FromBody] BatchApproveQuestionsRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty) return Unauthorized();
        var approverId = currentUserId;

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
                return QuestionProblem(403, result.Error);
            }

            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(404, result.Error);
            }

            if (result.Error.StartsWith("INVARIANT:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.StartsWith("VALIDATION:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(422, result.Error);
            }

            return QuestionProblem(400, result.Error);
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
        if (currentUserId == Guid.Empty) return Unauthorized();
        var lecturerId = currentUserId;

        var command = new BatchSubmitQuestionsForReviewCommand(
            request.CourseId,
            lecturerId,
            request.QuestionIds,
            request.SubmissionNotes
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(403, result.Error);
            }

            if (result.Error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(409, result.Error);
            }

            return QuestionProblem(422, result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Trưởng Bộ Môn ra quyết định thẩm định (APPROVED, NEEDS_REVISION, REJECTED)
    /// </summary>
    [Authorize(Roles = "department_head")]
    [HttpPost("{id}/review-decision")]
    [HttpPut("{id}/review-decision")]
    public async Task<IActionResult> ReviewDecision(Guid id, [FromBody] QuestionReviewDecisionRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == Guid.Empty) return Unauthorized();
        var reviewerId = currentUserId;

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
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("Chỉ Trưởng Bộ Môn", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(403, result.Error);
            }

            if (result.Error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(409, result.Error);
            }

            if (result.Error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.StartsWith("Không tìm thấy", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(404, result.Error);
            }

            if (result.Error.StartsWith("INVARIANT:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.StartsWith("VALIDATION:", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("không thể phê duyệt", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(422, result.Error);
            }

            return QuestionProblem(400, result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tra cứu danh mục câu hỏi theo môn học, trạng thái phê duyệt, độ khó, Bloom
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQuestions([FromQuery] GetQuestionsQuery query)
    {
        var role = User.IsInRole("department_head") ? "department_head" : User.IsInRole("lecturer") ? "lecturer" : string.Empty;
        var result = await _sender.Send(query with { ActorId = GetCurrentUserId(), ActorRole = role, ApprovedBy = null });
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
                return QuestionProblem(403, result.Error);
            return QuestionProblem(400, result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Xem các câu hỏi hiện đang được chính tài khoản đăng nhập phê duyệt, mới duyệt trước.
    /// </summary>
    [HttpGet("my-approved")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> GetMyApprovedQuestions([FromQuery] GetQuestionsQuery filters)
    {
        var actorId = GetCurrentUserId();
        if (actorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var role = User.IsInRole("department_head") ? "department_head" : string.Empty;

        // Identity and status come from the server, never from query-string filters.
        var query = filters with
        {
            ActorId = actorId,
            ActorRole = role,
            ApprovedBy = actorId,
            ApprovalStatus = OralExamination.Domain.Enums.ApprovalStatus.Approved
        };
        var result = await _sender.Send(query);
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.Ordinal))
            {
                return QuestionProblem(403, result.Error);
            }
            return QuestionProblem(400, result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Xem chi tiết câu hỏi kèm Barem và các tiêu chí con
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetQuestionById(Guid id)
    {
        var role = User.IsInRole("department_head") ? "department_head" : User.IsInRole("lecturer") ? "lecturer" : string.Empty;
        var result = await _sender.Send(new GetQuestionByIdQuery(id, GetCurrentUserId(), role));
        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase))
            {
                return QuestionProblem(StatusCodes.Status403Forbidden, result.Error);
            }

            return QuestionProblem(StatusCodes.Status404NotFound, result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateQuestion(Guid id, [FromBody] UpdateQuestionDraftDto request,
        CancellationToken cancellationToken)
    {
        var actorId = GetCurrentUserId();
        if (actorId == Guid.Empty) return Unauthorized();
        var role = User.IsInRole("department_head") ? "department_head" : User.IsInRole("lecturer") ? "lecturer" : string.Empty;
        var result = await _sender.Send(new MutateQuestionCommand(id, actorId, role,
            QuestionMutationKind.Update, Draft: request), cancellationToken);
        return result.IsSuccess ? Ok(new { questionId = result.Value }) : MutationProblem(result.Error);
    }

    [HttpPatch("{id:guid}/active")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> SetQuestionActive(Guid id, [FromBody] SetQuestionActiveRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetCurrentUserId();
        if (actorId == Guid.Empty) return Unauthorized();
        var role = User.IsInRole("department_head") ? "department_head" : string.Empty;
        var result = await _sender.Send(new MutateQuestionCommand(id, actorId, role,
            QuestionMutationKind.SetActive, Active: request.IsActive), cancellationToken);
        return result.IsSuccess ? NoContent() : MutationProblem(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDeleteQuestion(Guid id, CancellationToken cancellationToken)
    {
        var actorId = GetCurrentUserId();
        if (actorId == Guid.Empty) return Unauthorized();
        var role = User.IsInRole("department_head") ? "department_head" : User.IsInRole("lecturer") ? "lecturer" : string.Empty;
        var result = await _sender.Send(new MutateQuestionCommand(id, actorId, role,
            QuestionMutationKind.SoftDelete), cancellationToken);
        return result.IsSuccess ? NoContent() : MutationProblem(result.Error);
    }

    private IActionResult MutationProblem(string error) =>
        QuestionProblem(error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) ? 403 :
            error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) ? 404 :
            error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) ? 409 : 422, error);

    private IActionResult QuestionProblem(int status, string detail)
    {
        var problem = ApiProblems.CreateForController(HttpContext, status, detail);
        return status switch
        {
            400 => BadRequest(problem),
            404 => NotFound(problem),
            422 => UnprocessableEntity(problem),
            _ => StatusCode(status, problem)
        };
    }
}

public sealed record SetQuestionActiveRequest(bool IsActive);
