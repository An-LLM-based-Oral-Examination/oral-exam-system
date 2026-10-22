using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Authentication;

public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private const string DevGooglePrefix = "dev-google:";
    private readonly IConfiguration _configuration;

    public GoogleIdTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<GoogleUserIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(idToken) &&
            idToken.StartsWith(DevGooglePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var allowDevEmail = _configuration["Authentication:AllowDevGoogleEmail"]
                ?? Environment.GetEnvironmentVariable("ALLOW_DEV_GOOGLE_EMAIL")
                ?? "false";

            if (string.Equals(allowDevEmail, "true", StringComparison.OrdinalIgnoreCase))
            {
                var rawPayload = idToken[DevGooglePrefix.Length..].Trim();
                var parts = rawPayload.Split('|', 2, StringSplitOptions.TrimEntries);
                var devEmail = parts[0].ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(devEmail) && devEmail.Contains('@'))
                {
                    var devFullName = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
                        ? parts[1]
                        : FormatNameFromEmail(devEmail);
                    return new GoogleUserIdentity(devEmail, devFullName);
                }
            }

            throw new GoogleIdTokenRejectedException();
        }

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
                    Audience = [clientId]
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
            name = FormatNameFromEmail(email);
        }

        return name.Length <= 150 ? name : name[..150];
    }

    private static string FormatNameFromEmail(string email)
    {
        var localPart = email.Split('@')[0];
        if (localPart.StartsWith("thanhnq", StringComparison.OrdinalIgnoreCase))
        {
            return "Nguyễn Quang Thành";
        }

        return localPart;
    }
}
