using MediatR;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using System;
using System.Threading.Tasks;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class PracticeController : ControllerBase
{
    private readonly ISender _sender;

    public PracticeController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession([FromBody] StartPracticeSessionCommand command)
    {
        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }
        return Ok(new { sessionId = result.Value });
    }

    [HttpPost("sessions/{sessionId}/answers")]
    public async Task<IActionResult> SubmitAnswer(Guid sessionId, [FromBody] SubmitPracticeAnswerRequest request)
    {
        var command = new SubmitPracticeAnswerCommand(
            sessionId,
            request.QuestionId,
            request.StudentId,
            request.AnswerText,
            request.AudioUrl,
            request.IsFollowUp,
            request.ParentAnswerId
        );

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }
        return Ok(new { answerId = result.Value });
    }
}

public record SubmitPracticeAnswerRequest(
    Guid QuestionId,
    Guid StudentId,
    string AnswerText,
    string? AudioUrl,
    bool IsFollowUp,
    Guid? ParentAnswerId
);
