using FluentValidation;
using System;

namespace OralExamination.Application.Features.Practice.Commands.StartPracticeSession;

public sealed class StartPracticeSessionCommandValidator : AbstractValidator<StartPracticeSessionCommand>
{
    public StartPracticeSessionCommandValidator()
    {
        RuleFor(v => v.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(v => v.CourseId)
            .NotEmpty().WithMessage("CourseId không được để trống.");
    }
}
