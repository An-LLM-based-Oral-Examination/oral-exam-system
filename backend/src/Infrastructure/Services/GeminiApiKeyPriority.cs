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
        var primary = configuration["Gemini:ApiKey"]
            ?? configuration["Gemini__ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var pool = configuration["Gemini:ApiKeys"]
            ?? configuration["Gemini__ApiKeys"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEYS")
            ?? string.Empty;
        var fallback = configuration["Gemini:FallbackApiKey"]
            ?? configuration["Gemini__FallbackApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_FALLBACK_API_KEY");

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

    private static bool IsUsable(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        !key.Contains("Placeholder", StringComparison.OrdinalIgnoreCase) &&
        !key.Contains("YourActual", StringComparison.OrdinalIgnoreCase);
}
