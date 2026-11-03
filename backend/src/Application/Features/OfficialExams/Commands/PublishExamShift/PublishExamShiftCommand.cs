using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.PublishExamShift;

public sealed record PublishExamShiftCommand(
    Guid ShiftId,
    Guid ActorUserId,
    string? ActorRole = null) : IRequest<Result>;
