using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;

public sealed record OpenExamShiftCommand(
    Guid ActorUserId,
    Guid SessionId,
    string? ShiftName,
    string? RoomLab,
    DateTime StartTime,
    DateTime EndTime,
    Guid ProctorUserId) : IRequest<Result<ExamShiftResponse>>;
