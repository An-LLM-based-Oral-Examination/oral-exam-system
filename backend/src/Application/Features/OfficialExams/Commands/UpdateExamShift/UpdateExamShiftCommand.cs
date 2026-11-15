using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift;

public sealed record UpdateExamShiftCommand(Guid ActorUserId, string? ActorRole, Guid ShiftId,
    string? ShiftName, string? RoomLab, DateTime StartTime, DateTime EndTime,
    Guid ProctorUserId, Guid ExaminerUserId) : IRequest<Result>;
