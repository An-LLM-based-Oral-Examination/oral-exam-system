namespace OralExamination.Application.Common.Auth;

public static class AuthTokenDefaults
{
    public const string Issuer = "oral-exam-system";
    public const string Audience = "oral-exam-system";
    public const int AccessTokenMinutes = 15;
    public const int RefreshTokenDays = 7;
    public const string MustChangePasswordClaim = "must_change_password";
    public const int PasswordMinimumLength = 8;
    public const int PasswordMaximumLength = 128;
}
