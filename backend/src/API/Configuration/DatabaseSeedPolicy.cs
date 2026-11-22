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

    public static bool IsAcademicCourseBankImportEnabled(IConfiguration configuration)
    {
        return ReadOptionalFlagWithDefault(
            configuration,
            defaultValue: true,
            "Database:EnableAcademicCourseBankImport",
            "DATABASE_ENABLE_ACADEMIC_IMPORT");
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

    private static bool ReadOptionalFlagWithDefault(IConfiguration configuration, bool defaultValue, params string[] keys)
    {
        bool? resolved = null;
        foreach (var key in keys)
        {
            var raw = configuration[key];
            if (raw is null) continue;
            if (!bool.TryParse(raw, out var value))
                throw new InvalidOperationException($"{key} must be true or false; refusing unsafe academic import configuration.");
            if (resolved.HasValue && resolved.Value != value)
                throw new InvalidOperationException("Conflicting academic course bank import configuration flags; refusing unsafe startup.");
            resolved = value;
        }
        return resolved ?? defaultValue;
    }
}
