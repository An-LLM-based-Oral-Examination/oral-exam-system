using System.Text;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;

public sealed class ExportShiftPdfQueryHandler(
    IApplicationDbContext context,
    IExamGradeReportExporter exporter)
    : IRequestHandler<ExportShiftPdfQuery, Result<ExportShiftFileResponseDto>>
{
    public async Task<Result<ExportShiftFileResponseDto>> Handle(
        ExportShiftPdfQuery request,
        CancellationToken cancellationToken)
    {
        var shift = await OfficialExamReportQuery.LoadShiftAsync(context, request.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Result<ExportShiftFileResponseDto>.Failure("Ca thi không tồn tại trong hệ thống.");
        }

        if (!OfficialExamReportQuery.IsPublishedAndLocked(shift))
        {
            return Result<ExportShiftFileResponseDto>.Failure("Chỉ được xuất báo cáo khi ca thi đã công bố và khóa toàn bộ điểm.");
        }

        var report = ExamShiftGradeReportFactory.Create(shift);
        return Result<ExportShiftFileResponseDto>.Success(new ExportShiftFileResponseDto
        {
            FileContents = exporter.ExportPdf(report),
            ContentType = "application/pdf",
            FileName = $"BienBanDiem_{Sanitize(report.CourseCode)}_{Sanitize(report.ShiftName)}_{shift.Id.ToString("N")[..8]}.pdf",
            CertificateHash = report.CertificateHash
        });
    }

    private static string Sanitize(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            result.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_');
        }

        return result.Length == 0 ? "Report" : result.ToString();
    }
}
