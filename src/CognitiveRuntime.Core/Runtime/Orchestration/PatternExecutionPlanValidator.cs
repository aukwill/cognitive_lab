namespace CognitiveRuntime.Core.Runtime.Orchestration;

public sealed class PatternExecutionPlanValidator
{
    public void Validate(PatternExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(plan.PatternName);
        ArgumentNullException.ThrowIfNull(plan.ModeSources);
        ArgumentNullException.ThrowIfNull(plan.Nodes);
        ArgumentNullException.ThrowIfNull(plan.Stages);
        ArgumentNullException.ThrowIfNull(plan.EvalProfile);

        if (plan.Nodes.Count == 0)
        {
            throw Invalid(plan, "must declare at least one executable node.");
        }

        var modeSources = CreateUniqueLookup(
            plan,
            plan.ModeSources,
            source => source.Id,
            "mode source");
        foreach (var source in plan.ModeSources)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source.ModeName);
        }

        var nodes = CreateUniqueLookup(
            plan,
            plan.Nodes,
            node => node.Id,
            "node");
        var priorNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in plan.Nodes)
        {
            if (!Enum.IsDefined(node.Kind))
            {
                throw Invalid(plan, $"node '{node.Id}' has unknown kind '{node.Kind}'.");
            }

            if (!Enum.IsDefined(node.PhaseKind))
            {
                throw Invalid(
                    plan,
                    $"node '{node.Id}' has unknown phase kind '{node.PhaseKind}'.");
            }

            if (!modeSources.ContainsKey(node.ModeSourceId))
            {
                throw Invalid(
                    plan,
                    $"node '{node.Id}' references undeclared mode source " +
                    $"'{node.ModeSourceId}'.");
            }

            ValidateDeclaredPriorNodes(
                plan,
                node,
                node.DependencyNodeIds,
                priorNodeIds,
                nodes,
                "dependency");
            ValidateDeclaredPriorNodes(
                plan,
                node,
                node.ContextNodeIds,
                priorNodeIds,
                nodes,
                "context");

            if (node.ContextNodeIds.Any(
                    contextNodeId => !node.DependencyNodeIds.Contains(
                        contextNodeId,
                        StringComparer.Ordinal)))
            {
                throw Invalid(
                    plan,
                    $"node '{node.Id}' has a context input that is not also a dependency.");
            }

            if (node.InputNodeId is not null)
            {
                ValidateDeclaredPriorNodes(
                    plan,
                    node,
                    [node.InputNodeId],
                    priorNodeIds,
                    nodes,
                    "input");

                if (!node.DependencyNodeIds.Contains(
                        node.InputNodeId,
                        StringComparer.Ordinal))
                {
                    throw Invalid(
                        plan,
                        $"node '{node.Id}' input '{node.InputNodeId}' is not also a dependency.");
                }
            }

            priorNodeIds.Add(node.Id);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(plan.AuthoritativeNodeId);
        if (!nodes.ContainsKey(plan.AuthoritativeNodeId))
        {
            throw Invalid(
                plan,
                $"authoritative node '{plan.AuthoritativeNodeId}' is not declared.");
        }

        ValidateEvalProfile(plan, nodes);
        ValidateStages(plan, nodes, modeSources);
    }

    private static void ValidateEvalProfile(
        PatternExecutionPlan plan,
        IReadOnlyDictionary<string, PatternExecutionNode> nodes)
    {
        if (plan.EvalProfile.RequiredNodeIds.Count == 0)
        {
            throw Invalid(plan, "eval profile must require at least one node.");
        }

        foreach (var nodeId in plan.EvalProfile.RequiredNodeIds)
        {
            if (!nodes.ContainsKey(nodeId))
            {
                throw Invalid(
                    plan,
                    $"eval profile references undeclared node '{nodeId}'.");
            }
        }

        if (!plan.EvalProfile.RequiredNodeIds.Contains(
                plan.AuthoritativeNodeId,
                StringComparer.Ordinal))
        {
            throw Invalid(
                plan,
                "eval profile does not include the authoritative node.");
        }
    }

    private static void ValidateStages(
        PatternExecutionPlan plan,
        IReadOnlyDictionary<string, PatternExecutionNode> nodes,
        IReadOnlyDictionary<string, PatternModeSource> modeSources)
    {
        var stages = CreateUniqueLookup(
            plan,
            plan.Stages,
            stage => stage.Id,
            "stage");
        var assignedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var priorStageNodeIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < plan.Stages.Count; index++)
        {
            var stage = plan.Stages[index];
            if (stage.Index != index + 1)
            {
                throw Invalid(
                    plan,
                    $"stage '{stage.Id}' has index {stage.Index}; expected {index + 1}.");
            }

            if (!modeSources.ContainsKey(stage.ModeSourceId))
            {
                throw Invalid(
                    plan,
                    $"stage '{stage.Id}' references undeclared mode source " +
                    $"'{stage.ModeSourceId}'.");
            }

            if (stage.NodeIds.Count == 0)
            {
                throw Invalid(plan, $"stage '{stage.Id}' has no executable nodes.");
            }

            foreach (var nodeId in stage.NodeIds)
            {
                if (!nodes.TryGetValue(nodeId, out var node))
                {
                    throw Invalid(
                        plan,
                        $"stage '{stage.Id}' references undeclared node '{nodeId}'.");
                }

                if (!assignedNodeIds.Add(nodeId))
                {
                    throw Invalid(
                        plan,
                        $"node '{nodeId}' belongs to more than one stage.");
                }

                if (!string.Equals(node.StageId, stage.Id, StringComparison.Ordinal))
                {
                    throw Invalid(
                        plan,
                        $"node '{nodeId}' does not declare stage '{stage.Id}'.");
                }

                if (!string.Equals(
                        node.ModeSourceId,
                        stage.ModeSourceId,
                        StringComparison.Ordinal))
                {
                    throw Invalid(
                        plan,
                        $"node '{nodeId}' and stage '{stage.Id}' use different mode sources.");
                }

                if (!string.Equals(
                        node.InputNodeId,
                        stage.InputNodeId,
                        StringComparison.Ordinal))
                {
                    throw Invalid(
                        plan,
                        $"node '{nodeId}' input does not match stage '{stage.Id}'.");
                }
            }

            if (!stage.NodeIds.Contains(
                    stage.AuthoritativeNodeId,
                    StringComparer.Ordinal))
            {
                throw Invalid(
                    plan,
                    $"stage '{stage.Id}' authoritative node " +
                    $"'{stage.AuthoritativeNodeId}' is outside the stage.");
            }

            if (stage.InputNodeId is not null &&
                !priorStageNodeIds.Contains(stage.InputNodeId))
            {
                throw Invalid(
                    plan,
                    $"stage '{stage.Id}' input '{stage.InputNodeId}' is not from a prior stage.");
            }

            priorStageNodeIds.UnionWith(stage.NodeIds);
        }

        var stagedNodeIds = plan.Nodes
            .Where(node => node.StageId is not null)
            .Select(node => node.Id)
            .ToArray();
        if (stagedNodeIds.Any(nodeId => !assignedNodeIds.Contains(nodeId)))
        {
            throw Invalid(plan, "one or more staged nodes are not assigned to a stage.");
        }

        if (plan.Nodes.Any(node =>
                node.StageId is not null && !stages.ContainsKey(node.StageId)))
        {
            throw Invalid(plan, "one or more nodes reference an undeclared stage.");
        }

        if (plan.Stages.Count > 0)
        {
            var executionOrder = plan.Stages
                .SelectMany(stage => stage.NodeIds)
                .ToArray();
            var declaredOrder = plan.Nodes.Select(node => node.Id).ToArray();

            if (!executionOrder.SequenceEqual(declaredOrder, StringComparer.Ordinal))
            {
                throw Invalid(
                    plan,
                    "stage node order does not match the declared node order.");
            }
        }
    }

    private static void ValidateDeclaredPriorNodes(
        PatternExecutionPlan plan,
        PatternExecutionNode node,
        IReadOnlyList<string> referencedNodeIds,
        IReadOnlySet<string> priorNodeIds,
        IReadOnlyDictionary<string, PatternExecutionNode> allNodes,
        string relation)
    {
        foreach (var referencedNodeId in referencedNodeIds)
        {
            if (!allNodes.ContainsKey(referencedNodeId))
            {
                throw Invalid(
                    plan,
                    $"node '{node.Id}' {relation} references undeclared node " +
                    $"'{referencedNodeId}'.");
            }

            if (!priorNodeIds.Contains(referencedNodeId))
            {
                throw Invalid(
                    plan,
                    $"node '{node.Id}' {relation} references future node " +
                    $"'{referencedNodeId}'. Cycles and forward edges are unsupported.");
            }
        }
    }

    private static Dictionary<string, T> CreateUniqueLookup<T>(
        PatternExecutionPlan plan,
        IReadOnlyList<T> values,
        Func<T, string> getId,
        string label)
    {
        var lookup = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var id = getId(value);
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            if (!lookup.TryAdd(id, value))
            {
                throw Invalid(plan, $"contains duplicate {label} ID '{id}'.");
            }
        }

        return lookup;
    }

    private static InvalidOperationException Invalid(
        PatternExecutionPlan plan,
        string message) =>
        new($"Pattern plan '{plan.PatternName}' {message}");
}
