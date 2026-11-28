using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Common.Interfaces;

public interface IInClassShiftReportExporter
{
    byte[] ExportExcel(InClassShiftGradeReport report);
}
