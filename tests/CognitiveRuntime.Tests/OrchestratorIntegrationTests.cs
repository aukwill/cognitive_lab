using System.Security.Cryptography;
using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Persistence;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Tracing;
using CognitiveRuntime.Core.Views;
using static CognitiveRuntime.Tests.RuntimeTestHarness;

namespace CognitiveRuntime.Tests;

public sealed class OrchestratorIntegrationTests
{
    [Fact]
    public async Task RunAsync_UsesInjectedRunId()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var orchestrator = CreateOrchestrator(
            workspace,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero)),
            runIdGenerator: new FixedRunIdGenerator("run-001"));

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Use a deterministic run identity in this test.",
                "mock",
                workspace.OutputRoot));

        Assert.Equal("run-001", result.RunId);
        Assert.EndsWith(
            "20260614T120000000Z_frame_run-001",
            result.OutputDirectory);
    }

    [Fact]
    public async Task RunAsync_RecordsRuntimeOwnedDurations()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero));
        var modelClient = new AdvancingModelClient(
            timeProvider,
            TimeSpan.FromMilliseconds(100));
        var evalRunner = new AdvancingEvalRunner(
            new EvalRunner(new OutputContractValidator()),
            timeProvider,
            TimeSpan.FromMilliseconds(250));
        var orchestrator = CreateOrchestrator(
            workspace,
            timeProvider,
            modelClient,
            evalRunner,
            runIdGenerator: new FixedRunIdGenerator("run-timing"));

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Record deterministic runtime durations.",
                modelClient.ProviderName,
                workspace.OutputRoot));

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var events = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();

        Assert.All(
            events.Where(
                traceEvent =>
                    traceEvent.GetProperty("type").GetString() ==
                    TraceEventNames.ModelCompleted),
            traceEvent => Assert.Equal(
                100,
                traceEvent
                    .GetProperty("data")
                    .GetProperty("durationMs")
                    .GetInt64()));
        Assert.Equal(
            3,
            events.Count(
                traceEvent =>
                    traceEvent.GetProperty("type").GetString() ==
                    TraceEventNames.PhaseCompleted));
        Assert.All(
            events.Where(
                traceEvent =>
                    traceEvent.GetProperty("type").GetString() is
                        TraceEventNames.PhaseCompleted or
                        TraceEventNames.NodeCompleted),
            traceEvent => Assert.Equal(
                100,
                traceEvent
                    .GetProperty("data")
                    .GetProperty("durationMs")
                    .GetInt64()));

        var evalCompleted = events.Single(
            traceEvent =>
                traceEvent.GetProperty("type").GetString() ==
                TraceEventNames.EvalCompleted);
        Assert.Equal(
            250,
            evalCompleted
                .GetProperty("data")
                .GetProperty("durationMs")
                .GetInt64());

        var runFinalized = events.Single(
            traceEvent =>
                traceEvent.GetProperty("type").GetString() ==
                TraceEventNames.RunFinalized);
        Assert.Equal(
            550,
            runFinalized
                .GetProperty("data")
                .GetProperty("durationMs")
                .GetInt64());
    }

    [Fact]
    public async Task RunAsync_SeparateStoresMirrorArtifactsAndLifecycle()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var runStateStore = new InMemoryRunStateStore();
        var artifactStore = new InMemoryArtifactStore();
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            runStateStore: runStateStore,
            artifactStore: artifactStore);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Persist a portable run catalog and artifact mirror.",
                "mock",
                workspace.OutputRoot,
                WriteHtmlView: true));

        var storedRun = await runStateStore.GetRunAsync(result.RunId);
        var trace = await artifactStore.GetAsync(
            result.RunId,
            "trace.json");
        var html = await artifactStore.GetAsync(
            result.RunId,
            "index.html");
        var artifacts = await artifactStore.ListAsync(result.RunId);

        Assert.NotNull(storedRun);
        Assert.Equal(RunLifecycleStatus.Succeeded, storedRun.LifecycleStatus);
        Assert.Equal(RunOutcome.Success, storedRun.Outcome);
        Assert.True(storedRun.Payload.EvalPassed);

        Assert.NotNull(trace);
        Assert.Equal(
            "application/json; charset=utf-8",
            trace.MediaType);
        using (var document = JsonDocument.Parse(trace.Content))
        {
            Assert.Equal(
                "run.finalized",
                document.RootElement
                    .GetProperty("events")
                    .EnumerateArray()
                    .Last()
                    .GetProperty("type")
                    .GetString());
        }

        Assert.NotNull(html);
        Assert.Equal("text/html; charset=utf-8", html.MediaType);
        Assert.Contains(
            "<!doctype html>",
            System.Text.Encoding.UTF8.GetString(html.Content),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            artifacts,
            artifact => artifact.RelativePath == "result.md");
        Assert.Contains(
            artifacts,
            artifact => artifact.RelativePath == "trace.json");
        Assert.Contains(
            artifacts,
            artifact => artifact.RelativePath == "index.html");
        Assert.Contains(
            artifacts,
            artifact => artifact.RelativePath == "run.json");
    }

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
                result.EvalReportPath,
                Path.Combine(result.OutputDirectory, "pattern.md")
            },
            path => Assert.True(File.Exists(path), $"Missing artifact: {path}"));

        var patternMarkdown = await File.ReadAllTextAsync(
            Path.Combine(result.OutputDirectory, "pattern.md"));
        Assert.Contains("`critic-revision`", patternMarkdown);
        Assert.Contains("`main` (main) - context: no prior phase results", patternMarkdown);
        Assert.Contains("`critic` (critic) - context: main", patternMarkdown);
        Assert.Contains("`revision` (revision) - context: main, critic", patternMarkdown);

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
        Assert.Contains("declared plan execution", evalMarkdown);
        Assert.Contains("model call pairing", evalMarkdown);

        var runSummaryMarkdown = await File.ReadAllTextAsync(
            Path.Combine(result.OutputDirectory, "run_summary.md"));
        Assert.Contains("Pattern: `critic-revision`", runSummaryMarkdown);

        await using (var manifestStream = File.OpenRead(
                         Path.Combine(result.OutputDirectory, "run.json")))
        using (var manifest = await JsonDocument.ParseAsync(manifestStream))
        {
            var root = manifest.RootElement;
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(result.RunId, root.GetProperty("runId").GetString());
            Assert.Equal("frame", root.GetProperty("requestedMode").GetString());
            Assert.Equal(
                "critic-revision",
                root.GetProperty("patternName").GetString());
            Assert.Equal("mock", root.GetProperty("modelProvider").GetString());
            Assert.Equal(
                ["deterministic-template-v2"],
                root.GetProperty("models")
                    .EnumerateArray()
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToArray());
            Assert.Equal(
                "succeeded",
                root.GetProperty("lifecycleStatus").GetString());
            Assert.Equal("success", root.GetProperty("outcome").GetString());
            Assert.Equal(
                ["main", "critic", "revision"],
                root.GetProperty("plan")
                    .GetProperty("nodes")
                    .EnumerateArray()
                    .Select(node => node.GetProperty("id").GetString() ?? string.Empty)
                    .ToArray());
            Assert.All(
                root.GetProperty("executionNodes").EnumerateArray(),
                node => Assert.Equal(
                    "completed",
                    node.GetProperty("status").GetString()));
            Assert.True(
                root.GetProperty("evaluation").GetProperty("passed").GetBoolean());
            var artifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
            Assert.Contains(
                artifacts,
                artifact =>
                    artifact.GetProperty("relativePath").GetString() == "run.json" &&
                    artifact.GetProperty("mediaType").GetString() ==
                    "application/json; charset=utf-8");

            // Each completed artifact records integrity that matches its bytes on
            // disk, so a verifier can detect a modified or truncated file.
            var resultArtifact = artifacts.Single(
                artifact => artifact.GetProperty("relativePath").GetString() == "result.md");
            var resultBytes = await File.ReadAllBytesAsync(result.ResultPath);
            Assert.Equal(
                resultBytes.LongLength,
                resultArtifact.GetProperty("byteLength").GetInt64());
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(resultBytes)).ToLowerInvariant(),
                resultArtifact.GetProperty("sha256").GetString());

            // The manifest is written last and cannot hash itself, so its own
            // integrity is recorded as unknown rather than guessed.
            var manifestArtifact = artifacts.Single(
                artifact => artifact.GetProperty("relativePath").GetString() == "run.json");
            Assert.Equal(
                JsonValueKind.Null,
                manifestArtifact.GetProperty("byteLength").ValueKind);
            Assert.Equal(
                JsonValueKind.Null,
                manifestArtifact.GetProperty("sha256").ValueKind);

            // Individual phase outputs are listed in run.json (TA-008).
            var phaseArtifacts = artifacts
                .Where(artifact =>
                    artifact.GetProperty("kind").GetString() == "phaseOutput")
                .Select(artifact =>
                    artifact.GetProperty("relativePath").GetString() ?? string.Empty)
                .ToArray();
            Assert.Equal(
                ["phases/01-main.md", "phases/02-critic.md", "phases/03-revision.md"],
                phaseArtifacts);

            Assert.Equal(
                JsonValueKind.Null,
                root.GetProperty("failure").ValueKind);
        }

        // Phase outputs are persisted as numbered files; result.md above stays
        // the authoritative composition.
        var phasesDirectory = Path.Combine(result.OutputDirectory, "phases");
        foreach (var phaseFile in new[]
            { "01-main.md", "02-critic.md", "03-revision.md" })
        {
            var phasePath = Path.Combine(phasesDirectory, phaseFile);
            Assert.True(File.Exists(phasePath), $"Missing phase artifact: {phasePath}");
            Assert.NotEmpty(await File.ReadAllTextAsync(phasePath));
        }

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        Assert.Equal(
            TraceSchema.CurrentVersion,
            traceDocument.RootElement
                .GetProperty("schemaVersion")
                .GetInt32());
        var traceEvents = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(
            Enumerable.Range(1, traceEvents.Length).Select(value => (long)value),
            traceEvents.Select(
                traceEvent => traceEvent.GetProperty("sequence").GetInt64()));
        var eventTypes = traceEvents
            .Select(element =>
                element.GetProperty("type").GetString() ?? string.Empty)
            .ToArray();

        var runStartedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "run.started");
        Assert.Equal(
            "critic-revision",
            runStartedEvent.GetProperty("data").GetProperty("pattern").GetString());

        Assert.Contains("run.started", eventTypes);
        Assert.Contains("mode.loaded", eventTypes);
        Assert.Contains("pattern.started", eventTypes);
        Assert.Contains("pattern.completed", eventTypes);
        Assert.Contains("phase.started", eventTypes);
        Assert.Contains("model.called", eventTypes);
        Assert.Contains("model.completed", eventTypes);
        Assert.Contains("critic.started", eventTypes);
        Assert.Contains("critic.completed", eventTypes);
        Assert.Contains("revision.started", eventTypes);
        Assert.Contains("revision.completed", eventTypes);
        Assert.Contains("artifact.written", eventTypes);
        Assert.Contains(
            traceEvents,
            traceEvent =>
                traceEvent.GetProperty("type").GetString() == "artifact.written" &&
                traceEvent.GetProperty("data").GetProperty("name").GetString() == "pattern.md");
        Assert.Contains(
            traceEvents,
            traceEvent =>
                traceEvent.GetProperty("type").GetString() == "artifact.written" &&
                traceEvent.GetProperty("data").GetProperty("name").GetString() == "run.json");

        // Each phase output is traced as an artifact write carrying its
        // run-relative path and the "phase" kind.
        var tracedPhasePaths = traceEvents
            .Where(traceEvent =>
                traceEvent.GetProperty("type").GetString() == "artifact.written" &&
                traceEvent.GetProperty("data").GetProperty("kind").GetString() == "phase")
            .Select(traceEvent =>
                traceEvent.GetProperty("data").GetProperty("relativePath").GetString()
                    ?? string.Empty)
            .ToArray();
        Assert.Equal(
            ["phases/01-main.md", "phases/02-critic.md", "phases/03-revision.md"],
            tracedPhasePaths);

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
        var manifestWrittenIndex = Array.FindIndex(
            traceEvents,
            traceEvent =>
                traceEvent.GetProperty("type").GetString() == "artifact.written" &&
                traceEvent.GetProperty("data").GetProperty("name").GetString() == "run.json");
        Assert.True(manifestWrittenIndex < traceEvents.Length - 1);
        var terminalEvent = traceEvents[^1];
        var terminalData = terminalEvent.GetProperty("data");
        Assert.Equal(
            "finalizing",
            terminalData.GetProperty("fromStatus").GetString());
        Assert.Equal(
            "succeeded",
            terminalData.GetProperty("lifecycleStatus").GetString());
        Assert.Equal(
            "success",
            terminalData.GetProperty("outcome").GetString());
        var modelCallAnalysis = ModelCallTraceAnalyzer.Analyze(
            (await new JsonTraceReader().ReadAsync(result.TracePath)).Events);
        Assert.True(
            modelCallAnalysis.IsValid,
            string.Join(Environment.NewLine, modelCallAnalysis.Issues));
        Assert.Equal(3, modelCallAnalysis.Calls.Count);
        Assert.Equal(
            ["main", "critic", "revision"],
            modelCallAnalysis.Calls.Select(call => call.NodeId));
        Assert.All(
            modelCallAnalysis.Calls,
            call =>
            {
                Assert.Equal(1, call.Attempt);
                Assert.Equal(1, call.CalledCount);
                Assert.Equal(1, call.CompletedCount);
                Assert.Equal(0, call.FailedCount);
            });
        var terminalIntegrity = new TerminalTraceIntegrityEvaluator().Evaluate(
            (await new JsonTraceReader().ReadAsync(result.TracePath)).Events);
        Assert.True(terminalIntegrity.Passed, terminalIntegrity.Details);

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

        var patternStartedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "pattern.started");
        var patternStartedData = patternStartedEvent.GetProperty("data");
        Assert.Equal("critic-revision", patternStartedData.GetProperty("pattern").GetString());

        var stepDescriptors = patternStartedData.GetProperty("steps").EnumerateArray().ToArray();
        Assert.Equal(
            ["main", "critic", "revision"],
            stepDescriptors.Select(step => step.GetProperty("name").GetString() ?? string.Empty).ToArray());
        Assert.Equal(
            ["main", "critic", "revision"],
            stepDescriptors.Select(step => step.GetProperty("kind").GetString() ?? string.Empty).ToArray());

        var patternCompletedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "pattern.completed");
        var patternCompletedData = patternCompletedEvent.GetProperty("data");
        Assert.Equal("critic-revision", patternCompletedData.GetProperty("pattern").GetString());
        Assert.Equal(3, patternCompletedData.GetProperty("stepCount").GetInt32());

        var patternStartedIndex = Array.IndexOf(traceEvents, patternStartedEvent);
        var patternCompletedIndex = Array.IndexOf(traceEvents, patternCompletedEvent);
        var firstPhaseStartedIndex = Array.FindIndex(
            eventTypes, eventType => eventType == "phase.started");

        Assert.True(patternStartedIndex < firstPhaseStartedIndex);
        Assert.True(patternCompletedIndex > revisionCompletedIndex);
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
            new[] { "Run", "Artifacts", "Pattern", "Mode", "Phases", "Tool Policy", "Evals", "Trace" },
            heading => Assert.Contains($">{heading}<", html));
        Assert.All(
            new[]
            {
                "input.md", "result.md", "trace.json", "run_summary.md", "eval_report.md",
                "pattern.md"
            },
            artifact => Assert.Contains($"href=\"{artifact}\"", html));
        Assert.Contains(inputSource, html);
        Assert.Contains("No tool policy decisions were recorded", html);
        Assert.Contains(">critic-revision<", html);
        Assert.Contains(">main<", html);
        Assert.Contains(">critic<", html);
        Assert.Contains(">revision<", html);
        Assert.Contains("<strong>main</strong> &rarr; <strong>critic</strong>: provides context", html);
        Assert.Contains("<strong>main</strong> &rarr; <strong>revision</strong>: provides context", html);
        Assert.Contains("<strong>critic</strong> &rarr; <strong>revision</strong>: provides context", html);
        Assert.Contains("Authoritative result", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);

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
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero));
        const string secret = "super-secret-token";
        var failure = new ModelProviderException(
            $"Synthetic provider failure. Authorization: Bearer {secret} " +
            $"api-key={secret} {new string('x', 400)}");
        var orchestrator = CreateOrchestrator(
            workspace,
            timeProvider,
            new FailingModelClient(
                failure,
                timeProvider,
                TimeSpan.FromMilliseconds(40)));

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
        await AssertTerminalLifecycleAsync(
            exception.OutputDirectory,
            "run.failed",
            "running",
            "failed",
            "runtimeFailed");
        var modelCallAnalysis = ModelCallTraceAnalyzer.Analyze(
            (await new JsonTraceReader().ReadAsync(
                Path.Combine(exception.OutputDirectory, "trace.json"))).Events);
        Assert.True(
            modelCallAnalysis.IsValid,
            string.Join(Environment.NewLine, modelCallAnalysis.Issues));
        var failedCall = Assert.Single(modelCallAnalysis.Calls);
        Assert.Equal("main", failedCall.NodeId);
        Assert.Equal(1, failedCall.Attempt);
        Assert.Equal(1, failedCall.CalledCount);
        Assert.Equal(0, failedCall.CompletedCount);
        Assert.Equal(1, failedCall.FailedCount);

        await using var traceStream = File.OpenRead(
            Path.Combine(exception.OutputDirectory, "trace.json"));
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var events = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(
            40,
            events.Single(
                    traceEvent =>
                        traceEvent.GetProperty("type").GetString() ==
                        TraceEventNames.ModelFailed)
                .GetProperty("data")
                .GetProperty("durationMs")
                .GetInt64());
        Assert.Equal(
            40,
            events.Single(
                    traceEvent =>
                        traceEvent.GetProperty("type").GetString() ==
                        TraceEventNames.NodeFailed)
                .GetProperty("data")
                .GetProperty("durationMs")
                .GetInt64());
        Assert.Equal(
            40,
            events.Single(
                    traceEvent =>
                        traceEvent.GetProperty("type").GetString() ==
                        TraceEventNames.RunFailed)
                .GetProperty("data")
                .GetProperty("durationMs")
                .GetInt64());
        Assert.All(
            events.Where(
                traceEvent =>
                    traceEvent.GetProperty("type").GetString() is
                        TraceEventNames.ModelFailed or
                        TraceEventNames.NodeFailed or
                        TraceEventNames.RunFailed),
            traceEvent =>
            {
                var data = traceEvent.GetProperty("data");
                Assert.Equal("provider", data.GetProperty("category").GetString());
                Assert.Equal("main", data.GetProperty("phase").GetString());
                Assert.Equal("failing", data.GetProperty("provider").GetString());
                Assert.Equal(
                    nameof(ModelProviderException),
                    data.GetProperty("exceptionType").GetString());
                var message = data.GetProperty("message").GetString();
                Assert.NotNull(message);
                Assert.DoesNotContain(secret, message);
                Assert.Contains("[REDACTED]", message);
                Assert.True(message.Length <= 256);
            });

        Assert.DoesNotContain(secret, exception.Message);
        var manifestText = await File.ReadAllTextAsync(
            Path.Combine(exception.OutputDirectory, "run.json"));
        Assert.DoesNotContain(secret, manifestText);
        using (var manifest = JsonDocument.Parse(manifestText))
        {
            var root = manifest.RootElement;
            Assert.Equal("failed", root.GetProperty("lifecycleStatus").GetString());
            Assert.Equal("runtimeFailed", root.GetProperty("outcome").GetString());
            Assert.Equal(
                "provider",
                root.GetProperty("failure").GetProperty("category").GetString());
        }

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
        using var manifest = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(exception.OutputDirectory, "run.json")));
        Assert.Equal(
            "evaluation",
            manifest.RootElement
                .GetProperty("failure")
                .GetProperty("category")
                .GetString());
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

    [Fact]
    public async Task RunAsync_CancellationDuringModelCallRecordsCancelledLifecycle()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var modelClient = new BlockingModelClient();
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            modelClient);
        using var cancellationSource = new CancellationTokenSource();

        var runTask = orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Cancel during the model call.",
                modelClient.ProviderName,
                workspace.OutputRoot),
            cancellationSource.Token);
        await modelClient.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        var outputDirectory = Assert.Single(
            Directory.GetDirectories(workspace.OutputRoot));
        AssertRequiredArtifactsExist(outputDirectory);
        await AssertTerminalLifecycleAsync(
            outputDirectory,
            "run.cancelled",
            "running",
            "cancelled",
            "cancelled");
        var eventTypes = await ReadTraceEventTypesAsync(outputDirectory);
        Assert.DoesNotContain("run.failed", eventTypes);
        Assert.DoesNotContain("run.finalized", eventTypes);
        Assert.Equal("run.cancelled", eventTypes[^1]);
        var trace = await new JsonTraceReader().ReadAsync(
            Path.Combine(outputDirectory, "trace.json"));
        var modelFailure = trace.Events.Single(
            traceEvent => traceEvent.Type == TraceEventNames.ModelFailed);
        Assert.Equal(
            "cancellation",
            Assert.IsType<JsonElement>(modelFailure.Data["category"]).GetString());
        var terminalIntegrity = new TerminalTraceIntegrityEvaluator().Evaluate(
            trace.Events);
        Assert.True(terminalIntegrity.Passed, terminalIntegrity.Details);
        using var manifest = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "run.json")));
        Assert.Equal(
            "cancelled",
            manifest.RootElement.GetProperty("lifecycleStatus").GetString());
        Assert.Equal(
            "cancelled",
            manifest.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            manifest.RootElement.GetProperty("failure").ValueKind);
    }

    [Fact]
    public async Task RunAsync_RunManifestSerializationIsDeterministic()
    {
        using var firstWorkspace = new TestWorkspace();
        using var secondWorkspace = new TestWorkspace();
        firstWorkspace.CreateMode();
        secondWorkspace.CreateMode();
        var timestamp = new DateTimeOffset(
            2026,
            6,
            14,
            12,
            0,
            0,
            TimeSpan.Zero);
        var first = CreateOrchestrator(
            firstWorkspace,
            new FixedTimeProvider(timestamp),
            runIdGenerator: new FixedRunIdGenerator("run-deterministic"));
        var second = CreateOrchestrator(
            secondWorkspace,
            new FixedTimeProvider(timestamp),
            runIdGenerator: new FixedRunIdGenerator("run-deterministic"));
        var request = new RunRequest(
            "frame",
            "Serialize the same terminal run manifest.",
            "mock",
            firstWorkspace.OutputRoot);

        var firstResult = await first.RunAsync(request);
        var secondResult = await second.RunAsync(
            request with { OutputRoot = secondWorkspace.OutputRoot });

        Assert.Equal(
            await File.ReadAllTextAsync(
                Path.Combine(firstResult.OutputDirectory, "run.json")),
            await File.ReadAllTextAsync(
                Path.Combine(secondResult.OutputDirectory, "run.json")));
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

    private static async Task AssertTerminalLifecycleAsync(
        string outputDirectory,
        string eventType,
        string fromStatus,
        string lifecycleStatus,
        string outcome)
    {
        await using var stream = File.OpenRead(
            Path.Combine(outputDirectory, "trace.json"));
        using var document = await JsonDocument.ParseAsync(stream);
        var terminalEvent = document.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .Single(traceEvent =>
                traceEvent.GetProperty("type").GetString() == eventType);
        var data = terminalEvent.GetProperty("data");

        Assert.Equal(
            fromStatus,
            data.GetProperty("fromStatus").GetString());
        Assert.Equal(
            lifecycleStatus,
            data.GetProperty("lifecycleStatus").GetString());
        Assert.Equal(
            outcome,
            data.GetProperty("outcome").GetString());
    }

    private sealed class FailingModelClient : IModelClient
    {
        private readonly Exception _exception;
        private readonly FixedTimeProvider? _timeProvider;
        private readonly TimeSpan _duration;

        public FailingModelClient(
            Exception exception,
            FixedTimeProvider? timeProvider = null,
            TimeSpan duration = default)
        {
            _exception = exception;
            _timeProvider = timeProvider;
            _duration = duration;
        }

        public string ProviderName => "failing";

        public Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            _timeProvider?.Advance(_duration);
            throw _exception;
        }
    }

    private sealed class AdvancingModelClient(
        FixedTimeProvider timeProvider,
        TimeSpan duration) : IModelClient
    {
        private readonly MockModelClient _inner = new();

        public string ProviderName => "advancing-mock";

        public async Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _inner.CompleteAsync(request, cancellationToken);
            timeProvider.Advance(duration);
            return response with { Provider = ProviderName };
        }
    }

    private sealed class AdvancingEvalRunner(
        IEvalRunner inner,
        FixedTimeProvider timeProvider,
        TimeSpan duration) : IEvalRunner
    {
        public async Task<EvalReport> EvaluateAsync(
            EvalContext context,
            CancellationToken cancellationToken = default)
        {
            var report = await inner.EvaluateAsync(context, cancellationToken);
            timeProvider.Advance(duration);
            return report;
        }
    }

    private sealed class BlockingModelClient : IModelClient
    {
        public string ProviderName => "blocking";

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
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
