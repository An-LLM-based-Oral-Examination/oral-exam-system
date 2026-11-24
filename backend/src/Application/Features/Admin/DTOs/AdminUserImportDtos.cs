using System;
using System.Collections.Generic;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Admin.DTOs;

public static class AdminUserImportRules
{
    public const long MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxRows = 500;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int GeneratedPasswordLength = 12;

    public static readonly string[] AllowedRoles =
    [
        UserRole.Student,
        UserRole.Lecturer,
        UserRole.DepartmentHead,
        UserRole.ExaminationOfficer,
        UserRole.Proctor
    ];
}

public enum AdminUserImportFileErrorKind
{
    None = 0,
    EmptyFile = 1,
    PayloadTooLarge = 2,
    UnsupportedMediaType = 3,
    InvalidFormat = 4,
    TooManyRows = 5
}

public sealed record AdminUserImportRawRow(
    int RowNumber,
    string Email,
    string FullName,
    string Role,
    string? StudentCode,
    string? Username,
    string? Password);

public sealed record AdminUserImportParseResult(
    IReadOnlyList<AdminUserImportRawRow> Rows,
    IReadOnlyList<string> Errors,
    AdminUserImportFileErrorKind ErrorKind = AdminUserImportFileErrorKind.None)
{
    public bool IsSuccess => ErrorKind == AdminUserImportFileErrorKind.None && Errors.Count == 0;
}

public sealed record AdminUserSpreadsheetFile(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record AdminUserImportPreviewRowDto
{
    public int RowNumber { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? StudentCode { get; init; }
    public string? Username { get; init; }
    public string PasswordSource { get; init; } = "NONE";
    public bool IsValid { get; init; }
    public string Status { get; init; } = "INVALID";
    public string? ErrorMessage { get; init; }
}

public sealed record AdminUserImportPreviewResponseDto
{
    public string FileName { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int ValidCount { get; init; }
    public int InvalidCount { get; init; }
    public int DuplicateCount { get; init; }
    public int SkippedCount { get; init; }
    public bool HasDefaultPassword { get; init; }
    public IReadOnlyList<AdminUserImportPreviewRowDto> Rows { get; init; } = Array.Empty<AdminUserImportPreviewRowDto>();
}

public sealed record AdminUserImportCreatedRowDto
{
    public int RowNumber { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? StudentCode { get; init; }
    public string? Username { get; init; }
    public string TemporaryPassword { get; init; } = string.Empty;
    public string PasswordSource { get; init; } = "AUTO_GENERATED";
    public bool MustChangePassword { get; init; } = true;
}

public sealed record AdminUserImportSkippedRowDto
{
    public int RowNumber { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? StudentCode { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record AdminUserImportCommitResponseDto
{
    public int TotalRows { get; init; }
    public int CreatedCount { get; init; }
    public int SkippedCount { get; init; }
    public string ResultFileName { get; init; } = string.Empty;
    public string ResultFileContentType { get; init; } = string.Empty;
    public string ResultFileBase64 { get; init; } = string.Empty;
    public IReadOnlyList<AdminUserImportCreatedRowDto> CreatedRows { get; init; } = Array.Empty<AdminUserImportCreatedRowDto>();
    public IReadOnlyList<AdminUserImportSkippedRowDto> SkippedRows { get; init; } = Array.Empty<AdminUserImportSkippedRowDto>();
}
