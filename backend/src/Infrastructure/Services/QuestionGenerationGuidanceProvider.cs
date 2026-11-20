using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Services;

/// <summary>Versioned teaching profiles bundled with the backend; no database writes.</summary>
public sealed class QuestionGenerationGuidanceProvider : IQuestionGenerationGuidanceProvider
{
    private readonly Dictionary<string, AiQuestionGenerationGuidance> _profiles;

    public QuestionGenerationGuidanceProvider()
    {
        using var stream = typeof(QuestionGenerationGuidanceProvider).Assembly.GetManifestResourceStream(
            "OralExamination.QuestionGenerationGuidance.json")
            ?? throw new InvalidOperationException("Missing question generation guidance resource.");
        var profiles = JsonSerializer.Deserialize<Dictionary<string, AiQuestionGenerationGuidance>>(stream)
            ?? throw new InvalidOperationException("Invalid question generation guidance resource.");
        _profiles = new(profiles, StringComparer.OrdinalIgnoreCase);
        foreach (var profile in _profiles.Values)
        {
            if (string.IsNullOrWhiteSpace(profile.CourseScope) || profile.Clos is null ||
                profile.Clos.Any(c => c is null || string.IsNullOrWhiteSpace(c.Code) ||
                    string.IsNullOrWhiteSpace(c.Topics) || string.IsNullOrWhiteSpace(c.Boundaries)) ||
                profile.Clos.Select(c => c.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profile.Clos.Count)
                throw new InvalidOperationException("Incomplete or duplicate question generation guidance.");
        }
    }

    public AiQuestionGenerationGuidance? Get(string courseCode, IEnumerable<string> selectedCloCodes)
    {
        if (!_profiles.TryGetValue(courseCode.Trim(), out var profile)) return null;
        var selected = selectedCloCodes.Select(c => c.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Return a fresh collection and only the selected CLOs to avoid leaking another teaching scope.
        return profile with { Clos = profile.Clos.Where(c => selected.Contains(c.Code)).ToArray() };
    }
}
