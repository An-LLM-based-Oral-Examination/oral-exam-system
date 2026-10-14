using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.MockExams.DTOs;

/// <summary>
/// DTO đại diện cho một mục lịch sử thi thử trong danh sách phân trang (MF-02).
/// </summary>
public record MockExamHistoryItemDto(
    Guid SessionId,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    DateTime StartedAt,
    int DurationMinutes,
    decimal? TotalScore,
    string Status,
    int AnsweredQuestions,
    int TotalQuestions
);

/// <summary>
/// DTO danh sách lịch sử thi thử phân trang trả về cho Student Portal (FE-02).
/// </summary>
public record MockExamHistoryListResponse(
    List<MockExamHistoryItemDto> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    int TotalPages
);
