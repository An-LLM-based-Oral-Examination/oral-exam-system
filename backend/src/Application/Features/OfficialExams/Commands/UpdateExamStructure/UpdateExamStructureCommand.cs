using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;

/// <summary>
/// Command cập nhật cấu trúc đề thi (ExamStructure).
/// </summary>
public sealed record UpdateExamStructureCommand(
    Guid ActorUserId,
    Guid StructureId,
    string Name,
    int TotalQuestions,
    int DurationMinutes,
    string? BloomDistribution,
    bool IsActive = true) : IRequest<Result<ExamStructureDto>>;
