using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;

public sealed record CheckInExamTicketCommand(
    Guid ActorUserId,
    Guid TicketId,
    string? RemoteIpAddress,
    DateTime AtUtc) : IRequest<Result<ExamTicketResponse>>;
