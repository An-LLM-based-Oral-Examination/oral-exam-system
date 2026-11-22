using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.UpdateAcademicProgram;

public sealed record UpdateAcademicProgramCommand(
    Guid ActorUserId,
    Guid AcademicProgramId,
    string Code,
    string Name,
    bool IsActive,
    Guid? DepartmentHeadUserId = null) : IRequest<Result<AcademicProgramDto>>;
