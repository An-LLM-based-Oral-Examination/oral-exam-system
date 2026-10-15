using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;

public sealed record AssignExamTicketCommand(
    Guid ActorUserId,
    Guid ShiftId,
    Guid StudentId,
    int SeatNumber,
    string? IpAddress) : IRequest<Result<ExamTicketResponse>>;
