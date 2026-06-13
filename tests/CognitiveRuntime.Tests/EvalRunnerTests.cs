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
            CreateEvent("run.started"),
            CreateEvent("critic.completed"),
            CreateEvent("revision.completed"),
            CreateEvent("run.completed")
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
            new EvalContext(artifacts, mode, events, phaseResults));

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
            CreateEvent("run.started"),
            CreateEvent("critic.completed"),
            CreateEvent("revision.completed"),
            CreateEvent("run.completed")
        };
        var phaseResults = new[]
        {
            CreatePhaseResult(PhaseKind.Main, "draft"),
            CreatePhaseResult(PhaseKind.Critic, "critic"),
            CreatePhaseResult(PhaseKind.Revision, string.Empty)
        };
        var runner = new EvalRunner(new OutputContractValidator());

        var report = await runner.EvaluateAsync(
            new EvalContext(artifacts, mode, events, phaseResults));

        Assert.False(report.Passed);
        var revisionCheck = Assert.Single(
            report.Checks,
            check => check.Name == "revision is not empty");
        Assert.False(revisionCheck.Passed);
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

    private static TraceEvent CreateEvent(string type) =>
        new(
            DateTimeOffset.UtcNow,
            type,
            "run",
            new Dictionary<string, object?>());
}
