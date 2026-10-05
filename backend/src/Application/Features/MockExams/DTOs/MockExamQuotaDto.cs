using System;

namespace OralExamination.Application.Features.MockExams.DTOs;

/// <summary>
/// Data Transfer Object phản ánh thông tin hạn ngạch thi thử trong ngày của sinh viên (MF-02).
/// Ràng buộc Daily Quota Guard K = 3 lượt/ngày/môn.
/// </summary>
public sealed record MockExamQuotaDto(
    Guid CourseId,
    Guid StudentId,
    DateOnly QuotaDate,
    int UsedCount,
    int RemainingCount,
    int MaxDailyQuota,
    bool CanStartExam
);
