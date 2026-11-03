using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.Courses.Commands.DeleteCourseClo;

public sealed record DeleteCourseCloCommand(
    Guid CourseId,
    Guid CloId,
    string? ActorRole = null) : IRequest<Result>;
