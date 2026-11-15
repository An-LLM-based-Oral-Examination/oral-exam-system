namespace OralExamination.Application.Common.Interfaces;

public interface IGoogleLoginNonceStore
{
    string Issue();
    bool TryConsume(string nonce);
}

public static class GoogleLoginRules
{
    public const string NonceCookie = "oes_google_nonce";
    public const string CookiePath = "/api/v1/auth";
    public const int NonceLifetimeSeconds = 300;
}
