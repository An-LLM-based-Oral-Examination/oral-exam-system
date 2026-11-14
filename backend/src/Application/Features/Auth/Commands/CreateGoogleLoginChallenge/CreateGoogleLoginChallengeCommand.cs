using MediatR;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Application.Features.Auth.Commands.CreateGoogleLoginChallenge;

public sealed record GoogleLoginChallengeDto(string Nonce, int ExpiresIn);
public sealed record CreateGoogleLoginChallengeCommand : IRequest<GoogleLoginChallengeDto>;
public sealed class CreateGoogleLoginChallengeCommandHandler(IGoogleLoginNonceStore store)
    : IRequestHandler<CreateGoogleLoginChallengeCommand, GoogleLoginChallengeDto>
{
    public Task<GoogleLoginChallengeDto> Handle(CreateGoogleLoginChallengeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GoogleLoginChallengeDto(store.Issue(), GoogleLoginRules.NonceLifetimeSeconds));
    }
}
