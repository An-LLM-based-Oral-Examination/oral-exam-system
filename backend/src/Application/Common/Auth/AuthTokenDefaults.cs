namespace OralExamination.Application.Common.Auth;

public static class AuthTokenDefaults
{
    public const string Issuer = "oral-exam-system";
    public const string Audience = "oral-exam-system";
    public const int AccessTokenMinutes = 15;
    public const int RefreshTokenDays = 7;
}
