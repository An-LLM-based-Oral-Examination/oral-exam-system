using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;

/// <summary>
/// Command tạo mới cấu trúc đề thi (ExamStructure) cho môn học (phục vụ MF-02 & MF-04).
/// </summary>
public sealed record CreateExamStructureCommand(
    Guid ActorUserId,
    Guid CourseId,
    string Name,
    int TotalQuestions,
    int DurationMinutes,
    string? BloomDistribution,
    bool IsActive = true) : IRequest<Result<ExamStructureDto>>;
