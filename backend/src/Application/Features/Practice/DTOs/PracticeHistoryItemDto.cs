using System;

namespace OralExamination.Application.Features.Practice.DTOs;

/// <summary>
/// DTO tóm tắt lịch sử một phiên luyện tập của sinh viên.
/// </summary>
public sealed class PracticeHistoryItemDto
{
    public Guid SessionId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string PracticeMode { get; set; } = string.Empty; // "per_question" | "full_session"
    public string Status { get; set; } = string.Empty; // "in_progress" | "completed"
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public decimal? AverageScore { get; set; }
}
