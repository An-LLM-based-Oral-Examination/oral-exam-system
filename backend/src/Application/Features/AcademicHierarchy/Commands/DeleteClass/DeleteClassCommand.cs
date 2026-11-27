using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.DeleteClass;

public sealed record DeleteClassCommand(
    Guid ClassId,
    Guid ActorUserId,
    string? ActorRole) : IRequest<Result>;
