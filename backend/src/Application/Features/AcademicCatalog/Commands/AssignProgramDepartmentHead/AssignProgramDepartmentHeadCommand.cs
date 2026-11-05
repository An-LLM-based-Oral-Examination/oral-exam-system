using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.AssignProgramDepartmentHead;

public sealed record AssignProgramDepartmentHeadCommand(
    Guid ActorUserId,
    Guid AcademicProgramId,
    Guid DepartmentHeadUserId) : IRequest<Result<AcademicProgramDto>>;
