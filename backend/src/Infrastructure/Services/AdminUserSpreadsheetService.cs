using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Admin.DTOs;
using DrawingColor = System.Drawing.Color;

namespace OralExamination.Infrastructure.Services;

public sealed class AdminUserSpreadsheetService : IAdminUserSpreadsheetService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string CsvContentType = "text/csv; charset=utf-8";
    private const long MaxUncompressedZipBytes = 20L * 1024 * 1024;
    private const int MaxZipEntries = 100;
    private const int MaxXlsxScanColumns = 50;
    private const int MaxXlsxScanRows = 5000;

    private static readonly object LicenseLock = new();
    private static bool _licenseConfigured;
    private static readonly byte[] ZipMagicBytes = [0x50, 0x4B, 0x03, 0x04];

    private static readonly HashSet<string> EmailHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "mail", "emaildangnhap", "emailđăngnhập"
    };

    private static readonly HashSet<string> FullNameHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "fullname", "name", "hoten", "hovaten", "họtên", "họvàtên"
    };

    private static readonly HashSet<string> RoleHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "role", "vaitro", "vaitrò", "quyen", "quyền"
    };

    private static readonly HashSet<string> StudentCodeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "studentcode", "mssv", "masv", "masinhvien", "mãsv", "mãsinhvien", "mãsinhviên"
    };

    private static readonly HashSet<string> UsernameHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "username", "tendangnhap", "tênđăngnhập", "account"
    };

    private static readonly HashSet<string> PasswordHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "matkhau", "mậtkhẩu", "matkhautam", "mậtkhẩutạm"
    };

    public async Task<AdminUserImportParseResult> ParseAsync(
        Stream stream,
        string? fileName,
        long? contentLength = null,
        CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            return new AdminUserImportParseResult(
                [],
                ["Tệp danh sách tài khoản không được để trống."],
                AdminUserImportFileErrorKind.EmptyFile);
        }

        if (contentLength.HasValue && contentLength.Value > AdminUserImportRules.MaxFileBytes)
        {
            return new AdminUserImportParseResult(
                [],
                [$"Kích thước tệp vượt quá giới hạn tối đa {AdminUserImportRules.MaxFileBytes / (1024 * 1024)} MB."],
                AdminUserImportFileErrorKind.PayloadTooLarge);
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        var isCsv = string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);
        var isXlsx = string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);

        if (!isCsv && !isXlsx)
        {
            return new AdminUserImportParseResult(
                [],
                ["Định dạng tệp không được hỗ trợ. Chỉ chấp nhận tệp .xlsx hoặc .csv."],
                AdminUserImportFileErrorKind.UnsupportedMediaType);
        }

        var (bytes, exceededLimit) = await ReadBoundedBytesAsync(
            stream,
            AdminUserImportRules.MaxFileBytes,
            cancellationToken);

        if (exceededLimit)
        {
            return new AdminUserImportParseResult(
                [],
                [$"Kích thước tệp vượt quá giới hạn tối đa {AdminUserImportRules.MaxFileBytes / (1024 * 1024)} MB."],
                AdminUserImportFileErrorKind.PayloadTooLarge);
        }

        if (bytes.Length == 0)
        {
            return new AdminUserImportParseResult(
                [],
                ["Tệp danh sách tài khoản không có dữ liệu."],
                AdminUserImportFileErrorKind.EmptyFile);
        }

        if (isXlsx)
        {
            if (!HasZipMagicBytes(bytes))
            {
                return new AdminUserImportParseResult(
                    [],
                    ["Tệp .xlsx không hợp lệ (sai chữ ký nhị phân Magic Bytes 50 4B 03 04)."],
                    AdminUserImportFileErrorKind.UnsupportedMediaType);
            }

            return ParseXlsx(bytes);
        }

        if (LooksLikeBinaryPayload(bytes))
        {
            return new AdminUserImportParseResult(
                [],
                ["Tệp .csv chứa dữ liệu nhị phân không hợp lệ."],
                AdminUserImportFileErrorKind.UnsupportedMediaType);
        }

        return ParseCsv(bytes);
    }

    public AdminUserSpreadsheetFile GenerateTemplate(string format)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(format)
            ? "xlsx"
            : format.Trim().ToLowerInvariant();

        if (normalizedFormat == "csv")
        {
            var csvContent = string.Join(
                "\r\n",
                "email,full_name,role,student_code,username,password",
                "thanhnqse184236@fpt.edu.vn,Nguyễn Quang Thành,student,SE184236,,",
                "huongntcse181863@fpt.edu.vn,Nguyễn Thị Cẩm Hương,student,SE181863,,Fpt@2026",
                "khoilm@fpt.edu.vn,Lê Minh Khôi,lecturer,,khoilm,") + "\r\n";

            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var preamble = utf8WithBom.GetPreamble();
            var body = utf8WithBom.GetBytes(csvContent);
            var payload = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, payload, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, payload, preamble.Length, body.Length);

            return new AdminUserSpreadsheetFile(
                payload,
                CsvContentType,
                "mau-import-tai-khoan.csv");
        }

        ConfigureLicense();
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("TaiKhoan");

        var headers = new[] { "email", "full_name", "role", "student_code", "username", "password" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cells[1, i + 1].Value = headers[i];
        }

        using (var headerRange = sheet.Cells[1, 1, 1, headers.Length])
        {
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.Color.SetColor(DrawingColor.White);
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(DrawingColor.FromArgb(28, 77, 141));
        }

        var sampleRows = new[]
        {
            new[] { "thanhnqse184236@fpt.edu.vn", "Nguyễn Quang Thành", "student", "SE184236", "", "" },
            new[] { "huongntcse181863@fpt.edu.vn", "Nguyễn Thị Cẩm Hương", "student", "SE181863", "", "Fpt@2026" },
            new[] { "khoilm@fpt.edu.vn", "Lê Minh Khôi", "lecturer", "", "khoilm", "" }
        };

        for (var r = 0; r < sampleRows.Length; r++)
        {
            for (var c = 0; c < sampleRows[r].Length; c++)
            {
                sheet.Cells[r + 2, c + 1].Style.Numberformat.Format = "@";
                sheet.Cells[r + 2, c + 1].Value = sampleRows[r][c];
            }
        }

        sheet.View.FreezePanes(2, 1);
        sheet.Column(1).Width = 30;
        sheet.Column(2).Width = 26;
        sheet.Column(3).Width = 20;
        sheet.Column(4).Width = 16;
        sheet.Column(5).Width = 18;
        sheet.Column(6).Width = 18;

        var guide = package.Workbook.Worksheets.Add("HuongDan");
        guide.Cells[1, 1].Value = "Cột";
        guide.Cells[1, 2].Value = "Bắt buộc";
        guide.Cells[1, 3].Value = "Quy tắc";
        guide.Cells[1, 1, 1, 3].Style.Font.Bold = true;

        var notes = new[]
        {
            new[] { "email", "Có", "Email hợp lệ, duy nhất trong hệ thống (tối đa 255 ký tự)." },
            new[] { "full_name", "Có", "Họ và tên từ 2–150 ký tự. Với sinh viên bắt buộc >= 2 từ để tự sinh Username." },
            new[] { "role", "Có", "Một trong 5 vai trò: student, lecturer, department_head, examination_officer, proctor (không import admin)." },
            new[] { "student_code", "Bắt buộc với student", "Định dạng 2 chữ cái + 6 chữ số (VD: SE184236). Bỏ trống với vai trò khác." },
            new[] { "username", "Tùy chọn (ngoài student)", "Sinh viên bỏ trống vì hệ thống tự sinh (VD: Nguyễn Quang Thành + SE184236 -> ThanhNQSE184236). Cán bộ: 3–32 ký tự (a-z, 0-9, ., _, -)." },
            new[] { "password", "Tùy chọn", "Nếu bỏ trống: dùng mật khẩu mặc định chung (nếu Admin nhập) hoặc hệ thống tự sinh mật khẩu ngẫu nhiên mạnh 12 ký tự và trả lại trong file kết quả (.xlsx)." }
        };

        for (var i = 0; i < notes.Length; i++)
        {
            guide.Cells[i + 2, 1].Value = notes[i][0];
            guide.Cells[i + 2, 2].Value = notes[i][1];
            guide.Cells[i + 2, 3].Value = notes[i][2];
        }

        guide.Column(1).Width = 16;
        guide.Column(2).Width = 22;
        guide.Column(3).Width = 92;

        return new AdminUserSpreadsheetFile(
            package.GetAsByteArray(),
            XlsxContentType,
            "mau-import-tai-khoan.xlsx");
    }

    public AdminUserSpreadsheetFile GenerateResultWorkbook(
        IReadOnlyList<AdminUserImportCreatedRowDto> createdRows,
        IReadOnlyList<AdminUserImportSkippedRowDto> skippedRows,
        DateTime generatedAtUtc)
    {
        ConfigureLicense();
        using var package = new ExcelPackage();

        var createdSheet = package.Workbook.Worksheets.Add("TaiKhoanDaTao");
        var createdHeaders = new[]
        {
            "Dòng gốc", "Email", "Họ và tên", "Vai trò", "MSSV",
            "Username", "Mật khẩu tạm", "Nguồn mật khẩu", "Đổi MK lần đầu"
        };

        for (var i = 0; i < createdHeaders.Length; i++)
        {
            createdSheet.Cells[1, i + 1].Value = createdHeaders[i];
        }

        using (var header = createdSheet.Cells[1, 1, 1, createdHeaders.Length])
        {
            header.Style.Font.Bold = true;
            header.Style.Font.Color.SetColor(DrawingColor.White);
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(DrawingColor.FromArgb(28, 77, 141));
        }

        for (var idx = 0; idx < createdRows.Count; idx++)
        {
            var row = createdRows[idx];
            var excelRow = idx + 2;
            createdSheet.Cells[excelRow, 1].Value = row.RowNumber;
            WriteSafeTextCell(createdSheet, excelRow, 2, row.Email);
            WriteSafeTextCell(createdSheet, excelRow, 3, row.FullName);
            WriteSafeTextCell(createdSheet, excelRow, 4, row.Role);
            WriteSafeTextCell(createdSheet, excelRow, 5, row.StudentCode);
            WriteSafeTextCell(createdSheet, excelRow, 6, row.Username);
            WriteSafeTextCell(createdSheet, excelRow, 7, row.TemporaryPassword);
            WriteSafeTextCell(createdSheet, excelRow, 8, MapPasswordSourceLabel(row.PasswordSource));
            WriteSafeTextCell(createdSheet, excelRow, 9, row.MustChangePassword ? "Có" : "Không");
        }

        if (createdRows.Count > 0)
        {
            createdSheet.Cells[1, 1, createdRows.Count + 1, createdHeaders.Length].AutoFilter = true;
        }

        createdSheet.View.FreezePanes(2, 1);
        createdSheet.Column(1).Width = 11;
        createdSheet.Column(2).Width = 30;
        createdSheet.Column(3).Width = 26;
        createdSheet.Column(4).Width = 20;
        createdSheet.Column(5).Width = 14;
        createdSheet.Column(6).Width = 22;
        createdSheet.Column(7).Width = 20;
        createdSheet.Column(8).Width = 20;
        createdSheet.Column(9).Width = 16;

        var skippedSheet = package.Workbook.Worksheets.Add("DongBoQua");
        var skippedHeaders = new[] { "Dòng gốc", "Email", "Họ và tên", "Vai trò", "MSSV", "Lý do bỏ qua" };
        for (var i = 0; i < skippedHeaders.Length; i++)
        {
            skippedSheet.Cells[1, i + 1].Value = skippedHeaders[i];
        }

        using (var header = skippedSheet.Cells[1, 1, 1, skippedHeaders.Length])
        {
            header.Style.Font.Bold = true;
            header.Style.Font.Color.SetColor(DrawingColor.White);
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(DrawingColor.FromArgb(153, 27, 27));
        }

        for (var idx = 0; idx < skippedRows.Count; idx++)
        {
            var row = skippedRows[idx];
            var excelRow = idx + 2;
            skippedSheet.Cells[excelRow, 1].Value = row.RowNumber;
            WriteSafeTextCell(skippedSheet, excelRow, 2, row.Email);
            WriteSafeTextCell(skippedSheet, excelRow, 3, row.FullName);
            WriteSafeTextCell(skippedSheet, excelRow, 4, row.Role);
            WriteSafeTextCell(skippedSheet, excelRow, 5, row.StudentCode);
            WriteSafeTextCell(skippedSheet, excelRow, 6, row.Reason);
        }

        if (skippedRows.Count > 0)
        {
            skippedSheet.Cells[1, 1, skippedRows.Count + 1, skippedHeaders.Length].AutoFilter = true;
        }

        skippedSheet.View.FreezePanes(2, 1);
        skippedSheet.Column(1).Width = 11;
        skippedSheet.Column(2).Width = 30;
        skippedSheet.Column(3).Width = 26;
        skippedSheet.Column(4).Width = 20;
        skippedSheet.Column(5).Width = 14;
        skippedSheet.Column(6).Width = 56;

        var timestamp = generatedAtUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return new AdminUserSpreadsheetFile(
            package.GetAsByteArray(),
            XlsxContentType,
            $"ket-qua-import-tai-khoan-{timestamp}.xlsx");
    }

    private static void WriteSafeTextCell(ExcelWorksheet sheet, int row, int col, string? value)
    {
        var cell = sheet.Cells[row, col];
        cell.Style.Numberformat.Format = "@";
        cell.Value = SafeExcelText(value);
    }

    internal static string SafeExcelText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var withoutLeadingSpaces = value.TrimStart().TrimStart('\u200B', '\u200C', '\u200D', '\uFEFF');
        if (withoutLeadingSpaces.Length == 0)
        {
            return string.Empty;
        }

        var startsWithDangerousChar =
            withoutLeadingSpaces[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ||
            value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';

        return startsWithDangerousChar ? $"'{value}" : value;
    }

    private static string MapPasswordSourceLabel(string source) => source switch
    {
        "ROW" => "Từ file import",
        "DEFAULT" => "Mật khẩu mặc định",
        "AUTO_GENERATED" => "Hệ thống tự sinh",
        _ => source
    };

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
        if (HasZipMagicBytes(bytes))
        {
            return true;
        }

        if (bytes.Length >= 4
            && ((bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
                || (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                || (bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46)))
        {
            return true;
        }

        if (bytes.Length >= 2
            && ((bytes[0] == 0x4D && bytes[1] == 0x5A)
                || (bytes[0] == 0xFF && bytes[1] == 0xD8)))
        {
            return true;
        }

        var sampleLength = Math.Min(bytes.Length, 4096);
        for (var i = 0; i < sampleLength; i++)
        {
            if (bytes[i] == 0x00)
            {
                return true;
            }
        }

        return false;
    }

    private static AdminUserImportParseResult ParseCsv(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(memory, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var lines = new List<(int Number, IReadOnlyList<string> Values)>();
        var number = 0;
        var maxAcceptedLines = AdminUserImportRules.MaxRows + 1;
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
                if (lines.Count > maxAcceptedLines)
                {
                    return new AdminUserImportParseResult(
                        [],
                        [$"Danh sách tài khoản vượt quá giới hạn tối đa {AdminUserImportRules.MaxRows} dòng mỗi lần import."],
                        AdminUserImportFileErrorKind.TooManyRows);
                }
            }
        }

        return BuildRows(lines);
    }

    private static AdminUserImportParseResult? ValidateZipArchiveBounds(byte[] bytes)
    {
        try
        {
            using var zipMemory = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(zipMemory, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > MaxZipEntries)
            {
                return new AdminUserImportParseResult(
                    [],
                    ["Tệp .xlsx chứa quá nhiều thành phần nén không hợp lệ."],
                    AdminUserImportFileErrorKind.InvalidFormat);
            }

            long totalUncompressed = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length < 0 || entry.Length > MaxUncompressedZipBytes)
                {
                    return new AdminUserImportParseResult(
                        [],
                        ["Tệp .xlsx vượt quá giới hạn dung lượng giải nén an toàn cho phép."],
                        AdminUserImportFileErrorKind.PayloadTooLarge);
                }

                totalUncompressed += entry.Length;
                if (totalUncompressed > MaxUncompressedZipBytes)
                {
                    return new AdminUserImportParseResult(
                        [],
                        ["Tệp .xlsx vượt quá giới hạn dung lượng giải nén an toàn cho phép."],
                        AdminUserImportFileErrorKind.PayloadTooLarge);
                }
            }

            return null;
        }
        catch (Exception)
        {
            return new AdminUserImportParseResult(
                [],
                ["Không thể đọc cấu trúc nén tệp .xlsx. Vui lòng kiểm tra tệp Excel không bị hỏng."],
                AdminUserImportFileErrorKind.UnsupportedMediaType);
        }
    }

    private static AdminUserImportParseResult ParseXlsx(byte[] bytes)
    {
        var zipError = ValidateZipArchiveBounds(bytes);
        if (zipError is not null)
        {
            return zipError;
        }

        try
        {
            ConfigureLicense();
            using var memory = new MemoryStream(bytes, writable: false);
            using var package = new ExcelPackage(memory);
            var sheet = package.Workbook.Worksheets.FirstOrDefault();
            if (sheet?.Dimension is null)
            {
                return new AdminUserImportParseResult(
                    [],
                    ["Tệp Excel không có dữ liệu."],
                    AdminUserImportFileErrorKind.InvalidFormat);
            }

            var lines = new List<(int Number, IReadOnlyList<string> Values)>();
            var startCol = sheet.Dimension.Start.Column;
            var endCol = Math.Min(sheet.Dimension.End.Column, startCol + MaxXlsxScanColumns - 1);
            var startRow = sheet.Dimension.Start.Row;
            var endRow = Math.Min(sheet.Dimension.End.Row, startRow + MaxXlsxScanRows - 1);
            var colCount = endCol - startCol + 1;
            var maxAcceptedLines = AdminUserImportRules.MaxRows + 1;

            for (var row = startRow; row <= endRow; row++)
            {
                var values = new List<string>(colCount);
                var hasNonEmpty = false;
                for (var col = startCol; col <= endCol; col++)
                {
                    var text = sheet.Cells[row, col].Text ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        hasNonEmpty = true;
                    }

                    values.Add(text);
                }

                if (hasNonEmpty)
                {
                    lines.Add((row, values));
                    if (lines.Count > maxAcceptedLines)
                    {
                        return new AdminUserImportParseResult(
                            [],
                            [$"Danh sách tài khoản vượt quá giới hạn tối đa {AdminUserImportRules.MaxRows} dòng mỗi lần import."],
                            AdminUserImportFileErrorKind.TooManyRows);
                    }
                }
            }

            return BuildRows(lines);
        }
        catch (Exception)
        {
            return new AdminUserImportParseResult(
                [],
                ["Không thể đọc nội dung tệp .xlsx. Vui lòng kiểm tra tệp Excel không bị hỏng."],
                AdminUserImportFileErrorKind.UnsupportedMediaType);
        }
    }

    private static AdminUserImportParseResult BuildRows(IReadOnlyList<(int Number, IReadOnlyList<string> Values)> lines)
    {
        if (lines.Count == 0)
        {
            return new AdminUserImportParseResult(
                [],
                ["Tệp không có dòng dữ liệu."],
                AdminUserImportFileErrorKind.EmptyFile);
        }

        var normalizedHeaders = lines[0].Values
            .Select(NormalizeHeaderToken)
            .ToList();

        var emailIdx = normalizedHeaders.FindIndex(h => EmailHeaders.Contains(h));
        var fullNameIdx = normalizedHeaders.FindIndex(h => FullNameHeaders.Contains(h));
        var roleIdx = normalizedHeaders.FindIndex(h => RoleHeaders.Contains(h));

        var missingHeaders = new List<string>();
        if (emailIdx < 0) missingHeaders.Add("email");
        if (fullNameIdx < 0) missingHeaders.Add("full_name");
        if (roleIdx < 0) missingHeaders.Add("role");

        if (missingHeaders.Count > 0)
        {
            return new AdminUserImportParseResult(
                [],
                [$"Thiếu cột bắt buộc ở dòng tiêu đề: {string.Join(", ", missingHeaders)}."],
                AdminUserImportFileErrorKind.InvalidFormat);
        }

        var studentCodeIdx = normalizedHeaders.FindIndex(h => StudentCodeHeaders.Contains(h));
        var usernameIdx = normalizedHeaders.FindIndex(h => UsernameHeaders.Contains(h));
        var passwordIdx = normalizedHeaders.FindIndex(h => PasswordHeaders.Contains(h));

        var dataLineCount = lines.Count - 1;
        if (dataLineCount == 0)
        {
            return new AdminUserImportParseResult(
                [],
                ["Tệp không có dòng dữ liệu tài khoản nào sau dòng tiêu đề."],
                AdminUserImportFileErrorKind.InvalidFormat);
        }

        if (dataLineCount > AdminUserImportRules.MaxRows)
        {
            return new AdminUserImportParseResult(
                [],
                [$"Danh sách tài khoản vượt quá giới hạn tối đa {AdminUserImportRules.MaxRows} dòng mỗi lần import (hiện có {dataLineCount} dòng)."],
                AdminUserImportFileErrorKind.TooManyRows);
        }

        var rows = new List<AdminUserImportRawRow>(dataLineCount);
        foreach (var line in lines.Skip(1))
        {
            string RawCell(int index) => index >= 0 && index < line.Values.Count ? line.Values[index] : string.Empty;
            string TrimmedCell(int index) => RawCell(index).Trim();

            var email = TrimmedCell(emailIdx);
            var fullName = TrimmedCell(fullNameIdx);
            var role = TrimmedCell(roleIdx);
            var studentCode = studentCodeIdx >= 0 ? NullIfEmpty(TrimmedCell(studentCodeIdx)) : null;
            var username = usernameIdx >= 0 ? NullIfEmpty(TrimmedCell(usernameIdx)) : null;
            var rawPassword = passwordIdx >= 0 ? RawCell(passwordIdx) : string.Empty;
            var password = string.IsNullOrEmpty(rawPassword) ? null : rawPassword;

            rows.Add(new AdminUserImportRawRow(
                line.Number,
                email,
                fullName,
                role,
                studentCode,
                username,
                password));
        }

        return new AdminUserImportParseResult(rows, [], AdminUserImportFileErrorKind.None);
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
