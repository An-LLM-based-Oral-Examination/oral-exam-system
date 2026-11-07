using FluentValidation;

namespace OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;

public sealed class EnterInClassShiftCommandValidator : AbstractValidator<EnterInClassShiftCommand>
{
    public EnterInClassShiftCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.ShiftId)
            .NotEmpty().WithMessage("ShiftId không được để trống.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Mã OTP không được để trống.")
            .Length(6).WithMessage("Mã OTP phải có đúng 6 chữ số.");
    }
}
