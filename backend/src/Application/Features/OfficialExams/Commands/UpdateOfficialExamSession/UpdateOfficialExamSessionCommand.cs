using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateOfficialExamSession;

public sealed record UpdateOfficialExamSessionCommand(
    Guid ActorUserId,
    string? ActorRole,
    Guid SessionId,
    Guid? ExamStructureId,
    string? Title,
    DateOnly ExamDate,
    string? ExamInputMode,
    int TranscriptBufferSeconds) : IRequest<Result<OfficialExamSessionResponse>>;
