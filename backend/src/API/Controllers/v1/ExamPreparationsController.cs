using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries;

namespace API.Controllers.v1;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/official-exams")]
[Authorize(Roles = "department_head,lecturer,examination_officer")]
public sealed class ExamPreparationsController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
    private string? Role => new[] { "department_head", "lecturer", "examination_officer" }.FirstOrDefault(User.IsInRole);
    private IActionResult Respond(Result<object> result) => result.IsSuccess ? Ok(result.Value) : Problem(
        statusCode: result.Error == OfficialExamErrors.Forbidden || result.Error.StartsWith("FORBIDDEN", StringComparison.OrdinalIgnoreCase) ? 403 :
            result.Error == OfficialExamErrors.ExamSetNotFound || result.Error == OfficialExamErrors.CourseNotFound ||
            result.Error.StartsWith("NOT_FOUND", StringComparison.OrdinalIgnoreCase) ? 404 :
            result.Error == OfficialExamErrors.ExamSetStatusInvalid || result.Error.StartsWith("CONFLICT", StringComparison.OrdinalIgnoreCase) ? 409 : 422,
        title: "Không thể xử lý yêu cầu chuẩn bị đề", detail: result.Error);

    [HttpGet("preparations")]
    public async Task<IActionResult> List([FromQuery] Guid? sessionId, CancellationToken ct) =>
        Respond(await sender.Send(new GetExamPreparationQuery(sessionId, Actor, Role), ct));

    [HttpPost("sessions/{sessionId:guid}/preparation")]
    [Authorize(Roles = "examination_officer")]
    public async Task<IActionResult> RequestPreparation(Guid sessionId, RequestExamPreparation body, CancellationToken ct) =>
        Respond(await sender.Send(new ExamPreparationCommand(sessionId, Actor, Role, PreparationAction.Request, Request: body), ct));

    [HttpPost("sessions/{sessionId:guid}/preparation/blueprints")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> Configure(Guid sessionId, ConfigureExamBlueprint body, CancellationToken ct) =>
        Respond(await sender.Send(new ExamPreparationCommand(sessionId, Actor, Role, PreparationAction.Configure, Blueprint: body), ct));

    [HttpPost("sessions/{sessionId:guid}/preparation/assignments")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> Assign(Guid sessionId, AssignExamAuthor body, CancellationToken ct) =>
        Respond(await sender.Send(new ExamPreparationCommand(sessionId, Actor, Role, PreparationAction.Assign, Assignment: body), ct));

    [HttpPost("preparation/assignments/{id:guid}/submit-review")]
    [Authorize(Roles = "lecturer")]
    public async Task<IActionResult> SubmitAssignment(Guid id, SubmitExamAssignment body, CancellationToken ct) =>
        Respond(await sender.Send(new SubmitExamAssignmentCommand(id, body.ExamSetIds, Actor, Role), ct));

    [HttpPut("exam-sets/{id:guid}/draft")]
    [Authorize(Roles = "lecturer")]
    public async Task<IActionResult> Draft(Guid id, SaveExamSetDraft body, CancellationToken ct) =>
        Respond(await sender.Send(new ExamPreparationCommand(id, Actor, Role, PreparationAction.SaveDraft, Draft: body), ct));

    [HttpPost("exam-sets/{id:guid}/request-changes")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> RequestChanges(Guid id, ReviewExamSet body, CancellationToken ct) =>
        Respond(await sender.Send(new ExamPreparationCommand(id, Actor, Role, PreparationAction.RequestChanges, Review: body), ct));

    [HttpPost("exam-sets/{id:guid}/reject")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> Reject(Guid id, ReviewExamSet body, CancellationToken ct) =>
        Respond(await sender.Send(new ExamPreparationCommand(id, Actor, Role, PreparationAction.Reject, Review: body), ct));

    [HttpPost("sessions/{sessionId:guid}/preparation/hand-over")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> HandOver(Guid sessionId, HandOverExamPreparation body, CancellationToken ct) =>
        Respond(await sender.Send(new ExamPreparationCommand(sessionId, Actor, Role, PreparationAction.HandOver, Delivery: body), ct));

    [HttpGet("exam-sets/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        Respond(await sender.Send(new GetExamPreparationQuery(null, Actor, Role, id, Detail: true), ct));

    [HttpGet("exam-sets/{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) =>
        Respond(await sender.Send(new GetExamPreparationQuery(null, Actor, Role, id), ct));

    [HttpGet("exam-sets/{id:guid}/duplicates")]
    [Authorize(Roles = "department_head")]
    public async Task<IActionResult> Duplicates(Guid id, CancellationToken ct) =>
        Respond(await sender.Send(new GetExamPreparationQuery(null, Actor, Role, id, true), ct));
}
