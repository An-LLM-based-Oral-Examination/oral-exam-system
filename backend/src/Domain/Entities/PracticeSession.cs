using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace OralExamination.Domain.Entities;

public partial class PracticeSession
{
    private const string QuestionIdPrefix = "qid:";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public string PracticeMode { get; set; } = "per_question";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "in_progress";
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public string SelectedDifficulties { get; set; } = "[]";

    [NotMapped]
    public List<string> DifficultiesList
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedDifficulties))
            {
                return new List<string>();
            }

            return DeserializeDifficulties(SelectedDifficulties);
        }
        set
        {
            var existingQids = SelectedQuestionIds.Select(id => $"{QuestionIdPrefix}{id}").ToList();
            var cleanDiffs = (value ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            cleanDiffs.AddRange(existingQids);
            SelectedDifficulties = JsonSerializer.Serialize(cleanDiffs);
        }
    }

    [NotMapped]
    public List<Guid> SelectedQuestionIds
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedDifficulties))
            {
                return new List<Guid>();
            }

            try
            {
                var raw = JsonSerializer.Deserialize<List<string>>(SelectedDifficulties) ?? new List<string>();
                var ids = new List<Guid>();
                foreach (var item in raw)
                {
                    if (!string.IsNullOrWhiteSpace(item) &&
                        item.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase) &&
                        Guid.TryParse(item.Substring(QuestionIdPrefix.Length), out var qid) &&
                        qid != Guid.Empty &&
                        !ids.Contains(qid))
                    {
                        ids.Add(qid);
                    }
                }

                return ids;
            }
            catch
            {
                return new List<Guid>();
            }
        }
        set
        {
            var diffs = DifficultiesList;
            var qids = (value ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Select(id => $"{QuestionIdPrefix}{id}")
                .ToList();
            diffs.AddRange(qids);
            SelectedDifficulties = JsonSerializer.Serialize(diffs);
        }
    }

    public static string SerializeDifficultiesAndQuestions(IEnumerable<string> difficulties, IEnumerable<Guid>? questionIds = null)
    {
        var list = (difficulties ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (questionIds != null)
        {
            foreach (var id in questionIds.Where(x => x != Guid.Empty).Distinct())
            {
                list.Add($"{QuestionIdPrefix}{id}");
            }
        }

        return JsonSerializer.Serialize(list);
    }

    private static List<string> DeserializeDifficulties(string json)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return raw
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch
        {
            return json
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().Trim('"', '[', ']'))
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public virtual User Student { get; set; } = null!;
    public virtual Course Course { get; set; } = null!;
    public virtual ICollection<PracticeAnswer> Answers { get; set; } = new List<PracticeAnswer>();
}
