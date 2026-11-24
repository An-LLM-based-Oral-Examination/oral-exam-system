using System;

namespace OralExamination.Application.Features.QuestionBank;

/// <summary>
/// Chuẩn hóa và kiểm định biên số học cho tiêu chí Barem Rubric (numeric(4,2) &amp; ck_rubric_criteria_weight).
/// </summary>
internal static class RubricNormalization
{
    public const int MaxNameLength = 200;

    public static bool HasValidScorePrecision(decimal maxScore) =>
        decimal.Round(maxScore, 2) == maxScore;

    /// <summary>
    /// Chuẩn hóa trọng số Weight về khoảng [0.01, 1.00] (làm tròn 2 chữ số thập phân khớp cột DB numeric(3,2) trước khi clamp)
    /// để luôn thỏa mãn constraint PostgreSQL ck_rubric_criteria_weight (weight &gt; 0 AND weight &lt;= 1.00).
    /// </summary>
    public static decimal NormalizeWeight(decimal rawWeightInput, decimal maxScore)
    {
        var rawWeight = rawWeightInput > 1.00m
            ? rawWeightInput / 100m
            : rawWeightInput;

        if (rawWeight <= 0m || rawWeight > 1.00m)
        {
            rawWeight = maxScore / 10m;
        }

        return Math.Clamp(Math.Round(rawWeight, 2, MidpointRounding.AwayFromZero), 0.01m, 1.00m);
    }
}
