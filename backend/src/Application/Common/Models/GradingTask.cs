using System;

namespace OralExamination.Application.Common.Models;

public class GradingTask
{
    public Guid AnswerId { get; set; }
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid StudentId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public bool IsFollowUp { get; set; }
    public Guid? ParentAnswerId { get; set; }
    public bool IsFullSession { get; set; }
    public string ConnectionId { get; set; } = string.Empty; // For SignalR realtime callback
}
