using System;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.InClassShifts.DTOs;
using DrawingColor = System.Drawing.Color;

namespace OralExamination.Infrastructure.Reporting;

public sealed class InClassShiftReportExporter : IInClassShiftReportExporter
{
    private static readonly object LicenseLock = new();
    private static bool _licenseConfigured;

    public InClassShiftReportExporter()
    {
        ConfigureLicense();
    }

    public byte[] ExportExcel(InClassShiftGradeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Bảng điểm kiểm tra lớp");

        sheet.Cells["A1:J1"].Merge = true;
        sheet.Cells["A1"].Value = "TRƯỜNG ĐẠI HỌC FPT - PHÂN HIỆU TP.HCM";
        sheet.Cells["A2:J2"].Merge = true;
        sheet.Cells["A2"].Value = "BẢNG ĐIỂM CA KIỂM TRA TRONG LỚP (MF-05)";
        sheet.Cells["A1:J2"].Style.Font.Bold = true;
        sheet.Cells["A1:J2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        sheet.Cells["A4"].Value = "Học kỳ";
        sheet.Cells["B4"].Value = SafeText(report.SemesterCode);
        sheet.Cells["D4"].Value = "Môn học";
        sheet.Cells["E4"].Value = SafeText($"{report.CourseCode} - {report.CourseName}");
        sheet.Cells["A5"].Value = "Lớp học phần";
        sheet.Cells["B5"].Value = SafeText($"{report.ClassCode} - {report.ClassName}");
        sheet.Cells["D5"].Value = "Giảng viên";
        sheet.Cells["E5"].Value = SafeText(report.LecturerName);
        sheet.Cells["A6"].Value = "Ca thi";
        sheet.Cells["B6"].Value = SafeText(report.ShiftName);
        sheet.Cells["D6"].Value = "Trạng thái ca";
        sheet.Cells["E6"].Value = SafeText(report.ShiftStatus);
        sheet.Cells["G6"].Value = "Khóa sổ";
        sheet.Cells["H6"].Value = report.IsLocked ? "FINALIZED (LOCKED)" : "OPEN";

        var headers = new[]
        {
            "STT", "MSSV", "Họ và tên", "Email", "Trạng thái",
            "Điểm AI", "Điểm chính thức", "Nhận xét giảng viên",
            "Thời gian vào thi (UTC)", "Thời gian kết thúc (UTC)"
        };

        const int headerRow = 8;
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cells[headerRow, index + 1].Value = headers[index];
        }

        using (var header = sheet.Cells[headerRow, 1, headerRow, headers.Length])
        {
            header.Style.Font.Bold = true;
            header.Style.Font.Color.SetColor(DrawingColor.White);
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(DrawingColor.FromArgb(31, 78, 121));
            header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        var rowNumber = headerRow + 1;
        foreach (var row in report.Rows)
        {
            sheet.Cells[rowNumber, 1].Value = row.Index;
            sheet.Cells[rowNumber, 2].Value = SafeText(row.StudentCode);
            sheet.Cells[rowNumber, 3].Value = SafeText(row.FullName);
            sheet.Cells[rowNumber, 4].Value = SafeText(row.Email);
            sheet.Cells[rowNumber, 5].Value = SafeText(row.Status);
            sheet.Cells[rowNumber, 6].Value = row.AiScore;
            sheet.Cells[rowNumber, 7].Value = row.FinalScore;
            sheet.Cells[rowNumber, 8].Value = SafeText(row.LecturerNotes);
            sheet.Cells[rowNumber, 9].Value = row.JoinedAtUtc;
            sheet.Cells[rowNumber, 10].Value = row.FinishedAtUtc;
            rowNumber++;
        }

        if (report.Rows.Count > 0)
        {
            sheet.Cells[headerRow + 1, 6, rowNumber - 1, 7].Style.Numberformat.Format = "0.00";
            sheet.Cells[headerRow + 1, 9, rowNumber - 1, 10].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
            sheet.Cells[headerRow, 1, rowNumber - 1, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            sheet.Cells[headerRow, 1, rowNumber - 1, headers.Length].AutoFilter = true;
        }

        sheet.Cells[rowNumber + 1, 1].Value = "Xuất lúc (UTC)";
        sheet.Cells[rowNumber + 1, 2].Value = report.ExportedAtUtc;
        sheet.Cells[rowNumber + 1, 2].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";

        sheet.View.FreezePanes(headerRow + 1, 1);
        sheet.Cells[sheet.Dimension.Address].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        sheet.Cells[sheet.Dimension.Address].Style.WrapText = true;
        sheet.Column(1).Width = 7;
        sheet.Column(2).Width = 15;
        sheet.Column(3).Width = 28;
        sheet.Column(4).Width = 30;
        sheet.Column(5).Width = 16;
        sheet.Column(6).Width = 12;
        sheet.Column(7).Width = 16;
        sheet.Column(8).Width = 38;
        sheet.Column(9).Width = 22;
        sheet.Column(10).Width = 22;
        sheet.PrinterSettings.Orientation = eOrientation.Landscape;
        sheet.PrinterSettings.FitToPage = true;
        sheet.PrinterSettings.FitToWidth = 1;
        sheet.PrinterSettings.FitToHeight = 0;

        return package.GetAsByteArray();
    }

    private static string SafeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var withoutLeadingSpaces = value.TrimStart(' ');
        var startsWithDangerousChar =
            withoutLeadingSpaces[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ||
            trimmed[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';

        return startsWithDangerousChar ? $"'{trimmed}" : trimmed;
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
