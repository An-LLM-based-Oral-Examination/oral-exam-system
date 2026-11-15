namespace OralExamination.Application.Features.Auth.DTOs;

public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string? Username,
    string FullName,
    string? StudentCode,
    string Role,
    bool IsActive,
    bool MustChangePassword);
