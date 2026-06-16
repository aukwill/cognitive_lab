using System.Text;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Runtime;

namespace CognitiveRuntime.Core.Experiments.Dungeons;

public sealed class DungeonExperimentRunner
{
    private const int CandidateCount = 3;
    private readonly IModelClientFactory _modelClientFactory;
    private readonly IArtifactWriter _artifactWriter;
    private readonly DungeonArtifactWriter _dungeonArtifactWriter;
    private readonly ITraceSessionFactory _traceSessionFactory;
    private readonly IRunIdGenerator _runIdGenerator;
    private readonly DungeonExperimentDefinitionLoader _definitionLoader;
    private readonly DungeonPlanVerifier _verifier;
    private readonly DungeonCompiler _compiler;
    private readonly TimeProvider _timeProvider;

    public DungeonExperimentRunner(
        IModelClientFactory modelClientFactory,
        IArtifactWriter artifactWriter,
        DungeonArtifactWriter dungeonArtifactWriter,
        ITraceSessionFactory traceSessionFactory,
        IRunIdGenerator runIdGenerator,
        DungeonExperimentDefinitionLoader definitionLoader,
        DungeonPlanVerifier verifier,
        DungeonCompiler compiler,
        TimeProvider timeProvider)
    {
        _modelClientFactory = modelClientFactory;
        _artifactWriter = artifactWriter;
        _dungeonArtifactWriter = dungeonArtifactWriter;
        _traceSessionFactory = traceSessionFactory;
        _runIdGenerator = runIdGenerator;
        _definitionLoader = definitionLoader;
        _verifier = verifier;
        _compiler = compiler;
        _timeProvider = timeProvider;
    }

    public async Task<DungeonExperimentResult> RunAsync(
        DungeonExperimentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var runId = _runIdGenerator.GenerateRunId();
        var artifacts = await _artifactWriter.PrepareRunAsync(
            request.OutputRoot,
            "dungeon-builder",
            runId,
            cancellationToken);
        var trace = await _traceSessionFactory.CreateAsync(
            runId,
            artifacts.TracePath,
            cancellationToken);
        var startedAt = _timeProvider.GetUtcNow();

        try
        {
            await trace.EmitAsync(
                TraceEventNames.RunStarted,
                new Dictionary<string, object?>
                {
                    ["experiment"] = "dungeon-builder",
                    ["provider"] = request.ModelProvider,
                    ["candidateCount"] = CandidateCount,
                    ["revisionLimit"] = 1,
                    ["outputDirectory"] = artifacts.RunDirectory
                },
                cancellationToken);

            await WriteStandardArtifactAsync(
                artifacts,
                ArtifactKind.Input,
                $"# Dungeon Brief{Environment.NewLine}{Environment.NewLine}" +
                $"{request.Brief.Trim()}{Environment.NewLine}",
                trace,
                cancellationToken);

            var prompts = await _definitionLoader.LoadAsync(
                request.ExperimentsRoot,
                cancellationToken);
            var modelClient = _modelClientFactory.Resolve(request.ModelProvider);
            var candidates = new List<DungeonCandidateResult>(CandidateCount);

            await trace.EmitAsync(
                TraceEventNames.PatternStarted,
                new Dictionary<string, object?>
                {
                    ["pattern"] = "verifier-guided-dungeon",
                    ["candidateCount"] = CandidateCount,
                    ["maximumModelCalls"] = CandidateCount * 2
                },
                cancellationToken);

            for (var index = 1; index <= CandidateCount; index++)
            {
                candidates.Add(
                    await ExecuteCandidateAsync(
                        request,
                        runId,
                        artifacts,
                        trace,
                        modelClient,
                        prompts,
                        index,
                        cancellationToken));
            }

            var winner = candidates
                .Where(candidate => candidate.Revised.Report.Feasible)
                .OrderByDescending(candidate => candidate.Revised.Report.Score)
                .ThenBy(candidate => candidate.Index)
                .FirstOrDefault();

            await trace.EmitAsync(
                DungeonTraceEventNames.SelectionCompleted,
                new Dictionary<string, object?>
                {
                    ["winnerIndex"] = winner?.Index,
                    ["feasibleCandidateCount"] = candidates.Count(
                        candidate => candidate.Revised.Report.Feasible),
                    ["tieBreak"] = "candidate-index"
                },
                cancellationToken);

            string? tmxPath = null;
            string? flareMapPath = null;
            string? flareModPath = null;
            if (winner?.Revised.Plan is not null)
            {
                var compiled = _compiler.Compile(winner.Revised.Plan);
                tmxPath = await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine("winner", "dungeon.tmx"),
                    compiled.Tmx,
                    "application/xml; charset=utf-8",
                    trace,
                    cancellationToken);
                await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine("winner", "dungeon_plan.json"),
                    DungeonJson.Serialize(winner.Revised.Plan),
                    "application/json; charset=utf-8",
                    trace,
                    cancellationToken);
                await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine("winner", "verification.json"),
                    DungeonJson.Serialize(winner.Revised.Report),
                    "application/json; charset=utf-8",
                    trace,
                    cancellationToken);
                await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine("winner", "dungeon.txt"),
                    compiled.FlareMap,
                    "text/plain; charset=utf-8",
                    trace,
                    cancellationToken);
                flareMapPath = await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine(
                        "winner",
                        "flare-mod",
                        "generated_dungeon",
                        "maps",
                        "dungeon.txt"),
                    compiled.FlareMap,
                    "text/plain; charset=utf-8",
                    trace,
                    cancellationToken);
                await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine(
                        "winner",
                        "flare-mod",
                        "generated_dungeon",
                        "maps",
                        "spawn.txt"),
                    compiled.SpawnMap,
                    "text/plain; charset=utf-8",
                    trace,
                    cancellationToken);
                await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine(
                        "winner",
                        "flare-mod",
                        "generated_dungeon",
                        "settings.txt"),
                    compiled.ModSettings,
                    "text/plain; charset=utf-8",
                    trace,
                    cancellationToken);
                await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine(
                        "winner",
                        "flare-mod",
                        "generated_dungeon",
                        "ATTRIBUTION.md"),
                    compiled.Attribution,
                    "text/markdown; charset=utf-8",
                    trace,
                    cancellationToken);
                await WriteDungeonArtifactAsync(
                    artifacts,
                    Path.Combine("winner", "preview.txt"),
                    compiled.AsciiPreview,
                    "text/plain; charset=utf-8",
                    trace,
                    cancellationToken);
                flareModPath = Path.Combine(
                    artifacts.RunDirectory,
                    "winner",
                    "flare-mod",
                    "generated_dungeon");

                await CopyFlareAssetsIfConfiguredAsync(
                    request.FlareGameRoot,
                    artifacts,
                    trace,
                    cancellationToken);
            }

            await trace.EmitAsync(
                TraceEventNames.PatternCompleted,
                new Dictionary<string, object?>
                {
                    ["pattern"] = "verifier-guided-dungeon",
                    ["winnerIndex"] = winner?.Index
                },
                cancellationToken);

            var evalReport = CreateEvalReport(candidates, winner, artifacts);
            var resultMarkdown = RenderResult(candidates, winner);
            await WriteStandardArtifactAsync(
                artifacts,
                ArtifactKind.Result,
                resultMarkdown,
                trace,
                cancellationToken);
            await WriteStandardArtifactAsync(
                artifacts,
                ArtifactKind.Pattern,
                RenderPattern(),
                trace,
                cancellationToken);
            await WriteStandardArtifactAsync(
                artifacts,
                ArtifactKind.RunSummary,
                RenderRunSummary(request, candidates, winner, startedAt),
                trace,
                cancellationToken);

            await trace.EmitAsync(
                TraceEventNames.RunCompleted,
                new Dictionary<string, object?>
                {
                    ["winnerIndex"] = winner?.Index,
                    ["evalPassed"] = evalReport.Passed
                },
                cancellationToken);
            await trace.EmitAsync(
                TraceEventNames.EvalStarted,
                new Dictionary<string, object?>(),
                cancellationToken);
            await trace.EmitAsync(
                TraceEventNames.EvalCompleted,
                new Dictionary<string, object?>
                {
                    ["passed"] = evalReport.Passed,
                    ["checkCount"] = evalReport.Checks.Count
                },
                cancellationToken);
            await WriteStandardArtifactAsync(
                artifacts,
                ArtifactKind.EvalReport,
                EvalRunner.RenderMarkdown(evalReport),
                trace,
                cancellationToken);
            await WriteRunManifestAsync(
                request,
                artifacts,
                candidates,
                winner,
                startedAt,
                evalReport,
                trace,
                cancellationToken);

            var outcome = evalReport.Passed
                ? RunOutcome.Success
                : RunOutcome.EvalFailed;
            await trace.EmitAsync(
                TraceEventNames.RunFinalized,
                new Dictionary<string, object?>
                {
                    ["outcome"] = outcome.ToString(),
                    ["durationMs"] = RuntimeDuration.GetMilliseconds(
                        startedAt,
                        _timeProvider.GetUtcNow())
                },
                cancellationToken);

            return new DungeonExperimentResult(
                new RunResult(
                    runId,
                    artifacts.RunDirectory,
                    artifacts.ResultPath,
                    artifacts.TracePath,
                    artifacts.EvalReportPath,
                    outcome),
                winner?.Index,
                tmxPath,
                flareMapPath,
                flareModPath);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await WriteFailureArtifactsAsync(
                request,
                artifacts,
                "Run cancelled.",
                CancellationToken.None);
            await EmitTerminalBestEffortAsync(
                trace,
                TraceEventNames.RunCancelled,
                "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            await WriteFailureArtifactsAsync(
                request,
                artifacts,
                $"Dungeon experiment failed: {exception.Message}",
                CancellationToken.None);
            await EmitTerminalBestEffortAsync(
                trace,
                TraceEventNames.RunFailed,
                exception.GetType().Name);
            throw new RuntimeRunException(
                $"Dungeon experiment '{runId}' failed: {exception.Message}",
                artifacts.RunDirectory,
                exception);
        }
    }

    private async Task<DungeonCandidateResult> ExecuteCandidateAsync(
        DungeonExperimentRequest request,
        string runId,
        RunArtifactPaths artifacts,
        ITraceSession trace,
        IModelClient modelClient,
        DungeonExperimentPrompts prompts,
        int index,
        CancellationToken cancellationToken)
    {
        var candidateId = $"candidate-{index:D2}";
        var candidateDirectory = Path.Combine(
            artifacts.RunDirectory,
            "candidates",
            index.ToString("D2"));
        await trace.EmitAsync(
            DungeonTraceEventNames.CandidateStarted,
            new Dictionary<string, object?>
            {
                ["candidateId"] = candidateId,
                ["candidateIndex"] = index
            },
            cancellationToken);

        var proposed = await CallModelAsync(
            runId,
            candidateId + ".propose",
            "dungeon-builder",
            "propose",
            PhaseKind.Main,
            prompts.Propose,
            CreateProposalInput(request.Brief, index),
            [],
            modelClient,
            trace,
            cancellationToken);
        var proposedEvaluation = _verifier.Evaluate(proposed.Content);
        await TraceVerificationAsync(
            trace,
            candidateId,
            "proposed",
            proposedEvaluation.Report,
            cancellationToken);
        var proposedPath = await WriteDungeonArtifactAsync(
            artifacts,
            Path.Combine("candidates", index.ToString("D2"), "proposed.json"),
            proposed.Content.Trim(),
            "application/json; charset=utf-8",
            trace,
            cancellationToken);
        await WriteDungeonArtifactAsync(
            artifacts,
            Path.Combine(
                "candidates",
                index.ToString("D2"),
                "verification.json"),
            DungeonJson.Serialize(proposedEvaluation.Report),
            "application/json; charset=utf-8",
            trace,
            cancellationToken);

        var revision = await CallModelAsync(
            runId,
            candidateId + ".revise",
            "dungeon-builder",
            "revise",
            PhaseKind.Revision,
            prompts.Revise,
            CreateRevisionInput(
                request.Brief,
                proposed.Content,
                proposedEvaluation.Report),
            [
                new PhaseResult(
                    "propose",
                    PhaseKind.Main,
                    proposed.Content,
                    proposed.Provider,
                    proposed.Model)
            ],
            modelClient,
            trace,
            cancellationToken);
        var revisedEvaluation = _verifier.Evaluate(revision.Content);
        await TraceVerificationAsync(
            trace,
            candidateId,
            "revised",
            revisedEvaluation.Report,
            cancellationToken);
        await WriteDungeonArtifactAsync(
            artifacts,
            Path.Combine("candidates", index.ToString("D2"), "revised.json"),
            revision.Content.Trim(),
            "application/json; charset=utf-8",
            trace,
            cancellationToken);
        await WriteDungeonArtifactAsync(
            artifacts,
            Path.Combine(
                "candidates",
                index.ToString("D2"),
                "revised_verification.json"),
            DungeonJson.Serialize(revisedEvaluation.Report),
            "application/json; charset=utf-8",
            trace,
            cancellationToken);

        await trace.EmitAsync(
            DungeonTraceEventNames.CandidateCompleted,
            new Dictionary<string, object?>
            {
                ["candidateId"] = candidateId,
                ["proposedFeasible"] = proposedEvaluation.Report.Feasible,
                ["revisedFeasible"] = revisedEvaluation.Report.Feasible,
                ["proposedScore"] = proposedEvaluation.Report.Score,
                ["revisedScore"] = revisedEvaluation.Report.Score,
                ["proposedArtifact"] = proposedPath
            },
            cancellationToken);

        return new DungeonCandidateResult(
            index,
            proposed.Content,
            proposedEvaluation,
            revision.Content,
            revisedEvaluation,
            candidateDirectory);
    }

    private async Task<ModelResponse> CallModelAsync(
        string runId,
        string nodeId,
        string modeName,
        string phaseName,
        PhaseKind phaseKind,
        string prompt,
        string input,
        IReadOnlyList<PhaseResult> priorResults,
        IModelClient modelClient,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        var callId = $"{runId}:{nodeId}:1";
        var startedAt = _timeProvider.GetUtcNow();
        await trace.EmitAsync(
            TraceEventNames.ModelCalled,
            new Dictionary<string, object?>
            {
                ["callId"] = callId,
                ["nodeId"] = nodeId,
                ["attempt"] = 1,
                ["provider"] = modelClient.ProviderName,
                ["phase"] = phaseName
            },
            cancellationToken);

        try
        {
            var response = await modelClient.CompleteAsync(
                new ModelRequest(
                    runId,
                    modeName,
                    phaseName,
                    phaseKind,
                    prompt,
                    input,
                    priorResults),
                cancellationToken);
            if (string.IsNullOrWhiteSpace(response.Content))
            {
                throw new ModelProviderException(
                    $"Provider '{modelClient.ProviderName}' returned empty dungeon content.");
            }

            await trace.EmitAsync(
                TraceEventNames.ModelCompleted,
                new Dictionary<string, object?>
                {
                    ["callId"] = callId,
                    ["nodeId"] = nodeId,
                    ["attempt"] = 1,
                    ["provider"] = response.Provider,
                    ["model"] = response.Model,
                    ["phase"] = phaseName,
                    ["contentLength"] = response.Content.Length,
                    ["durationMs"] = RuntimeDuration.GetMilliseconds(
                        startedAt,
                        _timeProvider.GetUtcNow())
                },
                cancellationToken);
            return response;
        }
        catch
        {
            await trace.EmitAsync(
                TraceEventNames.ModelFailed,
                new Dictionary<string, object?>
                {
                    ["callId"] = callId,
                    ["nodeId"] = nodeId,
                    ["attempt"] = 1,
                    ["provider"] = modelClient.ProviderName,
                    ["phase"] = phaseName,
                    ["durationMs"] = RuntimeDuration.GetMilliseconds(
                        startedAt,
                        _timeProvider.GetUtcNow())
                },
                CancellationToken.None);
            throw;
        }
    }

    private static string CreateProposalInput(string brief, int candidateIndex) =>
        $"""
        Candidate index: {candidateIndex}

        Brief:
        {brief.Trim()}

        Bounds:
        - schemaVersion: 1
        - width and height: 12-48
        - rooms: 3-6
        - room dimensions: at least 3x3
        - corridor width: 1-2
        - exactly one entrance, objective, and exit
        """;

    private static string CreateRevisionInput(
        string brief,
        string proposedJson,
        DungeonVerificationReport report) =>
        $"""
        Brief:
        {brief.Trim()}

        Proposed plan:
        {proposedJson.Trim()}

        Deterministic verifier report:
        {DungeonJson.Serialize(report)}
        """;

    private static Task TraceVerificationAsync(
        ITraceSession trace,
        string candidateId,
        string pass,
        DungeonVerificationReport report,
        CancellationToken cancellationToken) =>
        trace.EmitAsync(
            DungeonTraceEventNames.VerifierCompleted,
            new Dictionary<string, object?>
            {
                ["candidateId"] = candidateId,
                ["pass"] = pass,
                ["feasible"] = report.Feasible,
                ["score"] = report.Score,
                ["failedCheckIds"] = report.Checks
                    .Where(check => !check.Passed)
                    .Select(check => check.Id)
                    .ToArray()
            },
            cancellationToken);

    private static EvalReport CreateEvalReport(
        IReadOnlyList<DungeonCandidateResult> candidates,
        DungeonCandidateResult? winner,
        RunArtifactPaths artifacts)
    {
        var checks = new List<EvalCheckResult>
        {
            new(
                "three candidates completed",
                candidates.Count == CandidateCount,
                $"Completed {candidates.Count} of {CandidateCount} candidates."),
            new(
                "one revision per candidate",
                candidates.All(candidate =>
                    !string.IsNullOrWhiteSpace(candidate.RevisedJson)),
                "Every candidate has one preserved revision."),
            new(
                "feasible winner selected",
                winner?.Revised.Report.Feasible == true,
                winner is null
                    ? "No revised candidate satisfied all hard checks."
                    : $"Candidate {winner.Index:D2} satisfied all hard checks."),
            new(
                "winner compiled",
                File.Exists(Path.Combine(artifacts.RunDirectory, "winner", "dungeon.tmx")) &&
                File.Exists(Path.Combine(
                    artifacts.RunDirectory,
                    "winner",
                    "flare-mod",
                    "generated_dungeon",
                    "maps",
                    "dungeon.txt")),
                "Winner TMX and Flare map artifacts must exist.")
        };
        return new EvalReport(checks);
    }

    private static string RenderResult(
        IReadOnlyList<DungeonCandidateResult> candidates,
        DungeonCandidateResult? winner)
    {
        var builder = new StringBuilder()
            .AppendLine("# Dungeon Builder Result")
            .AppendLine();
        if (winner is null)
        {
            builder.AppendLine("No feasible candidate was produced.");
        }
        else
        {
            builder
                .Append("Winner: **Candidate ")
                .Append(winner.Index.ToString("D2"))
                .AppendLine("**")
                .AppendLine()
                .Append("Score: **")
                .Append(winner.Revised.Report.Score)
                .AppendLine("/5**")
                .AppendLine()
                .AppendLine("Artifacts:")
                .AppendLine()
                .AppendLine("- `winner/dungeon.tmx`")
                .AppendLine("- `winner/preview.txt`")
                .AppendLine("- `winner/flare-mod/generated_dungeon/`");
        }

        builder.AppendLine().AppendLine("## Candidate Ledger").AppendLine();
        foreach (var candidate in candidates)
        {
            builder
                .Append("- Candidate ")
                .Append(candidate.Index.ToString("D2"))
                .Append(": proposed feasible=")
                .Append(candidate.Proposed.Report.Feasible)
                .Append(", revised feasible=")
                .Append(candidate.Revised.Report.Feasible)
                .Append(", revised score=")
                .Append(candidate.Revised.Report.Score)
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string RenderPattern() =>
        """
        # Pattern: Verifier-Guided Dungeon

        ```text
        candidate 01: propose -> verify -> revise -> verify
        candidate 02: propose -> verify -> revise -> verify
        candidate 03: propose -> verify -> revise -> verify
                                           |
                              deterministic select
                                           |
                                  compile artifacts
        ```

        The runtime fixes candidate count, revision count, verifier, scoring,
        tie-breaking, compilation, artifact paths, and terminal outcome.
        """;

    private string RenderRunSummary(
        DungeonExperimentRequest request,
        IReadOnlyList<DungeonCandidateResult> candidates,
        DungeonCandidateResult? winner,
        DateTimeOffset startedAt) =>
        $"""
        # Run Summary

        Experiment: `dungeon-builder`

        Provider: `{request.ModelProvider}`

        Candidates: `{candidates.Count}`

        Winner: `{(winner is null ? "none" : winner.Index.ToString("D2"))}`

        Winner score: `{winner?.Revised.Report.Score ?? 0}/5`

        Duration: `{RuntimeDuration.GetMilliseconds(startedAt, _timeProvider.GetUtcNow())} ms`
        """;

    private async Task WriteRunManifestAsync(
        DungeonExperimentRequest request,
        RunArtifactPaths artifacts,
        IReadOnlyList<DungeonCandidateResult> candidates,
        DungeonCandidateResult? winner,
        DateTimeOffset startedAt,
        EvalReport evalReport,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        var manifest = new
        {
            schemaVersion = 1,
            runId = artifacts.RunId,
            experiment = "dungeon-builder",
            provider = request.ModelProvider,
            startedAt,
            endedAt = _timeProvider.GetUtcNow(),
            inputSource = request.InputSource,
            candidateCount = candidates.Count,
            winnerIndex = winner?.Index,
            winnerScore = winner?.Revised.Report.Score,
            evalPassed = evalReport.Passed,
            flareAssetsCopied = request.FlareGameRoot is not null,
            // SHA-256 and byte length per artifact (TA-007), so the experiment
            // run.json carries the same integrity inventory as the core path.
            artifacts = RunManifestFactory.CreateArtifactInventory(artifacts)
        };
        await WriteStandardArtifactAsync(
            artifacts,
            ArtifactKind.RunManifest,
            DungeonJson.Serialize(manifest),
            trace,
            cancellationToken);
    }

    private async Task CopyFlareAssetsIfConfiguredAsync(
        string? flareGameRoot,
        RunArtifactPaths artifacts,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(flareGameRoot))
        {
            return;
        }

        var root = Path.GetFullPath(flareGameRoot);
        var assets = new[]
        {
            ("dungeon.png", "image/png"),
            ("tiled_collision.png", "image/png")
        };
        foreach (var (fileName, mediaType) in assets)
        {
            var sourcePath = Path.Combine(root, "tiled", "tilesheets", fileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"Required Flare tilesheet '{fileName}' was not found under '{root}'.",
                    sourcePath);
            }

            var path = await _dungeonArtifactWriter.CopyAsync(
                artifacts,
                Path.Combine("winner", "assets", fileName),
                sourcePath,
                mediaType,
                cancellationToken);
            await TraceArtifactAsync(trace, path, mediaType, cancellationToken);
        }
    }

    private async Task WriteStandardArtifactAsync(
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
        await TraceArtifactAsync(
            trace,
            artifacts.GetPath(kind),
            kind.ToString(),
            cancellationToken);
    }

    private async Task<string> WriteDungeonArtifactAsync(
        RunArtifactPaths artifacts,
        string relativePath,
        string content,
        string mediaType,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        var path = await _dungeonArtifactWriter.WriteTextAsync(
            artifacts,
            relativePath,
            content,
            mediaType,
            cancellationToken);
        await TraceArtifactAsync(trace, path, mediaType, cancellationToken);
        return path;
    }

    private static Task TraceArtifactAsync(
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

    private async Task WriteFailureArtifactsAsync(
        DungeonExperimentRequest request,
        RunArtifactPaths artifacts,
        string message,
        CancellationToken cancellationToken)
    {
        await WriteIfMissingAsync(
            artifacts,
            ArtifactKind.Input,
            $"# Dungeon Brief{Environment.NewLine}{Environment.NewLine}" +
            $"{request.Brief.Trim()}{Environment.NewLine}",
            cancellationToken);
        await WriteIfMissingAsync(
            artifacts,
            ArtifactKind.Result,
            $"# Dungeon Builder Failed{Environment.NewLine}{Environment.NewLine}" +
            $"{message}{Environment.NewLine}",
            cancellationToken);
        await WriteIfMissingAsync(
            artifacts,
            ArtifactKind.RunSummary,
            $"# Run Summary{Environment.NewLine}{Environment.NewLine}" +
            $"Status: **FAILED**{Environment.NewLine}",
            cancellationToken);
        await WriteIfMissingAsync(
            artifacts,
            ArtifactKind.EvalReport,
            $"# Evaluation Report{Environment.NewLine}{Environment.NewLine}" +
            $"Overall: **FAIL**{Environment.NewLine}",
            cancellationToken);
        await WriteIfMissingAsync(
            artifacts,
            ArtifactKind.Pattern,
            RenderPattern(),
            cancellationToken);
    }

    private async Task WriteIfMissingAsync(
        RunArtifactPaths artifacts,
        ArtifactKind kind,
        string content,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(artifacts.GetPath(kind)))
        {
            await _artifactWriter.WriteAsync(
                artifacts,
                kind,
                content,
                cancellationToken);
        }
    }

    private static async Task EmitTerminalBestEffortAsync(
        ITraceSession trace,
        string eventType,
        string reason)
    {
        try
        {
            await trace.EmitAsync(
                eventType,
                new Dictionary<string, object?> { ["reason"] = reason },
                CancellationToken.None);
        }
        catch
        {
            // The original failure or cancellation remains authoritative.
        }
    }

    private static void ValidateRequest(DungeonExperimentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Brief);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExperimentsRoot);
    }
}
