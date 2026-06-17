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

    public static PatternExecutionPlan CreateScatterGather(
        string patternName,
        PatternPlanRequest request)
    {
        ValidateModeName(request.ModeName);

        var branchModes = request.ScatterModes;
        if (branchModes is null || branchModes.Count == 0)
        {
            throw new ArgumentException(
                "Scatter-gather requires at least one scatter mode.",
                nameof(request));
        }

        if (branchModes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Scatter mode names cannot be null or blank.",
                nameof(request));
        }

        var modeSources = new List<PatternModeSource>(branchModes.Count + 1);
        var nodes = new List<PatternExecutionNode>(branchModes.Count + 1);
        var branchIds = new List<string>(branchModes.Count);

        for (var index = 0; index < branchModes.Count; index++)
        {
            var branchId = $"branch-{index + 1:D2}";
            modeSources.Add(new PatternModeSource(branchId, branchModes[index]));
            // Branches are independent by construction: no dependency or context
            // edge to one another, so the runtime is free to run them in any
            // order (and, in a later revision, concurrently).
            nodes.Add(
                new PatternExecutionNode(
                    branchId,
                    PatternNodeKind.Phase,
                    branchId,
                    PhaseKind.Main,
                    [],
                    []));
            branchIds.Add(branchId);
        }

        const string gatherSourceId = "gather";
        const string gatherId = "gather";
        modeSources.Add(
            new PatternModeSource(gatherSourceId, request.ModeName, request.Lens));
        // The gather depends on (and reads) every branch output, in declared
        // order, so context assembly is deterministic regardless of completion
        // order, and a missing branch fails context assembly rather than being
        // silently dropped.
        nodes.Add(
            new PatternExecutionNode(
                gatherId,
                PatternNodeKind.Phase,
                gatherSourceId,
                PhaseKind.Main,
                branchIds,
                branchIds));

        return new PatternExecutionPlan(
            patternName,
            modeSources,
            nodes,
            [],
            gatherId,
            new PatternEvalProfile([gatherId], EvaluateLoopEfficacy: false));
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
