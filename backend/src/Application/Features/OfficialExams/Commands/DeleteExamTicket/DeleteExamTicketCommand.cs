using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.DeleteExamTicket;

public sealed record DeleteExamTicketCommand(
    Guid ActorUserId,
    string? ActorRole,
    Guid TicketId) : IRequest<Result>;
