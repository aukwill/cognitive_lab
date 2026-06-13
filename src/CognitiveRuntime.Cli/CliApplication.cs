using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Runtime.Orchestration;
using CognitiveRuntime.Core.Tools;
using CognitiveRuntime.Core.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CognitiveRuntime.Cli;

internal sealed class CliApplication
{
    private readonly Orchestrator _orchestrator;
    private readonly ILogger<CliApplication> _logger;

    public CliApplication(
        Orchestrator orchestrator,
        ILogger<CliApplication> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        CliOptions options;
        try
        {
            options = CliOptions.Parse(args, configuration);
        }
        catch (CliUsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine();
            WriteHelp(Console.Error);
            return ExitCodes.UsageError;
        }

        if (options.ShowHelp)
        {
            WriteHelp(Console.Out);
            return ExitCodes.Success;
        }

        if (!File.Exists(options.InputPath))
        {
            Console.Error.WriteLine(
                $"Input file '{options.InputPath}' does not exist.");
            return ExitCodes.UsageError;
        }

        var services = BuildServices(options, configuration);
        await using var serviceProvider = services.BuildServiceProvider();
        var application = serviceProvider.GetRequiredService<CliApplication>();

        try
        {
            var input = await File.ReadAllTextAsync(
                options.InputPath,
                cancellationToken);
            return await application.ExecuteAsync(
                options,
                input,
                cancellationToken);
        }
        catch (RuntimeRunException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(
                $"Partial artifacts: {exception.OutputDirectory}");
            return ExitCodes.FromOutcome(RunOutcome.RuntimeFailed);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Run cancelled.");
            return ExitCodes.FromOutcome(RunOutcome.Cancelled);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Fatal error: {exception.Message}");
            return ExitCodes.FromOutcome(RunOutcome.RuntimeFailed);
        }
    }

    private async Task<int> ExecuteAsync(
        CliOptions options,
        string input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Running mode {Mode} with provider {Provider}.",
            options.Mode,
            options.ModelProvider);

        var result = await _orchestrator.RunAsync(
            new RunRequest(
                options.Mode,
                input,
                options.ModelProvider,
                options.OutputRoot,
                options.WriteHtmlView,
                options.InputPath,
                options.Lens,
                options.Pattern,
                options.PipelineStages),
            cancellationToken);

        Console.WriteLine($"Run ID: {result.RunId}");
        Console.WriteLine($"Output: {result.OutputDirectory}");
        Console.WriteLine(
            $"Evaluation: {(result.Outcome == RunOutcome.Success ? "PASS" : "FAIL")}");
        if (result.HtmlViewPath is not null)
        {
            Console.WriteLine($"HTML: {result.HtmlViewPath}");
        }

        return ExitCodes.FromOutcome(result.Outcome);
    }

    private static ServiceCollection BuildServices(
        CliOptions options,
        IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddLogging(
            builder => builder
                .SetMinimumLevel(LogLevel.Information)
                .AddSimpleConsole(console =>
                {
                    console.SingleLine = true;
                    console.TimestampFormat = "HH:mm:ss ";
                }));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<HttpClient>(
            _ => new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2)
            });

        services.AddSingleton(
            new GitHubModelsOptions(
                configuration["GITHUB_MODELS_ENDPOINT"]
                    ?? "https://models.github.ai/inference",
                configuration["GITHUB_MODELS_TOKEN"]
                    ?? configuration["GITHUB_TOKEN"],
                configuration["GITHUB_MODELS_MODEL"]
                    ?? "openai/gpt-4.1",
                configuration["GITHUB_MODELS_API_VERSION"]
                    ?? "2026-03-10"));
        services.AddSingleton(
            new AzureFoundryOptions(
                configuration["AZURE_FOUNDRY_ENDPOINT"],
                configuration["AZURE_FOUNDRY_API_KEY"],
                configuration["AZURE_FOUNDRY_DEPLOYMENT"],
                configuration["AZURE_FOUNDRY_API_VERSION"]));

        services.AddSingleton<IModeLoader>(
            _ => new FileModeLoader(options.ModesRoot));
        services.AddSingleton<IArtifactWriter, ArtifactWriter>();
        services.AddSingleton<IRunViewWriter, HtmlRunViewWriter>();
        services.AddSingleton<ITraceSessionFactory, JsonTraceSessionFactory>();
        services.AddSingleton<OutputContractValidator>();
        services.AddSingleton<LoopEfficacyEvaluator>();
        services.AddSingleton<IEvalRunner, EvalRunner>();
        services.AddSingleton<PhaseRunner>();
        services.AddSingleton<IOrchestrationPattern, CriticRevisionPattern>();
        services.AddSingleton<IOrchestrationPattern, SinglePassPattern>();
        services.AddSingleton<IOrchestrationPatternFactory, OrchestrationPatternFactory>();

        services.AddSingleton<IModelClient, MockModelClient>();
        services.AddSingleton<IModelClient>(
            provider => new GitHubModelsClient(
                provider.GetRequiredService<HttpClient>(),
                provider.GetRequiredService<GitHubModelsOptions>()));
        services.AddSingleton<IModelClient>(
            provider => new AzureFoundryModelClient(
                provider.GetRequiredService<AzureFoundryOptions>()));
        services.AddSingleton<IModelClientFactory, ModelClientFactory>();

        services.AddSingleton(new ToolPolicy(Array.Empty<string>()));
        services.AddSingleton<IToolExecutor, ToolExecutor>();
        services.AddSingleton<IToolProvider, MockToolProvider>();
        services.AddSingleton<IToolProvider, McpToolProvider>();

        services.AddSingleton<Orchestrator>();
        services.AddSingleton<CliApplication>();

        return services;
    }

    private static void WriteHelp(TextWriter writer)
    {
        writer.WriteLine("Cognitive Runtime Lab");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine(
            "  dotnet run --project src/CognitiveRuntime.Cli -- " +
            "--mode <name> --input <path> [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --mode <name>             Mode directory name.");
        writer.WriteLine("  --input <path>            Input text file.");
        writer.WriteLine(
            "  --run-mode <provider>     mock, github-models, or azure-foundry.");
        writer.WriteLine("  --modes-root <path>       Defaults to ./modes.");
        writer.WriteLine("  --output-root <path>      Defaults to ./outputs.");
        writer.WriteLine("  --html                    Write read-only index.html.");
        writer.WriteLine(
            "  --lens <name>             Prompt lens subdirectory " +
            "(e.g. warcraft for the lens mode).");
        writer.WriteLine(
            "  --pattern <name>          Orchestration pattern: single-pass, " +
            "critic-revision (default), or linear-pipeline.");
        writer.WriteLine(
            "  --pipeline <mode,mode,..> Stage modes for --pattern linear-pipeline.");
        writer.WriteLine("  --help                    Show this help.");
    }
}
