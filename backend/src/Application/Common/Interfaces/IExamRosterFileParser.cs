using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>Parses a user-supplied roster without coupling Application to HTTP upload types.</summary>
public interface IExamRosterFileParser
{
    Task<ExamRosterParseResult> ParseAsync(Stream stream, string? fileName, CancellationToken cancellationToken = default);
}

public sealed record ExamRosterParseResult(
    IReadOnlyList<BulkExamTicketImportRow> Rows,
    IReadOnlyList<string> Errors);
