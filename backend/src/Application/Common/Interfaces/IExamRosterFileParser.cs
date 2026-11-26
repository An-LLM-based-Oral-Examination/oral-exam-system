using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>Parses a user-supplied roster without coupling Application to HTTP upload types.</summary>
public interface IExamRosterFileParser
{
    Task<ExamRosterParseResult> ParseAsync(Stream stream, string? fileName, CancellationToken cancellationToken = default);
    CandidateRosterTemplateFile GenerateTemplate(string format) =>
        throw new NotSupportedException("Dịch vụ chưa hỗ trợ sinh tệp mẫu roster phát vé.");
}

public sealed record ExamRosterParseResult(
    IReadOnlyList<BulkExamTicketImportRow> Rows,
    IReadOnlyList<string> Errors);
