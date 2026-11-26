using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OralExamination.Application.Features.InClassShifts;

public sealed record PoolCandidateQuestion(
    Guid Id,
    Guid? CloId,
    string Difficulty
);

public static class InClassShiftPoolConfig
{
    public const string AllCourseBank = "ALL_COURSE_BANK";
    public const string SelectedClos = "SELECTED_CLOS";
    public const string SelectedPoolLegacy = "SELECTED_POOL";

    public static readonly IReadOnlyList<string> CanonicalDifficulties =
    [
        "easy",
        "medium",
        "hard"
    ];

    public static string GetDifficultyLabel(string difficulty) =>
        difficulty switch
        {
            "easy" => "Dễ",
            "medium" => "Trung bình",
            "hard" => "Khó",
            _ => difficulty
        };

    public static bool TryNormalizeDifficulty(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var lower = raw.Trim().ToLowerInvariant();
        if (lower is "easy" or "medium" or "hard")
        {
            normalized = lower;
            return true;
        }

        return false;
    }

    public static bool TryNormalizeCreateDifficulties(
        IReadOnlyList<string>? requestedDifficulties,
        out List<string> normalizedDifficulties,
        out bool wasExplicitlySpecified,
        out string? error)
    {
        error = null;
        if (requestedDifficulties is null)
        {
            wasExplicitlySpecified = false;
            normalizedDifficulties = CanonicalDifficulties.ToList();
            return true;
        }

        wasExplicitlySpecified = true;
        if (requestedDifficulties.Count == 0)
        {
            normalizedDifficulties = new List<string>();
            error = InClassShiftErrors.EmptySelectedDifficulties;
            return false;
        }

        var selectedSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in requestedDifficulties)
        {
            if (!TryNormalizeDifficulty(item, out var canonical))
            {
                normalizedDifficulties = new List<string>();
                error = InClassShiftErrors.InvalidDifficultyValue;
                return false;
            }

            selectedSet.Add(canonical);
        }

        normalizedDifficulties = CanonicalDifficulties
            .Where(selectedSet.Contains)
            .ToList();

        if (normalizedDifficulties.Count == 0)
        {
            error = InClassShiftErrors.EmptySelectedDifficulties;
            return false;
        }

        return true;
    }

    public static List<string> ParseStoredDifficulties(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CanonicalDifficulties.ToList();
        }

        try
        {
            var rawList = JsonSerializer.Deserialize<List<string>>(json);
            if (rawList is null || rawList.Count == 0)
            {
                return CanonicalDifficulties.ToList();
            }

            var selectedSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in rawList)
            {
                if (TryNormalizeDifficulty(item, out var canonical))
                {
                    selectedSet.Add(canonical);
                }
            }

            var ordered = CanonicalDifficulties.Where(selectedSet.Contains).ToList();
            return ordered.Count > 0 ? ordered : CanonicalDifficulties.ToList();
        }
        catch (JsonException)
        {
            return CanonicalDifficulties.ToList();
        }
    }

    public static string SerializeDifficulties(IEnumerable<string>? difficulties)
    {
        var selectedSet = new HashSet<string>(StringComparer.Ordinal);
        if (difficulties is not null)
        {
            foreach (var item in difficulties)
            {
                if (TryNormalizeDifficulty(item, out var canonical))
                {
                    selectedSet.Add(canonical);
                }
            }
        }

        var ordered = CanonicalDifficulties.Where(selectedSet.Contains).ToList();
        if (ordered.Count == 0)
        {
            ordered = CanonicalDifficulties.ToList();
        }

        return JsonSerializer.Serialize(ordered);
    }

    public static List<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Guid>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<Guid>>(json);
            return parsed?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        }
        catch (JsonException)
        {
            return new List<Guid>();
        }
    }

    public static string SerializeGuidList(IEnumerable<Guid>? ids)
    {
        var clean = (ids ?? Enumerable.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        return JsonSerializer.Serialize(clean);
    }

    public static List<Guid> AllocateBalancedQuestions(
        IReadOnlyList<PoolCandidateQuestion> candidates,
        string poolMode,
        IReadOnlyList<Guid> selectedCloIds,
        int targetCount,
        IReadOnlyDictionary<Guid, int> usageCounts)
    {
        if (targetCount <= 0 || candidates.Count < targetCount)
        {
            return new List<Guid>();
        }

        var pickedIds = new HashSet<Guid>();
        var pickedOrdered = new List<Guid>(targetCount);
        var diffPickedCount = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["easy"] = 0,
            ["medium"] = 0,
            ["hard"] = 0
        };

        if (string.Equals(poolMode, SelectedClos, StringComparison.OrdinalIgnoreCase))
        {
            if (selectedCloIds.Count == 0)
            {
                return new List<Guid>();
            }

            var distinctCloIds = selectedCloIds.Distinct().ToList();

            var candidatesByClo = new Dictionary<Guid, List<PoolCandidateQuestion>>();
            foreach (var cloId in distinctCloIds)
            {
                var cloCandidates = candidates.Where(q => q.CloId == cloId).ToList();
                if (cloCandidates.Count == 0)
                {
                    return new List<Guid>();
                }

                candidatesByClo[cloId] = cloCandidates;
            }

            if (distinctCloIds.Count > targetCount)
            {
                return new List<Guid>();
            }

            var cloPickedCount = distinctCloIds.ToDictionary(id => id, _ => 0);

            // Pha 1: Đảm bảo mỗi CLO đã chọn nhận ít nhất 1 câu (ưu tiên CLO ít câu hợp lệ trước để tối ưu cân bằng độ khó)
            var orderedCloIds = distinctCloIds
                .OrderBy(id => candidatesByClo[id].Count)
                .ToList();

            foreach (var cloId in orderedCloIds)
            {
                var availableForClo = candidatesByClo[cloId]
                    .Where(q => !pickedIds.Contains(q.Id))
                    .ToList();

                if (availableForClo.Count == 0)
                {
                    return new List<Guid>();
                }

                var chosen = availableForClo
                    .OrderBy(q => diffPickedCount.GetValueOrDefault(q.Difficulty, 0))
                    .ThenBy(q => usageCounts.GetValueOrDefault(q.Id, 0))
                    .ThenBy(_ => Random.Shared.Next())
                    .First();

                pickedIds.Add(chosen.Id);
                pickedOrdered.Add(chosen.Id);
                cloPickedCount[cloId]++;
                diffPickedCount[chosen.Difficulty] = diffPickedCount.GetValueOrDefault(chosen.Difficulty, 0) + 1;
            }

            // Pha 2: Bù các câu còn lại cân bằng đồng thời giữa CLO -> Độ khó -> Tần suất xuất hiện thấp nhất (Anti-Collision)
            while (pickedOrdered.Count < targetCount)
            {
                var remaining = candidates
                    .Where(q => q.CloId.HasValue && cloPickedCount.ContainsKey(q.CloId.Value) && !pickedIds.Contains(q.Id))
                    .ToList();

                if (remaining.Count == 0)
                {
                    return new List<Guid>();
                }

                var next = remaining
                    .OrderBy(q => cloPickedCount[q.CloId!.Value])
                    .ThenBy(q => diffPickedCount.GetValueOrDefault(q.Difficulty, 0))
                    .ThenBy(q => usageCounts.GetValueOrDefault(q.Id, 0))
                    .ThenBy(_ => Random.Shared.Next())
                    .First();

                pickedIds.Add(next.Id);
                pickedOrdered.Add(next.Id);
                cloPickedCount[next.CloId!.Value]++;
                diffPickedCount[next.Difficulty] = diffPickedCount.GetValueOrDefault(next.Difficulty, 0) + 1;
            }

            return pickedOrdered;
        }

        // Chế độ ALL_COURSE_BANK hoặc tương thích ca cũ SELECTED_POOL: cân bằng theo Độ khó -> Anti-Collision
        while (pickedOrdered.Count < targetCount)
        {
            var remaining = candidates
                .Where(q => !pickedIds.Contains(q.Id))
                .ToList();

            if (remaining.Count == 0)
            {
                return new List<Guid>();
            }

            var next = remaining
                .OrderBy(q => diffPickedCount.GetValueOrDefault(q.Difficulty, 0))
                .ThenBy(q => usageCounts.GetValueOrDefault(q.Id, 0))
                .ThenBy(_ => Random.Shared.Next())
                .First();

            pickedIds.Add(next.Id);
            pickedOrdered.Add(next.Id);
            diffPickedCount[next.Difficulty] = diffPickedCount.GetValueOrDefault(next.Difficulty, 0) + 1;
        }

        return pickedOrdered;
    }
}
