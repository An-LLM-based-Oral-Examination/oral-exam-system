using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.MockExams.DTOs;

/// <summary>
/// DTO tiếp nhận yêu cầu khởi tạo bài thi thử từ Client (MF-02).
/// Sinh viên chỉ chọn Môn và tùy chọn Có/Không Follow-up.
/// </summary>
public record StartMockExamRequest(
    Guid CourseId,
    bool HasFollowUp = false
);

/// <summary>
/// Tiêu chí chấm điểm rubric con rút gọn hiển thị cho sinh viên khi làm bài thi thử.
/// </summary>
public record MockExamRubricCriterionDto(
    string Name,
    decimal MaxScore
);

/// <summary>
/// Chi tiết câu hỏi trong đề thi thử rút từ kho practice_questions.
/// </summary>
public record MockExamQuestionDto(
    Guid Id,
    int OrderIndex,
    string Title,
    string Content,
    string BloomLevel,
    string Difficulty,
    List<MockExamRubricCriterionDto> RubricCriteria
);

/// <summary>
/// DTO phản hồi khởi tạo bài thi thử thành công (200 OK).
/// </summary>
public record StartMockExamResponse(
    Guid SessionId,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    bool HasFollowUp,
    int DurationMinutes,
    DateTime StartedAt,
    DateTime ExpiresAt,
    int UsedQuota,
    int RemainingQuota,
    int MaxDailyQuota,
    List<MockExamQuestionDto> Questions
);
