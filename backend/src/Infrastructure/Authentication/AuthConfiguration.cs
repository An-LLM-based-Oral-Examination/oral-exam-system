using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Auth;

namespace OralExamination.Infrastructure.Authentication;

public static class AuthConfiguration
{
    public static string? GoogleClientId(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration["Authentication:GoogleClientId"],
            configuration["GOOGLE_OAUTH_CLIENT_ID"]);

    public static string? JwtSigningKey(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration["Authentication:JwtSigningKey"],
            configuration["JWT_SIGNING_KEY"]);

    public static string JwtIssuer(IConfiguration configuration) =>
        FirstNonEmpty(configuration["Authentication:JwtIssuer"]) ?? AuthTokenDefaults.Issuer;

    public static string JwtAudience(IConfiguration configuration) =>
        FirstNonEmpty(configuration["Authentication:JwtAudience"]) ?? AuthTokenDefaults.Audience;

    public static int AccessTokenMinutes(IConfiguration configuration)
    {
        var raw = configuration["Authentication:AccessTokenMinutes"];
        return int.TryParse(raw, out var minutes) && minutes > 0
            ? minutes
            : AuthTokenDefaults.AccessTokenMinutes;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
