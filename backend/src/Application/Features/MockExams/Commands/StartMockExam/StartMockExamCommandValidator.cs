using FluentValidation;

namespace OralExamination.Application.Features.MockExams.Commands.StartMockExam;

public sealed class StartMockExamCommandValidator : AbstractValidator<StartMockExamCommand>
{
    public StartMockExamCommandValidator()
    {
        RuleFor(x => x.CourseId)
            .NotEmpty()
            .WithMessage("Mã môn học không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty()
            .WithMessage("Mã sinh viên không được để trống.");
    }
}
