using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;

namespace CognitiveRuntime.Tests;

public sealed class EvalRunnerTests
{
    [Fact]
    public async Task EvaluateAsync_FailsWhenOutputContractHeadingIsMissing()
    {
        using var workspace = new TestWorkspace();
        var writer = new ArtifactWriter(TimeProvider.System);
        var artifacts = await writer.PrepareRunAsync(
            workspace.OutputRoot,
            "frame",
            "12345678");

        foreach (var kind in Enum.GetValues<ArtifactKind>())
        {
            await writer.WriteAsync(artifacts, kind, "content");
        }

        await File.WriteAllTextAsync(artifacts.TracePath, "{}");
        await writer.WriteAsync(
            artifacts,
            ArtifactKind.Result,
            """
            # Frame Result

            ## Authoritative Revision

            ## Problem

            A sufficiently long revision without the objective heading.

            ## Critic Review

            ## Objective

            This heading appears only in the critic appendix.
            """);

        var mode = new LoadedMode(
            workspace.Root,
            "# Test",
            new ModeManifest
            {
                Name = "frame",
                Description = "Test.",
                OutputContract = new OutputContract
                {
                    RequiredHeadings = ["## Problem", "## Objective"],
                    MinimumLength = 20
                }
            },
            []);
        var events = new[]
        {
            CreateEvent(TraceEventNames.RunStarted),
            CreateEvent(TraceEventNames.CriticCompleted),
            CreateEvent(TraceEventNames.RevisionCompleted),
            CreateEvent(TraceEventNames.RunCompleted)
        };
        var phaseResults = new[]
        {
            CreatePhaseResult(PhaseKind.Main, "draft"),
            CreatePhaseResult(PhaseKind.Critic, "## Objective\n\nCritic appendix."),
            CreatePhaseResult(
                PhaseKind.Revision,
                "## Problem\n\nA sufficiently long revision without the objective heading.")
        };
        var runner = new EvalRunner(new OutputContractValidator());

        var report = await runner.EvaluateAsync(
            new EvalContext(
                artifacts,
                mode,
                events,
                phaseResults,
                CreateCriticRevisionPlan()));

        Assert.False(report.Passed);
        var contractCheck = Assert.Single(
            report.Checks,
            check => check.Name == "output contract satisfied");
        Assert.False(contractCheck.Passed);
        Assert.Contains("## Objective", contractCheck.Details);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenRevisionIsEmpty()
    {
        using var workspace = new TestWorkspace();
        var writer = new ArtifactWriter(TimeProvider.System);
        var artifacts = await writer.PrepareRunAsync(
            workspace.OutputRoot,
            "frame",
            "12345678");

        foreach (var kind in Enum.GetValues<ArtifactKind>())
        {
            await writer.WriteAsync(artifacts, kind, "content");
        }

        await File.WriteAllTextAsync(artifacts.TracePath, "{}");

        var mode = new LoadedMode(
            workspace.Root,
            "# Test",
            new ModeManifest
            {
                Name = "frame",
                Description = "Test.",
                OutputContract = new OutputContract
                {
                    RequiredHeadings = ["## Problem"],
                    MinimumLength = 20
                }
            },
            []);
        var events = new[]
        {
            CreateEvent(TraceEventNames.RunStarted),
            CreateEvent(TraceEventNames.CriticCompleted),
            CreateEvent(TraceEventNames.RevisionCompleted),
            CreateEvent(TraceEventNames.RunCompleted)
        };
        var phaseResults = new[]
        {
            CreatePhaseResult(PhaseKind.Main, "draft"),
            CreatePhaseResult(PhaseKind.Critic, "critic"),
            CreatePhaseResult(PhaseKind.Revision, string.Empty)
        };
        var runner = new EvalRunner(new OutputContractValidator());

        var report = await runner.EvaluateAsync(
            new EvalContext(
                artifacts,
                mode,
                events,
                phaseResults,
                CreateCriticRevisionPlan()));

        Assert.False(report.Passed);
        var revisionCheck = Assert.Single(
            report.Checks,
            check => check.Name == "revision is not empty");
        Assert.False(revisionCheck.Passed);
    }

    [Fact]
    public async Task EvaluateAsync_SinglePassValidatesMainAsAuthoritativeOutput()
    {
        using var workspace = new TestWorkspace();
        var writer = new ArtifactWriter(TimeProvider.System);
        var artifacts = await writer.PrepareRunAsync(
            workspace.OutputRoot,
            "frame",
            "12345678");

        foreach (var kind in Enum.GetValues<ArtifactKind>())
        {
            await writer.WriteAsync(artifacts, kind, "content");
        }

        await File.WriteAllTextAsync(artifacts.TracePath, "{}");
        const string mainContent = """
            ## Problem

            Bound the runtime problem.

            ## Objective

            Produce an inspectable result.
            """;
        var mode = new LoadedMode(
            workspace.Root,
            "# Test",
            new ModeManifest
            {
                Name = "frame",
                Description = "Test.",
                OutputContract = new OutputContract
                {
                    RequiredHeadings = ["## Problem", "## Objective"],
                    MinimumLength = 20
                }
            },
            []);
        var events = new[]
        {
            CreateEvent(TraceEventNames.RunStarted),
            CreateEvent(TraceEventNames.ModelCompleted, "main"),
            CreateEvent(TraceEventNames.RunCompleted)
        };
        var phaseResults = new[]
        {
            CreatePhaseResult(PhaseKind.Main, mainContent)
        };
        var runner = new EvalRunner(new OutputContractValidator());

        var report = await runner.EvaluateAsync(
            new EvalContext(
                artifacts,
                mode,
                events,
                phaseResults,
                new EvalPlan(
                    [PhaseKind.Main],
                    PhaseKind.Main,
                    EvaluateLoopEfficacy: false)));

        Assert.True(report.Passed);
        Assert.Contains(
            report.Checks,
            check => check.Name == "main phase ran" && check.Passed);
        Assert.Contains(
            report.Checks,
            check => check.Name == "main is not empty" && check.Passed);
        Assert.DoesNotContain(
            report.Checks,
            check => check.Name == "loop responded to critic findings");
    }

    [Fact]
    public async Task EvaluateAsync_RequiredArtifactsCheckUsesLedgerAndFailsOnFailedArtifact()
    {
        using var workspace = new TestWorkspace();
        var writer = new ArtifactWriter(TimeProvider.System);
        var artifacts = await writer.PrepareRunAsync(
            workspace.OutputRoot,
            "frame",
            "12345678");

        foreach (var kind in Enum.GetValues<ArtifactKind>())
        {
            await writer.WriteAsync(artifacts, kind, "content");
        }

        await File.WriteAllTextAsync(artifacts.TracePath, "{}");
        var mode = new LoadedMode(
            workspace.Root,
            "# Test",
            new ModeManifest
            {
                Name = "frame",
                Description = "Test.",
                OutputContract = new OutputContract
                {
                    RequiredHeadings = ["## Problem"],
                    MinimumLength = 5
                }
            },
            []);
        var events = new[]
        {
            CreateEvent(TraceEventNames.RunStarted),
            CreateEvent(TraceEventNames.ModelCompleted, "main"),
            CreateEvent(TraceEventNames.RunCompleted)
        };
        var phaseResults = new[]
        {
            CreatePhaseResult(PhaseKind.Main, "## Problem\n\nBound the problem.")
        };
        // A planned eval report is acceptable (produced after this check), but a
        // failed artifact write makes the check fail without any placeholder text.
        var ledger = new[]
        {
            new RunArtifactState("result.md", RunArtifactStatus.Written),
            new RunArtifactState("eval_report.md", RunArtifactStatus.Planned),
            new RunArtifactState("run_summary.md", RunArtifactStatus.Failed)
        };
        var runner = new EvalRunner(new OutputContractValidator());

        var report = await runner.EvaluateAsync(
            new EvalContext(
                artifacts,
                mode,
                events,
                phaseResults,
                new EvalPlan(
                    [PhaseKind.Main],
                    PhaseKind.Main,
                    EvaluateLoopEfficacy: false),
                ArtifactLedger: ledger));

        var artifactCheck = Assert.Single(
            report.Checks,
            check => check.Name == "required artifacts exist");
        Assert.False(artifactCheck.Passed);
        Assert.Contains("run_summary.md", artifactCheck.Details);
    }

    private static PhaseResult CreatePhaseResult(
        PhaseKind phaseKind,
        string content) =>
        new(
            phaseKind.ToString().ToLowerInvariant(),
            phaseKind,
            content,
            "test",
            "test-model");

    private static TraceEvent CreateEvent(string type, string? phase = null) =>
        new(
            1,
            DateTimeOffset.UtcNow,
            type,
            "run",
            phase is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?> { ["phase"] = phase });

    private static EvalPlan CreateCriticRevisionPlan() =>
        new(
            [PhaseKind.Main, PhaseKind.Critic, PhaseKind.Revision],
            PhaseKind.Revision,
            EvaluateLoopEfficacy: true);
}
