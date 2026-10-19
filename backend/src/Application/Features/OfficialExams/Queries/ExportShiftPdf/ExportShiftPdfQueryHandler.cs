using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

namespace OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;

/// <summary>
/// Handler xây dựng tệp biên bản bảng điểm PDF chuẩn PDF 1.4 có đóng dấu chữ ký số SHA-256 (BE-V01).
/// Phục vụ lưu trữ pháp lý Khảo thí ĐH FPT và đối chiếu qua cổng QR Code công khai.
/// </summary>
public sealed class ExportShiftPdfQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ExportShiftPdfQuery, Result<ExportShiftFileResponseDto>>
{
    private const string PdfContentType = "application/pdf";

    public async Task<Result<ExportShiftFileResponseDto>> Handle(ExportShiftPdfQuery request, CancellationToken cancellationToken)
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
        var fileName = $"BienBanDiem_{courseCode}_{safeShiftName}_{shift.Id.ToString("N")[..8]}.pdf";

        var pdfBytes = BuildSignedPdfDocumentBytes(shift, rows, certificateHash);

        return Result<ExportShiftFileResponseDto>.Success(new ExportShiftFileResponseDto
        {
            FileContents = pdfBytes,
            ContentType = PdfContentType,
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

    private static byte[] BuildSignedPdfDocumentBytes(
        RealExamSessionShift shift,
        IReadOnlyList<ShiftGradeRowDto> rows,
        string certificateHash)
    {
        var courseCode = shift.Session?.Course?.Code ?? "N/A";
        var courseName = RemoveDiacritics(shift.Session?.Course?.Name ?? "N/A");
        var semesterCode = shift.Session?.Course?.Semester?.Code ?? "FA26";
        var sessionTitle = RemoveDiacritics(shift.Session?.Title ?? "Official Oral Exam Session");
        var examDate = shift.Session?.ExamDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? shift.StartTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var proctorName = RemoveDiacritics(shift.ProctorUser?.FullName ?? "N/A");
        var shiftName = RemoveDiacritics(shift.ShiftName);
        var roomLab = RemoveDiacritics(shift.RoomLab);

        var contentBuilder = new StringBuilder(4096);
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F2 13 Tf");
        contentBuilder.AppendLine("40 800 Td");
        contentBuilder.AppendLine("16 TL");
        AppendPdfTextLine(contentBuilder, "TRUONG DAI HOC FPT - PHAN HIEU TP.HCM (FPT UNIVERSITY HCMC)");
        AppendPdfTextLine(contentBuilder, "BIEN BAN BANG DIEM THI VAN DAP CHINH THUC (MF-04)");

        contentBuilder.AppendLine("/F1 10 Tf");
        contentBuilder.AppendLine("14 TL");
        AppendPdfTextLine(contentBuilder, $"Hoc ky: {semesterCode} | Mon thi: {courseCode} - {courseName}");
        AppendPdfTextLine(contentBuilder, $"Ky thi: {sessionTitle} | Ngay thi: {examDate}");
        AppendPdfTextLine(contentBuilder, $"Ca thi: {shiftName} | Phong Lab: {roomLab} | Giam thi: {proctorName} | Trang thai: {shift.Status}");
        AppendPdfTextLine(contentBuilder, "----------------------------------------------------------------------------------------------------");

        contentBuilder.AppendLine("/F2 10 Tf");
        AppendPdfTextLine(contentBuilder, "STT | Seat | MSSV       | Ho va Ten                  | Diem AI | Diem Chot | Trang Thai | Khoa");
        contentBuilder.AppendLine("/F1 10 Tf");
        AppendPdfTextLine(contentBuilder, "----------------------------------------------------------------------------------------------------");

        foreach (var row in rows)
        {
            var aiStr = row.AiScore.HasValue ? row.AiScore.Value.ToString("0.00", CultureInfo.InvariantCulture) : "N/A ";
            var finalStr = row.FinalScore.HasValue ? row.FinalScore.Value.ToString("0.00", CultureInfo.InvariantCulture) : "N/A ";
            var lockStr = row.IsLocked ? "LOCKED" : "OPEN";
            var studentNameAscii = TruncateAndPad(RemoveDiacritics(row.FullName), 26);
            var studentCodeAscii = TruncateAndPad(RemoveDiacritics(row.StudentCode), 10);
            var statusAscii = TruncateAndPad(RemoveDiacritics(row.TicketStatus), 10);

            var line = string.Format(
                CultureInfo.InvariantCulture,
                "{0,3} | {1,4} | {2} | {3} | {4,7} | {5,9} | {6} | {7}",
                row.Index,
                row.SeatNumber,
                studentCodeAscii,
                studentNameAscii,
                aiStr,
                finalStr,
                statusAscii,
                lockStr);

            AppendPdfTextLine(contentBuilder, line);

            if (!string.IsNullOrWhiteSpace(row.OverrideReason))
            {
                AppendPdfTextLine(contentBuilder, $"      -> Ly do dieu chinh: {RemoveDiacritics(row.OverrideReason)}");
            }
        }

        AppendPdfTextLine(contentBuilder, "----------------------------------------------------------------------------------------------------");
        contentBuilder.AppendLine("/F2 10 Tf");
        AppendPdfTextLine(contentBuilder, "CHU KY SO NIEM PHONG (FPTU EXAMINATION BOARD DIGITAL SEAL - SHA-256):");
        contentBuilder.AppendLine("/F1 9 Tf");
        AppendPdfTextLine(contentBuilder, $"CertificateHash: {certificateHash}");
        AppendPdfTextLine(contentBuilder, $"QR Verification Endpoint: GET /api/v1/verification/grades/{certificateHash}");
        AppendPdfTextLine(contentBuilder, $"Issued At (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        contentBuilder.AppendLine("ET");

        var streamContent = contentBuilder.ToString();
        var streamBytes = Encoding.ASCII.GetBytes(streamContent);

        using var ms = new MemoryStream();
        WriteAscii(ms, "%PDF-1.4\n");

        var offsets = new List<long>();

        // 1. Catalog
        offsets.Add(ms.Position);
        WriteAscii(ms, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // 2. Pages
        offsets.Add(ms.Position);
        WriteAscii(ms, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // 3. Page
        offsets.Add(ms.Position);
        WriteAscii(ms, "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>\nendobj\n");

        // 4. Font Regular
        offsets.Add(ms.Position);
        WriteAscii(ms, "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>\nendobj\n");

        // 5. Font Bold
        offsets.Add(ms.Position);
        WriteAscii(ms, "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold >>\nendobj\n");

        // 6. Content Stream
        offsets.Add(ms.Position);
        WriteAscii(ms, $"6 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
        ms.Write(streamBytes, 0, streamBytes.Length);
        WriteAscii(ms, "\nendstream\nendobj\n");

        // XRef Table
        long xrefPosition = ms.Position;
        WriteAscii(ms, $"xref\n0 {offsets.Count + 1}\n");
        WriteAscii(ms, "0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteAscii(ms, $"{offset:D10} 00000 n \n");
        }

        // Trailer
        WriteAscii(ms, $"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF\n");

        return ms.ToArray();
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void AppendPdfTextLine(StringBuilder sb, string text)
    {
        var escaped = EscapePdfString(text);
        sb.Append('(').Append(escaped).AppendLine(") '");
    }

    private static string EscapePdfString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string TruncateAndPad(string input, int length)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new string(' ', length);
        }

        if (input.Length > length)
        {
            return input[..length];
        }

        return input.PadRight(length, ' ');
    }

    private static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                var mapped = c switch
                {
                    'đ' => 'd',
                    'Đ' => 'D',
                    _ when c <= 127 => c,
                    _ => ' '
                };
                sb.Append(mapped);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
