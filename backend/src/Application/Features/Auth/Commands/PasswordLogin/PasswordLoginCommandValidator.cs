using FluentValidation;
using OralExamination.Application.Common.Auth;
using OralExamination.Domain.Identity;

namespace OralExamination.Application.Features.Auth.Commands.PasswordLogin;

public sealed class PasswordLoginCommandValidator : AbstractValidator<PasswordLoginCommand>
{
    public PasswordLoginCommandValidator()
    {
        RuleFor(command => command.Username)
            .NotEmpty()
            .WithMessage("Username không được để trống.")
            .Must(value => string.IsNullOrWhiteSpace(value) || StudentUsername.Normalize(value).Length <= StudentUsername.MaxLength)
            .WithMessage($"Username không được dài quá {StudentUsername.MaxLength} ký tự.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .WithMessage("Mật khẩu không được để trống.")
            .MaximumLength(AuthTokenDefaults.PasswordMaximumLength)
            .WithMessage($"Mật khẩu không được dài quá {AuthTokenDefaults.PasswordMaximumLength} ký tự.");
    }
}
