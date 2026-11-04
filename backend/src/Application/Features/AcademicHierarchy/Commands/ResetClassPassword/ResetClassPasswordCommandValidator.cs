using FluentValidation;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;

public sealed class ResetClassPasswordCommandValidator : AbstractValidator<ResetClassPasswordCommand>
{
    public ResetClassPasswordCommandValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Định danh lớp học (ClassId) không được để trống.");

        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Định danh người thực hiện (ActorUserId) không được để trống.");

        When(x => !string.IsNullOrWhiteSpace(x.NewJoinCode), () =>
        {
            RuleFor(x => x.NewJoinCode!)
                .MinimumLength(4).WithMessage("Mã tham gia lớp (NewJoinCode) phải có ít nhất 4 ký tự.")
                .MaximumLength(32).WithMessage("Mã tham gia lớp (NewJoinCode) không được vượt quá 32 ký tự.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.NewPassword), () =>
        {
            RuleFor(x => x.NewPassword!)
                .MinimumLength(4).WithMessage("Mật khẩu lớp học (NewPassword) phải có ít nhất 4 ký tự.")
                .MaximumLength(128).WithMessage("Mật khẩu lớp học (NewPassword) không được vượt quá 128 ký tự.");
        });
    }
}
