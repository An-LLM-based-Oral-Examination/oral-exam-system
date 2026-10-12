using FluentValidation;

namespace OralExamination.Application.Features.Auth.Commands.GoogleLogin;

public sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(command => command.IdToken)
            .NotEmpty()
            .WithMessage("idToken không được để trống.");
    }
}
