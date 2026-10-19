using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Common;

public static class ExamShiftGradeReportFactory
{
    public static ExamShiftGradeReport Create(RealExamSessionShift shift)
    {
        var rows = ExamShiftCertificateHashGenerator.BuildGradeRows(shift);
        var certificateHash = ExamShiftCertificateHashGenerator.ComputeCertificateHash(shift, rows);

        return new ExamShiftGradeReport(
            shift.Id,
            shift.Session?.Course?.Semester?.Code ?? "N/A",
            shift.Session?.Course?.Code ?? "N/A",
            shift.Session?.Course?.Name ?? "N/A",
            shift.Session?.Title ?? "Kỳ thi vấn đáp chính thức",
            shift.Session?.ExamDate ?? DateOnly.FromDateTime(shift.StartTime),
            shift.ShiftName,
            shift.RoomLab,
            shift.ProctorUser?.FullName ?? "Chưa phân công",
            shift.Status,
            certificateHash,
            DateTime.UtcNow,
            rows);
    }
}
