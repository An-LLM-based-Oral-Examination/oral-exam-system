using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Common;

internal static class ExamSetStructureValidator
{
    public static Result Validate(
        ExamStructure? structure,
        IReadOnlyCollection<string> questionBloomLevels,
        bool enforceExactTotal = true,
        bool enforceBloomDistribution = true)
    {
        if (structure is null)
        {
            return Result.Success();
        }

        var count = questionBloomLevels.Count;
        if (structure.TotalQuestions > 0)
        {
            if (enforceExactTotal && count != structure.TotalQuestions)
            {
                return Result.Failure(
                    $"UNPROCESSABLE: Tổng số câu hỏi trong bộ đề ({count}) phải khớp chính xác với cấu trúc đề ({structure.TotalQuestions} câu).");
            }

            if (!enforceExactTotal && count > structure.TotalQuestions)
            {
                return Result.Failure(
                    $"UNPROCESSABLE: Tổng số câu hỏi trong bộ đề ({count}) không được vượt quá cấu trúc đề ({structure.TotalQuestions} câu).");
            }
        }

        if (!enforceBloomDistribution)
        {
            return Result.Success();
        }

        var rawBloom = structure.BloomDistribution?.Trim();
        if (string.IsNullOrWhiteSpace(rawBloom) || rawBloom == "{}")
        {
            return Result.Success();
        }

        Dictionary<string, int>? parsedMap;
        try
        {
            parsedMap = JsonSerializer.Deserialize<Dictionary<string, int>>(rawBloom);
        }
        catch (JsonException)
        {
            return Result.Failure("UNPROCESSABLE: Phân bổ Bloom của cấu trúc đề không hợp lệ.");
        }

        if (parsedMap is null)
        {
            return Result.Failure("UNPROCESSABLE: Phân bổ Bloom của cấu trúc đề không hợp lệ.");
        }

        var requiredByLevel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in parsedMap)
        {
            var level = kv.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(level))
            {
                continue;
            }

            if (kv.Value < 0)
            {
                return Result.Failure("UNPROCESSABLE: Phân bổ Bloom của cấu trúc đề không hợp lệ.");
            }

            requiredByLevel[level] = requiredByLevel.GetValueOrDefault(level) + kv.Value;
        }

        var configuredTotal = requiredByLevel.Values.Where(v => v > 0).Sum();
        if (configuredTotal == 0)
        {
            return Result.Success();
        }

        var actualByLevel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var bloom in questionBloomLevels)
        {
            var normalized = bloom?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            actualByLevel[normalized] = actualByLevel.GetValueOrDefault(normalized) + 1;
        }

        foreach (var (level, required) in requiredByLevel)
        {
            if (required <= 0)
            {
                continue;
            }

            var actual = actualByLevel.GetValueOrDefault(level, 0);
            var exactBloomMatchRequired = structure.TotalQuestions > 0 && configuredTotal == structure.TotalQuestions;
            if (actual < required || (exactBloomMatchRequired && actual != required))
            {
                return Result.Failure(
                    $"UNPROCESSABLE: Số lượng câu hỏi mức Bloom '{level}' ({actual}) không khớp cấu trúc đề (yêu cầu {required} câu).");
            }
        }

        return Result.Success();
    }
}
