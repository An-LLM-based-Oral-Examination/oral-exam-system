using Microsoft.Extensions.Configuration;

namespace OralExamination.Infrastructure.Services;

/// <summary>
/// Rotates the primary Gemini keys while keeping the explicitly configured fallback last.
/// </summary>
internal static class GeminiApiKeyPriority
{
    private static int _roundRobinCounter;

    public static IReadOnlyList<string> GetCandidates(IConfiguration configuration)
    {
        var primary = FirstNonWhiteSpace(
            configuration["Gemini:ApiKey"],
            configuration["Gemini__ApiKey"],
            Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
        var pool = FirstNonWhiteSpace(
            configuration["Gemini:ApiKeys"],
            configuration["Gemini__ApiKeys"],
            Environment.GetEnvironmentVariable("GEMINI_API_KEYS"))
            ?? string.Empty;
        var fallback = FirstNonWhiteSpace(
            configuration["Gemini:FallbackApiKey"],
            configuration["Gemini__FallbackApiKey"],
            Environment.GetEnvironmentVariable("GEMINI_FALLBACK_API_KEY"));

        var primaryKeys = new[] { primary }
            .Concat(pool.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(IsUsable)
            .Select(key => key!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var result = new List<string>(primaryKeys.Length + 1);
        if (primaryKeys.Length > 0)
        {
            var start = (int)(unchecked((uint)Interlocked.Increment(ref _roundRobinCounter)) % (uint)primaryKeys.Length);
            for (var index = 0; index < primaryKeys.Length; index++)
            {
                result.Add(primaryKeys[(start + index) % primaryKeys.Length]);
            }
        }

        if (IsUsable(fallback) && !result.Contains(fallback!.Trim(), StringComparer.Ordinal))
        {
            result.Add(fallback.Trim());
        }

        return result;
    }

    private static string? FirstNonWhiteSpace(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    internal static bool IsUsable(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var trimmed = key.Trim();
        if (trimmed.Length < 8)
        {
            return false;
        }

        if ((trimmed.StartsWith('<') && trimmed.EndsWith('>')) ||
            (trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith('}')))
        {
            return false;
        }

        return !trimmed.Contains("placeholder", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Contains("YourActual", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Contains("YOUR_GEMINI", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Contains("YOUR_API_KEY", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Contains("changeme", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Contains("replace_me", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Contains("replace-me", StringComparison.OrdinalIgnoreCase);
    }
}
