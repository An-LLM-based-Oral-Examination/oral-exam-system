using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OfficeOpenXml;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Infrastructure.Services;

public sealed class CandidateRosterFileParser : ICandidateRosterFileParser
{
    private static readonly object LicenseLock = new();
    private static bool _licenseConfigured;

    private static readonly byte[] ZipMagicBytes = [0x50, 0x4B, 0x03, 0x04];

    private static readonly HashSet<string> StudentCodeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "studentcode",
        "mssv",
        "masv",
        "masinhvien",
        "mãsv",
        "mãsinhvien",
        "mãsinhviên"
    };

    private static readonly HashSet<string> FullNameHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "fullname",
        "name",
        "hoten",
        "hovaten",
        "họtên",
        "họvàtên"
    };

    private static readonly HashSet<string> ClassCodeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "classcode",
        "class",
        "malop",
        "mãlớp",
        "lop",
        "lớp"
    };

    public async Task<CandidateRosterParseResult> ParseAsync(
        Stream stream,
        string? fileName,
        long? contentLength = null,
        CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            return new CandidateRosterParseResult(
                [],
                ["Tệp danh sách thí sinh không được để trống."],
                CandidateRosterFileErrorKind.EmptyFile);
        }

        if (contentLength.HasValue && contentLength.Value > OfficialExamRules.MaxCandidateRosterBytes)
        {
            return new CandidateRosterParseResult(
                [],
                [$"Kích thước tệp vượt quá giới hạn tối đa {OfficialExamRules.MaxCandidateRosterBytes / (1024 * 1024)} MB."],
                CandidateRosterFileErrorKind.PayloadTooLarge);
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        var isCsv = string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);
        var isXlsx = string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);

        if (!isCsv && !isXlsx)
        {
            return new CandidateRosterParseResult(
                [],
                ["Định dạng tệp không được hỗ trợ. Chỉ chấp nhận tệp .xlsx hoặc .csv."],
                CandidateRosterFileErrorKind.UnsupportedMediaType);
        }

        var (bytes, exceededLimit) = await ReadBoundedBytesAsync(
            stream,
            OfficialExamRules.MaxCandidateRosterBytes,
            cancellationToken);

        if (exceededLimit)
        {
            return new CandidateRosterParseResult(
                [],
                [$"Kích thước tệp vượt quá giới hạn tối đa {OfficialExamRules.MaxCandidateRosterBytes / (1024 * 1024)} MB."],
                CandidateRosterFileErrorKind.PayloadTooLarge);
        }

        if (bytes.Length == 0)
        {
            return new CandidateRosterParseResult(
                [],
                ["Tệp danh sách thí sinh không có dữ liệu."],
                CandidateRosterFileErrorKind.EmptyFile);
        }

        if (isXlsx)
        {
            if (!HasZipMagicBytes(bytes))
            {
                return new CandidateRosterParseResult(
                    [],
                    ["Tệp .xlsx không hợp lệ (sai chữ ký nhị phân Magic Bytes 50 4B 03 04)."],
                    CandidateRosterFileErrorKind.UnsupportedMediaType);
            }

            return ParseXlsx(bytes);
        }

        if (LooksLikeBinaryPayload(bytes))
        {
            return new CandidateRosterParseResult(
                [],
                ["Tệp .csv chứa dữ liệu nhị phân không hợp lệ."],
                CandidateRosterFileErrorKind.UnsupportedMediaType);
        }

        return ParseCsv(bytes);
    }

    public CandidateRosterTemplateFile GenerateTemplate(string format)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(format)
            ? "xlsx"
            : format.Trim().ToLowerInvariant();

        if (normalizedFormat == "csv")
        {
            var csvContent = "studentCode,fullName,classCode\r\nSE170001,Nguyen Van A,SE1801\r\n";
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var preamble = utf8WithBom.GetPreamble();
            var body = utf8WithBom.GetBytes(csvContent);
            var payload = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, payload, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, payload, preamble.Length, body.Length);

            return new CandidateRosterTemplateFile(
                payload,
                "text/csv; charset=utf-8",
                "official-exam-candidates-template.csv");
        }

        ConfigureLicense();
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Candidates");
        worksheet.Cells[1, 1].Value = "studentCode";
        worksheet.Cells[1, 2].Value = "fullName";
        worksheet.Cells[1, 3].Value = "classCode";
        worksheet.Cells[1, 1, 1, 3].Style.Font.Bold = true;

        worksheet.Cells[2, 1].Value = "SE170001";
        worksheet.Cells[2, 2].Value = "Nguyen Van A";
        worksheet.Cells[2, 3].Value = "SE1801";

        worksheet.Column(1).Width = 18;
        worksheet.Column(2).Width = 28;
        worksheet.Column(3).Width = 16;

        return new CandidateRosterTemplateFile(
            package.GetAsByteArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "official-exam-candidates-template.xlsx");
    }

    private static async Task<(byte[] Bytes, bool ExceededLimit)> ReadBoundedBytesAsync(
        Stream stream,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
            if (totalRead > maxBytes)
            {
                return (Array.Empty<byte>(), true);
            }

            memory.Write(buffer, 0, bytesRead);
        }

        return (memory.ToArray(), false);
    }

    private static bool HasZipMagicBytes(byte[] bytes) =>
        bytes.Length >= ZipMagicBytes.Length
        && bytes[0] == ZipMagicBytes[0]
        && bytes[1] == ZipMagicBytes[1]
        && bytes[2] == ZipMagicBytes[2]
        && bytes[3] == ZipMagicBytes[3];

    private static bool LooksLikeBinaryPayload(byte[] bytes)
    {
        // Reject known binary magic headers (ZIP/XLSX, PDF, PNG, JPEG, GIF, ELF, PE/EXE)
        if (HasZipMagicBytes(bytes))
        {
            return true;
        }

        if (bytes.Length >= 4
            && ((bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46) // %PDF
                || (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) // PNG
                || (bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46))) // ELF
        {
            return true;
        }

        if (bytes.Length >= 2
            && ((bytes[0] == 0x4D && bytes[1] == 0x5A) // MZ (PE/EXE)
                || (bytes[0] == 0xFF && bytes[1] == 0xD8))) // JPEG
        {
            return true;
        }

        var sampleLength = Math.Min(bytes.Length, 4096);
        for (var i = 0; i < sampleLength; i++)
        {
            var b = bytes[i];
            if (b == 0x00)
            {
                return true;
            }
        }

        return false;
    }

    private static CandidateRosterParseResult ParseCsv(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(memory, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var lines = new List<(int Number, IReadOnlyList<string> Values)>();
        var number = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            number++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            if (values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                lines.Add((number, values));
            }
        }

        return BuildRows(lines);
    }

    private static CandidateRosterParseResult ParseXlsx(byte[] bytes)
    {
        try
        {
            ConfigureLicense();
            using var memory = new MemoryStream(bytes, writable: false);
            using var package = new ExcelPackage(memory);
            var sheet = package.Workbook.Worksheets.FirstOrDefault();
            if (sheet?.Dimension is null)
            {
                return new CandidateRosterParseResult(
                    [],
                    ["Tệp Excel không có dữ liệu."],
                    CandidateRosterFileErrorKind.InvalidFormat);
            }

            var lines = new List<(int Number, IReadOnlyList<string> Values)>();
            var startCol = sheet.Dimension.Start.Column;
            var colCount = sheet.Dimension.End.Column - startCol + 1;

            for (var row = sheet.Dimension.Start.Row; row <= sheet.Dimension.End.Row; row++)
            {
                var values = new List<string>(colCount);
                var hasNonEmpty = false;
                for (var col = startCol; col <= sheet.Dimension.End.Column; col++)
                {
                    var text = sheet.Cells[row, col].Text?.Trim() ?? string.Empty;
                    if (text.Length > 0)
                    {
                        hasNonEmpty = true;
                    }
                    values.Add(text);
                }

                if (hasNonEmpty)
                {
                    lines.Add((row, values));
                }
            }

            return BuildRows(lines);
        }
        catch (Exception)
        {
            return new CandidateRosterParseResult(
                [],
                ["Không thể đọc nội dung tệp .xlsx. Vui lòng kiểm tra tệp Excel không bị hỏng."],
                CandidateRosterFileErrorKind.UnsupportedMediaType);
        }
    }

    private static CandidateRosterParseResult BuildRows(IReadOnlyList<(int Number, IReadOnlyList<string> Values)> lines)
    {
        if (lines.Count == 0)
        {
            return new CandidateRosterParseResult(
                [],
                ["Tệp không có dòng dữ liệu."],
                CandidateRosterFileErrorKind.EmptyFile);
        }

        var normalizedHeaders = lines[0].Values
            .Select(NormalizeHeaderToken)
            .ToList();

        var studentCodeIdx = normalizedHeaders.FindIndex(h => StudentCodeHeaders.Contains(h));
        if (studentCodeIdx < 0)
        {
            return new CandidateRosterParseResult(
                [],
                ["Thiếu cột bắt buộc 'studentCode' (hoặc 'MSSV' / 'student_code' / 'ma_sv') ở dòng tiêu đề."],
                CandidateRosterFileErrorKind.InvalidFormat);
        }

        var fullNameIdx = normalizedHeaders.FindIndex(h => FullNameHeaders.Contains(h));
        var classCodeIdx = normalizedHeaders.FindIndex(h => ClassCodeHeaders.Contains(h));

        var dataLineCount = lines.Count - 1;
        if (dataLineCount == 0)
        {
            return new CandidateRosterParseResult(
                [],
                ["Tệp không có dòng dữ liệu thí sinh nào sau dòng tiêu đề."],
                CandidateRosterFileErrorKind.InvalidFormat);
        }

        if (dataLineCount > OfficialExamRules.MaxCandidateRosterRows)
        {
            return new CandidateRosterParseResult(
                [],
                [$"Danh sách thí sinh vượt quá giới hạn tối đa {OfficialExamRules.MaxCandidateRosterRows} dòng (hiện có {dataLineCount} dòng)."],
                CandidateRosterFileErrorKind.TooManyRows);
        }

        var rows = new List<CandidateRosterImportRow>(dataLineCount);
        foreach (var line in lines.Skip(1))
        {
            string Cell(int index) => index >= 0 && index < line.Values.Count ? line.Values[index].Trim() : string.Empty;
            var studentCode = Cell(studentCodeIdx);
            var fullName = fullNameIdx >= 0 ? NullIfEmpty(Cell(fullNameIdx)) : null;
            var classCode = classCodeIdx >= 0 ? NullIfEmpty(Cell(classCodeIdx)) : null;

            rows.Add(new CandidateRosterImportRow(line.Number, studentCode, fullName, classCode));
        }

        return new CandidateRosterParseResult(rows, [], CandidateRosterFileErrorKind.None);
    }

    private static string NormalizeHeaderToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim().TrimStart('\uFEFF');
        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch) || ch is '_' or '-')
            {
                continue;
            }

            sb.Append(char.ToLower(ch, CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
                else
                {
                    quoted = !quoted;
                }
            }
            else if (current == ',' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(current);
            }
        }

        values.Add(value.ToString());
        return values;
    }

    private static void ConfigureLicense()
    {
        lock (LicenseLock)
        {
            if (_licenseConfigured)
            {
                return;
            }

            ExcelPackage.License.SetNonCommercialOrganization("FPT University Oral Examination Project");
            _licenseConfigured = true;
        }
    }
}
