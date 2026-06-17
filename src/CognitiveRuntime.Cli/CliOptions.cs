using Microsoft.Extensions.Configuration;

namespace CognitiveRuntime.Cli;

internal sealed record CliOptions(
    string Mode,
    string InputPath,
    string ModelProvider,
    string ModesRoot,
    string OutputRoot,
    bool WriteHtmlView,
    bool ShowHelp,
    string? Lens = null,
    string Pattern = "critic-revision",
    IReadOnlyList<string>? PipelineStages = null,
    string? Experiment = null,
    string ExperimentsRoot = "",
    string? Brief = null,
    string? FlareGameRoot = null,
    IReadOnlyList<string>? ScatterModes = null)
{
    public const string DungeonBuilderBrief =
        "Build a compact crypt with an entrance, three to six rooms, one " +
        "optional branch, one locked or guarded objective room, and a " +
        "reachable exit.";

    private static readonly HashSet<string> KnownPatterns = new(
        ["single-pass", "critic-revision", "linear-pipeline", "scatter-gather"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> KnownExperiments = new(
        ["dungeon-builder"],
        StringComparer.OrdinalIgnoreCase);

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
                WriteHtmlView: false,
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

            if (string.Equals(argument, "--html", StringComparison.OrdinalIgnoreCase))
            {
                values[argument] = bool.TrueString;
                continue;
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

        if (values.TryGetValue("--experiment", out var experiment))
        {
            if (!KnownExperiments.Contains(experiment))
            {
                var available = string.Join(", ", KnownExperiments.Order());
                throw new CliUsageException(
                    $"Unknown experiment '{experiment}'. Available experiments: {available}.");
            }

            var experimentProvider = GetValue(values, "--run-mode", "--model-provider")
                ?? configuration["MODEL_PROVIDER"]
                ?? "mock";
            var experimentsRoot = Path.GetFullPath(
                values.GetValueOrDefault("--experiments-root")
                ?? Path.Combine(currentDirectory, "experiments"));
            var experimentOutputRoot = Path.GetFullPath(
                values.GetValueOrDefault("--output-root")
                ?? Path.Combine(currentDirectory, "outputs"));
            var brief = values.GetValueOrDefault("--brief");
            var flareGameRoot = values.GetValueOrDefault("--flare-game-root");

            return new CliOptions(
                Mode: string.Empty,
                InputPath: string.Empty,
                ModelProvider: experimentProvider,
                ModesRoot: Path.GetFullPath(
                    values.GetValueOrDefault("--modes-root")
                    ?? Path.Combine(currentDirectory, "modes")),
                OutputRoot: experimentOutputRoot,
                WriteHtmlView: false,
                ShowHelp: false,
                Experiment: experiment,
                ExperimentsRoot: experimentsRoot,
                Brief: string.IsNullOrWhiteSpace(brief) ? null : brief,
                FlareGameRoot: string.IsNullOrWhiteSpace(flareGameRoot)
                    ? null
                    : Path.GetFullPath(flareGameRoot));
        }

        var pattern = values.GetValueOrDefault("--pattern") ?? "critic-revision";
        if (!KnownPatterns.Contains(pattern))
        {
            var available = string.Join(", ", KnownPatterns.Order());
            throw new CliUsageException(
                $"Unknown pattern '{pattern}'. Available patterns: {available}.");
        }

        var pipelineStages = ParseModeList(values, "--pipeline");
        var isLinearPipeline = string.Equals(
            pattern, "linear-pipeline", StringComparison.OrdinalIgnoreCase);

        if (isLinearPipeline && pipelineStages is null)
        {
            throw new CliUsageException(
                "'--pattern linear-pipeline' requires '--pipeline <mode,mode,...>'.");
        }

        if (!isLinearPipeline && pipelineStages is not null)
        {
            throw new CliUsageException(
                "'--pipeline' requires '--pattern linear-pipeline'.");
        }

        var scatterModes = ParseModeList(values, "--scatter");
        var isScatterGather = string.Equals(
            pattern, "scatter-gather", StringComparison.OrdinalIgnoreCase);

        if (isScatterGather && scatterModes is null)
        {
            throw new CliUsageException(
                "'--pattern scatter-gather' requires '--scatter <mode,mode,...>'.");
        }

        if (!isScatterGather && scatterModes is not null)
        {
            throw new CliUsageException(
                "'--scatter' requires '--pattern scatter-gather'.");
        }

        var mode = isLinearPipeline
            ? values.GetValueOrDefault("--mode") ?? "pipeline"
            : Require(values, "--mode");
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
        var lens = values.GetValueOrDefault("--lens");

        return new CliOptions(
            mode,
            inputPath,
            provider,
            modesRoot,
            outputRoot,
            values.ContainsKey("--html"),
            ShowHelp: false,
            Lens: string.IsNullOrWhiteSpace(lens) ? null : lens,
            Pattern: pattern,
            PipelineStages: pipelineStages,
            ScatterModes: scatterModes);
    }

    private static IReadOnlyList<string>? ParseModeList(
        IReadOnlyDictionary<string, string> values,
        string option)
    {
        if (!values.TryGetValue(option, out var rawValue))
        {
            return null;
        }

        var modes = rawValue
            .Split(',', StringSplitOptions.TrimEntries)
            .ToArray();

        if (modes.Any(string.IsNullOrEmpty))
        {
            throw new CliUsageException(
                $"'{option}' must be a comma-separated list of mode names " +
                "with no empty entries.");
        }

        return modes;
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
                "--output-root",
                "--html",
                "--lens",
                "--pattern",
                "--pipeline",
                "--scatter",
                "--experiment",
                "--experiments-root",
                "--brief",
                "--flare-game-root"
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
