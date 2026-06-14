using System.Text.Json;
using System.Text.Json.Serialization;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Core.Runtime;

internal static class RunManifestFactory
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

    public static RunManifest Create(
        RunRequest request,
        RunState state,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? htmlViewPath = null,
        RuntimeFailureInfo? failure = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);

        if (!RunLifecycleStateMachine.IsTerminal(state.LifecycleStatus) ||
            state.Outcome is null)
        {
            throw new InvalidOperationException(
                "run.json requires a terminal run state and outcome.");
        }

        var loadedModes = state.LoadedModes;
        var modes = state.Plan.ModeSources
            .Select(source => new RunManifestMode(
                source.Id,
                source.ModeName,
                loadedModes.TryGetValue(source.Id, out var mode)
                    ? mode.Manifest.Version
                    : null,
                source.Lens))
            .ToArray();
        var plan = new RunManifestPlan(
            state.Plan.PatternName,
            state.Plan.Nodes.Select(
                node => new RunManifestPlanNode(
                    node.Id,
                    ToCamelCase(node.Kind),
                    node.ModeSourceId,
                    ToCamelCase(node.PhaseKind),
                    node.DependencyNodeIds,
                    node.ContextNodeIds,
                    node.InputNodeId,
                    node.StageId))
                .ToArray(),
            state.Plan.Stages.Select(
                stage => new RunManifestPlanStage(
                    stage.Id,
                    stage.Index,
                    stage.ModeSourceId,
                    stage.NodeIds,
                    stage.AuthoritativeNodeId,
                    stage.InputNodeId))
                .ToArray(),
            state.Plan.AuthoritativeNodeId,
            state.Plan.EvalProfile.RequiredNodeIds,
            state.Plan.EvalProfile.EvaluateLoopEfficacy);
        var nodes = state.ExecutionNodes
            .Select(node => new RunManifestExecutionNode(
                node.NodeId,
                node.StageId,
                node.PhaseName,
                ToCamelCase(node.PhaseKind),
                node.ModeName,
                ToCamelCase(node.Status),
                node.Provider,
                node.Model,
                node.StartedAt,
                node.EndedAt,
                node.OutputLength))
            .ToArray();
        var modeSources = state.Plan.ModeSources.ToDictionary(
            source => source.Id,
            StringComparer.Ordinal);
        var nodeStates = state.ExecutionNodes.ToDictionary(
            node => node.NodeId,
            StringComparer.Ordinal);
        var stages = state.Plan.Stages
            .Select(stage => new RunManifestStageOutcome(
                stage.Id,
                stage.Index,
                modeSources[stage.ModeSourceId].ModeName,
                GetStageStatus(
                    stage.NodeIds.Select(nodeId => nodeStates[nodeId].Status)),
                stage.NodeIds))
            .ToArray();
        var models = state.ExecutionNodes
            .Select(node => node.Model)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var eval = state.EvalReport is null
            ? null
            : new RunManifestEvalSummary(
                state.EvalReport.Passed,
                state.EvalReport.Checks.Count,
                state.EvalReport.Checks.Count(check => check.Passed),
                state.EvalReport.Checks.Count(check => !check.Passed));

        return new RunManifest(
            RunManifestSchema.CurrentVersion,
            state.RunId,
            request.ModeName,
            state.Plan.PatternName,
            request.ModelProvider,
            models,
            startedAt,
            endedAt,
            ToCamelCase(state.LifecycleStatus),
            state.Outcome.Value,
            modes,
            plan,
            nodes,
            stages,
            CreateArtifactInventory(state.Artifacts, htmlViewPath),
            eval,
            failure);
    }

    public static string Serialize(RunManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    private static IReadOnlyList<RunManifestArtifact> CreateArtifactInventory(
        RunArtifactPaths artifacts,
        string? htmlViewPath)
    {
        var paths = Directory
            .EnumerateFiles(
                artifacts.RunDirectory,
                "*",
                SearchOption.AllDirectories)
            .Where(path =>
                !Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal))
            .Append(artifacts.RunManifestPath);

        if (!string.IsNullOrWhiteSpace(htmlViewPath))
        {
            paths = paths.Append(htmlViewPath);
        }

        return paths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                ArtifactWriter.EnsureWithinRunDirectory(
                    artifacts.RunDirectory,
                    path);
                var relativePath = Path
                    .GetRelativePath(artifacts.RunDirectory, path)
                    .Replace('\\', '/');
                return new RunManifestArtifact(
                    relativePath,
                    GetArtifactKind(relativePath),
                    GetMediaType(path));
            })
            .OrderBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetStageStatus(
        IEnumerable<ExecutionNodeStatus> statuses)
    {
        var values = statuses.ToArray();
        if (values.All(status => status == ExecutionNodeStatus.Completed))
        {
            return "completed";
        }

        if (values.Any(status => status == ExecutionNodeStatus.Failed))
        {
            return "failed";
        }

        if (values.Any(status => status == ExecutionNodeStatus.Cancelled))
        {
            return "cancelled";
        }

        if (values.Any(status => status == ExecutionNodeStatus.Running))
        {
            return "running";
        }

        return "pending";
    }

    private static string GetArtifactKind(string relativePath) =>
        relativePath switch
        {
            "input.md" => "input",
            "result.md" => "result",
            "trace.json" => "trace",
            "run_summary.md" => "runSummary",
            "eval_report.md" => "evalReport",
            "pattern.md" => "pattern",
            "run.json" => "runManifest",
            "index.html" => "htmlView",
            _ when relativePath.StartsWith("stages/", StringComparison.Ordinal) =>
                "stageArtifact",
            _ => "artifact"
        };

    private static string GetMediaType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => "application/json; charset=utf-8",
            ".html" => "text/html; charset=utf-8",
            ".md" => "text/markdown; charset=utf-8",
            _ => "application/octet-stream"
        };

    private static string ToCamelCase<T>(T value)
        where T : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
