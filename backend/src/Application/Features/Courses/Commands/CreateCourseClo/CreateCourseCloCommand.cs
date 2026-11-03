using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Commands.CreateCourseClo;

public sealed record CreateCourseCloCommand(
    Guid CourseId,
    string CloCode,
    string Description,
    string BloomLevel,
    int OrderIndex,
    string? ActorRole = null) : IRequest<Result<CourseCloDto>>;
