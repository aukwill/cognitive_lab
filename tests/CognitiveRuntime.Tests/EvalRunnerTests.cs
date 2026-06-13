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
            "## Problem\n\nA sufficiently long result without the objective heading.");

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
            CreateEvent("run.completed")
        };
        var runner = new EvalRunner(new OutputContractValidator());

        var report = await runner.EvaluateAsync(
            new EvalContext(artifacts, mode, events));

        Assert.False(report.Passed);
        var contractCheck = Assert.Single(
            report.Checks,
            check => check.Name == "output contract satisfied");
        Assert.False(contractCheck.Passed);
        Assert.Contains("## Objective", contractCheck.Details);
    }

    private static TraceEvent CreateEvent(string type) =>
        new(
            DateTimeOffset.UtcNow,
            type,
            "run",
            new Dictionary<string, object?>());
}
