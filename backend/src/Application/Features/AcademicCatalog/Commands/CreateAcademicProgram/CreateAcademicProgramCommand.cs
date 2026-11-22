using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.CreateAcademicProgram;

public sealed record CreateAcademicProgramCommand(
    Guid ActorUserId,
    string Code,
    string Name,
    Guid? DepartmentHeadUserId = null,
    bool IsActive = true) : IRequest<Result<AcademicProgramDto>>;
