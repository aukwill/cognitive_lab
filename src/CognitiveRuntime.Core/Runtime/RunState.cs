using System.Collections.Immutable;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Core.Runtime;

public sealed record RunState(
    string RunId,
    PatternExecutionPlan Plan,
    RunArtifactPaths Artifacts,
    RunLifecycleStatus LifecycleStatus,
    ImmutableDictionary<string, LoadedMode> LoadedModes,
    ImmutableArray<ExecutionNodeState> ExecutionNodes,
    ImmutableArray<RunArtifactState> ArtifactLedger,
    PatternExecutionResult? Execution = null,
    EvalReport? EvalReport = null,
    RunOutcome? Outcome = null,
    RunLifecycleTransition? LastTransition = null);

public static class RunStateUpdates
{
    public static RunState Create(
        string runId,
        PatternExecutionPlan plan,
        RunArtifactPaths artifacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(artifacts);

        return new RunState(
            runId,
            plan,
            artifacts,
            RunLifecycleStatus.Created,
            ImmutableDictionary<string, LoadedMode>.Empty,
            ExecutionNodeStateFactory.CreatePending(plan),
            CreatePlannedLedger(artifacts));
    }

    private static ImmutableArray<RunArtifactState> CreatePlannedLedger(
        RunArtifactPaths artifacts) =>
        new[]
        {
            artifacts.InputPath,
            artifacts.ResultPath,
            artifacts.PatternPath,
            artifacts.RunSummaryPath,
            artifacts.TracePath,
            artifacts.EvalReportPath,
            artifacts.RunManifestPath
        }
        .Select(path =>
            new RunArtifactState(
                Path.GetFileName(path),
                RunArtifactStatus.Planned))
        .ToImmutableArray();

    /// <summary>Records that a required artifact completed successfully.</summary>
    public static RunState MarkArtifactWritten(RunState state, string name) =>
        SetArtifactStatus(Require(state), name, RunArtifactStatus.Written);

    /// <summary>Records that a required artifact write was attempted but failed.</summary>
    public static RunState MarkArtifactFailed(RunState state, string name) =>
        SetArtifactStatus(Require(state), name, RunArtifactStatus.Failed);

    private static RunState SetArtifactStatus(
        RunState state,
        string name,
        RunArtifactStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var entry = new RunArtifactState(name, status);
        for (var index = 0; index < state.ArtifactLedger.Length; index++)
        {
            if (string.Equals(
                    state.ArtifactLedger[index].Name,
                    name,
                    StringComparison.Ordinal))
            {
                return state with
                {
                    ArtifactLedger = state.ArtifactLedger.SetItem(index, entry)
                };
            }
        }

        return state with
        {
            ArtifactLedger = state.ArtifactLedger.Add(entry)
        };
    }

    public static RunState Start(RunState state) =>
        RunLifecycleStateMachine.Transition(
            Require(state),
            RunLifecycleStatus.Running);

    public static RunState UpdateExecutionNodes(
        RunState state,
        IReadOnlyList<ExecutionNodeState> executionNodes)
    {
        ArgumentNullException.ThrowIfNull(executionNodes);
        state = Require(state);
        RequireStatus(
            state,
            RunLifecycleStatus.Running,
            "update execution nodes");
        var requiredNodeIds = state.Plan.Nodes
            .Select(node => node.Id)
            .ToArray();
        var suppliedNodeIds = executionNodes
            .Select(node => node.NodeId)
            .ToArray();
        if (!requiredNodeIds.SequenceEqual(
                suppliedNodeIds,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Execution node state must match the resolved pattern plan.");
        }

        return state with
        {
            ExecutionNodes = executionNodes.ToImmutableArray()
        };
    }

    public static RunState CompleteExecution(
        RunState state,
        PatternExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        var transitioned = RunLifecycleStateMachine.Transition(
            Require(state),
            RunLifecycleStatus.ExecutionCompleted);
        return transitioned with
        {
            LoadedModes = execution.LoadedModes.ToImmutableDictionary(
                StringComparer.Ordinal),
            ExecutionNodes = execution.NodeStates.ToImmutableArray(),
            Execution = execution
        };
    }

    public static RunState BeginEvaluation(RunState state) =>
        RunLifecycleStateMachine.Transition(
            Require(state),
            RunLifecycleStatus.Evaluating);

    public static RunState CompleteEvaluation(
        RunState state,
        EvalReport evalReport)
    {
        ArgumentNullException.ThrowIfNull(evalReport);
        var transitioned = RunLifecycleStateMachine.Transition(
            Require(state),
            RunLifecycleStatus.Finalizing);
        return transitioned with
        {
            EvalReport = evalReport
        };
    }

    public static RunState Succeed(RunState state, RunOutcome outcome) =>
        RunLifecycleStateMachine.Transition(
            Require(state),
            RunLifecycleStatus.Succeeded,
            outcome);

    public static RunState Fail(RunState state) =>
        RunLifecycleStateMachine.Transition(
            Require(state),
            RunLifecycleStatus.Failed,
            RunOutcome.RuntimeFailed);

    public static RunState Cancel(RunState state) =>
        RunLifecycleStateMachine.Transition(
            Require(state),
            RunLifecycleStatus.Cancelled,
            RunOutcome.Cancelled);

    private static RunState Require(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state;
    }

    private static void RequireStatus(
        RunState state,
        RunLifecycleStatus required,
        string operation)
    {
        if (state.LifecycleStatus != required)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} while run lifecycle is " +
                $"'{state.LifecycleStatus}'; expected '{required}'.");
        }
    }
}
