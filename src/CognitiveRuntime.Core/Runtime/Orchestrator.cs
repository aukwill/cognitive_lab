using System.Text;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Persistence;
using CognitiveRuntime.Core.Runtime.Orchestration;
using CognitiveRuntime.Core.Views;

namespace CognitiveRuntime.Core.Runtime;

public sealed class Orchestrator
{
    private readonly IModelClientFactory _modelClientFactory;
    private readonly IArtifactWriter _artifactWriter;
    private readonly ITraceSessionFactory _traceSessionFactory;
    private readonly IEvalRunner _evalRunner;
    private readonly IRunViewWriter _runViewWriter;
    private readonly IOrchestrationPatternFactory _patternFactory;
    private readonly PatternExecutionPlanValidator _planValidator;
    private readonly PatternExecutor _patternExecutor;
    private readonly IRunStateStore _runStateStore;
    private readonly TimeProvider _timeProvider;
    private readonly IRunIdGenerator _runIdGenerator;

    public Orchestrator(
        IModelClientFactory modelClientFactory,
        IArtifactWriter artifactWriter,
        ITraceSessionFactory traceSessionFactory,
        IEvalRunner evalRunner,
        IRunViewWriter runViewWriter,
        IOrchestrationPatternFactory patternFactory,
        PatternExecutionPlanValidator planValidator,
        PatternExecutor patternExecutor,
        IRunStateStore? runStateStore = null,
        TimeProvider? timeProvider = null,
        IRunIdGenerator? runIdGenerator = null)
    {
        _modelClientFactory = modelClientFactory;
        _artifactWriter = artifactWriter;
        _traceSessionFactory = traceSessionFactory;
        _evalRunner = evalRunner;
        _runViewWriter = runViewWriter;
        _patternFactory = patternFactory;
        _planValidator = planValidator;
        _patternExecutor = patternExecutor;
        _runStateStore = runStateStore ?? new NullRunStateStore();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _runIdGenerator = runIdGenerator ?? new GuidRunIdGenerator();
    }

    public async Task<RunResult> RunAsync(
        RunRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var pattern = _patternFactory.Resolve(request.Pattern);
        var plan = pattern.CreatePlan(
            new PatternPlanRequest(
                request.ModeName,
                request.Lens,
                request.PipelineStages));
        _planValidator.Validate(plan);

        var runId = _runIdGenerator.GenerateRunId();
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var artifacts = await _artifactWriter.PrepareRunAsync(
            request.OutputRoot,
            request.ModeName,
            runId,
            cancellationToken);
        var trace = await _traceSessionFactory.CreateAsync(
            runId,
            artifacts.TracePath,
            cancellationToken);
        var state = RunStateUpdates.Create(runId, plan, artifacts);
        var createdAt = _timeProvider.GetUtcNow();

        try
        {
            await PersistRunAsync(
                request,
                state,
                createdAt,
                cancellationToken);
            state = RunStateUpdates.Start(state);
            await PersistRunAsync(
                request,
                state,
                createdAt,
                cancellationToken);

            await trace.EmitAsync(
                TraceEventNames.RunStarted,
                new Dictionary<string, object?>
                {
                    ["mode"] = request.ModeName,
                    ["provider"] = request.ModelProvider,
                    // Run directory name only, so trace.json stays
                    // location-independent and leaks no machine username.
                    ["outputDirectory"] = Path.GetFileName(artifacts.RunDirectory),
                    ["pattern"] = plan.PatternName,
                    ["stages"] = GetStageModeNames(plan)
                },
                cancellationToken);

            await WriteArtifactAsync(
                artifacts,
                ArtifactKind.Input,
                $"# Input{Environment.NewLine}{Environment.NewLine}{request.Input.Trim()}{Environment.NewLine}",
                trace,
                cancellationToken);

            await trace.EmitAsync(
                TraceEventNames.PatternStarted,
                CreatePatternStartedData(plan),
                cancellationToken);

            var modelClient = _modelClientFactory.Resolve(request.ModelProvider);
            var execution = await _patternExecutor.ExecuteAsync(
                state.RunId,
                request.Input,
                state.Plan,
                modelClient,
                state.Artifacts,
                trace,
                nodeStates =>
                    state = RunStateUpdates.UpdateExecutionNodes(
                        state,
                        nodeStates),
                cancellationToken);
            state = RunStateUpdates.CompleteExecution(state, execution);
            await PersistRunAsync(
                request,
                state,
                createdAt,
                cancellationToken);

            await trace.EmitAsync(
                TraceEventNames.PatternCompleted,
                CreatePatternCompletedData(state.Execution!),
                cancellationToken);

            await WriteArtifactAsync(
                state.Artifacts,
                ArtifactKind.Pattern,
                PatternMarkdownRenderer.Render(state.Execution!),
                trace,
                cancellationToken);

            await WriteArtifactAsync(
                state.Artifacts,
                ArtifactKind.Result,
                state.Execution!.ResultContent,
                trace,
                cancellationToken);

            // result.md above remains the authoritative composition; these are
            // the individual phase outputs persisted for inspection.
            await WritePhaseOutputsAsync(state, trace, cancellationToken);

            var summary = RenderRunSummary(
                request,
                state);
            await WriteArtifactAsync(
                state.Artifacts,
                ArtifactKind.RunSummary,
                summary,
                trace,
                cancellationToken);

            // Reserve the self-referential eval artifact before checking artifact existence.
            await _artifactWriter.WriteAsync(
                state.Artifacts,
                ArtifactKind.EvalReport,
                "# Evaluation Report\n\nEvaluation has not run yet.\n",
                cancellationToken);
            await trace.EmitAsync(
                TraceEventNames.ArtifactReserved,
                new Dictionary<string, object?>
                {
                    ["name"] = Path.GetFileName(state.Artifacts.EvalReportPath)
                },
                cancellationToken);

            // This event marks completion of the cognitive loop. Post-run eval follows.
            await trace.EmitAsync(
                TraceEventNames.RunCompleted,
                CreateRunCompletedData(state.Execution!),
                cancellationToken);

            state = RunStateUpdates.BeginEvaluation(state);
            await PersistRunAsync(
                request,
                state,
                createdAt,
                cancellationToken);
            var evalStartedAt = _timeProvider.GetUtcNow();
            await trace.EmitAsync(
                TraceEventNames.EvalStarted,
                new Dictionary<string, object?>(),
                cancellationToken);

            var evalReport = await _evalRunner.EvaluateAsync(
                new EvalContext(
                    state.Artifacts,
                    state.Execution!.AuthoritativeMode,
                    trace.Events,
                    state.Execution.EvalPhaseResults,
                    CreateEvalPlan(state.Plan),
                    state.Execution.NodeResults.ToDictionary(
                        nodeResult => nodeResult.Node.Id,
                        nodeResult => nodeResult.PhaseResult,
                        StringComparer.Ordinal),
                    state.Execution.NodeResults.Single(
                            nodeResult => string.Equals(
                                nodeResult.Node.Id,
                                state.Plan.AuthoritativeNodeId,
                                StringComparison.Ordinal))
                        .PhaseResult
                        .Content),
                cancellationToken);
            state = RunStateUpdates.CompleteEvaluation(state, evalReport);
            await PersistRunAsync(
                request,
                state,
                createdAt,
                cancellationToken);

            var evalCompletedAt = _timeProvider.GetUtcNow();
            await trace.EmitAsync(
                TraceEventNames.EvalCompleted,
                new Dictionary<string, object?>
                {
                    ["passed"] = evalReport.Passed,
                    ["checkCount"] = evalReport.Checks.Count,
                    ["durationMs"] = RuntimeDuration.GetMilliseconds(
                        evalStartedAt,
                        evalCompletedAt)
                },
                cancellationToken);

            await WriteArtifactAsync(
                state.Artifacts,
                ArtifactKind.EvalReport,
                EvalRunner.RenderMarkdown(state.EvalReport!),
                trace,
                cancellationToken);

            string? htmlViewPath = null;
            if (request.WriteHtmlView)
            {
                var viewModel = RunViewModelFactory.Create(
                    request,
                    state,
                    trace.Events);
                htmlViewPath = await _runViewWriter.WriteAsync(
                    viewModel,
                    cancellationToken);
                await TraceArtifactWrittenAsync(
                    trace,
                    htmlViewPath,
                    "html",
                    cancellationToken);
            }

            var outcome = state.EvalReport!.Passed
                ? RunOutcome.Success
                : RunOutcome.EvalFailed;
            var succeededState = RunStateUpdates.Succeed(state, outcome);
            await PersistRunAsync(
                request,
                succeededState,
                createdAt,
                cancellationToken);
            var endedAt = _timeProvider.GetUtcNow();
            await WriteRunManifestAsync(
                request,
                succeededState,
                createdAt,
                endedAt,
                htmlViewPath,
                failure: null,
                trace,
                cancellationToken);
            await EmitTerminalEventAsync(
                trace,
                succeededState,
                createdAt,
                endedAt,
                new Dictionary<string, object?>
                {
                    ["evalPassed"] = state.EvalReport.Passed
                },
                cancellationToken);
            state = succeededState;

            return new RunResult(
                state.RunId,
                state.Artifacts.RunDirectory,
                state.Artifacts.ResultPath,
                state.Artifacts.TracePath,
                state.Artifacts.EvalReportPath,
                state.Outcome!.Value,
                htmlViewPath);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            state = RunStateUpdates.Cancel(state);
            await TryPersistRunAsync(
                request,
                state,
                createdAt);
            await RecordCancellationAsync(
                trace,
                state,
                request,
                createdAt);
            throw;
        }
        catch (Exception exception) when (exception is not RuntimeRunException)
        {
            state = RunStateUpdates.Fail(state);
            var failure = RuntimeFailureFactory.CreateForRun(exception, state);
            await TryPersistRunAsync(
                request,
                state,
                createdAt);
            await RecordFailureAsync(
                trace,
                state,
                request,
                createdAt,
                failure);
            throw new RuntimeRunException(
                $"Run '{state.RunId}' failed: {failure.SafeMessage}",
                state.Artifacts.RunDirectory,
                exception);
        }
    }

    private Task PersistRunAsync(
        RunRequest request,
        RunState state,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken) =>
        _runStateStore.UpsertRunAsync(
            RunCatalogProjection.Create(
                request,
                state,
                createdAt,
                _timeProvider.GetUtcNow()),
            cancellationToken);

    private async Task TryPersistRunAsync(
        RunRequest request,
        RunState state,
        DateTimeOffset createdAt)
    {
        try
        {
            await PersistRunAsync(
                request,
                state,
                createdAt,
                CancellationToken.None);
        }
        catch
        {
            // Preserve the authoritative runtime failure or cancellation.
        }
    }

    private async Task WriteArtifactAsync(
        RunArtifactPaths artifacts,
        ArtifactKind kind,
        string content,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        await _artifactWriter.WriteAsync(
            artifacts,
            kind,
            content,
            cancellationToken);
        await TraceArtifactWrittenAsync(
            trace,
            artifacts.GetPath(kind),
            kind.ToString().ToLowerInvariant(),
            cancellationToken);
    }

    private async Task WritePhaseOutputsAsync(
        RunState state,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        var execution = state.Execution!;
        if (execution.Stages.Count == 0)
        {
            await WritePhaseGroupAsync(
                state.Artifacts,
                state.Artifacts.RunDirectory,
                execution.NodeResults
                    .Select(node => node.PhaseResult)
                    .ToArray(),
                trace,
                cancellationToken);
            return;
        }

        // Pipeline phases live under their own stage directory, mirroring the
        // stage's input.md/result.md layout.
        foreach (var stage in execution.Stages)
        {
            await WritePhaseGroupAsync(
                state.Artifacts,
                stage.StageDirectory,
                stage.PhaseResults,
                trace,
                cancellationToken);
        }
    }

    private async Task WritePhaseGroupAsync(
        RunArtifactPaths artifacts,
        string baseDirectory,
        IReadOnlyList<PhaseResult> phaseResults,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < phaseResults.Count; index++)
        {
            var phaseResult = phaseResults[index];
            var path = await _artifactWriter.WritePhaseAsync(
                artifacts,
                baseDirectory,
                index + 1,
                phaseResult.PhaseName,
                phaseResult.Content,
                cancellationToken);
            await TracePhaseWrittenAsync(
                trace,
                artifacts.RunDirectory,
                path,
                cancellationToken);
        }
    }

    private static Task TracePhaseWrittenAsync(
        ITraceSession trace,
        string runDirectory,
        string path,
        CancellationToken cancellationToken) =>
        trace.EmitAsync(
            TraceEventNames.ArtifactWritten,
            new Dictionary<string, object?>
            {
                ["name"] = Path.GetFileName(path),
                ["kind"] = "phase",
                ["relativePath"] = Path
                    .GetRelativePath(runDirectory, path)
                    .Replace('\\', '/')
            },
            cancellationToken);

    private static Task TraceArtifactWrittenAsync(
        ITraceSession trace,
        string path,
        string kind,
        CancellationToken cancellationToken) =>
        trace.EmitAsync(
            TraceEventNames.ArtifactWritten,
            new Dictionary<string, object?>
            {
                ["name"] = Path.GetFileName(path),
                ["kind"] = kind
            },
            cancellationToken);

    private async Task RecordFailureAsync(
        ITraceSession trace,
        RunState state,
        RunRequest request,
        DateTimeOffset runStartedAt,
        RuntimeFailureInfo failure)
    {
        await WriteFailureArtifactIfMissingAsync(
            state.Artifacts,
            ArtifactKind.Input,
            $"# Input{Environment.NewLine}{Environment.NewLine}" +
            $"{request.Input.Trim()}{Environment.NewLine}");
        await WriteFailureArtifactIfMissingAsync(
            state.Artifacts,
            ArtifactKind.Result,
            $"# Run Failed{Environment.NewLine}{Environment.NewLine}" +
            "No cognitive result was produced." +
            Environment.NewLine);
        await WriteFailureArtifactIfMissingAsync(
            state.Artifacts,
            ArtifactKind.RunSummary,
            $"# Run Summary{Environment.NewLine}{Environment.NewLine}" +
            $"Status: **FAILED**{Environment.NewLine}{Environment.NewLine}" +
            $"Category: `{failure.Category}`{Environment.NewLine}{Environment.NewLine}" +
            $"{failure.SafeMessage}{Environment.NewLine}");
        await WriteFailureArtifactIfMissingAsync(
            state.Artifacts,
            ArtifactKind.EvalReport,
            $"# Evaluation Report{Environment.NewLine}{Environment.NewLine}" +
            $"Overall: **FAIL**{Environment.NewLine}{Environment.NewLine}" +
            "- [ ] run completed: The runtime failed before evaluation completed." +
            Environment.NewLine);

        var endedAt = _timeProvider.GetUtcNow();
        try
        {
            await WriteRunManifestAsync(
                request,
                state,
                runStartedAt,
                endedAt,
                htmlViewPath: null,
                failure,
                trace,
                CancellationToken.None);
        }
        catch
        {
            // Preserve the authoritative runtime failure if manifest writing fails.
        }

        try
        {
            var data = RuntimeFailureFactory.ToTraceData(failure);
            await EmitTerminalEventAsync(
                trace,
                state,
                runStartedAt,
                endedAt,
                data,
                CancellationToken.None);
        }
        catch
        {
            // Preserve the original exception when terminal failure tracing also fails.
        }
    }

    private async Task RecordCancellationAsync(
        ITraceSession trace,
        RunState state,
        RunRequest request,
        DateTimeOffset runStartedAt)
    {
        await WriteFailureArtifactIfMissingAsync(
            state.Artifacts,
            ArtifactKind.Input,
            $"# Input{Environment.NewLine}{Environment.NewLine}" +
            $"{request.Input.Trim()}{Environment.NewLine}");
        await WriteFailureArtifactIfMissingAsync(
            state.Artifacts,
            ArtifactKind.Result,
            $"# Run Cancelled{Environment.NewLine}{Environment.NewLine}" +
            "The run was cancelled before a cognitive result was finalized." +
            Environment.NewLine);
        await WriteFailureArtifactIfMissingAsync(
            state.Artifacts,
            ArtifactKind.RunSummary,
            $"# Run Summary{Environment.NewLine}{Environment.NewLine}" +
            $"Status: **CANCELLED**{Environment.NewLine}");
        await WriteFailureArtifactIfMissingAsync(
            state.Artifacts,
            ArtifactKind.EvalReport,
            $"# Evaluation Report{Environment.NewLine}{Environment.NewLine}" +
            $"Overall: **NOT RUN**{Environment.NewLine}{Environment.NewLine}" +
            "- [ ] run completed: The run was cancelled." +
            Environment.NewLine);

        var endedAt = _timeProvider.GetUtcNow();
        try
        {
            await WriteRunManifestAsync(
                request,
                state,
                runStartedAt,
                endedAt,
                htmlViewPath: null,
                failure: null,
                trace,
                CancellationToken.None);
        }
        catch
        {
            // Cancellation remains authoritative if manifest writing fails.
        }

        try
        {
            await EmitTerminalEventAsync(
                trace,
                state,
                runStartedAt,
                endedAt,
                additionalData: null,
                cancellationToken: CancellationToken.None);
        }
        catch
        {
            // Cancellation remains authoritative when terminal tracing fails.
        }
    }

    private Task EmitTerminalEventAsync(
        ITraceSession trace,
        RunState terminalState,
        DateTimeOffset runStartedAt,
        DateTimeOffset endedAt,
        IReadOnlyDictionary<string, object?>? additionalData,
        CancellationToken cancellationToken)
    {
        var data = additionalData is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(additionalData);
        data["durationMs"] = RuntimeDuration.GetMilliseconds(
            runStartedAt,
            endedAt);
        var terminalEvent = RunTerminalTraceEventFactory.Create(
            terminalState,
            data);
        return trace.EmitAsync(
            terminalEvent.EventType,
            terminalEvent.Data,
            cancellationToken);
    }

    private async Task WriteRunManifestAsync(
        RunRequest request,
        RunState state,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? htmlViewPath,
        RuntimeFailureInfo? failure,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        var manifest = RunManifestFactory.Create(
            request,
            state,
            startedAt,
            endedAt,
            htmlViewPath,
            failure);
        await WriteArtifactAsync(
            state.Artifacts,
            ArtifactKind.RunManifest,
            RunManifestFactory.Serialize(manifest),
            trace,
            cancellationToken);
    }

    private async Task WriteFailureArtifactIfMissingAsync(
        RunArtifactPaths artifacts,
        ArtifactKind kind,
        string content)
    {
        if (File.Exists(artifacts.GetPath(kind)))
        {
            return;
        }

        try
        {
            await _artifactWriter.WriteAsync(artifacts, kind, content);
        }
        catch
        {
            // Best effort only; the original run error remains authoritative.
        }
    }

    private static string RenderRunSummary(
        RunRequest request,
        RunState state)
    {
        var execution = state.Execution
            ?? throw new InvalidOperationException(
                "Run execution must complete before rendering the summary.");
        var builder = new StringBuilder()
            .AppendLine("# Run Summary")
            .AppendLine()
            .AppendLine($"- Run ID: `{state.RunId}`")
            .AppendLine($"- Pattern: `{execution.Plan.PatternName}`");

        if (execution.Stages.Count == 0)
        {
            builder
                .AppendLine($"- Mode: `{execution.AuthoritativeMode.Manifest.Name}`")
                .AppendLine($"- Phases run: {execution.NodeResults.Count}");
        }
        else
        {
            builder
                .AppendLine(
                    $"- Pipeline stages: `" +
                    $"{string.Join(" -> ", execution.Stages.Select(stage => stage.ModeName))}`")
                .AppendLine($"- Stages run: {execution.Stages.Count}");
        }

        builder
            .AppendLine($"- Model provider: `{request.ModelProvider}`")
            // Use the run directory name, not the absolute path: the artifact
            // stays location-independent (reproducible, no machine username leak)
            // and hashes identically across runs of the same fixed run.
            .AppendLine(
                $"- Output directory: `{Path.GetFileName(state.Artifacts.RunDirectory)}`")
            .AppendLine()
            .AppendLine(execution.Stages.Count == 0
                ? "## Phase Results"
                : "## Stage Results")
            .AppendLine();

        if (execution.Stages.Count == 0)
        {
            foreach (var nodeResult in execution.NodeResults)
            {
                var phase = nodeResult.PhaseResult;
                builder.AppendLine(
                    $"- `{phase.PhaseName}` " +
                    $"({phase.PhaseKind.ToString().ToLowerInvariant()}): " +
                    $"{phase.Content.Length} characters via `{phase.Provider}`.");
            }
        }
        else
        {
            foreach (var stage in execution.Stages)
            {
                var stageDirectory = Path.GetRelativePath(
                        state.Artifacts.RunDirectory,
                        stage.StageDirectory)
                    .Replace('\\', '/');
                builder.AppendLine(
                    $"- Stage {stage.StageIndex:D2} (`{stage.ModeName}`): " +
                    $"{stage.PhaseResults.Count} phases, artifacts in " +
                    $"`{stageDirectory}/`.");
            }
        }

        builder
            .AppendLine()
            .AppendLine("Evaluation is recorded in `eval_report.md`.");

        return builder.ToString();
    }

    private static IReadOnlyDictionary<string, object?> CreatePatternStartedData(
        PatternExecutionPlan plan)
    {
        var data = new Dictionary<string, object?>
        {
            ["pattern"] = plan.PatternName,
            ["authoritativeNodeId"] = plan.AuthoritativeNodeId,
            ["plan"] = new Dictionary<string, object?>
            {
                ["modeSources"] = plan.ModeSources.Select(
                    source => new Dictionary<string, object?>
                    {
                        ["id"] = source.Id,
                        ["mode"] = source.ModeName,
                        ["lens"] = source.Lens
                    }).ToArray(),
                ["nodes"] = plan.Nodes.Select(
                    node => new Dictionary<string, object?>
                    {
                        ["id"] = node.Id,
                        ["kind"] = node.Kind.ToString().ToLowerInvariant(),
                        ["modeSourceId"] = node.ModeSourceId,
                        ["phaseKind"] = node.PhaseKind.ToString().ToLowerInvariant(),
                        ["dependencies"] = node.DependencyNodeIds,
                        ["contextInputs"] = node.ContextNodeIds,
                        ["inputNodeId"] = node.InputNodeId,
                        ["stageId"] = node.StageId
                    }).ToArray(),
                ["stages"] = plan.Stages.Select(
                    stage => new Dictionary<string, object?>
                    {
                        ["id"] = stage.Id,
                        ["index"] = stage.Index,
                        ["modeSourceId"] = stage.ModeSourceId,
                        ["nodeIds"] = stage.NodeIds,
                        ["authoritativeNodeId"] = stage.AuthoritativeNodeId,
                        ["inputNodeId"] = stage.InputNodeId
                    }).ToArray(),
                ["authoritativeNodeId"] = plan.AuthoritativeNodeId,
                ["evalProfile"] = new Dictionary<string, object?>
                {
                    ["requiredNodeIds"] = plan.EvalProfile.RequiredNodeIds,
                    ["evaluateLoopEfficacy"] =
                        plan.EvalProfile.EvaluateLoopEfficacy
                }
            }
        };

        if (plan.Stages.Count == 0)
        {
            data["steps"] = plan.Nodes.Select(
                node => new Dictionary<string, object?>
                {
                    ["id"] = node.Id,
                    ["name"] = node.PhaseKind.ToString().ToLowerInvariant(),
                    ["kind"] = node.PhaseKind.ToString().ToLowerInvariant()
                }).ToArray();
        }
        else
        {
            data["stages"] = GetStageModeNames(plan);
        }

        return data;
    }

    private static IReadOnlyDictionary<string, object?> CreatePatternCompletedData(
        PatternExecutionResult execution)
    {
        var data = new Dictionary<string, object?>
        {
            ["pattern"] = execution.Plan.PatternName,
            ["authoritativeNodeId"] = execution.Plan.AuthoritativeNodeId
        };

        if (execution.Stages.Count == 0)
        {
            data["stepCount"] = execution.NodeResults.Count;
        }
        else
        {
            data["stageCount"] = execution.Stages.Count;
        }

        return data;
    }

    private static IReadOnlyDictionary<string, object?> CreateRunCompletedData(
        PatternExecutionResult execution) =>
        execution.Stages.Count == 0
            ? new Dictionary<string, object?>
            {
                ["mode"] = execution.AuthoritativeMode.Manifest.Name,
                ["phaseCount"] = execution.NodeResults.Count
            }
            : new Dictionary<string, object?>
            {
                ["pattern"] = execution.Plan.PatternName,
                ["stageCount"] = execution.Stages.Count
            };

    private static string[] GetStageModeNames(PatternExecutionPlan plan)
    {
        var modeSources = plan.ModeSources.ToDictionary(
            source => source.Id,
            StringComparer.Ordinal);
        return plan.Stages
            .Select(stage => modeSources[stage.ModeSourceId].ModeName)
            .ToArray();
    }

    private static void ValidateRequest(RunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Input);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputRoot);
    }

    private static EvalPlan CreateEvalPlan(PatternExecutionPlan plan)
    {
        var nodes = plan.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var phaseKinds = plan.EvalProfile.RequiredNodeIds
            .Select(nodeId => nodes[nodeId].PhaseKind)
            .ToArray();
        return new EvalPlan(
            phaseKinds,
            nodes[plan.AuthoritativeNodeId].PhaseKind,
            plan.EvalProfile.EvaluateLoopEfficacy,
            new EvalExecutionPlan(
                plan.Nodes.Select(
                    node => new EvalExecutionNode(
                        node.Id,
                        node.DependencyNodeIds,
                        node.ContextNodeIds,
                        node.StageId))
                    .ToArray(),
                plan.AuthoritativeNodeId));
    }
}
