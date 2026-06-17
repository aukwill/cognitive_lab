using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Persistence;
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
        IOrchestrationPatternFactory? patternFactory = null,
        IRunStateStore? runStateStore = null,
        IArtifactStore? artifactStore = null,
        IRunIdGenerator? runIdGenerator = null,
        RunBudget? budget = null,
        int maxBranchConcurrency = 0)
    {
        IModelClient[] modelClients = [modelClient ?? new MockModelClient()];
        runStateStore ??= new NullRunStateStore();
        artifactStore ??= new NullArtifactStore();
        var modeLoader = new FileModeLoader(workspace.ModesRoot);
        var phaseRunner = new PhaseRunner(timeProvider);
        var artifactWriter = new ArtifactWriter(timeProvider, artifactStore);

        return new Orchestrator(
            new ModelClientFactory(modelClients),
            artifactWriter,
            traceSessionFactory ??
                new JsonTraceSessionFactory(timeProvider, artifactStore),
            evalRunner ?? new EvalRunner(new OutputContractValidator()),
            runViewWriter ?? new HtmlRunViewWriter(artifactStore, timeProvider),
            patternFactory ?? new OrchestrationPatternFactory(
                [
                    new CriticRevisionPattern(),
                    new SinglePassPattern(),
                    new LinearPipelinePattern(),
                    new ScatterGatherPattern()
                ]),
            new PatternExecutionPlanValidator(),
            new PatternExecutor(
                modeLoader,
                phaseRunner,
                artifactWriter,
                timeProvider,
                maxBranchConcurrency),
            runStateStore,
            timeProvider,
            runIdGenerator,
            budget);
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
                "eval_report.md",
                "run.json"
            },
            file => Assert.True(
                File.Exists(Path.Combine(outputDirectory, file)),
                $"Missing failure artifact: {file}"));
    }

    public static async Task AssertTerminalFailureAsync(string outputDirectory)
    {
        var traceDocument = await new JsonTraceReader().ReadAsync(
            Path.Combine(outputDirectory, "trace.json"));
        var integrity = new TerminalTraceIntegrityEvaluator().Evaluate(
            traceDocument.Events);
        Assert.True(integrity.Passed, integrity.Details);
        var eventTypes = traceDocument.Events
            .Select(traceEvent => traceEvent.Type)
            .ToArray();
        var terminalEvents = eventTypes
            .Where(TraceEventNames.IsTerminal)
            .ToArray();

        Assert.Equal(["run.failed"], terminalEvents);
        Assert.Equal("run.failed", eventTypes[^1]);
    }

    public static async Task<string[]> ReadTraceEventTypesAsync(
        string outputDirectory)
    {
        var traceDocument = await new JsonTraceReader().ReadAsync(
            Path.Combine(outputDirectory, "trace.json"));
        return traceDocument.Events
            .Select(traceEvent => traceEvent.Type)
            .ToArray();
    }
}
