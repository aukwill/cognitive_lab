using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Tests;

public sealed class LinearPipelinePatternTests
{
    [Fact]
    public void CreatePlan_DeclaresOrderedStagesAndStableNodes()
    {
        var plan = new LinearPipelinePattern().CreatePlan(
            new PatternPlanRequest(
                "pipeline",
                null,
                ["frame", "challenge"]));

        Assert.Equal("linear-pipeline", plan.PatternName);
        Assert.Equal(
            ["frame", "challenge"],
            plan.ModeSources.Select(source => source.ModeName));
        Assert.Equal(2, plan.Stages.Count);
        Assert.Equal(
            [
                "stage-01.main",
                "stage-01.critic",
                "stage-01.revision",
                "stage-02.main",
                "stage-02.critic",
                "stage-02.revision"
            ],
            plan.Nodes.Select(node => node.Id));
        Assert.Equal("stage-02.revision", plan.AuthoritativeNodeId);
        Assert.Equal(
            ["stage-02.main", "stage-02.critic", "stage-02.revision"],
            plan.EvalProfile.RequiredNodeIds);
    }

    [Fact]
    public void CreatePlan_PassesPriorStageAuthorityAsNextStageInput()
    {
        var plan = new LinearPipelinePattern().CreatePlan(
            new PatternPlanRequest(
                "pipeline",
                null,
                ["frame", "challenge"]));

        var firstStage = plan.Stages[0];
        var secondStage = plan.Stages[1];

        Assert.Null(firstStage.InputNodeId);
        Assert.Equal(firstStage.AuthoritativeNodeId, secondStage.InputNodeId);
        Assert.All(
            secondStage.NodeIds.Select(
                nodeId => plan.Nodes.Single(node => node.Id == nodeId)),
            node =>
            {
                Assert.Equal(firstStage.AuthoritativeNodeId, node.InputNodeId);
                Assert.Contains(firstStage.AuthoritativeNodeId, node.DependencyNodeIds);
            });
    }

    [Fact]
    public void CreatePlan_DeclaresContextWithinEachStage()
    {
        var plan = new LinearPipelinePattern().CreatePlan(
            new PatternPlanRequest(
                "pipeline",
                null,
                ["frame", "challenge"]));

        foreach (var stage in plan.Stages)
        {
            var nodes = stage.NodeIds
                .Select(nodeId => plan.Nodes.Single(node => node.Id == nodeId))
                .ToArray();

            Assert.Equal(PhaseKind.Main, nodes[0].PhaseKind);
            Assert.Empty(nodes[0].ContextNodeIds);
            Assert.Equal([nodes[0].Id], nodes[1].ContextNodeIds);
            Assert.Equal([nodes[0].Id, nodes[1].Id], nodes[2].ContextNodeIds);
        }
    }

    [Fact]
    public void CreatePlan_ThrowsForEmptyOrBlankStageList()
    {
        var pattern = new LinearPipelinePattern();

        Assert.Throws<ArgumentException>(
            () => pattern.CreatePlan(new PatternPlanRequest("pipeline", null, [])));
        Assert.Throws<ArgumentException>(
            () => pattern.CreatePlan(
                new PatternPlanRequest("pipeline", null, ["frame", ""])));
    }
}
