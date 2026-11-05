using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;

public record JoinClassCommand(
    Guid StudentId,
    string? JoinCode,
    string? Password = null,
    Guid? ClassId = null) : IRequest<Result<JoinClassResponseDto>>;
