using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Tracing;
using CognitiveRuntime.Core.Views;
using static CognitiveRuntime.Tests.RuntimeTestHarness;

namespace CognitiveRuntime.Tests;

public sealed class OrchestratorIntegrationTests
{
    [Fact]
    public async Task RunAsync_MockFrameRunWritesValidPassingArtifacts()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(workspace, timeProvider);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Build a traceable local-first cognitive runtime.",
                "mock",
                workspace.OutputRoot));

        Assert.Equal(RunOutcome.Success, result.Outcome);
        Assert.Null(result.HtmlViewPath);
        Assert.False(File.Exists(Path.Combine(result.OutputDirectory, "index.html")));
        Assert.All(
            new[]
            {
                Path.Combine(result.OutputDirectory, "input.md"),
                result.ResultPath,
                result.TracePath,
                Path.Combine(result.OutputDirectory, "run_summary.md"),
                result.EvalReportPath
            },
            path => Assert.True(File.Exists(path), $"Missing artifact: {path}"));

        var resultMarkdown = await File.ReadAllTextAsync(result.ResultPath);
        Assert.Contains("## Authoritative Revision", resultMarkdown);
        Assert.Contains("## Problem", resultMarkdown);
        Assert.Contains("## Initial Draft", resultMarkdown);
        Assert.Contains("## Critic Review", resultMarkdown);
        Assert.True(
            resultMarkdown.IndexOf("## Authoritative Revision", StringComparison.Ordinal) <
            resultMarkdown.IndexOf("## Initial Draft", StringComparison.Ordinal));

        var evalMarkdown = await File.ReadAllTextAsync(result.EvalReportPath);
        Assert.Contains("Overall: **PASS**", evalMarkdown);
        Assert.Contains("revision phase ran", evalMarkdown);
        Assert.Contains("revision is not empty", evalMarkdown);

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var traceEvents = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();
        var eventTypes = traceEvents
            .Select(element =>
                element.GetProperty("type").GetString() ?? string.Empty)
            .ToArray();

        Assert.Contains("run.started", eventTypes);
        Assert.Contains("mode.loaded", eventTypes);
        Assert.Contains("phase.started", eventTypes);
        Assert.Contains("model.called", eventTypes);
        Assert.Contains("model.completed", eventTypes);
        Assert.Contains("critic.started", eventTypes);
        Assert.Contains("critic.completed", eventTypes);
        Assert.Contains("revision.started", eventTypes);
        Assert.Contains("revision.completed", eventTypes);
        Assert.Contains("artifact.written", eventTypes);
        Assert.Contains("eval.started", eventTypes);
        Assert.Contains("eval.completed", eventTypes);
        Assert.Contains("run.completed", eventTypes);
        Assert.Single(eventTypes, eventType => eventType == "run.finalized");
        Assert.DoesNotContain(
            traceEvents,
            traceEvent =>
                traceEvent.GetProperty("type").GetString() == "artifact.written" &&
                traceEvent.GetProperty("data").GetProperty("name").GetString() == "index.html");
        Assert.Equal("run.finalized", eventTypes[^1]);

        var criticCompletedIndex = FindEventIndex(
            traceEvents,
            "critic.completed",
            "critic");
        var revisionStartedIndex = FindEventIndex(
            traceEvents,
            "revision.started",
            "revision");
        var revisionModelCalledIndex = FindEventIndex(
            traceEvents,
            "model.called",
            "revision");
        var revisionModelCompletedIndex = FindEventIndex(
            traceEvents,
            "model.completed",
            "revision");
        var revisionCompletedIndex = FindEventIndex(
            traceEvents,
            "revision.completed",
            "revision");

        Assert.All(
            new[]
            {
                criticCompletedIndex,
                revisionStartedIndex,
                revisionModelCalledIndex,
                revisionModelCompletedIndex,
                revisionCompletedIndex
            },
            index => Assert.True(index >= 0));
        Assert.True(criticCompletedIndex < revisionStartedIndex);
        Assert.True(revisionStartedIndex < revisionModelCalledIndex);
        Assert.True(revisionModelCalledIndex < revisionModelCompletedIndex);
        Assert.True(revisionModelCompletedIndex < revisionCompletedIndex);
    }

    [Fact]
    public async Task RunAsync_HtmlEnabledWritesInspectionViewWithCoreSectionsAndLinks()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var orchestrator = CreateOrchestrator(workspace, TimeProvider.System);
        var inputSource = Path.Combine(workspace.Root, "goal.txt");

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Build a traceable local-first cognitive runtime.",
                "mock",
                workspace.OutputRoot,
                WriteHtmlView: true,
                InputSource: inputSource));

        Assert.NotNull(result.HtmlViewPath);
        Assert.True(File.Exists(result.HtmlViewPath));

        var html = await File.ReadAllTextAsync(result.HtmlViewPath);
        Assert.All(
            new[] { "Run", "Artifacts", "Mode", "Phases", "Tool Policy", "Evals", "Trace" },
            heading => Assert.Contains($">{heading}<", html));
        Assert.All(
            new[] { "input.md", "result.md", "trace.json", "run_summary.md", "eval_report.md" },
            artifact => Assert.Contains($"href=\"{artifact}\"", html));
        Assert.Contains(inputSource, html);
        Assert.Contains("No tool policy decisions were recorded", html);
        Assert.Contains(">revision<", html);
        Assert.Contains("Authoritative result", html);

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var traceEvents = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();
        var eventTypes = traceEvents
            .Select(traceEvent => traceEvent.GetProperty("type").GetString())
            .ToArray();
        var htmlArtifactIndex = Array.FindIndex(
            traceEvents,
            traceEvent =>
                traceEvent.GetProperty("type").GetString() == "artifact.written" &&
                traceEvent.GetProperty("data").GetProperty("name").GetString() == "index.html" &&
                traceEvent.GetProperty("data").GetProperty("kind").GetString() == "html");

        Assert.True(htmlArtifactIndex >= 0);
        Assert.Single(eventTypes, eventType => eventType == "run.finalized");
        Assert.Equal("run.finalized", eventTypes[^1]);
        Assert.True(htmlArtifactIndex < eventTypes.Length - 1);
    }

    [Fact]
    public async Task RunAsync_HtmlViewEscapesModelProvidedMarkup()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            new UnsafeMarkupModelClient());

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "<script>alert('input')</script>",
                "unsafe",
                workspace.OutputRoot,
                WriteHtmlView: true,
                InputSource: "<script>alert('path')</script>"));

        var html = await File.ReadAllTextAsync(
            Assert.IsType<string>(result.HtmlViewPath));

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("alert", html);
    }

    [Fact]
    public async Task RunAsync_ModelFailureLeavesRequiredFailureArtifacts()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var failure = new ModelProviderException("Synthetic provider failure.");
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            new FailingModelClient(failure));

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Input that reaches a failing provider.",
                    "failing",
                    workspace.OutputRoot)));

        Assert.Same(failure, exception.InnerException);
        AssertRequiredArtifactsExist(exception.OutputDirectory);
        await AssertTerminalFailureAsync(exception.OutputDirectory);

        var eval = await File.ReadAllTextAsync(
            Path.Combine(exception.OutputDirectory, "eval_report.md"));
        Assert.Contains("Overall: **FAIL**", eval);
    }

    [Fact]
    public async Task RunAsync_EvalFailureRecordsOneTerminalFailure()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var failure = new InvalidOperationException("Synthetic eval failure.");
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            evalRunner: new FailingEvalRunner(failure));

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Input that reaches a failing evaluator.",
                    "mock",
                    workspace.OutputRoot)));

        Assert.Same(failure, exception.InnerException);
        AssertRequiredArtifactsExist(exception.OutputDirectory);
        await AssertTerminalFailureAsync(exception.OutputDirectory);
    }

    [Fact]
    public async Task RunAsync_TraceFailureRecordsOneTerminalFailure()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var failure = new InvalidOperationException("Synthetic trace failure.");
        var traceFactory = new FailOnceTraceSessionFactory(
            new JsonTraceSessionFactory(TimeProvider.System),
            "eval.started",
            failure);
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            traceSessionFactory: traceFactory);

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Input that reaches a transient trace failure.",
                    "mock",
                    workspace.OutputRoot)));

        Assert.Same(failure, exception.InnerException);
        AssertRequiredArtifactsExist(exception.OutputDirectory);
        await AssertTerminalFailureAsync(exception.OutputDirectory);
    }

    [Fact]
    public async Task RunAsync_HtmlViewFailureRecordsOneTerminalFailure()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var failure = new IOException("Synthetic HTML view failure.");
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            runViewWriter: new FailingRunViewWriter(failure));

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Input that reaches a failing HTML writer.",
                    "mock",
                    workspace.OutputRoot,
                    WriteHtmlView: true)));

        Assert.Same(failure, exception.InnerException);
        AssertRequiredArtifactsExist(exception.OutputDirectory);
        Assert.False(File.Exists(Path.Combine(exception.OutputDirectory, "index.html")));
        await AssertTerminalFailureAsync(exception.OutputDirectory);
    }

    private static int FindEventIndex(
        IReadOnlyList<JsonElement> traceEvents,
        string eventType,
        string phaseName)
    {
        for (var index = 0; index < traceEvents.Count; index++)
        {
            var traceEvent = traceEvents[index];
            if (traceEvent.GetProperty("type").GetString() == eventType &&
                traceEvent.GetProperty("data").GetProperty("phase").GetString() == phaseName)
            {
                return index;
            }
        }

        return -1;
    }

    private sealed class FailingModelClient : IModelClient
    {
        private readonly Exception _exception;

        public FailingModelClient(Exception exception)
        {
            _exception = exception;
        }

        public string ProviderName => "failing";

        public Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default) =>
            throw _exception;
    }

    private sealed class FailingEvalRunner : IEvalRunner
    {
        private readonly Exception _exception;

        public FailingEvalRunner(Exception exception)
        {
            _exception = exception;
        }

        public Task<EvalReport> EvaluateAsync(
            EvalContext context,
            CancellationToken cancellationToken = default) =>
            throw _exception;
    }

    private sealed class FailingRunViewWriter : IRunViewWriter
    {
        private readonly Exception _exception;

        public FailingRunViewWriter(Exception exception)
        {
            _exception = exception;
        }

        public Task<string> WriteAsync(
            RunViewModel viewModel,
            CancellationToken cancellationToken = default) =>
            throw _exception;
    }

    private sealed class FailOnceTraceSessionFactory : ITraceSessionFactory
    {
        private readonly ITraceSessionFactory _inner;
        private readonly string _eventType;
        private readonly Exception _exception;

        public FailOnceTraceSessionFactory(
            ITraceSessionFactory inner,
            string eventType,
            Exception exception)
        {
            _inner = inner;
            _eventType = eventType;
            _exception = exception;
        }

        public async Task<ITraceSession> CreateAsync(
            string runId,
            string tracePath,
            CancellationToken cancellationToken = default) =>
            new FailOnceTraceSession(
                await _inner.CreateAsync(runId, tracePath, cancellationToken),
                _eventType,
                _exception);
    }

    private sealed class FailOnceTraceSession : ITraceSession
    {
        private readonly ITraceSession _inner;
        private readonly string _eventType;
        private readonly Exception _exception;
        private int _failed;

        public FailOnceTraceSession(
            ITraceSession inner,
            string eventType,
            Exception exception)
        {
            _inner = inner;
            _eventType = eventType;
            _exception = exception;
        }

        public string FilePath => _inner.FilePath;

        public IReadOnlyList<TraceEvent> Events => _inner.Events;

        public Task EmitAsync(
            string eventType,
            IReadOnlyDictionary<string, object?>? data = null,
            CancellationToken cancellationToken = default)
        {
            if (eventType == _eventType &&
                Interlocked.Exchange(ref _failed, 1) == 0)
            {
                throw _exception;
            }

            return _inner.EmitAsync(eventType, data, cancellationToken);
        }
    }

    private sealed class UnsafeMarkupModelClient : IModelClient
    {
        public string ProviderName => "unsafe";

        public Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            var content = request.PhaseKind == PhaseKind.Critic
                ? "### Review\n\n<script>alert('critic')</script>"
                : """
                    ## Problem

                    <script>alert('x')</script>

                    ## Objective

                    Confirm HTML escaping.

                    ## Constraints

                    Preserve typed runtime control and deterministic rendering.

                    ## Unknowns

                    None for this test.

                    ## Next Actions

                    Inspect the generated static artifact for encoded markup.
                    """;

            return Task.FromResult(
                new ModelResponse(content, ProviderName, "unsafe-test"));
        }
    }
}
