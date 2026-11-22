namespace OralExamination.API.Configuration;

public enum CliMode
{
    None = 0,
    ImportCourseBanks = 1,
    ProvisionTestAccounts = 2
}

public sealed record CliCommandOptions(
    CliMode Mode,
    string? ConfigPath,
    string[] RemainingArgs)
{
    private static readonly HashSet<string> AllowedHostFlagsInCliMode = new(StringComparer.OrdinalIgnoreCase)
    {
        "--environment",
        "--contentroot",
        "--applicationname",
        "--urls"
    };

    public static bool TryParse(
        string[] args,
        out CliCommandOptions options,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(args);

        var mode = CliMode.None;
        string? configPath = null;
        var remaining = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--import-course-banks", StringComparison.OrdinalIgnoreCase))
            {
                if (mode != CliMode.None && mode != CliMode.ImportCourseBanks)
                {
                    options = new CliCommandOptions(CliMode.None, null, args);
                    errorMessage = "Cannot combine --import-course-banks and --provision-test-accounts in one invocation.";
                    return false;
                }
                mode = CliMode.ImportCourseBanks;
                continue;
            }

            if (string.Equals(arg, "--provision-test-accounts", StringComparison.OrdinalIgnoreCase))
            {
                if (mode != CliMode.None && mode != CliMode.ProvisionTestAccounts)
                {
                    options = new CliCommandOptions(CliMode.None, null, args);
                    errorMessage = "Cannot combine --import-course-banks and --provision-test-accounts in one invocation.";
                    return false;
                }
                mode = CliMode.ProvisionTestAccounts;
                continue;
            }

            if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || (args[i + 1].StartsWith("--", StringComparison.Ordinal) && args[i + 1] != "-"))
                {
                    options = new CliCommandOptions(CliMode.None, null, args);
                    errorMessage = "Option --config requires a file path or '-' for stdin.";
                    return false;
                }

                configPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg["--config=".Length..];
                if (string.IsNullOrWhiteSpace(value))
                {
                    options = new CliCommandOptions(CliMode.None, null, args);
                    errorMessage = "Option --config= requires a non-empty file path or '-'.";
                    return false;
                }

                configPath = value;
                continue;
            }

            remaining.Add(arg);
        }

        if (configPath is not null && mode != CliMode.ProvisionTestAccounts)
        {
            options = new CliCommandOptions(CliMode.None, null, args);
            errorMessage = "Option --config is valid only with --provision-test-accounts.";
            return false;
        }

        if (mode != CliMode.None)
        {
            foreach (var token in remaining)
            {
                if (!token.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                var keyPart = token.Split('=', 2)[0];
                if (!AllowedHostFlagsInCliMode.Contains(keyPart))
                {
                    options = new CliCommandOptions(CliMode.None, null, args);
                    errorMessage = $"Unknown CLI option '{token}' for {mode}.";
                    return false;
                }
            }
        }

        options = new CliCommandOptions(mode, configPath, remaining.ToArray());
        errorMessage = null;
        return true;
    }
}
