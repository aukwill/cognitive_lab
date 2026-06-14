using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Cli;

public sealed record CliOptions(
    string InputDirectory,
    string? DiscoveryQuery,
    string DiscoveryProvider,
    string[] IncludeDomains,
    int MaxSources,
    int MaxSourceCharacters,
    int MaxSourcesPerDomain,
    string OutputRoot,
    string PromptsRoot,
    string Provider,
    int MaxInputCharacters,
    int ChunkSizeCharacters,
    int ChunkOverlapCharacters,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        var projectRoot = FindProjectRoot();
        string? inputDirectory = null;
        string? discoveryQuery = null;
        var discoveryProvider = "firecrawl";
        var includeDomains = new List<string>();
        var maxSources = 8;
        var maxSourceCharacters = 30_000;
        var maxSourcesPerDomain = 2;
        var outputRoot = Path.Combine(projectRoot, "outputs");
        var promptsRoot = Path.Combine(projectRoot, "prompts");
        var provider = "mock";
        var maxInputCharacters = 120_000;
        var chunkSizeCharacters = 3_000;
        var chunkOverlapCharacters = 300;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--input":
                    inputDirectory = ReadValue(args, ref index, argument);
                    break;
                case "--discover":
                    discoveryQuery = ReadValue(args, ref index, argument);
                    break;
                case "--discovery-provider":
                    discoveryProvider = ReadValue(args, ref index, argument);
                    break;
                case "--include-domain":
                    includeDomains.Add(ReadValue(args, ref index, argument));
                    break;
                case "--max-sources":
                    maxSources = ReadPositiveInt(args, ref index, argument);
                    break;
                case "--max-source-chars":
                    maxSourceCharacters = ReadPositiveInt(args, ref index, argument);
                    break;
                case "--max-sources-per-domain":
                    maxSourcesPerDomain = ReadPositiveInt(
                        args,
                        ref index,
                        argument);
                    break;
                case "--output-root":
                    outputRoot = ReadValue(args, ref index, argument);
                    break;
                case "--prompts-root":
                    promptsRoot = ReadValue(args, ref index, argument);
                    break;
                case "--provider":
                    provider = ReadValue(args, ref index, argument);
                    break;
                case "--max-input-chars":
                    maxInputCharacters = ReadPositiveInt(args, ref index, argument);
                    break;
                case "--chunk-size":
                    chunkSizeCharacters = ReadPositiveInt(args, ref index, argument);
                    break;
                case "--chunk-overlap":
                    chunkOverlapCharacters = ReadNonNegativeInt(
                        args,
                        ref index,
                        argument);
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new CliUsageException($"Unknown argument '{argument}'.");
            }
        }

        if (!showHelp &&
            string.IsNullOrWhiteSpace(inputDirectory) &&
            string.IsNullOrWhiteSpace(discoveryQuery))
        {
            throw new CliUsageException("--input or --discover is required.");
        }

        if (!showHelp &&
            !string.IsNullOrWhiteSpace(inputDirectory) &&
            !string.IsNullOrWhiteSpace(discoveryQuery))
        {
            throw new CliUsageException(
                "--input and --discover cannot be used together.");
        }

        return new CliOptions(
            inputDirectory ?? string.Empty,
            discoveryQuery,
            discoveryProvider,
            includeDomains.ToArray(),
            maxSources,
            maxSourceCharacters,
            maxSourcesPerDomain,
            outputRoot,
            promptsRoot,
            provider,
            maxInputCharacters,
            chunkSizeCharacters,
            chunkOverlapCharacters,
            showHelp);
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            var nested = Path.Combine(
                current.FullName,
                "projects",
                "document-distiller",
                "prompts");
            if (Directory.Exists(nested))
            {
                return Path.GetDirectoryName(nested)
                    ?? throw new InvalidOperationException(
                        "Could not resolve the document-distiller project root.");
            }

            if (Directory.Exists(Path.Combine(current.FullName, "prompts")) &&
                File.Exists(Path.Combine(current.FullName, "DocumentDistiller.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Environment.CurrentDirectory;
    }

    private static string ReadValue(
        string[] args,
        ref int index,
        string argument)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"{argument} requires a value.");
        }

        index++;
        return args[index];
    }

    private static int ReadPositiveInt(
        string[] args,
        ref int index,
        string argument)
    {
        var value = ReadValue(args, ref index, argument);
        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            throw new CliUsageException(
                $"{argument} requires a positive integer.");
        }

        return parsed;
    }

    private static int ReadNonNegativeInt(
        string[] args,
        ref int index,
        string argument)
    {
        var value = ReadValue(args, ref index, argument);
        if (!int.TryParse(value, out var parsed) || parsed < 0)
        {
            throw new CliUsageException(
                $"{argument} requires a non-negative integer.");
        }

        return parsed;
    }
}

public sealed class CliUsageException : Exception
{
    public CliUsageException(string message)
        : base(message)
    {
    }
}
