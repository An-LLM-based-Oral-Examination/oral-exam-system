using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;

public sealed record AssignAppealLecturerCommand(
    Guid AppealId,
    Guid DepartmentHeadId,
    Guid LecturerId) : IRequest<Result<AppealResponseDto>>;
