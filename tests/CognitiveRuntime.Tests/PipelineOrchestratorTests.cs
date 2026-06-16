using System.Text.Json;
using CognitiveRuntime.Core.Contracts;
using static CognitiveRuntime.Tests.RuntimeTestHarness;

namespace CognitiveRuntime.Tests;

public sealed class PipelineOrchestratorTests
{
    [Fact]
    public async Task RunAsync_LinearPipelinePatternWritesStageArtifactsAndPassingEval()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode("frame");
        workspace.CreateMode(
            "challenge",
            requiredHeadings:
            [
                "## Target Claim",
                "## Assumptions",
                "## Failure Modes",
                "## Counterarguments",
                "## Tests"
            ]);
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(workspace, timeProvider);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "pipeline",
                "Build a traceable local-first cognitive runtime.",
                "mock",
                workspace.OutputRoot,
                WriteHtmlView: true,
                Pattern: "linear-pipeline",
                PipelineStages: ["frame", "challenge"]));

        Assert.Equal(RunOutcome.Success, result.Outcome);
        Assert.NotNull(result.HtmlViewPath);
        Assert.True(File.Exists(result.HtmlViewPath));

        var resultMarkdown = await File.ReadAllTextAsync(result.ResultPath);
        Assert.Contains("## Authoritative Revision", resultMarkdown);
        Assert.Contains("## Target Claim", resultMarkdown);

        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "stages", "01-frame", "input.md")));
        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "stages", "01-frame", "result.md")));
        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "stages", "02-challenge", "input.md")));
        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "stages", "02-challenge", "result.md")));

        // Each stage persists its own phase outputs under stages/NN-mode/phases.
        foreach (var stageDir in new[] { "01-frame", "02-challenge" })
        {
            foreach (var phaseFile in new[]
                { "01-main.md", "02-critic.md", "03-revision.md" })
            {
                var phasePath = Path.Combine(
                    result.OutputDirectory, "stages", stageDir, "phases", phaseFile);
                Assert.True(
                    File.Exists(phasePath), $"Missing phase artifact: {phasePath}");
            }
        }

        var runSummaryMarkdown = await File.ReadAllTextAsync(
            Path.Combine(result.OutputDirectory, "run_summary.md"));
        Assert.Contains("Pattern: `linear-pipeline`", runSummaryMarkdown);
        Assert.Contains("frame -> challenge", runSummaryMarkdown);
        var evalMarkdown = await File.ReadAllTextAsync(result.EvalReportPath);
        Assert.Contains("declared plan execution", evalMarkdown);
        Assert.Contains("model call pairing", evalMarkdown);

        var patternMarkdown = await File.ReadAllTextAsync(
            Path.Combine(result.OutputDirectory, "pattern.md"));
        Assert.Contains("`linear-pipeline`", patternMarkdown);
        Assert.Contains("frame -> challenge", patternMarkdown);
        Assert.Contains("input: the run's initial input", patternMarkdown);
        Assert.Contains("input: stage 01 (`frame`)'s authoritative revision", patternMarkdown);

        var html = await File.ReadAllTextAsync(result.HtmlViewPath);
        Assert.Contains(">linear-pipeline<", html);
        Assert.Contains("Stage 01", html);
        Assert.Contains(">frame<", html);
        Assert.Contains("Stage 02", html);
        Assert.Contains(">challenge<", html);
        Assert.Contains(
            "<strong>frame</strong> &rarr; <strong>challenge</strong>: authoritative revision",
            html);
        Assert.Contains("href=\"stages/01-frame/input.md\"", html);
        Assert.Contains("href=\"stages/02-challenge/result.md\"", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);

        using (var manifest = JsonDocument.Parse(
                   await File.ReadAllTextAsync(
                       Path.Combine(result.OutputDirectory, "run.json"))))
        {
            var root = manifest.RootElement;
            Assert.Equal(
                ["stage-01", "stage-02"],
                root.GetProperty("stages")
                    .EnumerateArray()
                    .Select(stage =>
                        stage.GetProperty("stageId").GetString() ?? string.Empty)
                    .ToArray());
            Assert.All(
                root.GetProperty("stages").EnumerateArray(),
                stage => Assert.Equal(
                    "completed",
                    stage.GetProperty("status").GetString()));
            Assert.Contains(
                root.GetProperty("artifacts").EnumerateArray(),
                artifact =>
                    artifact.GetProperty("relativePath").GetString() ==
                    "stages/01-frame/input.md");
            Assert.Contains(
                root.GetProperty("artifacts").EnumerateArray(),
                artifact =>
                    artifact.GetProperty("relativePath").GetString() ==
                    "stages/02-challenge/result.md");
            Assert.Contains(
                root.GetProperty("artifacts").EnumerateArray(),
                artifact =>
                    artifact.GetProperty("relativePath").GetString() ==
                    "stages/01-frame/phases/01-main.md" &&
                    artifact.GetProperty("kind").GetString() == "phaseOutput");
        }

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var traceEvents = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();
        var eventTypes = traceEvents
            .Select(element => element.GetProperty("type").GetString() ?? string.Empty)
            .ToArray();

        var runStartedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "run.started");
        Assert.Equal(
            "linear-pipeline",
            runStartedEvent.GetProperty("data").GetProperty("pattern").GetString());

        Assert.Contains("run.completed", eventTypes);
        Assert.Contains("eval.started", eventTypes);
        Assert.Contains("eval.completed", eventTypes);
        Assert.Contains("run.finalized", eventTypes);
        Assert.Equal("run.finalized", eventTypes[^1]);
        Assert.Contains(
            traceEvents,
            traceEvent =>
                traceEvent.GetProperty("type").GetString() == "artifact.written" &&
                traceEvent.GetProperty("data").GetProperty("name").GetString() == "index.html");

        var patternStartedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "pattern.started");
        var patternStartedData = patternStartedEvent.GetProperty("data");
        Assert.Equal("linear-pipeline", patternStartedData.GetProperty("pattern").GetString());
        Assert.Equal(
            ["frame", "challenge"],
            patternStartedData.GetProperty("stages").EnumerateArray()
                .Select(stage => stage.GetString() ?? string.Empty)
                .ToArray());
        var resolvedPlan = patternStartedData.GetProperty("plan");
        Assert.Equal(
            "stage-02.revision",
            resolvedPlan.GetProperty("authoritativeNodeId").GetString());
        Assert.Equal(
            [
                "stage-01.main",
                "stage-01.critic",
                "stage-01.revision",
                "stage-02.main",
                "stage-02.critic",
                "stage-02.revision"
            ],
            resolvedPlan.GetProperty("nodes")
                .EnumerateArray()
                .Select(node => node.GetProperty("id").GetString() ?? string.Empty)
                .ToArray());
        Assert.Equal(
            ["stage-02.main", "stage-02.critic", "stage-02.revision"],
            resolvedPlan.GetProperty("evalProfile")
                .GetProperty("requiredNodeIds")
                .EnumerateArray()
                .Select(node => node.GetString() ?? string.Empty)
                .ToArray());

        var patternCompletedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "pattern.completed");
        var patternCompletedData = patternCompletedEvent.GetProperty("data");
        Assert.Equal("linear-pipeline", patternCompletedData.GetProperty("pattern").GetString());
        Assert.Equal(2, patternCompletedData.GetProperty("stageCount").GetInt32());

        var stageStartedEvents = traceEvents
            .Where(traceEvent => traceEvent.GetProperty("type").GetString() == "stage.started")
            .ToArray();
        var stageCompletedEvents = traceEvents
            .Where(traceEvent => traceEvent.GetProperty("type").GetString() == "stage.completed")
            .ToArray();

        Assert.Equal(2, stageStartedEvents.Length);
        Assert.Equal(2, stageCompletedEvents.Length);
        Assert.Equal([1, 2], stageStartedEvents.Select(e => e.GetProperty("data").GetProperty("stageIndex").GetInt32()).ToArray());
        Assert.Equal(["frame", "challenge"], stageStartedEvents.Select(e => e.GetProperty("data").GetProperty("mode").GetString() ?? string.Empty).ToArray());
        Assert.Equal([1, 2], stageCompletedEvents.Select(e => e.GetProperty("data").GetProperty("stageIndex").GetInt32()).ToArray());
        Assert.Equal(["frame", "challenge"], stageCompletedEvents.Select(e => e.GetProperty("data").GetProperty("mode").GetString() ?? string.Empty).ToArray());

        var patternStartedIndex = Array.IndexOf(traceEvents, patternStartedEvent);
        var patternCompletedIndex = Array.IndexOf(traceEvents, patternCompletedEvent);
        var stage1StartedIndex = Array.IndexOf(traceEvents, stageStartedEvents[0]);
        var stage1CompletedIndex = Array.IndexOf(traceEvents, stageCompletedEvents[0]);
        var stage2StartedIndex = Array.IndexOf(traceEvents, stageStartedEvents[1]);
        var stage2CompletedIndex = Array.IndexOf(traceEvents, stageCompletedEvents[1]);

        Assert.True(patternStartedIndex < stage1StartedIndex);
        Assert.True(stage1StartedIndex < stage1CompletedIndex);
        Assert.True(stage1CompletedIndex < stage2StartedIndex);
        Assert.True(stage2StartedIndex < stage2CompletedIndex);
        Assert.True(stage2CompletedIndex < patternCompletedIndex);
    }

    [Fact]
    public async Task RunAsync_SinglePassPatternRunsOnlyMainPhase()
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
                workspace.OutputRoot,
                WriteHtmlView: true,
                Pattern: "single-pass"));

        Assert.Equal(RunOutcome.Success, result.Outcome);
        AssertRequiredArtifactsExist(result.OutputDirectory);
        Assert.NotNull(result.HtmlViewPath);

        var resultMarkdown = await File.ReadAllTextAsync(result.ResultPath);
        Assert.Contains("## Authoritative Result", resultMarkdown);
        Assert.Contains("## Problem", resultMarkdown);
        Assert.DoesNotContain("## Critic Review", resultMarkdown);

        // Single-pass persists only its one main phase output.
        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "phases", "01-main.md")));
        Assert.False(File.Exists(
            Path.Combine(result.OutputDirectory, "phases", "02-critic.md")));

        var evalMarkdown = await File.ReadAllTextAsync(result.EvalReportPath);
        Assert.Contains("Overall: **PASS**", evalMarkdown);
        Assert.Contains("main phase ran", evalMarkdown);
        Assert.Contains("main is not empty", evalMarkdown);
        Assert.DoesNotContain("critic phase ran", evalMarkdown);
        Assert.DoesNotContain("revision phase ran", evalMarkdown);
        Assert.DoesNotContain("loop responded to critic findings", evalMarkdown);
        Assert.Contains("declared plan execution", evalMarkdown);
        Assert.Contains("model call pairing", evalMarkdown);

        var runSummaryMarkdown = await File.ReadAllTextAsync(
            Path.Combine(result.OutputDirectory, "run_summary.md"));
        Assert.Contains("Pattern: `single-pass`", runSummaryMarkdown);

        var patternMarkdown = await File.ReadAllTextAsync(
            Path.Combine(result.OutputDirectory, "pattern.md"));
        Assert.Contains("`single-pass`", patternMarkdown);
        Assert.Contains("`main` (main) - context: no prior phase results", patternMarkdown);

        var html = await File.ReadAllTextAsync(result.HtmlViewPath);
        Assert.Contains(">single-pass<", html);
        Assert.Contains(">main<", html);
        Assert.Contains("1 step executed in runtime-defined order.", html);
        Assert.Contains("This pattern has no relationships between steps.", html);

        var eventTypes = await ReadTraceEventTypesAsync(result.OutputDirectory);
        Assert.DoesNotContain("critic.started", eventTypes);
        Assert.DoesNotContain("revision.started", eventTypes);
        Assert.Contains("run.finalized", eventTypes);

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var traceEvents = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();

        var patternStartedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "pattern.started");
        var patternStartedData = patternStartedEvent.GetProperty("data");
        Assert.Equal("single-pass", patternStartedData.GetProperty("pattern").GetString());
        Assert.Equal(
            "main",
            patternStartedData.GetProperty("plan")
                .GetProperty("authoritativeNodeId")
                .GetString());

        var stepDescriptors = patternStartedData.GetProperty("steps").EnumerateArray().ToArray();
        Assert.Single(stepDescriptors);
        Assert.Equal("main", stepDescriptors[0].GetProperty("name").GetString());
        Assert.Equal("main", stepDescriptors[0].GetProperty("kind").GetString());

        var patternCompletedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "pattern.completed");
        var patternCompletedData = patternCompletedEvent.GetProperty("data");
        Assert.Equal("single-pass", patternCompletedData.GetProperty("pattern").GetString());
        Assert.Equal(1, patternCompletedData.GetProperty("stepCount").GetInt32());
    }
}
