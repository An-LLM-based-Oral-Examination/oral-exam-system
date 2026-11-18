using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShiftStatus;

public sealed record UpdateExamShiftStatusCommand(
    Guid ActorUserId,
    string? ActorRole,
    Guid ShiftId,
    string Status) : IRequest<Result<ExamShiftResponse>>;
