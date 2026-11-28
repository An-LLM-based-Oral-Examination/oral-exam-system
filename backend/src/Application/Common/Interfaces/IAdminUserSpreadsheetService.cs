using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OralExamination.Application.Features.Admin.DTOs;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Parses Admin bulk user import spreadsheets (.xlsx / .csv), generates downloadable import templates,
/// and builds one-time .xlsx result workbooks containing created credentials and skipped rows.
/// </summary>
public interface IAdminUserSpreadsheetService
{
    Task<AdminUserImportParseResult> ParseAsync(
        Stream stream,
        string? fileName,
        long? contentLength = null,
        CancellationToken cancellationToken = default);

    AdminUserSpreadsheetFile GenerateTemplate(string format);

    AdminUserSpreadsheetFile GenerateResultWorkbook(
        IReadOnlyList<AdminUserImportCreatedRowDto> createdRows,
        IReadOnlyList<AdminUserImportSkippedRowDto> skippedRows,
        DateTime generatedAtUtc);
}
