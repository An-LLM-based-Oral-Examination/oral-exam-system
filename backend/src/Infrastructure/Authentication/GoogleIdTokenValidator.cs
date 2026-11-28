using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Exceptions;

namespace OralExamination.Infrastructure.Authentication;

public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly IConfiguration _configuration;
    private readonly IGoogleTokenSignatureVerifier _signatureVerifier;

    public GoogleIdTokenValidator(IConfiguration configuration, IGoogleTokenSignatureVerifier? signatureVerifier = null)
    {
        _configuration = configuration;
        _signatureVerifier = signatureVerifier ?? new GoogleTokenSignatureVerifier();
    }

    public async Task<GoogleUserIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // No unsigned development identity, authorization-code or access-token exchange.
        if (string.IsNullOrWhiteSpace(idToken) || idToken.StartsWith("dev-google:", StringComparison.OrdinalIgnoreCase))
            throw new GoogleIdTokenRejectedException();

        var clientId = AuthConfiguration.GoogleClientId(_configuration);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new GoogleOAuthNotConfiguredException(
                "Thiếu GOOGLE_OAUTH_CLIENT_ID. Thêm Client ID vào file .env ở thư mục oral-exam-system.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await _signatureVerifier.VerifyAsync(idToken, clientId, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidJwtException or ArgumentException)
        {
            throw new GoogleIdTokenRejectedException();
        }

        var email = payload.Email?.Trim().ToLowerInvariant();
        // Student self-provisioning remains enabled for production data collection.
        // An explicit allowlist may narrow access; do not silently exclude Gmail students.
        var domains = _configuration.GetSection("Authentication:GoogleAllowedDomains").Get<string[]>() ?? [];
        var domain = email is not null && email.Count(character => character == '@') == 1 ? email.Split('@')[1] : null;
        if (payload.Issuer is not ("accounts.google.com" or "https://accounts.google.com")
            || !payload.AudienceAsList.Contains(clientId, StringComparer.Ordinal) || payload.ExpirationTimeSeconds is null
            || payload.ExpirationTimeSeconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            || payload.EmailVerified != true || string.IsNullOrWhiteSpace(payload.Subject)
            || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(payload.Nonce) || payload.Nonce.Length > 128
            || domain is null || domain.Length == 0
            || (domains.Length > 0 && !domains.Any(allowed => !string.IsNullOrWhiteSpace(allowed) && string.Equals(allowed.Trim(), domain, StringComparison.OrdinalIgnoreCase)))
            // Google is authoritative for Gmail or a signed Workspace hosted domain.
            || (domain != "gmail.com" && !string.Equals(payload.HostedDomain, domain, StringComparison.OrdinalIgnoreCase)))
        {
            throw new GoogleIdTokenRejectedException();
        }

        return new GoogleUserIdentity(email, ResolveFullName(payload, email), payload.Nonce, payload.Subject);
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
        return email.Split('@')[0];
    }
}
