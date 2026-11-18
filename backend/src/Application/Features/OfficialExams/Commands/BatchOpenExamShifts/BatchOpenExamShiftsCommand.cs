using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;

public sealed record BatchOpenExamShiftsCommand(
    Guid ActorUserId,
    Guid SessionId,
    string? ShiftGroupName,
    DateTime StartTime,
    DateTime EndTime,
    IReadOnlyList<BatchOpenExamShiftRoomDto>? Rooms) : IRequest<Result<BatchOpenExamShiftsResponse>>;
