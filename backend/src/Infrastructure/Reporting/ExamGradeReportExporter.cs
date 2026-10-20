using OfficeOpenXml;
using OfficeOpenXml.Style;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DrawingColor = System.Drawing.Color;

namespace OralExamination.Infrastructure.Reporting;

public sealed class ExamGradeReportExporter : IExamGradeReportExporter
{
    private static readonly object LicenseLock = new();
    private static bool _licensesConfigured;

    public ExamGradeReportExporter()
    {
        ConfigureLicenses();
    }

    public byte[] ExportExcel(ExamShiftGradeReport report)
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Bảng điểm khảo thí");

        sheet.Cells["A1:K1"].Merge = true;
        sheet.Cells["A1"].Value = "TRƯỜNG ĐẠI HỌC FPT - PHÂN HIỆU TP.HCM";
        sheet.Cells["A2:K2"].Merge = true;
        sheet.Cells["A2"].Value = "BIÊN BẢN BẢNG ĐIỂM THI VẤN ĐÁP CHÍNH THỨC";
        sheet.Cells["A1:K2"].Style.Font.Bold = true;
        sheet.Cells["A1:K2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        sheet.Cells["A4"].Value = "Học kỳ";
        sheet.Cells["B4"].Value = report.SemesterCode;
        sheet.Cells["D4"].Value = "Môn học";
        sheet.Cells["E4"].Value = $"{report.CourseCode} - {report.CourseName}";
        sheet.Cells["A5"].Value = "Kỳ thi";
        sheet.Cells["B5"].Value = report.SessionTitle;
        sheet.Cells["D5"].Value = "Ngày thi";
        sheet.Cells["E5"].Value = report.ExamDate.ToString("dd/MM/yyyy");
        sheet.Cells["A6"].Value = "Ca thi";
        sheet.Cells["B6"].Value = report.ShiftName;
        sheet.Cells["D6"].Value = "Phòng";
        sheet.Cells["E6"].Value = report.RoomLab;
        sheet.Cells["G6"].Value = "Giám thị";
        sheet.Cells["H6"].Value = report.ProctorName;

        var headers = new[]
        {
            "STT", "Số ghế", "MSSV", "Họ và tên", "Email", "Điểm AI",
            "Điểm chính thức", "Trạng thái", "Khóa", "Lý do điều chỉnh", "Audio SHA-256"
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
            sheet.Cells[rowNumber, 2].Value = row.SeatNumber;
            sheet.Cells[rowNumber, 3].Value = SafeText(row.StudentCode);
            sheet.Cells[rowNumber, 4].Value = SafeText(row.FullName);
            sheet.Cells[rowNumber, 5].Value = SafeText(row.Email);
            sheet.Cells[rowNumber, 6].Value = row.AiScore;
            sheet.Cells[rowNumber, 7].Value = row.FinalScore;
            sheet.Cells[rowNumber, 8].Value = SafeText(row.TicketStatus);
            sheet.Cells[rowNumber, 9].Value = row.IsLocked ? "LOCKED" : "OPEN";
            sheet.Cells[rowNumber, 10].Value = SafeText(row.OverrideReason);
            sheet.Cells[rowNumber, 11].Value = SafeText(row.AudioHashSha256);
            rowNumber++;
        }

        if (report.Rows.Count > 0)
        {
            sheet.Cells[headerRow + 1, 6, rowNumber - 1, 7].Style.Numberformat.Format = "0.00";
            sheet.Cells[headerRow, 1, rowNumber - 1, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            sheet.Cells[headerRow, 1, rowNumber - 1, headers.Length].AutoFilter = true;
        }

        sheet.Cells[rowNumber + 1, 1].Value = "Mã niêm phong SHA-256";
        sheet.Cells[rowNumber + 1, 2, rowNumber + 1, 11].Merge = true;
        sheet.Cells[rowNumber + 1, 2].Value = report.CertificateHash;
        sheet.Cells[rowNumber + 2, 1].Value = "Phát hành lúc (UTC)";
        sheet.Cells[rowNumber + 2, 2].Value = report.IssuedAtUtc;
        sheet.Cells[rowNumber + 2, 2].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";

        sheet.View.FreezePanes(headerRow + 1, 1);
        sheet.Cells[sheet.Dimension.Address].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        sheet.Cells[sheet.Dimension.Address].Style.WrapText = true;
        sheet.Column(1).Width = 7;
        sheet.Column(2).Width = 9;
        sheet.Column(3).Width = 15;
        sheet.Column(4).Width = 28;
        sheet.Column(5).Width = 30;
        sheet.Column(6).Width = 12;
        sheet.Column(7).Width = 16;
        sheet.Column(8).Width = 14;
        sheet.Column(9).Width = 10;
        sheet.Column(10).Width = 36;
        sheet.Column(11).Width = 68;
        sheet.PrinterSettings.Orientation = eOrientation.Landscape;
        sheet.PrinterSettings.FitToPage = true;
        sheet.PrinterSettings.FitToWidth = 1;
        sheet.PrinterSettings.FitToHeight = 0;
        sheet.HeaderFooter.OddFooter.CenteredText = "Trang &P / &N";

        return package.GetAsByteArray();
    }

    public byte[] ExportPdf(ExamShiftGradeReport report) =>
        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(8));
                page.Header().Column(column =>
                {
                    column.Item().AlignCenter().Text("TRƯỜNG ĐẠI HỌC FPT - PHÂN HIỆU TP.HCM").Bold().FontSize(13);
                    column.Item().AlignCenter().Text("BIÊN BẢN BẢNG ĐIỂM THI VẤN ĐÁP CHÍNH THỨC").Bold().FontSize(12);
                    column.Item().PaddingTop(6).Text(
                        $"Học kỳ: {report.SemesterCode} | Môn: {report.CourseCode} - {report.CourseName} | Ngày thi: {report.ExamDate:dd/MM/yyyy}");
                    column.Item().Text(
                        $"Ca: {report.ShiftName} | Phòng: {report.RoomLab} | Giám thị: {report.ProctorName}");
                });

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(28);
                        columns.ConstantColumn(38);
                        columns.ConstantColumn(72);
                        columns.RelativeColumn(1.4f);
                        columns.ConstantColumn(50);
                        columns.ConstantColumn(58);
                        columns.ConstantColumn(58);
                        columns.RelativeColumn(1.5f);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "STT");
                        HeaderCell(header.Cell(), "Ghế");
                        HeaderCell(header.Cell(), "MSSV");
                        HeaderCell(header.Cell(), "Họ và tên");
                        HeaderCell(header.Cell(), "Điểm AI");
                        HeaderCell(header.Cell(), "Điểm chốt");
                        HeaderCell(header.Cell(), "Khóa");
                        HeaderCell(header.Cell(), "Lý do điều chỉnh");
                    });

                    foreach (var row in report.Rows)
                    {
                        BodyCell(table.Cell(), row.Index.ToString());
                        BodyCell(table.Cell(), row.SeatNumber.ToString());
                        BodyCell(table.Cell(), row.StudentCode);
                        BodyCell(table.Cell(), row.FullName);
                        BodyCell(table.Cell(), row.AiScore?.ToString("0.00") ?? "-");
                        BodyCell(table.Cell(), row.FinalScore?.ToString("0.00") ?? "-");
                        BodyCell(table.Cell(), row.IsLocked ? "LOCKED" : "OPEN");
                        BodyCell(table.Cell(), row.OverrideReason ?? string.Empty);
                    }
                });

                page.Footer().Column(column =>
                {
                    column.Item().Text(text =>
                    {
                        text.Span("Mã niêm phong SHA-256: ").Bold();
                        text.Span(report.CertificateHash).FontSize(7);
                    });
                    column.Item().AlignRight().Text(text =>
                    {
                        text.Span("Trang ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            });
        }).GeneratePdf();

    private static string SafeText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > 0 && text[0] is '=' or '+' or '-' or '@' ? $"'{text}" : text;
    }

    private static void HeaderCell(IContainer container, string text) =>
        container.Border(0.5f).Background(Colors.Blue.Darken2).Padding(3)
            .AlignCenter().Text(text).FontColor(Colors.White).Bold();

    private static void BodyCell(IContainer container, string text) =>
        container.Border(0.5f).Padding(3).Text(text);

    private static void ConfigureLicenses()
    {
        lock (LicenseLock)
        {
            if (_licensesConfigured)
            {
                return;
            }

            ExcelPackage.License.SetNonCommercialOrganization("FPT University Oral Examination Project");
            QuestPDF.Settings.License = LicenseType.Community;
            _licensesConfigured = true;
        }
    }
}
