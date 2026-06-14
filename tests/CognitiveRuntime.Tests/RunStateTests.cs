using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Tests;

public sealed class RunStateTests
{
    [Fact]
    public void Create_CapturesRunIdentityPlanArtifactsAndCreatedStatus()
    {
        var plan = CreatePlan();
        var artifacts = CreateArtifacts();

        var state = RunStateUpdates.Create("run-001", plan, artifacts);

        Assert.Equal("run-001", state.RunId);
        Assert.Same(plan, state.Plan);
        Assert.Same(artifacts, state.Artifacts);
        Assert.Equal(RunLifecycleStatus.Created, state.LifecycleStatus);
        Assert.Empty(state.LoadedModes);
        var node = Assert.Single(state.ExecutionNodes);
        Assert.Equal("main", node.NodeId);
        Assert.Equal("main", node.PhaseName);
        Assert.Equal("frame", node.ModeName);
        Assert.Equal(ExecutionNodeStatus.Pending, node.Status);
        Assert.Null(node.Provider);
        Assert.Null(node.StartedAt);
        Assert.Null(state.Execution);
        Assert.Null(state.EvalReport);
        Assert.Null(state.Outcome);
    }

    [Fact]
    public void Updates_ReturnNewStatesWithoutMutatingPriorSnapshots()
    {
        var created = RunStateUpdates.Create(
            "run-001",
            CreatePlan(),
            CreateArtifacts());
        var running = RunStateUpdates.Start(created);
        var execution = CreateExecution(created.Plan);
        var completed = RunStateUpdates.CompleteExecution(running, execution);
        var evaluating = RunStateUpdates.BeginEvaluation(completed);
        var report = new EvalReport(
            [new EvalCheckResult("check", true, "passed")]);
        var finalizing = RunStateUpdates.CompleteEvaluation(evaluating, report);
        var succeeded = RunStateUpdates.Succeed(
            finalizing,
            RunOutcome.Success);

        Assert.Equal(RunLifecycleStatus.Created, created.LifecycleStatus);
        Assert.Equal(RunLifecycleStatus.Running, running.LifecycleStatus);
        Assert.Equal(
            new RunLifecycleTransition(
                RunLifecycleStatus.Created,
                RunLifecycleStatus.Running),
            running.LastTransition);
        Assert.Null(running.Execution);
        Assert.Equal(
            RunLifecycleStatus.ExecutionCompleted,
            completed.LifecycleStatus);
        Assert.Same(execution, completed.Execution);
        Assert.Equal(RunLifecycleStatus.Evaluating, evaluating.LifecycleStatus);
        Assert.Null(evaluating.EvalReport);
        Assert.Equal(RunLifecycleStatus.Finalizing, finalizing.LifecycleStatus);
        Assert.Same(report, finalizing.EvalReport);
        Assert.Equal(RunLifecycleStatus.Succeeded, succeeded.LifecycleStatus);
        Assert.Equal(RunOutcome.Success, succeeded.Outcome);
        Assert.Equal(
            new RunLifecycleTransition(
                RunLifecycleStatus.Finalizing,
                RunLifecycleStatus.Succeeded),
            succeeded.LastTransition);
        Assert.Null(finalizing.Outcome);
    }

    [Fact]
    public void CompleteExecution_TakesImmutableSnapshots()
    {
        var state = RunStateUpdates.Start(
            RunStateUpdates.Create(
                "run-001",
                CreatePlan(),
                CreateArtifacts()));
        var mode = CreateMode();
        var modes = new Dictionary<string, LoadedMode>
        {
            ["primary"] = mode
        };
        var nodeResults = new List<PatternNodeExecutionResult>
        {
            CreateNodeResult(state.Plan.Nodes[0], mode)
        };
        var phaseResult = nodeResults[0].PhaseResult;
        var execution = new PatternExecutionResult(
            state.Plan,
            modes,
            nodeResults,
            CreateCompletedNodeStates(state.Plan),
            [],
            mode,
            [phaseResult],
            phaseResult.Content);

        var completed = RunStateUpdates.CompleteExecution(state, execution);
        modes.Clear();
        nodeResults.Clear();

        Assert.Single(completed.LoadedModes);
        var node = Assert.Single(completed.ExecutionNodes);
        Assert.Equal("frame", completed.LoadedModes["primary"].Manifest.Name);
        Assert.Equal("main", node.NodeId);
        Assert.Equal(ExecutionNodeStatus.Completed, node.Status);
        Assert.Equal("mock", node.Provider);
        Assert.Equal("deterministic-template", node.Model);
        Assert.Equal(34, node.OutputLength);
    }

    [Fact]
    public void UpdateExecutionNodes_RejectsSnapshotsThatDoNotMatchThePlan()
    {
        var state = RunStateUpdates.Create(
            "run-001",
            CreatePlan(),
            CreateArtifacts());
        state = RunStateUpdates.Start(state);
        var invalid = state.ExecutionNodes
            .Select(node => node with { NodeId = "different" })
            .ToArray();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RunStateUpdates.UpdateExecutionNodes(state, invalid));

        Assert.Contains("must match", exception.Message);
    }

    [Fact]
    public void FailureAndCancellationProduceRuntimeOwnedOutcomes()
    {
        var state = RunStateUpdates.Create(
            "run-001",
            CreatePlan(),
            CreateArtifacts());

        var failed = RunStateUpdates.Fail(state);
        var cancelled = RunStateUpdates.Cancel(state);

        Assert.Equal(RunLifecycleStatus.Failed, failed.LifecycleStatus);
        Assert.Equal(RunOutcome.RuntimeFailed, failed.Outcome);
        Assert.Equal(
            new RunLifecycleTransition(
                RunLifecycleStatus.Created,
                RunLifecycleStatus.Failed),
            failed.LastTransition);
        Assert.Equal(RunLifecycleStatus.Cancelled, cancelled.LifecycleStatus);
        Assert.Equal(RunOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(
            new RunLifecycleTransition(
                RunLifecycleStatus.Created,
                RunLifecycleStatus.Cancelled),
            cancelled.LastTransition);
        Assert.Null(state.Outcome);
    }

    [Fact]
    public void Lifecycle_RejectsInvalidTransitions()
    {
        var created = RunStateUpdates.Create(
            "run-001",
            CreatePlan(),
            CreateArtifacts());

        var exception = Assert.Throws<InvalidOperationException>(
            () => RunStateUpdates.BeginEvaluation(created));

        Assert.Contains(
            "cannot transition from 'Created' to 'Evaluating'",
            exception.Message);
    }

    [Fact]
    public void Lifecycle_RejectsInvalidTerminalOutcome()
    {
        var finalizing = CreateFinalizingState();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RunStateUpdates.Succeed(
                finalizing,
                RunOutcome.RuntimeFailed));

        Assert.Contains(
            "does not accept outcome 'RuntimeFailed'",
            exception.Message);
    }

    [Fact]
    public void Lifecycle_TerminalStatesCannotTransitionAgain()
    {
        var created = RunStateUpdates.Create(
            "run-001",
            CreatePlan(),
            CreateArtifacts());
        var terminalStates = new[]
        {
            RunStateUpdates.Succeed(
                CreateFinalizingState(),
                RunOutcome.Success),
            RunStateUpdates.Fail(created),
            RunStateUpdates.Cancel(created)
        };

        Assert.All(
            terminalStates,
            terminalState =>
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => RunStateUpdates.Fail(terminalState));
                Assert.Contains(
                    "is terminal and cannot transition",
                    exception.Message);
            });
    }

    [Fact]
    public void RunState_DoesNotStoreProviderSpecificConfiguration()
    {
        var properties = typeof(RunState).GetProperties();

        Assert.DoesNotContain(
            properties,
            property => property.PropertyType == typeof(GitHubModelsOptions));
        Assert.DoesNotContain(
            properties,
            property => property.PropertyType == typeof(AzureFoundryOptions));
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains(
                "Token",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains(
                "Endpoint",
                StringComparison.OrdinalIgnoreCase));
    }

    private static PatternExecutionPlan CreatePlan() =>
        new SinglePassPattern().CreatePlan(
            new PatternPlanRequest("frame", null, null));

    private static PatternExecutionResult CreateExecution(
        PatternExecutionPlan plan)
    {
        var mode = CreateMode();
        var nodeResult = CreateNodeResult(plan.Nodes[0], mode);
        return new PatternExecutionResult(
            plan,
            new Dictionary<string, LoadedMode> { ["primary"] = mode },
            [nodeResult],
            CreateCompletedNodeStates(plan),
            [],
            mode,
            [nodeResult.PhaseResult],
            nodeResult.PhaseResult.Content);
    }

    private static RunState CreateFinalizingState()
    {
        var created = RunStateUpdates.Create(
            "run-001",
            CreatePlan(),
            CreateArtifacts());
        var running = RunStateUpdates.Start(created);
        var completed = RunStateUpdates.CompleteExecution(
            running,
            CreateExecution(created.Plan));
        var evaluating = RunStateUpdates.BeginEvaluation(completed);
        return RunStateUpdates.CompleteEvaluation(
            evaluating,
            new EvalReport(
                [new EvalCheckResult("check", true, "passed")]));
    }

    private static PatternNodeExecutionResult CreateNodeResult(
        PatternExecutionNode node,
        LoadedMode mode)
    {
        var phase = mode.Phases.Single(
            candidate => candidate.Kind == node.PhaseKind);
        var phaseResult = new PhaseResult(
            phase.Name,
            phase.Kind,
            "## Problem\n\nA runtime-owned loop.",
            "mock",
            "deterministic-template");

        return new PatternNodeExecutionResult(
            node,
            mode,
            phase,
            "input",
            [],
            phaseResult);
    }

    private static IReadOnlyList<ExecutionNodeState> CreateCompletedNodeStates(
        PatternExecutionPlan plan) =>
        RunStateUpdates.Create(
                "run-001",
                plan,
                CreateArtifacts())
            .ExecutionNodes
            .Select(
                node => node with
                {
                    Status = ExecutionNodeStatus.Completed,
                    Provider = "mock",
                    Model = "deterministic-template",
                    StartedAt = new DateTimeOffset(
                        2026,
                        6,
                        13,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    EndedAt = new DateTimeOffset(
                        2026,
                        6,
                        13,
                        12,
                        0,
                        1,
                        TimeSpan.Zero),
                    OutputLength = 34
                })
            .ToArray();

    private static LoadedMode CreateMode()
    {
        var phase = new LoadedPhase(
            "main",
            PhaseKind.Main,
            "Produce the output contract.");
        return new LoadedMode(
            "modes/frame",
            "# Frame",
            new ModeManifest
            {
                Name = "frame",
                Description = "Frame a problem.",
                Phases =
                [
                    new ModePhaseManifest
                    {
                        Name = phase.Name,
                        Kind = phase.Kind,
                        Prompt = "prompts/main.md"
                    }
                ]
            },
            [phase]);
    }

    private static RunArtifactPaths CreateArtifacts() =>
        new(
            "outputs/run-001",
            "outputs/run-001/input.md",
            "outputs/run-001/result.md",
            "outputs/run-001/trace.json",
            "outputs/run-001/run_summary.md",
            "outputs/run-001/eval_report.md",
            "outputs/run-001/pattern.md");
}
