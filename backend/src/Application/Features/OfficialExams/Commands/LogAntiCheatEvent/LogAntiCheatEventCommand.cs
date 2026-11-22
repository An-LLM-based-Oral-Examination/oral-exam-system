using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;

public sealed record LogAntiCheatEventCommand(
    Guid ActorUserId,
    Guid TicketId,
    Guid ShiftId,
    Guid StudentId,
    string EventType,
    string Severity,
    string? Details,
    string? ClientEventId = null,
    string? DeviceId = null,
    string? KioskSessionToken = null) : IRequest<Result<ExamAntiCheatLogDto>>;
