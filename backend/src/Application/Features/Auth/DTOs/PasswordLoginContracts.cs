namespace OralExamination.Application.Features.Auth.DTOs;

public record PasswordLoginRequest(string? Username, string? Password);

public record PasswordLoginResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    bool MustChangePassword,
    GoogleLoginUserDto User);

public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public record ChangePasswordResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    bool MustChangePassword);
