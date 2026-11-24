using System;
using System.Collections.Generic;
using System.Text;

namespace OralExamination.Application.Features.OfficialExams.Common;

/// <summary>
/// Bộ tính toán độ tương đồng văn bản chuẩn trigram tương thích ngữ nghĩa PostgreSQL pg_trgm (similarity).
/// Hoạt động nhất quán 100% giữa môi trường kiểm thử InMemory và PostgreSQL thực tế.
/// </summary>
public static class TrigramSimilarityCalculator
{
    public const decimal YellowSimilarityThreshold = 0.70m;
    public const decimal RedSimilarityThreshold = 0.80m;

    public const string BandClear = "CLEAR";
    public const string BandYellow70To79 = "YELLOW_70_79";
    public const string BandRed80Plus = "RED_80_PLUS";

    public static string ComposeQuestionText(string? title, string? content)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        var trimmedContent = content?.Trim() ?? string.Empty;

        if (trimmedTitle.Length == 0)
        {
            return trimmedContent;
        }

        if (trimmedContent.Length == 0)
        {
            return trimmedTitle;
        }

        return $"{trimmedTitle} {trimmedContent}";
    }

    /// <summary>
    /// Trích xuất tập 3-gram theo chuẩn PostgreSQL pg_trgm:
    /// chuẩn hóa NFC + chữ thường, tách từ chữ/số, đệm 2 dấu cách đầu và 1 dấu cách cuối cho mỗi từ.
    /// </summary>
    public static HashSet<string> ExtractTrigrams(string? input)
    {
        var trigrams = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(input))
        {
            return trigrams;
        }

        var normalized = input.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var tokenBuilder = new StringBuilder();

        for (var i = 0; i < normalized.Length; i++)
        {
            var ch = normalized[i];
            if (char.IsLetterOrDigit(ch))
            {
                tokenBuilder.Append(ch);
            }
            else if (tokenBuilder.Length > 0)
            {
                AddWordTrigrams(tokenBuilder.ToString(), trigrams);
                tokenBuilder.Clear();
            }
        }

        if (tokenBuilder.Length > 0)
        {
            AddWordTrigrams(tokenBuilder.ToString(), trigrams);
        }

        return trigrams;
    }

    public static decimal CalculateSimilarity(string? left, string? right)
    {
        var leftSet = ExtractTrigrams(left);
        var rightSet = ExtractTrigrams(right);
        return CalculateSimilarity(leftSet, rightSet);
    }

    public static decimal CalculateSimilarity(IReadOnlySet<string> leftTrigrams, IReadOnlySet<string> rightTrigrams)
    {
        if (leftTrigrams.Count == 0 || rightTrigrams.Count == 0)
        {
            return 0m;
        }

        var smaller = leftTrigrams.Count <= rightTrigrams.Count ? leftTrigrams : rightTrigrams;
        var larger = ReferenceEquals(smaller, leftTrigrams) ? rightTrigrams : leftTrigrams;

        var intersection = 0;
        foreach (var gram in smaller)
        {
            if (larger.Contains(gram))
            {
                intersection++;
            }
        }

        var union = leftTrigrams.Count + rightTrigrams.Count - intersection;
        if (union <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)intersection / union, 4, MidpointRounding.AwayFromZero);
    }

    public static decimal ToSimilarityPercent(decimal similarity)
    {
        var roundedScore = Math.Round(similarity, 4, MidpointRounding.AwayFromZero);
        return Math.Round(roundedScore * 100m, 1, MidpointRounding.AwayFromZero);
    }

    public static string ClassifyBand(decimal similarity)
    {
        var similarityPercent = ToSimilarityPercent(similarity);
        return similarityPercent >= 80.0m
            ? BandRed80Plus
            : similarityPercent >= 70.0m
                ? BandYellow70To79
                : BandClear;
    }

    private static void AddWordTrigrams(string word, HashSet<string> target)
    {
        var padded = $"  {word} ";
        for (var i = 0; i <= padded.Length - 3; i++)
        {
            target.Add(padded.Substring(i, 3));
        }
    }
}
