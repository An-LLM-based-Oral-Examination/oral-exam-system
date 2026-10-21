using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.MockExams.DTOs;

/// <summary>
/// Chi tiết điểm tiêu chí rubric con theo chuẩn đầu ra CLO.
/// </summary>
public record MockExamCriterionScoreDto(
    Guid CriterionId,
    string CriterionName,
    string? BloomLevel,
    decimal MaxScore,
    decimal Score,
    string? Comment
);

/// <summary>
/// Chi tiết điểm và nhận xét của từng câu hỏi trong đề thi thử.
/// </summary>
public record MockExamQuestionScoreDto(
    Guid QuestionId,
    int OrderIndex,
    string Title,
    string Content,
    string BloomLevel,
    string Difficulty,
    string? Transcript,
    decimal Score,
    string? Feedback,
    decimal? ConfidenceScore,
    List<MockExamCriterionScoreDto> CriteriaScores,
    string? ImageUrl = null,
    string? ImageDescription = null
);

/// <summary>
/// DTO Bảng điểm Instant Scorecard đầy đủ phản hồi sau khi nộp bài thi thử (MF-02).
/// </summary>
public record MockExamScorecardDto(
    Guid SessionId,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    decimal TotalScore,
    string Classification,
    int TotalQuestions,
    int AnsweredQuestions,
    int DurationMinutes,
    bool IsOverdue,
    DateTime StartedAt,
    DateTime SubmittedAt,
    List<MockExamQuestionScoreDto> Questions
);
