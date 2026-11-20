namespace OralExamination.API.Configuration;

public static class DatabaseSeedPolicy
{
    public static bool IsDevelopmentSeedEnabled(IConfiguration configuration, string environmentName)
    {
        var enabled = ReadFlag(configuration, "Database:EnableDevelopmentSeed", "DATABASE_ENABLE_DEVELOPMENT_SEED");
        var reviewEnabled = ReadFlag(configuration, "Database:EnableTask04ReviewSeed", "DATABASE_ENABLE_TASK04_REVIEW_SEED");
        if ((enabled || reviewEnabled) && !string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Demo/review seed is permitted only in Development. Disable development seed before startup.");
        return enabled;
    }

    private static bool ReadFlag(IConfiguration configuration, params string[] keys)
    {
        var enabled = false;
        foreach (var key in keys)
        {
            var raw = configuration[key];
            if (raw is null) continue;
            if (!bool.TryParse(raw, out var value))
                throw new InvalidOperationException($"{key} must be true or false; refusing unsafe seed configuration.");
            enabled |= value;
        }
        return enabled;
    }
}
