using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Artifacts;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Discovery;
using DocumentDistiller.Core.Evaluation;
using DocumentDistiller.Core.Ingestion;
using DocumentDistiller.Core.Models;
using DocumentDistiller.Core.Runtime;
using DocumentDistiller.Core.Safety;
using DocumentDistiller.Core.Tracing;

namespace DocumentDistiller.Cli;

public static class CliApplication
{
    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        EnvFile.Load();

        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (CliUsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine();
            WriteHelp(Console.Error);
            return 2;
        }

        if (options.ShowHelp)
        {
            WriteHelp(Console.Out);
            return 0;
        }

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        var timeProvider = TimeProvider.System;
        var inputDirectory = options.InputDirectory;
        if (!string.IsNullOrWhiteSpace(options.DiscoveryQuery))
        {
            if (!string.Equals(
                    options.DiscoveryProvider,
                    "firecrawl",
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    $"Unknown discovery provider '{options.DiscoveryProvider}'.");
                return 2;
            }

            try
            {
                var discoveryProvider = new FirecrawlCorpusDiscoveryProvider(
                    httpClient,
                    new FirecrawlOptions(
                        Environment.GetEnvironmentVariable("FIRECRAWL_ENDPOINT")
                            ?? "https://api.firecrawl.dev",
                        Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY")));
                var funnel = new CorpusFunnel(discoveryProvider, timeProvider);
                var discovery = await funnel.BuildAsync(
                    new CorpusFunnelRequest(
                        options.DiscoveryQuery,
                        options.OutputRoot,
                        options.IncludeDomains,
                        options.MaxSources,
                        options.MaxSourceCharacters,
                        options.MaxSourcesPerDomain),
                    cancellationToken);
                inputDirectory = discovery.CorpusDirectory;
                Console.WriteLine($"Discovery: {discovery.DiscoveryDirectory}");
                Console.WriteLine($"Discovery manifest: {discovery.ManifestPath}");
                Console.WriteLine($"Discovered sources: {discovery.SourceCount}");
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Corpus discovery failed: {exception.Message}");
                return 4;
            }
        }

        var clients = new IDistillationModelClient[]
        {
            new MockDistillationModelClient(),
            new OpenAiResponsesDistillationClient(
                httpClient,
                new ResponsesApiOptions(
                    "openai",
                    "OPENAI_API_KEY",
                    Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")
                        ?? "https://api.openai.com/v1",
                    Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
                    Environment.GetEnvironmentVariable("OPENAI_MODEL")
                        ?? "gpt-5.5",
                    Environment.GetEnvironmentVariable("OPENAI_REASONING_EFFORT")
                        ?? "medium")),
            new OpenAiResponsesDistillationClient(
                httpClient,
                new ResponsesApiOptions(
                    "openrouter",
                    "OPENROUTER_API_KEY",
                    Environment.GetEnvironmentVariable("OPENROUTER_ENDPOINT")
                        ?? "https://openrouter.ai/api/v1",
                    Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
                    Environment.GetEnvironmentVariable("OPENROUTER_MODEL")
                        ?? "openai/gpt-5",
                    Environment.GetEnvironmentVariable("OPENROUTER_REASONING_EFFORT")
                        ?? "medium"))
        };
        var orchestrator = new DistillationOrchestrator(
            new DocumentIngestor(),
            new FilePromptLoader(),
            new DistillationModelClientFactory(clients),
            new RunArtifactWriter(timeProvider),
            new JsonTraceSessionFactory(timeProvider),
            new DistillationEvaluator(),
            new SourceRiskScanner(),
            new EvidenceMatrixBuilder(),
            new DistillationContractValidator(),
            timeProvider);

        try
        {
            var result = await orchestrator.RunAsync(
                new DistillationRunRequest(
                    inputDirectory,
                    options.OutputRoot,
                    options.PromptsRoot,
                    options.Provider,
                    options.MaxInputCharacters,
                    options.ChunkSizeCharacters,
                    options.ChunkOverlapCharacters),
                cancellationToken);

            Console.WriteLine($"Run ID: {result.RunId}");
            Console.WriteLine($"Output: {result.OutputDirectory}");
            Console.WriteLine($"Report: {result.ReportPath}");
            Console.WriteLine($"Dashboard: {result.HtmlReportPath}");
            Console.WriteLine(
                $"Evaluation: {(result.Outcome == DistillationRunOutcome.Success ? "PASS" : "FAIL")}");

            return result.Outcome == DistillationRunOutcome.Success ? 0 : 3;
        }
        catch (DistillationRunException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine($"Partial artifacts: {exception.OutputDirectory}");
            return 4;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Run cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Fatal error: {exception.Message}");
            return 4;
        }
    }

    private static void WriteHelp(TextWriter writer)
    {
        writer.WriteLine("Document Distiller");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine(
            "  dotnet run --project src/DocumentDistiller.Cli -- " +
            "(--input <directory> | --discover <query>) [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine(
            "  --input <directory>       Corpus containing .md, .markdown, or .txt files.");
        writer.WriteLine(
            "  --discover <query>        Build a web corpus, then distill it.");
        writer.WriteLine(
            "  --discovery-provider <n>  Discovery provider (firecrawl).");
        writer.WriteLine(
            "  --include-domain <domain> Repeatable discovery domain allowlist.");
        writer.WriteLine(
            "  --max-sources <n>         Maximum discovered sources (default 8).");
        writer.WriteLine(
            "  --max-source-chars <n>    Maximum characters per source (default 30000).");
        writer.WriteLine(
            "  --max-sources-per-domain  Per-domain cap (default 2).");
        writer.WriteLine(
            "  --provider <name>         mock (default), openai, or openrouter.");
        writer.WriteLine(
            "  --output-root <path>      Timestamped run artifacts are written here.");
        writer.WriteLine(
            "  --prompts-root <path>     Directory containing analyze/critic/revise prompts.");
        writer.WriteLine(
            "  --max-input-chars <n>     Corpus character safety limit (default 120000).");
        writer.WriteLine(
            "  --chunk-size <n>          Evidence chunk size (default 3000).");
        writer.WriteLine(
            "  --chunk-overlap <n>       Adjacent chunk overlap (default 300).");
        writer.WriteLine("  --help                    Show this help.");
    }
}
