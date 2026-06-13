using System.Text;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Views;

namespace CognitiveRuntime.Core.Runtime;

public sealed class Orchestrator
{
    private readonly IModeLoader _modeLoader;
    private readonly IModelClientFactory _modelClientFactory;
    private readonly PhaseRunner _phaseRunner;
    private readonly IArtifactWriter _artifactWriter;
    private readonly ITraceSessionFactory _traceSessionFactory;
    private readonly IEvalRunner _evalRunner;
    private readonly IRunViewWriter _runViewWriter;

    public Orchestrator(
        IModeLoader modeLoader,
        IModelClientFactory modelClientFactory,
        PhaseRunner phaseRunner,
        IArtifactWriter artifactWriter,
        ITraceSessionFactory traceSessionFactory,
        IEvalRunner evalRunner,
        IRunViewWriter runViewWriter)
    {
        _modeLoader = modeLoader;
        _modelClientFactory = modelClientFactory;
        _phaseRunner = phaseRunner;
        _artifactWriter = artifactWriter;
        _traceSessionFactory = traceSessionFactory;
        _evalRunner = evalRunner;
        _runViewWriter = runViewWriter;
    }

    public async Task<RunResult> RunAsync(
        RunRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var runId = Guid.NewGuid().ToString("N");
        var artifacts = await _artifactWriter.PrepareRunAsync(
            request.OutputRoot,
            request.ModeName,
            runId,
            cancellationToken);
        var trace = await _traceSessionFactory.CreateAsync(
            runId,
            artifacts.TracePath,
            cancellationToken);

        try
        {
            await trace.EmitAsync(
                "run.started",
                new Dictionary<string, object?>
                {
                    ["mode"] = request.ModeName,
                    ["provider"] = request.ModelProvider,
                    ["outputDirectory"] = artifacts.RunDirectory
                },
                cancellationToken);

            await WriteArtifactAsync(
                artifacts,
                ArtifactKind.Input,
                $"# Input{Environment.NewLine}{Environment.NewLine}{request.Input.Trim()}{Environment.NewLine}",
                trace,
                cancellationToken);

            var mode = await _modeLoader.LoadAsync(
                request.ModeName,
                request.Lens,
                cancellationToken);
            await trace.EmitAsync(
                "mode.loaded",
                new Dictionary<string, object?>
                {
                    ["name"] = mode.Manifest.Name,
                    ["version"] = mode.Manifest.Version,
                    ["phaseCount"] = mode.Phases.Count,
                    ["lens"] = request.Lens
                },
                cancellationToken);

            var modelClient = _modelClientFactory.Resolve(request.ModelProvider);
            var phaseResults = new List<PhaseResult>(mode.Phases.Count);

            foreach (var phase in mode.Phases)
            {
                var phaseResult = await _phaseRunner.RunAsync(
                    runId,
                    mode,
                    phase,
                    request.Input,
                    Array.AsReadOnly(phaseResults.ToArray()),
                    modelClient,
                    trace,
                    cancellationToken);

                phaseResults.Add(phaseResult);
            }

            var result = ResultComposer.Compose(mode, phaseResults);
            await WriteArtifactAsync(
                artifacts,
                ArtifactKind.Result,
                result,
                trace,
                cancellationToken);

            var summary = RenderRunSummary(
                runId,
                request,
                mode,
                phaseResults,
                artifacts);
            await WriteArtifactAsync(
                artifacts,
                ArtifactKind.RunSummary,
                summary,
                trace,
                cancellationToken);

            // Reserve the self-referential eval artifact before checking artifact existence.
            await _artifactWriter.WriteAsync(
                artifacts,
                ArtifactKind.EvalReport,
                "# Evaluation Report\n\nEvaluation has not run yet.\n",
                cancellationToken);
            await trace.EmitAsync(
                "artifact.reserved",
                new Dictionary<string, object?>
                {
                    ["name"] = Path.GetFileName(artifacts.EvalReportPath)
                },
                cancellationToken);

            // This event marks completion of the cognitive loop. Post-run eval follows.
            await trace.EmitAsync(
                "run.completed",
                new Dictionary<string, object?>
                {
                    ["mode"] = mode.Manifest.Name,
                    ["phaseCount"] = phaseResults.Count
                },
                cancellationToken);

            await trace.EmitAsync(
                "eval.started",
                new Dictionary<string, object?>(),
                cancellationToken);

            var evalReport = await _evalRunner.EvaluateAsync(
                new EvalContext(
                    artifacts,
                    mode,
                    trace.Events,
                    Array.AsReadOnly(phaseResults.ToArray())),
                cancellationToken);

            await trace.EmitAsync(
                "eval.completed",
                new Dictionary<string, object?>
                {
                    ["passed"] = evalReport.Passed,
                    ["checkCount"] = evalReport.Checks.Count
                },
                cancellationToken);

            await WriteArtifactAsync(
                artifacts,
                ArtifactKind.EvalReport,
                EvalRunner.RenderMarkdown(evalReport),
                trace,
                cancellationToken);

            string? htmlViewPath = null;
            if (request.WriteHtmlView)
            {
                var viewModel = RunViewModelFactory.Create(
                    runId,
                    request,
                    mode,
                    phaseResults,
                    evalReport,
                    artifacts,
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

            await trace.EmitAsync(
                "run.finalized",
                new Dictionary<string, object?> { ["evalPassed"] = evalReport.Passed },
                cancellationToken);

            return new RunResult(
                runId,
                artifacts.RunDirectory,
                artifacts.ResultPath,
                artifacts.TracePath,
                artifacts.EvalReportPath,
                evalReport.Passed ? RunOutcome.Success : RunOutcome.EvalFailed,
                htmlViewPath);
        }
        catch (Exception exception) when (exception is not RuntimeRunException)
        {
            await RecordFailureAsync(trace, artifacts, request.Input, exception);
            throw new RuntimeRunException(
                $"Run '{runId}' failed: {exception.Message}",
                artifacts.RunDirectory,
                exception);
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

    private static Task TraceArtifactWrittenAsync(
        ITraceSession trace,
        string path,
        string kind,
        CancellationToken cancellationToken) =>
        trace.EmitAsync(
            "artifact.written",
            new Dictionary<string, object?>
            {
                ["name"] = Path.GetFileName(path),
                ["kind"] = kind
            },
            cancellationToken);

    private async Task RecordFailureAsync(
        ITraceSession trace,
        RunArtifactPaths artifacts,
        string input,
        Exception exception)
    {
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            ArtifactKind.Input,
            $"# Input{Environment.NewLine}{Environment.NewLine}" +
            $"{input.Trim()}{Environment.NewLine}");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            ArtifactKind.Result,
            $"# Run Failed{Environment.NewLine}{Environment.NewLine}" +
            "No cognitive result was produced." +
            Environment.NewLine);
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            ArtifactKind.RunSummary,
            $"# Run Summary{Environment.NewLine}{Environment.NewLine}" +
            $"Status: **FAILED**{Environment.NewLine}{Environment.NewLine}" +
            $"{exception.Message}{Environment.NewLine}");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            ArtifactKind.EvalReport,
            $"# Evaluation Report{Environment.NewLine}{Environment.NewLine}" +
            $"Overall: **FAIL**{Environment.NewLine}{Environment.NewLine}" +
            "- [ ] run completed: The runtime failed before evaluation completed." +
            Environment.NewLine);

        try
        {
            await trace.EmitAsync(
                "run.failed",
                new Dictionary<string, object?>
                {
                    ["exceptionType"] = exception.GetType().Name,
                    ["message"] = exception.Message
                });
        }
        catch
        {
            // Preserve the original exception when terminal failure tracing also fails.
        }
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
        string runId,
        RunRequest request,
        LoadedMode mode,
        IReadOnlyList<PhaseResult> phaseResults,
        RunArtifactPaths artifacts)
    {
        var builder = new StringBuilder()
            .AppendLine("# Run Summary")
            .AppendLine()
            .AppendLine($"- Run ID: `{runId}`")
            .AppendLine($"- Mode: `{mode.Manifest.Name}`")
            .AppendLine($"- Model provider: `{request.ModelProvider}`")
            .AppendLine($"- Output directory: `{artifacts.RunDirectory}`")
            .AppendLine($"- Phases run: {phaseResults.Count}")
            .AppendLine()
            .AppendLine("## Phase Results")
            .AppendLine();

        foreach (var phase in phaseResults)
        {
            builder.AppendLine(
                $"- `{phase.PhaseName}` ({phase.PhaseKind.ToString().ToLowerInvariant()}): " +
                $"{phase.Content.Length} characters via `{phase.Provider}`.");
        }

        builder
            .AppendLine()
            .AppendLine("Evaluation is recorded in `eval_report.md`.");

        return builder.ToString();
    }

    private static void ValidateRequest(RunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Input);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputRoot);
    }
}
