using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;

public sealed record SubmitOfficialExamCommand(
    Guid ActorUserId,
    Guid TicketId,
    string? RemoteIpAddress,
    IReadOnlyList<OfficialExamAnswerSubmissionRequest>? Answers)
    : IRequest<Result<SubmitOfficialExamResponse>>;
