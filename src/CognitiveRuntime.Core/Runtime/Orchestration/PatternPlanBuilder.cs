using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

internal static class PatternPlanBuilder
{
    public static PatternExecutionPlan CreateSinglePass(
        string patternName,
        PatternPlanRequest request)
    {
        ValidateModeName(request.ModeName);

        const string modeSourceId = "primary";
        var node = new PatternExecutionNode(
            "main",
            PatternNodeKind.Phase,
            modeSourceId,
            PhaseKind.Main,
            [],
            []);

        return new PatternExecutionPlan(
            patternName,
            [new PatternModeSource(modeSourceId, request.ModeName, request.Lens)],
            [node],
            [],
            node.Id,
            new PatternEvalProfile([node.Id], EvaluateLoopEfficacy: false));
    }

    public static PatternExecutionPlan CreateCriticRevision(
        string patternName,
        PatternPlanRequest request)
    {
        ValidateModeName(request.ModeName);

        const string modeSourceId = "primary";
        var nodes = CreateCriticRevisionNodes(modeSourceId);

        return new PatternExecutionPlan(
            patternName,
            [new PatternModeSource(modeSourceId, request.ModeName, request.Lens)],
            nodes,
            [],
            nodes[^1].Id,
            new PatternEvalProfile(
                nodes.Select(node => node.Id).ToArray(),
                EvaluateLoopEfficacy: true));
    }

    public static PatternExecutionPlan CreateLinearPipeline(
        string patternName,
        PatternPlanRequest request)
    {
        var stageModeNames = request.PipelineStages;
        if (stageModeNames is null || stageModeNames.Count == 0)
        {
            throw new ArgumentException(
                "A linear pipeline requires at least one stage.",
                nameof(request));
        }

        if (stageModeNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Pipeline stage mode names cannot be null or blank.",
                nameof(request));
        }

        var modeSources = new List<PatternModeSource>(stageModeNames.Count);
        var nodes = new List<PatternExecutionNode>(stageModeNames.Count * 3);
        var stages = new List<PatternStage>(stageModeNames.Count);
        string? priorStageOutputNodeId = null;

        for (var index = 0; index < stageModeNames.Count; index++)
        {
            var stageIndex = index + 1;
            var stageId = $"stage-{stageIndex:D2}";
            var modeSourceId = stageId;
            var stageNodes = CreateCriticRevisionNodes(
                modeSourceId,
                $"{stageId}.",
                stageId,
                priorStageOutputNodeId);

            modeSources.Add(
                new PatternModeSource(modeSourceId, stageModeNames[index]));
            nodes.AddRange(stageNodes);
            stages.Add(
                new PatternStage(
                    stageId,
                    stageIndex,
                    modeSourceId,
                    stageNodes.Select(node => node.Id).ToArray(),
                    stageNodes[^1].Id,
                    priorStageOutputNodeId));

            priorStageOutputNodeId = stageNodes[^1].Id;
        }

        var evalNodeIds = stages[^1].NodeIds;
        return new PatternExecutionPlan(
            patternName,
            modeSources,
            nodes,
            stages,
            priorStageOutputNodeId!,
            new PatternEvalProfile(
                evalNodeIds,
                EvaluateLoopEfficacy: true));
    }

    private static IReadOnlyList<PatternExecutionNode> CreateCriticRevisionNodes(
        string modeSourceId,
        string idPrefix = "",
        string? stageId = null,
        string? inputNodeId = null)
    {
        var mainId = $"{idPrefix}main";
        var criticId = $"{idPrefix}critic";
        var revisionId = $"{idPrefix}revision";
        IReadOnlyList<string> inputDependencies =
            inputNodeId is null ? [] : new[] { inputNodeId };

        return
        [
            new PatternExecutionNode(
                mainId,
                PatternNodeKind.Phase,
                modeSourceId,
                PhaseKind.Main,
                inputDependencies,
                [],
                inputNodeId,
                stageId),
            new PatternExecutionNode(
                criticId,
                PatternNodeKind.Phase,
                modeSourceId,
                PhaseKind.Critic,
                Append(inputDependencies, mainId),
                [mainId],
                inputNodeId,
                stageId),
            new PatternExecutionNode(
                revisionId,
                PatternNodeKind.Phase,
                modeSourceId,
                PhaseKind.Revision,
                Append(inputDependencies, mainId, criticId),
                [mainId, criticId],
                inputNodeId,
                stageId)
        ];
    }

    private static IReadOnlyList<string> Append(
        IReadOnlyList<string> values,
        params string[] additional) =>
        values.Concat(additional).ToArray();

    private static void ValidateModeName(string modeName) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(modeName);
}
