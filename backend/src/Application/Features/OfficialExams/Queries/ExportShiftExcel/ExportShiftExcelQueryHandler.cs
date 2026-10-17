using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Queries.ExportShiftExcel;

/// <summary>
/// Handler xây dựng tệp bảng điểm Excel (.xlsx chuẩn OpenXML SpreadsheetML) cho ca thi thật (BE-V01).
/// Đính kèm mã niêm phong chữ ký số SHA-256 (CertificateHash) phục vụ đối chiếu QR Code.
/// </summary>
public sealed class ExportShiftExcelQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ExportShiftExcelQuery, Result<ExportShiftFileResponseDto>>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<Result<ExportShiftFileResponseDto>> Handle(ExportShiftExcelQuery request, CancellationToken cancellationToken)
    {
        var shift = await context.RealExamSessionShifts
            .AsNoTracking()
            .Include(s => s.Session)
                .ThenInclude(ss => ss.Course)
                    .ThenInclude(c => c.Semester)
            .Include(s => s.ProctorUser)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.Student)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.LecturerAudit)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.Submissions)
                    .ThenInclude(sub => sub.AiEvaluations)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<ExportShiftFileResponseDto>.Failure("Ca thi không tồn tại trong hệ thống.");
        }

        var rows = ExamShiftCertificateHashGenerator.BuildGradeRows(shift);
        var certificateHash = ExamShiftCertificateHashGenerator.ComputeCertificateHash(shift, rows);

        var courseCode = shift.Session?.Course?.Code ?? "EXAM";
        var safeShiftName = SanitizeFileNameSegment(shift.ShiftName);
        var fileName = $"BangDiem_{courseCode}_{safeShiftName}_{shift.Id.ToString("N")[..8]}.xlsx";

        var excelBytes = BuildOpenXmlWorkbookBytes(shift, rows, certificateHash);

        return Result<ExportShiftFileResponseDto>.Success(new ExportShiftFileResponseDto
        {
            FileContents = excelBytes,
            ContentType = ExcelContentType,
            FileName = fileName,
            CertificateHash = certificateHash
        });
    }

    private static string SanitizeFileNameSegment(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Shift";
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                sb.Append(ch);
            }
            else if (char.IsWhiteSpace(ch))
            {
                sb.Append('_');
            }
        }

        return sb.Length > 0 ? sb.ToString() : "Shift";
    }

    private static byte[] BuildOpenXmlWorkbookBytes(
        RealExamSessionShift shift,
        IReadOnlyList<ShiftGradeRowDto> rows,
        string certificateHash)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "[Content_Types].xml", GetContentTypesXml());
            WriteZipEntry(archive, "_rels/.rels", GetRootRelsXml());
            WriteZipEntry(archive, "xl/workbook.xml", GetWorkbookXml());
            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", GetWorkbookRelsXml());
            WriteZipEntry(archive, "xl/styles.xml", GetStylesXml());
            WriteZipEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(shift, rows, certificateHash));
        }

        return memoryStream.ToArray();
    }

    private static void WriteZipEntry(ZipArchive archive, string entryPath, string xmlContent)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(xmlContent);
    }

    private static string GetContentTypesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private static string GetRootRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string GetWorkbookXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="BangDiemKhaoThi" sheetId="1" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    private static string GetWorkbookRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private static string GetStylesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="2">
            <font><sz val="11"/><name val="Arial"/></font>
            <font><b/><sz val="11"/><name val="Arial"/></font>
          </fonts>
          <fills count="2">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
          </fills>
          <borders count="1">
            <border><left/><right/><top/><bottom/><diagonal/></border>
          </borders>
          <cellStyleXfs count="1">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
          </cellStyleXfs>
          <cellXfs count="2">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
          </cellXfs>
        </styleSheet>
        """;

    private static string BuildWorksheetXml(
        RealExamSessionShift shift,
        IReadOnlyList<ShiftGradeRowDto> rows,
        string certificateHash)
    {
        var courseCode = shift.Session?.Course?.Code ?? "N/A";
        var courseName = shift.Session?.Course?.Name ?? "N/A";
        var semesterCode = shift.Session?.Course?.Semester?.Code ?? "FA26";
        var sessionTitle = shift.Session?.Title ?? "Kỳ Thi Vấn Đáp Chính Thức";
        var examDate = shift.Session?.ExamDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? shift.StartTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var proctorName = shift.ProctorUser?.FullName ?? "Chưa phân công";

        var sb = new StringBuilder(4096);
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

        int rowIndex = 1;

        AppendTextRow(sb, rowIndex++, new[]
        {
            "TRƯỜNG ĐẠI HỌC FPT - PHÂN HIỆU TP.HCM (FPTU HCMC) - PHÒNG KHẢO THÍ",
            "BIÊN BẢN BẢNG ĐIỂM THI VẤN ĐÁP CHÍNH THỨC (MF-04)"
        }, bold: true);

        AppendTextRow(sb, rowIndex++, new[]
        {
            $"Học kỳ: {semesterCode}",
            $"Môn học: {courseCode} - {courseName}",
            $"Kỳ thi: {sessionTitle}",
            $"Ngày thi: {examDate}"
        }, bold: false);

        AppendTextRow(sb, rowIndex++, new[]
        {
            $"Ca thi: {shift.ShiftName}",
            $"Phòng Lab: {shift.RoomLab}",
            $"Giám thị: {proctorName}",
            $"Trạng thái ca: {shift.Status}"
        }, bold: false);

        AppendTextRow(sb, rowIndex++, new[]
        {
            "Mã niêm phong chữ ký số SHA-256 (CertificateHash):",
            certificateHash,
            $"Tra cứu QR: /api/v1/verification/grades/{certificateHash}"
        }, bold: true);

        // Dòng trống ngăn cách
        rowIndex++;

        // Header bảng điểm
        AppendTextRow(sb, rowIndex++, new[]
        {
            "STT",
            "Số Máy (Seat)",
            "MSSV",
            "Họ và Tên",
            "Email",
            "Điểm AI Gợi Ý",
            "Điểm Chốt Chính Thức",
            "Trạng Thái Vé",
            "Khóa 1 Chiều",
            "Lý Do Điều Chỉnh (Override Reason)",
            "Audio SHA-256"
        }, bold: true);

        foreach (var item in rows)
        {
            sb.Append($@"<row r=""{rowIndex}"">");
            AppendNumberCell(sb, "A", rowIndex, item.Index);
            AppendNumberCell(sb, "B", rowIndex, item.SeatNumber);
            AppendStringCell(sb, "C", rowIndex, item.StudentCode);
            AppendStringCell(sb, "D", rowIndex, item.FullName);
            AppendStringCell(sb, "E", rowIndex, item.Email);

            if (item.AiScore.HasValue)
            {
                AppendDecimalCell(sb, "F", rowIndex, item.AiScore.Value);
            }
            else
            {
                AppendStringCell(sb, "F", rowIndex, "Chưa chấm");
            }

            if (item.FinalScore.HasValue)
            {
                AppendDecimalCell(sb, "G", rowIndex, item.FinalScore.Value);
            }
            else
            {
                AppendStringCell(sb, "G", rowIndex, "Chưa chốt");
            }

            AppendStringCell(sb, "H", rowIndex, item.TicketStatus);
            AppendStringCell(sb, "I", rowIndex, item.IsLocked ? "LOCKED" : "OPEN");
            AppendStringCell(sb, "J", rowIndex, item.OverrideReason ?? string.Empty);
            AppendStringCell(sb, "K", rowIndex, item.AudioHashSha256 ?? string.Empty);
            sb.Append("</row>");
            rowIndex++;
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendTextRow(StringBuilder sb, int rowNumber, IReadOnlyList<string> values, bool bold)
    {
        sb.Append($@"<row r=""{rowNumber}"">");
        for (int i = 0; i < values.Count; i++)
        {
            var colName = ((char)('A' + i)).ToString();
            AppendStringCell(sb, colName, rowNumber, values[i], bold ? 1 : 0);
        }
        sb.Append("</row>");
    }

    private static void AppendStringCell(StringBuilder sb, string col, int rowNumber, string value, int styleIndex = 0)
    {
        var escaped = SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        sb.Append($@"<c r=""{col}{rowNumber}"" s=""{styleIndex}"" t=""inlineStr""><is><t>{escaped}</t></is></c>");
    }

    private static void AppendNumberCell(StringBuilder sb, string col, int rowNumber, int value)
    {
        sb.Append($@"<c r=""{col}{rowNumber}""><v>{value.ToString(CultureInfo.InvariantCulture)}</v></c>");
    }

    private static void AppendDecimalCell(StringBuilder sb, string col, int rowNumber, decimal value)
    {
        sb.Append($@"<c r=""{col}{rowNumber}""><v>{value.ToString("0.00", CultureInfo.InvariantCulture)}</v></c>");
    }
}
