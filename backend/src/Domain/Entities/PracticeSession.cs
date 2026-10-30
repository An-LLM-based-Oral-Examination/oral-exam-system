using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace OralExamination.Domain.Entities;

public partial class PracticeSession
{
    private const string QuestionIdPrefix = "qid:";
    private string? _cachedRawDifficulties;
    private string? _cachedRawQuestionIds;
    private List<string>? _cachedDifficultiesList;
    private List<Guid>? _cachedQuestionIdsList;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public string PracticeMode { get; set; } = "per_question";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "in_progress";
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public string SelectedDifficulties { get; set; } = "[]";
    public string SelectedQuestionIdsJson { get; set; } = "[]";

    private void EnsureCacheParsed()
    {
        var currentRawDiffs = SelectedDifficulties ?? string.Empty;
        var currentRawQids = SelectedQuestionIdsJson ?? string.Empty;
        if (string.Equals(_cachedRawDifficulties, currentRawDiffs, StringComparison.Ordinal) &&
            string.Equals(_cachedRawQuestionIds, currentRawQids, StringComparison.Ordinal) &&
            _cachedDifficultiesList is not null &&
            _cachedQuestionIdsList is not null)
        {
            return;
        }

        _cachedRawDifficulties = currentRawDiffs;
        _cachedRawQuestionIds = currentRawQids;

        _cachedDifficultiesList = string.IsNullOrWhiteSpace(currentRawDiffs)
            ? new List<string>()
            : DeserializeDifficulties(currentRawDiffs);

        var ids = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(currentRawQids) && currentRawQids != "[]")
        {
            try
            {
                var parsedGuids = JsonSerializer.Deserialize<List<Guid>>(currentRawQids);
                if (parsedGuids != null)
                {
                    foreach (var g in parsedGuids)
                    {
                        if (g != Guid.Empty && !ids.Contains(g))
                        {
                            ids.Add(g);
                        }
                    }
                }
            }
            catch
            {
                try
                {
                    var rawStrings = JsonSerializer.Deserialize<List<string>>(currentRawQids) ?? new List<string>();
                    foreach (var item in rawStrings)
                    {
                        var span = item.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase)
                            ? item.AsSpan(QuestionIdPrefix.Length)
                            : item.AsSpan();
                        if (Guid.TryParse(span, out var qid) && qid != Guid.Empty && !ids.Contains(qid))
                        {
                            ids.Add(qid);
                        }
                    }
                }
                catch
                {
                    // Bỏ qua nếu chuỗi JSON không hợp lệ
                }
            }
        }

        // Hỗ trợ tương thích ngược nếu dữ liệu cũ hoặc test gán chuỗi "qid:<guid>" vào SelectedDifficulties
        if (ids.Count == 0 && !string.IsNullOrWhiteSpace(currentRawDiffs))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<List<string>>(currentRawDiffs) ?? new List<string>();
                foreach (var item in raw)
                {
                    if (!string.IsNullOrWhiteSpace(item) &&
                        item.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase) &&
                        Guid.TryParse(item.AsSpan(QuestionIdPrefix.Length), out var qid) &&
                        qid != Guid.Empty &&
                        !ids.Contains(qid))
                    {
                        ids.Add(qid);
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu chuỗi JSON không hợp lệ
            }
        }

        _cachedQuestionIdsList = ids;
    }

    [NotMapped]
    public List<string> DifficultiesList
    {
        get
        {
            EnsureCacheParsed();
            return new List<string>(_cachedDifficultiesList!);
        }
        set
        {
            var cleanDiffs = (value ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            SelectedDifficulties = JsonSerializer.Serialize(cleanDiffs);
            _cachedRawDifficulties = null;
        }
    }

    [NotMapped]
    public List<Guid> SelectedQuestionIds
    {
        get
        {
            EnsureCacheParsed();
            return new List<Guid>(_cachedQuestionIdsList!);
        }
        set
        {
            var cleanIds = (value ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            SelectedQuestionIdsJson = JsonSerializer.Serialize(cleanIds);

            // Làm sạch cột SelectedDifficulties nếu trước đó có chứa tiền tố "qid:"
            var cleanDiffs = DifficultiesList;
            SelectedDifficulties = JsonSerializer.Serialize(cleanDiffs);

            _cachedRawQuestionIds = null;
            _cachedRawDifficulties = null;
        }
    }

    public static string SerializeDifficulties(IEnumerable<string> difficulties)
    {
        var list = (difficulties ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith(QuestionIdPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return JsonSerializer.Serialize(list);
    }

    public static string SerializeQuestionIds(IEnumerable<Guid>? questionIds)
    {
        var list = (questionIds ?? Enumerable.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        return JsonSerializer.Serialize(list);
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
