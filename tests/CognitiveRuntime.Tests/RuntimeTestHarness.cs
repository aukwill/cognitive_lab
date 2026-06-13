using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Runtime.Orchestration;
using CognitiveRuntime.Core.Tracing;

namespace CognitiveRuntime.Tests;

internal static class RuntimeTestHarness
{
    public static Orchestrator CreateOrchestrator(
        TestWorkspace workspace,
        TimeProvider timeProvider,
        IModelClient? modelClient = null,
        IEvalRunner? evalRunner = null,
        ITraceSessionFactory? traceSessionFactory = null,
        IRunViewWriter? runViewWriter = null,
        IOrchestrationPatternFactory? patternFactory = null)
    {
        IModelClient[] modelClients = [modelClient ?? new MockModelClient()];

        return new Orchestrator(
            new FileModeLoader(workspace.ModesRoot),
            new ModelClientFactory(modelClients),
            new PhaseRunner(),
            new ArtifactWriter(timeProvider),
            traceSessionFactory ?? new JsonTraceSessionFactory(timeProvider),
            evalRunner ?? new EvalRunner(new OutputContractValidator()),
            runViewWriter ?? new HtmlRunViewWriter(),
            patternFactory ?? new OrchestrationPatternFactory(
                [new CriticRevisionPattern(), new SinglePassPattern()]));
    }

    public static void AssertRequiredArtifactsExist(string outputDirectory)
    {
        Assert.All(
            new[]
            {
                "input.md",
                "result.md",
                "trace.json",
                "run_summary.md",
                "eval_report.md"
            },
            file => Assert.True(
                File.Exists(Path.Combine(outputDirectory, file)),
                $"Missing failure artifact: {file}"));
    }

    public static async Task AssertTerminalFailureAsync(string outputDirectory)
    {
        var eventTypes = await ReadTraceEventTypesAsync(outputDirectory);
        var terminalEvents = eventTypes
            .Where(eventType => eventType is "run.finalized" or "run.failed")
            .ToArray();

        Assert.Equal(["run.failed"], terminalEvents);
        Assert.Equal("run.failed", eventTypes[^1]);
    }

    public static async Task<string[]> ReadTraceEventTypesAsync(
        string outputDirectory)
    {
        await using var traceStream = File.OpenRead(
            Path.Combine(outputDirectory, "trace.json"));
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        return traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .Select(traceEvent =>
                traceEvent.GetProperty("type").GetString() ?? string.Empty)
            .ToArray();
    }
}
