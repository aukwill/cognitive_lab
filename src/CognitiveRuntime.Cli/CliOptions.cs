using Microsoft.Extensions.Configuration;

namespace CognitiveRuntime.Cli;

internal sealed record CliOptions(
    string Mode,
    string InputPath,
    string ModelProvider,
    string ModesRoot,
    string OutputRoot,
    bool ShowHelp)
{
    public static CliOptions Parse(
        IReadOnlyList<string> args,
        IConfiguration configuration)
    {
        if (args.Count == 0 || args.Any(IsHelpFlag))
        {
            return new CliOptions(
                string.Empty,
                string.Empty,
                "mock",
                string.Empty,
                string.Empty,
                ShowHelp: true);
        }

        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new CliUsageException(
                    $"Unexpected positional argument '{argument}'.");
            }

            if (index + 1 >= args.Count ||
                args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new CliUsageException(
                    $"Option '{argument}' requires a value.");
            }

            values[argument] = args[++index];
        }

        EnsureKnownOptions(values.Keys);

        var currentDirectory = Environment.CurrentDirectory;
        var mode = Require(values, "--mode");
        var inputPath = Path.GetFullPath(Require(values, "--input"));
        var provider = GetValue(values, "--run-mode", "--model-provider")
            ?? configuration["MODEL_PROVIDER"]
            ?? "mock";
        var modesRoot = Path.GetFullPath(
            values.GetValueOrDefault("--modes-root")
            ?? Path.Combine(currentDirectory, "modes"));
        var outputRoot = Path.GetFullPath(
            values.GetValueOrDefault("--output-root")
            ?? Path.Combine(currentDirectory, "outputs"));

        return new CliOptions(
            mode,
            inputPath,
            provider,
            modesRoot,
            outputRoot,
            ShowHelp: false);
    }

    private static bool IsHelpFlag(string value) =>
        string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase);

    private static void EnsureKnownOptions(IEnumerable<string> options)
    {
        var known = new HashSet<string>(
            [
                "--mode",
                "--input",
                "--run-mode",
                "--model-provider",
                "--modes-root",
                "--output-root"
            ],
            StringComparer.OrdinalIgnoreCase);

        var unknown = options.FirstOrDefault(option => !known.Contains(option));
        if (unknown is not null)
        {
            throw new CliUsageException($"Unknown option '{unknown}'.");
        }
    }

    private static string Require(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        if (!values.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            throw new CliUsageException($"Required option '{key}' is missing.");
        }

        return value;
    }

    private static string? GetValue(
        IReadOnlyDictionary<string, string> values,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}

internal sealed class CliUsageException : Exception
{
    public CliUsageException(string message)
        : base(message)
    {
    }
}
