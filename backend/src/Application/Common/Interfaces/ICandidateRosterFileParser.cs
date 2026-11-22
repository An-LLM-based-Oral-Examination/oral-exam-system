using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Common.Interfaces;

public enum CandidateRosterFileErrorKind
{
    None = 0,
    EmptyFile = 1,
    PayloadTooLarge = 2,
    UnsupportedMediaType = 3,
    InvalidFormat = 4,
    TooManyRows = 5
}

public sealed record CandidateRosterParseResult(
    IReadOnlyList<CandidateRosterImportRow> Rows,
    IReadOnlyList<string> Errors,
    CandidateRosterFileErrorKind ErrorKind = CandidateRosterFileErrorKind.None)
{
    public bool IsSuccess => ErrorKind == CandidateRosterFileErrorKind.None && Errors.Count == 0;
}

/// <summary>
/// Parses MSSV-only official exam candidate rosters (.xlsx / .csv) with strict size, row, and magic-bytes checks,
/// and generates downloadable import templates.
/// </summary>
public interface ICandidateRosterFileParser
{
    Task<CandidateRosterParseResult> ParseAsync(
        Stream stream,
        string? fileName,
        long? contentLength = null,
        CancellationToken cancellationToken = default);

    CandidateRosterTemplateFile GenerateTemplate(string format);
}
