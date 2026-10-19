using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Student.DTOs;

/// <summary>
/// Tổng quan dữ liệu bảng điều khiển học tập và khảo thí của sinh viên trên Student Portal (FE-10 / BE-V03).
/// </summary>
public sealed class StudentDashboardDto
{
    public Guid StudentId { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;

    public int TotalPracticeSessions { get; init; }
    public int CompletedPracticeSessions { get; init; }
    public decimal? AveragePracticeScore { get; init; }

    public int TotalMockExams { get; init; }
    public int CompletedMockExams { get; init; }
    public decimal? AverageMockExamScore { get; init; }

    public List<StudentCourseQuotaDto> CourseQuotas { get; init; } = new();
    public List<StudentCriterionMasteryDto> CriterionMastery { get; init; } = new();
    public List<StudentOfficialExamSummaryDto> OfficialExamTickets { get; init; } = new();
}

/// <summary>
/// Hạn ngạch thi thử (MF-02) trong ngày theo từng môn học của sinh viên.
/// </summary>
public sealed class StudentCourseQuotaDto
{
    public Guid CourseId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public int MaxMockExamsPerDay { get; init; }
    public int UsedMockExamsToday { get; init; }
    public int RemainingMockExamsToday { get; init; }
}

/// <summary>
/// Thống kê mức độ thành thạo theo Tiêu chí Barem / Bloom Level từ kết quả AI chấm.
/// </summary>
public sealed class StudentCriterionMasteryDto
{
    public string CriterionName { get; init; } = string.Empty;
    public string BloomLevel { get; init; } = string.Empty;
    public int EvaluatedCount { get; init; }
    public decimal AverageScorePercentage { get; init; }
}

/// <summary>
/// Lịch thi thật phòng Lab và kết quả điểm đã công bố của sinh viên (MF-04).
/// </summary>
public sealed class StudentOfficialExamSummaryDto
{
    public Guid TicketId { get; init; }
    public Guid ShiftId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public string SessionTitle { get; init; } = string.Empty;
    public string ShiftName { get; init; } = string.Empty;
    public string RoomLab { get; init; } = string.Empty;
    public DateOnly ExamDate { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int SeatNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal? FinalScore { get; init; }
    public bool IsLocked { get; init; }
    public string StudentAcknowledgementStatus { get; init; } = "PENDING";
}

/// <summary>
/// Phản hồi khi sinh viên xác nhận đồng ý kết quả điểm thi thật trên Student Portal (MF-04).
/// </summary>
public sealed class AcknowledgeOfficialGradeResponseDto
{
    public Guid TicketId { get; init; }
    public Guid StudentId { get; init; }
    public string StudentAcknowledgementStatus { get; init; } = "ACKNOWLEDGED";
    public DateTime AcknowledgedAtUtc { get; init; }
    public string Message { get; init; } = string.Empty;
}
