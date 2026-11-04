using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AssignDepartmentHead;

public record AssignDepartmentHeadCommand(
    Guid CourseId,
    Guid DepartmentHeadUserId,
    string? ActorRole = null) : IRequest<Result<AssignDepartmentHeadResponseDto>>;
