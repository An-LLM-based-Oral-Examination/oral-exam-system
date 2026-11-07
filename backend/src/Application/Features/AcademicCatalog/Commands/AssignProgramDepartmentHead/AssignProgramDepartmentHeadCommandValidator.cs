using FluentValidation;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.AssignProgramDepartmentHead;

public sealed class AssignProgramDepartmentHeadCommandValidator
    : AbstractValidator<AssignProgramDepartmentHeadCommand>
{
    public AssignProgramDepartmentHeadCommandValidator()
    {
        RuleFor(x => x.AcademicProgramId).NotEmpty();
        RuleFor(x => x.DepartmentHeadUserId).NotEmpty();
    }
}
