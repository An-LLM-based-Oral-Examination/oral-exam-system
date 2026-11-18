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

        When(v => !v.IsFollowUp, () =>
        {
            RuleFor(v => v.ParentAnswerId)
                .Null().WithMessage("Câu trả lời chính (isFollowUp = false) không được chứa parentAnswerId.");
        });

        When(v => v.IsFollowUp, () =>
        {
            RuleFor(v => v.ParentAnswerId)
                .NotNull().WithMessage("Câu trả lời phụ (isFollowUp = true) bắt buộc phải có parentAnswerId.")
                .NotEqual(Guid.Empty).WithMessage("ParentAnswerId không hợp lệ.");
        });
    }
}
