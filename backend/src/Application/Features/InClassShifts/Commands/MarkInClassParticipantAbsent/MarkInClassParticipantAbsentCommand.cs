using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Commands.MarkInClassParticipantAbsent;

public sealed record MarkInClassParticipantAbsentCommand(
    Guid ActorUserId,
    Guid ShiftId,
    Guid StudentId) : IRequest<Result<InClassShiftParticipantResponse>>;
