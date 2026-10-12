using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Authentication;

public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly IConfiguration _configuration;

    public GoogleIdTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<GoogleUserIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        var clientId = AuthConfiguration.GoogleClientId(_configuration);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(
                "Thiếu GOOGLE_OAUTH_CLIENT_ID. Thêm Client ID vào file .env ở thư mục oral-exam-system.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });
        }
        catch (Exception exception) when (exception is InvalidJwtException or ArgumentException)
        {
            throw new GoogleIdTokenRejectedException();
        }

        if (string.IsNullOrWhiteSpace(payload.Email) || payload.EmailVerified != true)
        {
            throw new GoogleIdTokenRejectedException();
        }

        var email = payload.Email.Trim().ToLowerInvariant();
        return new GoogleUserIdentity(email, ResolveFullName(payload, email));
    }

    private static string ResolveFullName(GoogleJsonWebSignature.Payload payload, string email)
    {
        var name = payload.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"{payload.GivenName} {payload.FamilyName}".Trim();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = email.Split('@')[0];
        }

        return name.Length <= 150 ? name : name[..150];
    }
}
