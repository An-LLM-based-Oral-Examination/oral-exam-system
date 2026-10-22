using System;
using FluentValidation;

namespace OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;

public sealed class SubmitPracticeBatchCommandValidator : AbstractValidator<SubmitPracticeBatchCommand>
{
    public SubmitPracticeBatchCommandValidator()
    {
        RuleFor(v => v.SessionId)
            .NotEmpty().WithMessage("SessionId không được để trống.");

        RuleFor(v => v.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(v => v.Answers)
            .NotNull().WithMessage("Danh sách câu trả lời nộp hàng loạt không được để trống.")
            .Must(answers => answers != null && answers.Count > 0)
            .WithMessage("Danh sách câu trả lời nộp hàng loạt không được để trống.");

        RuleForEach(v => v.Answers)
            .ChildRules(answer =>
            {
                answer.RuleFor(a => a.QuestionId)
                    .NotEmpty().WithMessage("QuestionId không được để trống.");

                answer.RuleFor(a => a.AnswerText)
                    .NotEmpty().WithMessage("Nội dung câu trả lời không được để trống.");
            })
            .When(v => v.Answers != null);
    }
}
