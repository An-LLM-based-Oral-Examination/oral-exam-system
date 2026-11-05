using System;
using FluentValidation;

namespace OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;

public sealed class CreateInClassShiftCommandValidator : AbstractValidator<CreateInClassShiftCommand>
{
    public CreateInClassShiftCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("ActorUserId không được để trống.");

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("ClassId không được để trống.");

        RuleFor(x => x.ShiftName)
            .NotEmpty().WithMessage("Tên ca thi không được để trống.")
            .MaximumLength(255).WithMessage("Tên ca thi không được vượt quá 255 ký tự.");

        RuleFor(x => x.EndTime)
            .GreaterThanOrEqualTo(x => x.StartTime)
            .WithMessage(InClassShiftErrors.InvalidTimeRange);

        RuleFor(x => x.QuestionPoolMode)
            .Must(m => string.Equals(m, "ALL_COURSE_BANK", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(m, "SELECTED_POOL", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chế độ nguồn câu hỏi chỉ chấp nhận 'ALL_COURSE_BANK' hoặc 'SELECTED_POOL'.");

        RuleFor(x => x.TotalQuestionsPerStudent)
            .InclusiveBetween(1, 10)
            .WithMessage("Tổng số câu hỏi mỗi sinh viên phải nằm trong khoảng từ 1 đến 10.");

        RuleFor(x => x.MaxFollowUpQuestions)
            .InclusiveBetween(0, 5)
            .WithMessage("Số lượng câu hỏi phụ tối đa phải nằm trong khoảng từ 0 đến 5.");
    }
}
