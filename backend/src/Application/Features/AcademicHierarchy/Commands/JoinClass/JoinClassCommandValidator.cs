using System;
using FluentValidation;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;

public class JoinClassCommandValidator : AbstractValidator<JoinClassCommand>
{
    public JoinClassCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Mã sinh viên (StudentId) không được để trống.");

        RuleFor(x => x.JoinCode)
            .NotEmpty()
            .When(x => !x.ClassId.HasValue || x.ClassId.Value == Guid.Empty)
            .WithMessage("Mã tham gia lớp (JoinCode) hoặc định danh lớp (ClassId) không được để trống.");

        RuleFor(x => x.JoinCode)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.JoinCode))
            .WithMessage("Mã tham gia lớp không được vượt quá 50 ký tự.");
    }
}
