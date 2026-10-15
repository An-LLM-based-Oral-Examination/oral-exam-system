using FluentValidation;
using OralExamination.Application.Common.Auth;

namespace OralExamination.Application.Features.Auth.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .WithMessage("Không xác định được tài khoản đăng nhập.");

        RuleFor(command => command.CurrentPassword)
            .NotEmpty()
            .WithMessage("Mật khẩu hiện tại không được để trống.");

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(AuthTokenDefaults.PasswordMinimumLength)
            .WithMessage($"Mật khẩu mới phải có ít nhất {AuthTokenDefaults.PasswordMinimumLength} ký tự.")
            .MaximumLength(AuthTokenDefaults.PasswordMaximumLength)
            .WithMessage($"Mật khẩu mới không được dài quá {AuthTokenDefaults.PasswordMaximumLength} ký tự.");
    }
}
