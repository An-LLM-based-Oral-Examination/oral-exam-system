using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;

public sealed record AssignAppealLecturerCommand(
    Guid AppealId,
    Guid AssignerId,
    Guid LecturerId) : IRequest<Result<AppealResponseDto>>
{
    // Backwards compatibility property for existing tests
    public Guid DepartmentHeadId => AssignerId;
}
