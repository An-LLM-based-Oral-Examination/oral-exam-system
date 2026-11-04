using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AssignLecturersToCourse;

public record AssignLecturersToCourseCommand(
    Guid CourseId,
    List<Guid> LecturerUserIds,
    Guid AssignedByUserId,
    Guid? SemesterId = null,
    string? ActorRole = null) : IRequest<Result<AssignLecturersToCourseResponseDto>>;
