namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed record ExamShiftGradeReport(
    Guid ShiftId,
    string SemesterCode,
    string CourseCode,
    string CourseName,
    string SessionTitle,
    DateOnly ExamDate,
    string ShiftName,
    string RoomLab,
    string ProctorName,
    string ShiftStatus,
    string CertificateHash,
    DateTime IssuedAtUtc,
    IReadOnlyList<ShiftGradeRowDto> Rows);
