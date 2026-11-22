using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;

public sealed class ExportShiftPdfQueryHandler(
    IApplicationDbContext context,
    IExamGradeReportExporter exporter,
    IPdfDigitalSigner? pdfSigner = null)
    : IRequestHandler<ExportShiftPdfQuery, Result<ExportShiftFileResponseDto>>
{
    public async Task<Result<ExportShiftFileResponseDto>> Handle(
        ExportShiftPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<ExportShiftFileResponseDto>.Failure(
                "FORBIDDEN: Không xác định được danh tính người xuất báo cáo bảng điểm.");
        }

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null || actor.Role is not (UserRole.ExaminationOfficer or UserRole.Lecturer or UserRole.DepartmentHead))
        {
            return Result<ExportShiftFileResponseDto>.Failure(
                "FORBIDDEN: Bạn không có quyền xuất biên bản bảng điểm của ca thi này.");
        }

        var shift = await OfficialExamReportQuery.LoadShiftAsync(context, request.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Result<ExportShiftFileResponseDto>.Failure("Ca thi không tồn tại trong hệ thống.");
        }

        if (!await OfficialExamAuthorization.CanAuditShiftAsync(context, actor.Id, shift, cancellationToken))
        {
            return Result<ExportShiftFileResponseDto>.Failure(
                "FORBIDDEN: Bạn không phải Giám khảo được phân công ca thi hoặc Trưởng bộ môn phụ trách môn học này.");
        }

        if (!OfficialExamReportQuery.IsPublishedAndLocked(shift))
        {
            return Result<ExportShiftFileResponseDto>.Failure("Chỉ được xuất báo cáo khi ca thi đã công bố và khóa toàn bộ điểm.");
        }

        var report = ExamShiftGradeReportFactory.Create(shift);
        var thumbprint = !string.IsNullOrWhiteSpace(shift.CertificateThumbprint)
            ? shift.CertificateThumbprint
            : pdfSigner?.GetActiveCertificateThumbprint();
        return Result<ExportShiftFileResponseDto>.Success(new ExportShiftFileResponseDto
        {
            FileContents = exporter.ExportPdf(report),
            ContentType = "application/pdf",
            FileName = $"BienBanDiem_{Sanitize(report.CourseCode)}_{Sanitize(report.ShiftName)}_{shift.Id.ToString("N")[..8]}.pdf",
            CertificateHash = report.CertificateHash,
            CertificateThumbprint = thumbprint
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
