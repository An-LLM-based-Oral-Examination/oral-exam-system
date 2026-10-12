namespace OralExamination.Application.Common.Interfaces;

public interface IGoogleIdTokenValidator
{
    Task<GoogleUserIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken);
}

public sealed record GoogleUserIdentity(string Email, string FullName);
