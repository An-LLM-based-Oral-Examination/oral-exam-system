using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CurateExamQuestionBank;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.ReturnExamSetForRevision;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamSet;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAvailableExamSetQuestions;
using OralExamination.Application.Features.OfficialExams.Queries.GetBankCurationCandidates;

namespace API.Controllers.v1;

/// <summary>
/// Controller MF-03 M3: Chọn câu hỏi khả dụng gắn nhãn vàng ẩn danh, chỉnh sửa/xóa bộ đề DRAFT/REJECTED,
/// trả bộ đề về chỉnh sửa và màn tổng hợp lọc trùng văn bản (70–79% vàng / >=80% đỏ) nạp kho kỳ sau.
/// Chuẩn RESTful RFC 7807 Problem Details.
/// </summary>
[ApiController]
[Route("api/v1/official-exams")]
[Authorize]
public class OfficialExamCurationController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    /// <summary>
    /// Lấy danh sách câu hỏi khả dụng để soạn bộ đề theo cấu trúc đề (ẩn câu Đề mẫu, câu đã duyệt đợt này và câu chính GV đã chọn; gắn nhãn vàng ẩn danh).
    /// </summary>
    [HttpGet("structures/{structureId:guid}/available-questions")]
    [Authorize(Roles = "lecturer,department_head")]
    [ProducesResponseType(typeof(AvailableExamSetQuestionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableExamSetQuestions(
        [FromRoute] Guid structureId,
        [FromQuery] Guid? cloId = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] string? bloomLevel = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? sessionId = null,
        [FromQuery] Guid? excludeExamSetId = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new GetAvailableExamSetQuestionsQuery(
                structureId,
                actorId.Value,
                GetActorRole(),
                cloId,
                difficulty,
                bloomLevel,
                search, sessionId, excludeExamSetId),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : CurationProblem(result.Error);
    }

    /// <summary>
    /// Giảng viên sở hữu hoặc Trưởng Bộ Môn cập nhật bộ đề ở trạng thái DRAFT hoặc REJECTED (đưa REJECTED về DRAFT để nộp duyệt lại).
    /// </summary>
    [HttpPut("exam-sets/{examSetId:guid}")]
    [Authorize(Roles = "lecturer,department_head")]
    [ProducesResponseType(typeof(ExamSetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateExamSet(
        [FromRoute] Guid examSetId,
        [FromBody] UpdateExamSetRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return CurationProblem("UNPROCESSABLE: Nội dung cập nhật bộ đề không được để trống.");
        }

        var result = await _sender.Send(
            new UpdateExamSetCommand(
                examSetId,
                request.SetCode,
                request.QuestionIds,
                actorId.Value,
                GetActorRole(),
                request.InlineManualQuestions),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : CurationProblem(result.Error);
    }

    /// <summary>
    /// Giảng viên sở hữu hoặc Trưởng Bộ Môn xóa mềm bộ đề ở trạng thái DRAFT hoặc REJECTED, giải phóng các câu hỏi kho về danh sách khả dụng.
    /// </summary>
    [HttpDelete("exam-sets/{examSetId:guid}")]
    [Authorize(Roles = "lecturer,department_head")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteExamSet(
        [FromRoute] Guid examSetId,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new DeleteExamSetCommand(
                examSetId,
                actorId.Value,
                GetActorRole()),
            cancellationToken);

        return result.IsSuccess ? NoContent() : CurationProblem(result.Error);
    }

    /// <summary>
    /// Trưởng Bộ Môn trả bộ đề đang chờ duyệt (SUBMITTED_FOR_REVIEW) về trạng thái REJECTED kèm lý do để GV thay câu/chỉnh sửa.
    /// </summary>
    [HttpPost("exam-sets/{examSetId:guid}/return-for-revision")]
    [Authorize(Roles = "department_head")]
    [ProducesResponseType(typeof(ReturnExamSetForRevisionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReturnExamSetForRevision(
        [FromRoute] Guid examSetId,
        [FromBody] ReturnExamSetForRevisionRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return CurationProblem("UNPROCESSABLE: Vui lòng cung cấp lý do trả về chỉnh sửa (reviewNotes).");
        }

        var result = await _sender.Send(
            new ReturnExamSetForRevisionCommand(
                examSetId,
                request.ReviewNotes ?? string.Empty,
                actorId.Value,
                GetActorRole()),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : CurationProblem(result.Error);
    }

    /// <summary>
    /// Trưởng Bộ Môn xem danh sách câu tạo tay (PENDING_CURATION) đối chiếu 2 cột lọc trùng 70–79% (vàng) và >=80% (đỏ) cho kho kỳ sau.
    /// </summary>
    [HttpGet("courses/{courseId:guid}/bank-curation-candidates")]
    [Authorize(Roles = "department_head")]
    [ProducesResponseType(typeof(BankCurationCandidatesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBankCurationCandidates(
        [FromRoute] Guid courseId,
        [FromQuery] string? band = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new GetBankCurationCandidatesQuery(
                courseId,
                actorId.Value,
                GetActorRole(),
                band),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : CurationProblem(result.Error);
    }

    /// <summary>
    /// Trưởng Bộ Môn quyết định nạp kho (CURATED_INTO_BANK) hoặc loại khỏi kho chọn chung (EXCLUDED_FROM_BANK) lẻ hoặc hàng loạt.
    /// </summary>
    [HttpPost("courses/{courseId:guid}/bank-curation/decide")]
    [Authorize(Roles = "department_head")]
    [ProducesResponseType(typeof(CurateExamQuestionBankResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DecideBankCuration(
        [FromRoute] Guid courseId,
        [FromBody] CurateExamQuestionBankRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return CurationProblem("UNPROCESSABLE: Nội dung quyết định lọc kho không được để trống.");
        }

        var result = await _sender.Send(
            new CurateExamQuestionBankCommand(
                courseId,
                request.QuestionIds ?? [],
                request.Decision ?? string.Empty,
                request.Notes,
                actorId.Value,
                GetActorRole()),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : CurationProblem(result.Error);
    }

    private Guid? GetActorId()
    {
        var claim = User?.FindFirst("sub")?.Value
            ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) && id != Guid.Empty ? id : null;
    }

    private string? GetActorRole()
    {
        if (User?.IsInRole("department_head") == true) return "department_head";
        if (User?.IsInRole("lecturer") == true) return "lecturer";
        if (User?.IsInRole("admin") == true) return "admin";
        if (User?.IsInRole("examination_officer") == true) return "examination_officer";
        if (User?.IsInRole("proctor") == true) return "proctor";
        if (User?.IsInRole("student") == true) return "student";
        return null;
    }

    private IActionResult CurationProblem(string error)
    {
        var (statusCode, detail) = error switch
        {
            _ when error.StartsWith("FORBIDDEN:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status403Forbidden, error["FORBIDDEN:".Length..].Trim()),
            _ when error == OfficialExamErrors.Forbidden =>
                (StatusCodes.Status403Forbidden, error),
            _ when error.StartsWith("NOT_FOUND:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status404NotFound, error["NOT_FOUND:".Length..].Trim()),
            _ when error is OfficialExamErrors.CourseNotFound
                or OfficialExamErrors.StructureNotFound
                or OfficialExamErrors.ExamSetNotFound =>
                (StatusCodes.Status404NotFound, error),
            _ when error.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status409Conflict, error["CONFLICT:".Length..].Trim()),
            _ when error.StartsWith("UNPROCESSABLE:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status422UnprocessableEntity, error["UNPROCESSABLE:".Length..].Trim()),
            _ when error is OfficialExamErrors.ExamSetNoQuestions =>
                (StatusCodes.Status422UnprocessableEntity, error),
            _ when error.StartsWith("VALIDATION:", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status422UnprocessableEntity, error["VALIDATION:".Length..].Trim()),
            _ => (StatusCodes.Status400BadRequest, error)
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Thao tác bị từ chối.",
                StatusCodes.Status404NotFound => "Không tìm thấy tài nguyên.",
                StatusCodes.Status409Conflict => "Xung đột trạng thái dữ liệu.",
                StatusCodes.Status422UnprocessableEntity => "Không thể xử lý yêu cầu nghiệp vụ MF-03.",
                _ => "Dữ liệu đầu vào không hợp lệ."
            },
            Detail = string.IsNullOrWhiteSpace(detail) ? error : detail,
            Type = $"https://oralexam.fpt.edu.vn/errors/official-exam-curation-{statusCode}"
        });
    }
}
