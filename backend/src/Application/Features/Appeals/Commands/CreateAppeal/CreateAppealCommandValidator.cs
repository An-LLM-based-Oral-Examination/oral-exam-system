using System;
using FluentValidation;

namespace OralExamination.Application.Features.Appeals.Commands.CreateAppeal;

/// <summary>
/// Validator cho lệnh nộp đơn phúc khảo:
/// - StudentId, TicketId không rỗng.
/// - Reason không rỗng, tối thiểu 10 ký tự, tối đa 2000 ký tự.
/// </summary>
public sealed class CreateAppealCommandValidator : AbstractValidator<CreateAppealCommand>
{
    public CreateAppealCommandValidator()
    {
        RuleFor(v => v.StudentId)
            .NotEmpty()
            .WithMessage("Mã sinh viên không được để trống.");

        RuleFor(v => v.TicketId)
            .NotEmpty()
            .WithMessage("Mã vé thi không được để trống.");

        RuleFor(v => v.Reason)
            .NotEmpty()
            .WithMessage("Lý do phúc khảo không được để trống.")
            .MinimumLength(10)
            .WithMessage("Lý do phúc khảo phải có tối thiểu 10 ký tự.")
            .MaximumLength(2000)
            .WithMessage("Lý do phúc khảo không được vượt quá 2000 ký tự.");
    }
}
