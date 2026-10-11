using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class PracticeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public string PracticeMode { get; set; } = "per_question";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "in_progress";
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public string SelectedDifficulties { get; set; } = "[]";

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<string> DifficultiesList =>
        string.IsNullOrWhiteSpace(SelectedDifficulties)
            ? new List<string>()
            : DeserializeDifficulties(SelectedDifficulties);

    private static List<string> DeserializeDifficulties(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public virtual User Student { get; set; } = null!;
    public virtual Course Course { get; set; } = null!;
    public virtual ICollection<PracticeAnswer> Answers { get; set; } = new List<PracticeAnswer>();
}
