using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Tests;

public sealed class OrchestrationPatternTests
{
    [Fact]
    public void Name_IsCriticRevision()
    {
        var pattern = new CriticRevisionPattern();

        Assert.Equal("critic-revision", pattern.Name);
    }

    [Fact]
    public async Task Plan_ReturnsPhasesInManifestOrder()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var mode = await new FileModeLoader(workspace.ModesRoot).LoadAsync("frame");

        var steps = new CriticRevisionPattern().Plan(mode);

        Assert.Equal(mode.Phases.Count, steps.Count);
        for (var index = 0; index < mode.Phases.Count; index++)
        {
            Assert.Equal(mode.Phases[index].Name, steps[index].Name);
            Assert.Equal(mode.Phases[index].Kind, steps[index].Kind);
        }

        Assert.Equal(
            [PhaseKind.Main, PhaseKind.Critic, PhaseKind.Revision],
            steps.Select(step => step.Kind));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task SelectContext_ReturnsCompletedResultsUnchanged(int completedCount)
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var mode = await new FileModeLoader(workspace.ModesRoot).LoadAsync("frame");
        var pattern = new CriticRevisionPattern();
        var steps = pattern.Plan(mode);

        var completedResults = Enumerable.Range(0, completedCount)
            .Select(index => new PhaseResult(
                $"phase-{index}",
                PhaseKind.Main,
                $"content-{index}",
                "mock",
                null))
            .ToArray();

        var context = pattern.SelectContext(steps[0], completedResults);

        Assert.Equal(completedResults, context);
    }
}
