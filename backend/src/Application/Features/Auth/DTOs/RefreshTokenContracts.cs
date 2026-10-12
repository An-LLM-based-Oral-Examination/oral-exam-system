namespace OralExamination.Application.Features.Auth.DTOs;

public record RefreshTokenRequest(string? RefreshToken);

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn);
