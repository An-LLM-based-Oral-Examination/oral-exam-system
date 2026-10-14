using FluentValidation;

namespace OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;

/// <summary>
/// Validator cho SubmitMockExamSessionCommand.
/// </summary>
public sealed class SubmitMockExamSessionCommandValidator : AbstractValidator<SubmitMockExamSessionCommand>
{
    public SubmitMockExamSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Mã ca thi thử không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Mã sinh viên không được để trống.");
    }
}
