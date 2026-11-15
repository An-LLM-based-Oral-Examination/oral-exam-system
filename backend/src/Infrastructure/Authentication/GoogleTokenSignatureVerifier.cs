using Google.Apis.Auth;

namespace OralExamination.Infrastructure.Authentication;

public interface IGoogleTokenSignatureVerifier
{
    Task<GoogleJsonWebSignature.Payload> VerifyAsync(string token, string audience, CancellationToken cancellationToken);
}

/// <summary>Always verifies Google public keys; no alternate signing keys can be configured.</summary>
public sealed class GoogleTokenSignatureVerifier : IGoogleTokenSignatureVerifier
{
    public Task<GoogleJsonWebSignature.Payload> VerifyAsync(string token, string audience, CancellationToken cancellationToken)
        => GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings { Audience = [audience] })
            .WaitAsync(cancellationToken);
}
