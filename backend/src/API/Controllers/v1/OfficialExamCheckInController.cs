using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;

namespace API.Controllers.v1;

/// <summary>
/// Điểm danh kiosk. Tách khỏi OfficialExamsController vì sinh viên ngồi tại máy trạm được gọi API này.
/// Địa chỉ IP lấy từ kết nối, không đọc từ body.
/// </summary>
[ApiController]
[Route("api/v1/official-exams")]
[Authorize(Roles = "student,proctor,lecturer,department_head")]
public class OfficialExamCheckInController : ControllerBase
{
    private readonly ISender _sender;

    public OfficialExamCheckInController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Sinh viên lấy vé của chính mình trên máy đang ngồi. IP lấy từ kết nối, giờ lấy từ server.
    /// </summary>
    [HttpGet("tickets/current")]
    public async Task<IActionResult> GetCurrentTicket(CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new GetMyWorkstationTicketQuery(actorId.Value, HttpContext.Connection.RemoteIpAddress?.ToString(), DateTime.UtcNow),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpPost("tickets/{ticketId:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid ticketId, CancellationToken cancellationToken)
    {
        var actorId = GetActorId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new CheckInExamTicketCommand(
                actorId.Value,
                ticketId,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                DateTime.UtcNow),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    private Guid? GetActorId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var actorId) ? actorId : null;
    }

    private IActionResult Problem(string error)
    {
        var statusCode = error switch
        {
            OfficialExamErrors.Forbidden or OfficialExamErrors.WrongWorkstation => StatusCodes.Status403Forbidden,
            OfficialExamErrors.TicketNotFound or OfficialExamErrors.WorkstationTicketNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status403Forbidden => "Không được phép.",
                StatusCodes.Status404NotFound => "Không tìm thấy.",
                _ => "Dữ liệu đầu vào không hợp lệ."
            },
            Detail = error,
            Type = "https://tools.ietf.org/html/rfc7231"
        });
    }
}
