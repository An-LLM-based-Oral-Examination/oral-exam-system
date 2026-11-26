using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.UpdateClass;

public sealed record UpdateClassCommand(
    Guid ClassId,
    Guid ActorUserId,
    string? ActorRole,
    string Name,
    int MaxStudents,
    bool? AllowSelfEnrollment = null,
    bool? IsJoinEnabled = null,
    string? Code = null) : IRequest<Result<LecturerClassItemDto>>;
