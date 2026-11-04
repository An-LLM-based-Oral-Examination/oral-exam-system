using FluentValidation;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AssignDepartmentHead;

public class AssignDepartmentHeadCommandValidator : AbstractValidator<AssignDepartmentHeadCommand>
{
    public AssignDepartmentHeadCommandValidator()
    {
        RuleFor(x => x.CourseId)
            .NotEmpty().WithMessage("Mã môn học (CourseId) không được để trống.");

        RuleFor(x => x.DepartmentHeadUserId)
            .NotEmpty().WithMessage("Mã người dùng Trưởng bộ môn (DepartmentHeadUserId) không được để trống.");
    }
}
