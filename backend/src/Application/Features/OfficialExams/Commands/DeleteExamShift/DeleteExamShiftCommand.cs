using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.DeleteExamShift;

public sealed record DeleteExamShiftCommand(Guid ActorUserId, string? ActorRole, Guid ShiftId) : IRequest<Result>;
