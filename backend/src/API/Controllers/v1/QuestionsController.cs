using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromFlm;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class QuestionsController : ControllerBase
{
    private readonly ISender _sender;

    public QuestionsController(ISender sender)
    {
        _sender = sender;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        if (Request.Headers.TryGetValue("X-User-Id", out var headerVal) && Guid.TryParse(headerVal, out var headerId))
        {
            return headerId;
        }

        return Guid.Empty;
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
            request.Rubric
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new { type = "https://oralexam.fpt.edu.vn/errors/validation-failed", detail = result.Error });
        }

        return CreatedAtAction(nameof(GetQuestionById), new { id = result.Value }, new { questionId = result.Value });
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
