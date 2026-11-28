namespace OralExamination.Application.Features.Auth.DTOs;

public record BrowserGoogleLoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    GoogleLoginUserDto User,
    bool MustChangePassword = false);

public record BrowserPasswordLoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    bool MustChangePassword,
    GoogleLoginUserDto User);

public record BrowserRefreshTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn);

public record BrowserChangePasswordResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    bool MustChangePassword);
