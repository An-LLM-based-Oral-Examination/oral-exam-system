using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Common.Interfaces;

public interface IExamGradeReportExporter
{
    byte[] ExportExcel(ExamShiftGradeReport report);

    byte[] ExportPdf(ExamShiftGradeReport report);
}
