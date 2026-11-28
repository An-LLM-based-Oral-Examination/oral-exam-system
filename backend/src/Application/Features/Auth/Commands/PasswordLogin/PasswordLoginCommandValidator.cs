using FluentValidation;
using OralExamination.Application.Common.Auth;

namespace OralExamination.Application.Features.Auth.Commands.PasswordLogin;

public sealed class PasswordLoginCommandValidator : AbstractValidator<PasswordLoginCommand>
{
    private const int MaxLoginIdentifierLength = 255;

    public PasswordLoginCommandValidator()
    {
        RuleFor(command => command.Username)
            .NotEmpty()
            .WithMessage("Username không được để trống.")
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= MaxLoginIdentifierLength)
            .WithMessage($"Username hoặc email không được dài quá {MaxLoginIdentifierLength} ký tự.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .WithMessage("Mật khẩu không được để trống.")
            .MaximumLength(AuthTokenDefaults.PasswordMaximumLength)
            .WithMessage($"Mật khẩu không được dài quá {AuthTokenDefaults.PasswordMaximumLength} ký tự.");
    }
}
