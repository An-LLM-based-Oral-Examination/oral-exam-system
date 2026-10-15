namespace OralExamination.Application.Common.Interfaces;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false);
}

public sealed record IssuedAccessToken(string Token, int ExpiresInSeconds);
