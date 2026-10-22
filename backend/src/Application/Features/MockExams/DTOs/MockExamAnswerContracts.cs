using System;

namespace OralExamination.Application.Features.MockExams.DTOs;

/// <summary>
/// DTO phản hồi nộp câu trả lời thi thử thành công (MF-02).
/// Khi session bật follow-up, có thể kèm NeedsFollowUp + FollowUpPrompt ngay sau câu chính/phụ.
/// </summary>
public record SubmitMockExamAnswerResponse(
    Guid AnswerId,
    Guid QuestionId,
    string Transcript,
    string Status,
    DateTime AnsweredAt,
    bool IsFollowUp = false,
    Guid? ParentAnswerId = null,
    bool NeedsFollowUp = false,
    string? FollowUpPrompt = null,
    Guid? FollowUpAnswerId = null,
    int FollowUpIndex = 0,
    int MaxFollowUps = 0
);
