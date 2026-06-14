using CognitiveRuntime.Core.Contracts;
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
    public void CreatePlan_ReturnsTypedNodesInRuntimeOrder()
    {
        var plan = new CriticRevisionPattern().CreatePlan(
            new PatternPlanRequest("frame", null, null));

        Assert.Equal(
            [PhaseKind.Main, PhaseKind.Critic, PhaseKind.Revision],
            plan.Nodes.Select(node => node.PhaseKind));
        Assert.Equal(["main", "critic", "revision"], plan.Nodes.Select(node => node.Id));
        Assert.Empty(plan.Nodes[0].ContextNodeIds);
        Assert.Equal(["main"], plan.Nodes[1].ContextNodeIds);
        Assert.Equal(["main", "critic"], plan.Nodes[2].ContextNodeIds);
        Assert.Equal("revision", plan.AuthoritativeNodeId);
        Assert.Equal(
            ["main", "critic", "revision"],
            plan.EvalProfile.RequiredNodeIds);
        Assert.True(plan.EvalProfile.EvaluateLoopEfficacy);
    }

    [Fact]
    public void CreatePlan_IsDataOnlyAndCarriesModeSource()
    {
        var plan = new CriticRevisionPattern().CreatePlan(
            new PatternPlanRequest("lens", "warcraft", null));

        var source = Assert.Single(plan.ModeSources);
        Assert.Equal("primary", source.Id);
        Assert.Equal("lens", source.ModeName);
        Assert.Equal("warcraft", source.Lens);
        Assert.All(plan.Nodes, node => Assert.Equal(source.Id, node.ModeSourceId));
    }
}
