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
    List<MockExamRubricCriterionDto> RubricCriteria,
    string? ImageUrl = null,
    string? ImageDescription = null
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

/// <summary>
/// Câu trả lời đã nộp trong ca thi thử (dùng khi resume).
/// </summary>
public record MockExamStoredAnswerDto(
    Guid AnswerId,
    Guid QuestionId,
    string Transcript,
    int TimeTakenSeconds,
    string Status,
    DateTime AnsweredAt,
    bool IsFollowUp = false,
    Guid? ParentAnswerId = null,
    string? PromptText = null
);

/// <summary>
/// Câu hỏi phụ đang chờ trả lời khi khôi phục ca thi.
/// </summary>
public record MockExamActiveFollowUpDto(
    Guid AnswerId,
    Guid QuestionId,
    Guid ParentAnswerId,
    string Prompt,
    int FollowUpIndex,
    int MaxFollowUps
);

/// <summary>
/// DTO khôi phục ca thi thử đang in_progress (câu hỏi + đáp án đã làm).
/// </summary>
public record ResumeMockExamResponse(
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
    List<MockExamQuestionDto> Questions,
    List<MockExamStoredAnswerDto> Answers,
    MockExamActiveFollowUpDto? ActiveFollowUp = null
);
