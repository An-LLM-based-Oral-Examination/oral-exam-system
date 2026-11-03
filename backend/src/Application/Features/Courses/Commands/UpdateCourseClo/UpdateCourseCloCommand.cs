using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourseClo;

public sealed record UpdateCourseCloCommand(
    Guid CourseId,
    Guid CloId,
    string CloCode,
    string Description,
    string BloomLevel,
    int OrderIndex,
    bool IsActive,
    string? ActorRole = null) : IRequest<Result<CourseCloDto>>;
