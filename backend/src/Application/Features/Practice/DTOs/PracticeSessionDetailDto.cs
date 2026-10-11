using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Practice.DTOs;

/// <summary>
/// DTO chi tiết phiên luyện tập và các câu trả lời đã nộp phục vụ Frontend.
/// </summary>
public sealed class PracticeSessionDetailDto
{
    public Guid SessionId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string PracticeMode { get; set; } = null!; // "per_question" | "full_session"
    public string Status { get; set; } = null!; // "in_progress" | "completed"
    public int TranscriptBufferSeconds { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<PracticeQuestionDto> Questions { get; set; } = new();
    public List<PracticeAnswerDetailDto> Answers { get; set; } = new();
}

public sealed class PracticeAnswerDetailDto
{
    public Guid AnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public string AnswerText { get; set; } = null!;
    public bool IsFollowUp { get; set; }
    public Guid? ParentAnswerId { get; set; }
    public string Status { get; set; } = "pending"; // "pending" | "graded" | "failed"
    public DateTime SubmittedAt { get; set; }
    public decimal? TotalScore { get; set; }
    public string? Feedback { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public bool? IsSuspicious { get; set; }
    public bool NeedsFollowUp { get; set; }
    public string? FollowUpPrompt { get; set; }
    public List<PracticeEvaluationDetailDto> CriteriaScores { get; set; } = new();
}

public sealed class PracticeEvaluationDetailDto
{
    public Guid CriterionId { get; set; }
    public string CriterionName { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string? Comment { get; set; }
}
