using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;

public sealed record CreateOfficialExamSessionCommand(
    Guid ActorUserId,
    Guid CourseId,
    Guid ExamStructureId,
    string? Title,
    DateOnly ExamDate,
    bool HasFollowUp,
    int MaxFollowUpQuestions,
    string? ExamInputMode,
    int TranscriptBufferSeconds) : IRequest<Result<OfficialExamSessionResponse>>;
