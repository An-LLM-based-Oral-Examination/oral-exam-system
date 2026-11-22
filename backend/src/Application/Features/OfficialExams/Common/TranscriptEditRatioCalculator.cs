using System;
using System.Text;
using System.Text.RegularExpressions;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Common;

public static partial class TranscriptEditRatioCalculator
{
    private const int MaxLength = 50_000;

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiWhitespaceRegex();

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            trimmed = trimmed[..MaxLength];
        }

        var nfc = trimmed.Normalize(NormalizationForm.FormC);
        return MultiWhitespaceRegex().Replace(nfc, " ").ToLowerInvariant();
    }

    /// <summary>
    /// Computes normalized Levenshtein edit ratio in [0.0000, 1.0000] using O(min(m, n)) memory.
    /// Empty raw + non-empty edited returns 1.0000m (blocks typing without voice).
    /// </summary>
    public static decimal CalculateRatio(string? initialVoiceTranscript, string? editedTranscript)
    {
        var raw = Normalize(initialVoiceTranscript);
        var edited = Normalize(editedTranscript);

        if (raw.Length == 0 && edited.Length == 0)
        {
            return 0.0000m;
        }

        if (raw.Length == 0 || edited.Length == 0)
        {
            return 1.0000m;
        }

        if (string.Equals(raw, edited, StringComparison.Ordinal))
        {
            return 0.0000m;
        }

        var distance = ComputeLevenshteinDistance(raw, edited);
        var denominator = Math.Max(raw.Length, edited.Length);
        var ratio = (decimal)distance / denominator;
        return Math.Clamp(Math.Round(ratio, 4, MidpointRounding.AwayFromZero), 0.0000m, 1.0000m);
    }

    public static Result<decimal?> ValidateAndCompute(
        string? examInputMode,
        string? initialVoiceTranscript,
        string? editedTranscript,
        bool isManuallyEdited)
    {
        var isVoiceOnly = string.Equals(examInputMode, ExamInputMode.VoiceOnly, StringComparison.OrdinalIgnoreCase);

        if (isVoiceOnly)
        {
            if (isManuallyEdited)
            {
                return Result<decimal?>.Failure(OfficialExamErrors.TranscriptEditNotAllowedInVoiceOnlyMode);
            }

            if (initialVoiceTranscript is not null)
            {
                var normalizedRaw = Normalize(initialVoiceTranscript);
                var normalizedEdited = Normalize(editedTranscript);
                if (!string.Equals(normalizedRaw, normalizedEdited, StringComparison.Ordinal))
                {
                    return Result<decimal?>.Failure(OfficialExamErrors.TranscriptEditNotAllowedInVoiceOnlyMode);
                }

                return Result<decimal?>.Success(0.0000m);
            }

            return Result<decimal?>.Success(null);
        }

        // VoiceWithTranscriptEdit mode
        if (initialVoiceTranscript is not null)
        {
            var ratio = CalculateRatio(initialVoiceTranscript, editedTranscript);
            if (ratio > OfficialExamRules.MaxTranscriptEditRatio)
            {
                var pct = Math.Round(ratio * 100m, 1, MidpointRounding.AwayFromZero);
                return Result<decimal?>.Failure(
                    $"{OfficialExamErrors.TranscriptEditLimitExceeded} (Thực tế: {pct:0.0}% > 35.0%).");
            }

            return Result<decimal?>.Success(ratio);
        }

        return Result<decimal?>.Success(null);
    }

    private static int ComputeLevenshteinDistance(string source, string target)
    {
        // Keep shorter string as columns for O(min(m, n)) memory
        if (source.Length < target.Length)
        {
            (source, target) = (target, source);
        }

        var n = target.Length;
        var prev = new int[n + 1];
        var curr = new int[n + 1];

        for (var j = 0; j <= n; j++)
        {
            prev[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            curr[0] = i;
            var sc = source[i - 1];
            for (var j = 1; j <= n; j++)
            {
                var cost = sc == target[j - 1] ? 0 : 1;
                var del = prev[j] + 1;
                var ins = curr[j - 1] + 1;
                var sub = prev[j - 1] + cost;
                curr[j] = Math.Min(Math.Min(del, ins), sub);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[n];
    }
}
