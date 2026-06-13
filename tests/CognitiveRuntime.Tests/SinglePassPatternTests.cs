using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Tests;

public sealed class SinglePassPatternTests
{
    [Fact]
    public void Name_IsSinglePass()
    {
        var pattern = new SinglePassPattern();

        Assert.Equal("single-pass", pattern.Name);
    }

    [Fact]
    public async Task Plan_ReturnsOnlyTheMainPhase()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var mode = await new FileModeLoader(workspace.ModesRoot).LoadAsync("frame");

        var steps = new SinglePassPattern().Plan(mode);

        var step = Assert.Single(steps);
        Assert.Equal(PhaseKind.Main, step.Kind);
        Assert.Equal(
            mode.Phases.Single(phase => phase.Kind == PhaseKind.Main).Name,
            step.Name);
    }

    [Fact]
    public async Task SelectContext_ReturnsCompletedResultsUnchanged()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var mode = await new FileModeLoader(workspace.ModesRoot).LoadAsync("frame");
        var pattern = new SinglePassPattern();
        var step = pattern.Plan(mode)[0];

        var completedResults = Array.Empty<PhaseResult>();

        var context = pattern.SelectContext(step, completedResults);

        Assert.Equal(completedResults, context);
    }
}
