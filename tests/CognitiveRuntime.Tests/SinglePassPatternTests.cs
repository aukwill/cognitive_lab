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
    public void CreatePlan_ReturnsOnlyTheMainNode()
    {
        var plan = new SinglePassPattern().CreatePlan(
            new PatternPlanRequest("frame", null, null));

        var node = Assert.Single(plan.Nodes);
        Assert.Equal("main", node.Id);
        Assert.Equal(CognitiveRuntime.Core.Contracts.PhaseKind.Main, node.PhaseKind);
        Assert.Empty(node.DependencyNodeIds);
        Assert.Empty(node.ContextNodeIds);
        Assert.Equal(node.Id, plan.AuthoritativeNodeId);
        Assert.Equal([node.Id], plan.EvalProfile.RequiredNodeIds);
        Assert.False(plan.EvalProfile.EvaluateLoopEfficacy);
    }
}
