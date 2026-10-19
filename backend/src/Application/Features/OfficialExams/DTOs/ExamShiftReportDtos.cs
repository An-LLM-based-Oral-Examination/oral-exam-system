using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.OfficialExams.DTOs;

/// <summary>
/// Kết quả xuất file báo cáo bảng điểm khảo thí (Excel .xlsx hoặc PDF có chữ ký số).
/// </summary>
public sealed class ExportShiftFileResponseDto
{
    public byte[] FileContents { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string CertificateHash { get; init; } = string.Empty;
}

/// <summary>
/// Dữ liệu dòng điểm của từng sinh viên trong biên bản khảo thí ca thi.
/// </summary>
public sealed class ShiftGradeRowDto
{
    public int Index { get; init; }
    public int SeatNumber { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public decimal? AiScore { get; init; }
    public decimal? FinalScore { get; init; }
    public string TicketStatus { get; init; } = string.Empty;
    public bool IsLocked { get; init; }
    public string? OverrideReason { get; init; }
    public string? AudioHashSha256 { get; init; }
}

/// <summary>
/// Kết quả tra cứu & xác thực bảng điểm điện tử công khai qua mã QR (BE-V02).
/// </summary>
public sealed class GradeCertificateVerificationDto
{
    public string Status { get; init; } = "VALID";
    public string University { get; init; } = "Trường Đại học FPT - Phân hiệu TP.HCM";
    public string DigitalSignatureIssuer { get; init; } = "FPTU Examination Board SHA-256 Cryptographic Seal";
    public string CertificateHash { get; init; } = string.Empty;
    public Guid ShiftId { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public string RoomLab { get; init; } = string.Empty;
    public string SessionTitle { get; init; } = string.Empty;
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public string SemesterCode { get; init; } = string.Empty;
    public DateOnly ExamDate { get; init; }
    public DateTime ShiftStartTime { get; init; }
    public DateTime ShiftEndTime { get; init; }
    public string ProctorName { get; init; } = string.Empty;
    public int TotalStudents { get; init; }
    public int LockedTicketsCount { get; init; }
    public bool IsFullyLocked { get; init; }
    public DateTime VerifiedAtUtc { get; init; }
    public List<VerifiedStudentScoreSummaryDto> StudentScores { get; init; } = new();
}

/// <summary>
/// Tóm tắt điểm sinh viên đã niêm phong phục vụ đối chiếu trên cổng xác thực QR.
/// </summary>
public sealed class VerifiedStudentScoreSummaryDto
{
    public int SeatNumber { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public decimal? FinalScore { get; init; }
    public bool IsLocked { get; init; }
}
