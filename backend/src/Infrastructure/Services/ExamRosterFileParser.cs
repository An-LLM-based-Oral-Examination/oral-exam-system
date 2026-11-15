using System.Globalization;
using System.Text;
using OfficeOpenXml;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Infrastructure.Services;

public sealed class ExamRosterFileParser : IExamRosterFileParser
{
    private static readonly object LicenseLock = new();
    private static bool _licenseConfigured;
    private static readonly string[] RequiredHeaders = ["studentCode", "seatNumber", "ipAddress"];

    public async Task<ExamRosterParseResult> ParseAsync(Stream stream, string? fileName,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            return await ParseCsvAsync(stream, cancellationToken);
        if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return ParseXlsx(stream);
        return new ExamRosterParseResult([], ["Chỉ hỗ trợ tệp .csv hoặc .xlsx."]);
    }

    private static async Task<ExamRosterParseResult> ParseCsvAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var lines = new List<(int Number, IReadOnlyList<string> Values)>();
        var number = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            number++;
            if (!string.IsNullOrWhiteSpace(line)) lines.Add((number, ParseCsvLine(line)));
        }
        return BuildRows(lines);
    }

    private static ExamRosterParseResult ParseXlsx(Stream stream)
    {
        ConfigureLicense();
        using var package = new ExcelPackage(stream);
        var sheet = package.Workbook.Worksheets.FirstOrDefault();
        if (sheet?.Dimension is null)
            return new ExamRosterParseResult([], ["Tệp Excel không có dữ liệu."]);

        var lines = new List<(int Number, IReadOnlyList<string> Values)>();
        for (var row = sheet.Dimension.Start.Row; row <= sheet.Dimension.End.Row; row++)
        {
            var values = Enumerable.Range(sheet.Dimension.Start.Column, sheet.Dimension.End.Column - sheet.Dimension.Start.Column + 1)
                .Select(column => sheet.Cells[row, column].Text?.Trim() ?? string.Empty).ToList();
            if (values.Any(value => !string.IsNullOrWhiteSpace(value))) lines.Add((row, values));
        }
        return BuildRows(lines);
    }

    private static ExamRosterParseResult BuildRows(IReadOnlyList<(int Number, IReadOnlyList<string> Values)> lines)
    {
        if (lines.Count == 0) return new ExamRosterParseResult([], ["Tệp không có dòng dữ liệu."]);
        var headers = lines[0].Values.Select(value => value.Trim()).ToList();
        var indexes = RequiredHeaders.Select(header => headers.FindIndex(value =>
            string.Equals(value, header, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (indexes.Any(index => index < 0))
            return new ExamRosterParseResult([], ["Header bắt buộc: studentCode, seatNumber, ipAddress."]);

        var rows = new List<BulkExamTicketImportRow>();
        var errors = new List<string>();
        foreach (var line in lines.Skip(1))
        {
            string Cell(int index) => index < line.Values.Count ? line.Values[index].Trim() : string.Empty;
            var seatText = Cell(indexes[1]);
            if (!int.TryParse(seatText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seat))
            {
                errors.Add($"Dòng {line.Number}: seatNumber phải là số nguyên.");
                continue;
            }
            rows.Add(new BulkExamTicketImportRow(line.Number, Cell(indexes[0]), seat, Cell(indexes[2])));
        }
        return new ExamRosterParseResult(rows, errors);
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];
            if (current == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (current == ',' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else value.Append(current);
        }
        values.Add(value.ToString());
        return values;
    }

    private static void ConfigureLicense()
    {
        lock (LicenseLock)
        {
            if (_licenseConfigured) return;
            ExcelPackage.License.SetNonCommercialOrganization("FPT University Oral Examination Project");
            _licenseConfigured = true;
        }
    }
}
