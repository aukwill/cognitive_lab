namespace DocumentDistiller.Cli;

/// <summary>
/// Loads KEY=VALUE pairs from a ".env" file into the process environment.
/// Existing environment variables always take precedence.
/// </summary>
public static class EnvFile
{
    private const int MaxDirectoryLevels = 6;

    public static void Load()
    {
        var path = FindEnvFile(Directory.GetCurrentDirectory());
        if (path is null)
        {
            return;
        }

        foreach (var (key, value) in Parse(File.ReadAllLines(path)))
        {
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    public static string? FindEnvFile(string startDirectory)
    {
        var directory = startDirectory;
        for (var level = 0; level < MaxDirectoryLevels && directory is not null; level++)
        {
            var candidate = Path.Combine(directory, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 && IsQuoted(value))
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        return values;
    }

    private static bool IsQuoted(string value) =>
        (value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'');
}
