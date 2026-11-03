using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;

public sealed record LogAntiCheatEventCommand(
    Guid TicketId,
    Guid ShiftId,
    Guid StudentId,
    string EventType,
    string Severity,
    string? Details) : IRequest<Result<ExamAntiCheatLogDto>>;
