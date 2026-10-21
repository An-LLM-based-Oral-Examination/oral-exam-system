using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket;

public sealed record LockExamTicketResponseDto(
    Guid TicketId,
    bool IsLocked,
    DateTime LockedAt,
    string Status,
    string Message);

/// <summary>
/// Command khóa một chiều điểm của một vé thi cụ thể trên Evidence Panel (MF-04 / R2.1).
/// </summary>
public sealed record LockExamTicketCommand(
    Guid ActorUserId,
    Guid TicketId) : IRequest<Result<LockExamTicketResponseDto>>;
