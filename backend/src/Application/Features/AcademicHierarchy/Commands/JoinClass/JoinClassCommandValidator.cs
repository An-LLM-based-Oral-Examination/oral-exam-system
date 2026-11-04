using FluentValidation;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;

public class JoinClassCommandValidator : AbstractValidator<JoinClassCommand>
{
    public JoinClassCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Mã sinh viên (StudentId) không được để trống.");

        RuleFor(x => x.JoinCode)
            .NotEmpty().WithMessage("Mã tham gia lớp (JoinCode) không được để trống.")
            .MaximumLength(20).WithMessage("Mã tham gia lớp không được vượt quá 20 ký tự.");
    }
}
