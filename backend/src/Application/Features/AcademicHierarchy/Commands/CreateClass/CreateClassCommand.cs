using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;

public record CreateClassCommand(
    string Code,
    string Name,
    Guid CourseId,
    Guid SemesterId,
    Guid LecturerId,
    string? JoinCode = null,
    string? Password = null,
    int MaxStudents = 35,
    bool AllowSelfEnrollment = true,
    bool IsJoinEnabled = true,
    string? ActorRole = null,
    Guid? CourseOfferingId = null,
    Guid? ActorUserId = null) : IRequest<Result<CreateClassResponseDto>>;
