using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AddClassStudent;

public sealed record AddClassStudentCommand(
    Guid ClassId,
    Guid ActorUserId,
    string? ActorRole,
    Guid? StudentId = null,
    string? StudentCode = null,
    string? Email = null,
    string? Identifier = null) : IRequest<Result<AddClassStudentResponseDto>>;
