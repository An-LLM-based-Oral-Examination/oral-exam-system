using FluentValidation;
using System;

namespace OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;

public sealed class SubmitPracticeAnswerCommandValidator : AbstractValidator<SubmitPracticeAnswerCommand>
{
    public SubmitPracticeAnswerCommandValidator()
    {
        RuleFor(v => v.SessionId)
            .NotEmpty().WithMessage("SessionId không được để trống.");

        RuleFor(v => v.QuestionId)
            .NotEmpty().WithMessage("QuestionId không được để trống.");

        RuleFor(v => v.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(v => v.AnswerText)
            .NotEmpty().WithMessage("Nội dung câu trả lời không được để trống.")
            .MinimumLength(10).WithMessage("Câu trả lời quá ngắn.");
    }
}
