using System;

namespace OralExamination.Application.Features.MockExams.DTOs;

/// <summary>
/// DTO phản hồi nộp câu trả lời thi thử thành công (MF-02).
/// </summary>
public record SubmitMockExamAnswerResponse(
    Guid AnswerId,
    Guid QuestionId,
    string Transcript,
    string Status,
    DateTime AnsweredAt
);
