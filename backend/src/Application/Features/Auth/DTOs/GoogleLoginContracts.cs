namespace OralExamination.Application.Features.Auth.DTOs;

public record GoogleLoginRequest(string? IdToken);

public record GoogleLoginUserDto(
    Guid Id,
    string Email,
    string FullName,
    string? StudentCode,
    string Role);

public record GoogleLoginResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    GoogleLoginUserDto User);
