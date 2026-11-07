using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.Proctor.Commands.RecordProctorViolation;

public sealed record RecordProctorViolationCommand(
    Guid ActorUserId,
    Guid ShiftId,
    Guid? TicketId,
    Guid? StudentId,
    string EventType,
    string Severity,
    string Details) : IRequest<Result<ExamAntiCheatLogDto>>;
