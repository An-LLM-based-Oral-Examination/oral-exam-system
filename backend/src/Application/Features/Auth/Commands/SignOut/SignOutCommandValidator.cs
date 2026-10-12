using FluentValidation;

namespace OralExamination.Application.Features.Auth.Commands.SignOut;

public sealed class SignOutCommandValidator : AbstractValidator<SignOutCommand>
{
    public SignOutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("refreshToken không được để trống.");
    }
}
