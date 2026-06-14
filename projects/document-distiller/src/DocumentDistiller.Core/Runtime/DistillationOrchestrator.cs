using System.Security.Cryptography;
using System.Text;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Evaluation;
using DocumentDistiller.Core.Rendering;

namespace DocumentDistiller.Core.Runtime;

public sealed class DistillationOrchestrator
{
    private readonly IDocumentIngestor _documentIngestor;
    private readonly IPromptLoader _promptLoader;
    private readonly IDistillationModelClientFactory _modelClientFactory;
    private readonly IRunArtifactWriter _artifactWriter;
    private readonly ITraceSessionFactory _traceSessionFactory;
    private readonly IDistillationEvaluator _evaluator;
    private readonly ISourceRiskScanner _sourceRiskScanner;
    private readonly IEvidenceMatrixBuilder _evidenceMatrixBuilder;
    private readonly IDistillationContractValidator _contractValidator;
    private readonly TimeProvider _timeProvider;

    public DistillationOrchestrator(
        IDocumentIngestor documentIngestor,
        IPromptLoader promptLoader,
        IDistillationModelClientFactory modelClientFactory,
        IRunArtifactWriter artifactWriter,
        ITraceSessionFactory traceSessionFactory,
        IDistillationEvaluator evaluator,
        ISourceRiskScanner sourceRiskScanner,
        IEvidenceMatrixBuilder evidenceMatrixBuilder,
        IDistillationContractValidator contractValidator,
        TimeProvider timeProvider)
    {
        _documentIngestor = documentIngestor;
        _promptLoader = promptLoader;
        _modelClientFactory = modelClientFactory;
        _artifactWriter = artifactWriter;
        _traceSessionFactory = traceSessionFactory;
        _evaluator = evaluator;
        _sourceRiskScanner = sourceRiskScanner;
        _evidenceMatrixBuilder = evidenceMatrixBuilder;
        _contractValidator = contractValidator;
        _timeProvider = timeProvider;
    }

    public async Task<DistillationRunResult> RunAsync(
        DistillationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var runId = Guid.NewGuid().ToString("N");
        var artifacts = await _artifactWriter.PrepareRunAsync(
            request.OutputRoot,
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
                    ["inputDirectory"] = Path.GetFullPath(request.InputDirectory),
                    ["provider"] = request.Provider,
                    ["outputDirectory"] = artifacts.RunDirectory,
                    ["pattern"] = "analyze-critic-revise"
                },
                cancellationToken);

            var ingestion = await _documentIngestor.IngestAsync(
                request.InputDirectory,
                request.MaxInputCharacters,
                request.ChunkSizeCharacters,
                request.ChunkOverlapCharacters,
                cancellationToken);

            await trace.EmitAsync(
                "documents.discovered",
                new Dictionary<string, object?>
                {
                    ["count"] = ingestion.Documents.Count
                },
                cancellationToken);
            foreach (var document in ingestion.Documents)
            {
                await trace.EmitAsync(
                    "document.loaded",
                    new Dictionary<string, object?>
                    {
                        ["documentId"] = document.Id,
                        ["path"] = document.RelativePath,
                        ["characters"] = document.Content.Length,
                        ["sha256"] = document.Sha256
                    },
                    cancellationToken);
            }

            await trace.EmitAsync(
                "chunks.created",
                new Dictionary<string, object?>
                {
                    ["count"] = ingestion.Chunks.Count,
                    ["chunkSizeCharacters"] = request.ChunkSizeCharacters,
                    ["chunkOverlapCharacters"] = request.ChunkOverlapCharacters
                },
                cancellationToken);

            var sourceRiskReport = _sourceRiskScanner.Scan(ingestion.Chunks);
            await trace.EmitAsync(
                "source_risk.scanned",
                new Dictionary<string, object?>
                {
                    ["findingCount"] = sourceRiskReport.Findings.Count,
                    ["highSeverityCount"] = sourceRiskReport.HighSeverityCount
                },
                cancellationToken);

            await WriteTextArtifactAsync(
                artifacts,
                artifacts.InputManifestPath,
                RenderInputManifest(ingestion.Documents),
                "input-manifest",
                trace,
                cancellationToken);
            await WriteJsonArtifactAsync(
                artifacts,
                artifacts.SourceRiskPath,
                sourceRiskReport,
                "source-risk",
                trace,
                cancellationToken);
            await WriteJsonArtifactAsync(
                artifacts,
                artifacts.EvidencePath,
                new
                {
                    documents = ingestion.Documents.Select(
                        document => new
                        {
                            document.Id,
                            document.RelativePath,
                            document.Title,
                            document.Sha256
                        }),
                    chunks = ingestion.Chunks
                },
                "evidence",
                trace,
                cancellationToken);

            var prompts = await _promptLoader.LoadAsync(
                request.PromptsRoot,
                cancellationToken);
            await trace.EmitAsync(
                "prompts.loaded",
                new Dictionary<string, object?>
                {
                    ["root"] = Path.GetFullPath(request.PromptsRoot)
                },
                cancellationToken);

            var modelClient = _modelClientFactory.Resolve(request.Provider);
            var modelInvocations = new List<ModelInvocationMetadata>();
            var runManifest = CreateRunManifest(
                runId,
                request,
                ingestion,
                prompts,
                modelClient);
            await WriteJsonArtifactAsync(
                artifacts,
                artifacts.RunManifestPath,
                runManifest,
                "run-manifest",
                trace,
                cancellationToken);

            await TracePhaseStartedAsync(trace, "analysis", modelClient, cancellationToken);
            var draftCompletion = await modelClient.AnalyzeAsync(
                new AnalysisRequest(
                    runId,
                    prompts,
                    ingestion.Documents,
                    ingestion.Chunks),
                cancellationToken);
            var draft = draftCompletion.Value;
            modelInvocations.Add(draftCompletion.Metadata);
            _contractValidator.ValidateDraft(draft);
            await trace.EmitAsync(
                "analysis.validated",
                new Dictionary<string, object?>
                {
                    ["pillarCount"] = draft.Pillars.Length,
                    ["claimCount"] = CountClaims(draft)
                },
                cancellationToken);
            await TracePhaseCompletedAsync(
                trace,
                "analysis",
                draft.Topic,
                draftCompletion.Metadata,
                cancellationToken);
            await WriteJsonArtifactAsync(
                artifacts,
                artifacts.DraftPath,
                draft,
                "draft",
                trace,
                cancellationToken);

            await TracePhaseStartedAsync(trace, "critic", modelClient, cancellationToken);
            var critiqueCompletion = await modelClient.CritiqueAsync(
                new CritiqueRequest(
                    runId,
                    prompts,
                    ingestion.Documents,
                    ingestion.Chunks,
                    draft),
                cancellationToken);
            var critique = critiqueCompletion.Value;
            modelInvocations.Add(critiqueCompletion.Metadata);
            await TracePhaseCompletedAsync(
                trace,
                "critic",
                null,
                critiqueCompletion.Metadata,
                cancellationToken);
            await WriteJsonArtifactAsync(
                artifacts,
                artifacts.CritiquePath,
                critique,
                "critique",
                trace,
                cancellationToken);

            await TracePhaseStartedAsync(trace, "revision", modelClient, cancellationToken);
            var analysisCompletion = await modelClient.ReviseAsync(
                new RevisionRequest(
                    runId,
                    prompts,
                    ingestion.Documents,
                    ingestion.Chunks,
                    draft,
                    critique),
                cancellationToken);
            var analysis = analysisCompletion.Value;
            modelInvocations.Add(analysisCompletion.Metadata);
            _contractValidator.ValidateFinal(analysis, ingestion.Chunks);
            await trace.EmitAsync(
                "revision.validated",
                new Dictionary<string, object?>
                {
                    ["pillarCount"] = analysis.Pillars.Length,
                    ["claimCount"] = CountClaims(analysis)
                },
                cancellationToken);
            await TracePhaseCompletedAsync(
                trace,
                "revision",
                analysis.Topic,
                analysisCompletion.Metadata,
                cancellationToken);
            await WriteJsonArtifactAsync(
                artifacts,
                artifacts.AnalysisPath,
                analysis,
                "analysis",
                trace,
                cancellationToken);
            await WriteJsonArtifactAsync(
                artifacts,
                artifacts.ModelUsagePath,
                new
                {
                    invocations = modelInvocations,
                    totals = new
                    {
                        inputTokens = modelInvocations.Sum(item => item.InputTokens),
                        outputTokens = modelInvocations.Sum(item => item.OutputTokens),
                        cachedInputTokens = modelInvocations.Sum(
                            item => item.CachedInputTokens),
                        reasoningTokens = modelInvocations.Sum(
                            item => item.ReasoningTokens)
                    }
                },
                "model-usage",
                trace,
                cancellationToken);

            var evidenceMatrix = _evidenceMatrixBuilder.Build(
                analysis,
                ingestion.Documents,
                ingestion.Chunks);
            await WriteJsonArtifactAsync(
                artifacts,
                artifacts.EvidenceMatrixPath,
                evidenceMatrix,
                "evidence-matrix",
                trace,
                cancellationToken);
            await trace.EmitAsync(
                "evidence_matrix.completed",
                new Dictionary<string, object?>
                {
                    ["claimCount"] = evidenceMatrix.Claims.Count,
                    ["sourceCoverage"] = evidenceMatrix.CorpusSourceCoverage,
                    ["averageLexicalGrounding"] =
                        evidenceMatrix.AverageLexicalGroundingScore
                },
                cancellationToken);

            var report = MarkdownReportRenderer.Render(
                analysis,
                ingestion.Documents,
                evidenceMatrix,
                sourceRiskReport);
            await WriteTextArtifactAsync(
                artifacts,
                artifacts.ReportPath,
                report,
                "report",
                trace,
                cancellationToken);
            await WriteTextArtifactAsync(
                artifacts,
                artifacts.HtmlReportPath,
                HtmlReportRenderer.Render(
                    analysis,
                    ingestion.Documents,
                    ingestion.Chunks,
                    evidenceMatrix,
                    sourceRiskReport),
                "html-report",
                trace,
                cancellationToken);
            await WriteTextArtifactAsync(
                artifacts,
                artifacts.RunSummaryPath,
                RenderRunSummary(
                    runId,
                    request.Provider,
                    ingestion,
                    analysis,
                    evidenceMatrix,
                    sourceRiskReport,
                    modelInvocations,
                    artifacts),
                "run-summary",
                trace,
                cancellationToken);

            await _artifactWriter.WriteTextAsync(
                artifacts,
                artifacts.EvalReportPath,
                "# Evaluation Report\n\nEvaluation has not run yet.\n",
                cancellationToken);
            await trace.EmitAsync(
                "artifact.reserved",
                new Dictionary<string, object?>
                {
                    ["name"] = Path.GetFileName(artifacts.EvalReportPath)
                },
                cancellationToken);

            await trace.EmitAsync(
                "run.completed",
                new Dictionary<string, object?>
                {
                    ["topic"] = analysis.Topic,
                    ["pillarCount"] = analysis.Pillars.Length,
                    ["claimCount"] = CountClaims(analysis),
                    ["inputTokens"] = modelInvocations.Sum(item => item.InputTokens),
                    ["outputTokens"] = modelInvocations.Sum(item => item.OutputTokens)
                },
                cancellationToken);

            await trace.EmitAsync(
                "eval.started",
                cancellationToken: cancellationToken);
            var evalReport = await _evaluator.EvaluateAsync(
                new EvalContext(
                    artifacts,
                    ingestion.Documents,
                    ingestion.Chunks,
                    analysis,
                    evidenceMatrix,
                    sourceRiskReport,
                    report,
                    trace.Events),
                cancellationToken);
            await trace.EmitAsync(
                "eval.completed",
                new Dictionary<string, object?>
                {
                    ["passed"] = evalReport.Passed,
                    ["checkCount"] = evalReport.Checks.Count
                },
                cancellationToken);
            await WriteTextArtifactAsync(
                artifacts,
                artifacts.EvalReportPath,
                DistillationEvaluator.RenderMarkdown(evalReport),
                "eval-report",
                trace,
                cancellationToken);

            await trace.EmitAsync(
                "run.finalized",
                new Dictionary<string, object?>
                {
                    ["evalPassed"] = evalReport.Passed
                },
                cancellationToken);

            return new DistillationRunResult(
                runId,
                artifacts.RunDirectory,
                artifacts.ReportPath,
                artifacts.HtmlReportPath,
                artifacts.TracePath,
                artifacts.EvalReportPath,
                evalReport.Passed
                    ? DistillationRunOutcome.Success
                    : DistillationRunOutcome.EvalFailed);
        }
        catch (Exception exception) when (
            exception is not DistillationRunException &&
            exception is not OperationCanceledException)
        {
            await RecordFailureAsync(
                trace,
                artifacts,
                exception,
                cancellationToken);
            throw new DistillationRunException(
                $"Distillation run '{runId}' failed: {exception.Message}",
                artifacts.RunDirectory,
                exception);
        }
    }

    private async Task WriteTextArtifactAsync(
        RunArtifactPaths artifacts,
        string path,
        string content,
        string kind,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        await _artifactWriter.WriteTextAsync(
            artifacts,
            path,
            content,
            cancellationToken);
        await TraceArtifactWrittenAsync(
            trace,
            path,
            kind,
            cancellationToken);
    }

    private async Task WriteJsonArtifactAsync<T>(
        RunArtifactPaths artifacts,
        string path,
        T value,
        string kind,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        await _artifactWriter.WriteJsonAsync(
            artifacts,
            path,
            value,
            cancellationToken);
        await TraceArtifactWrittenAsync(
            trace,
            path,
            kind,
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

    private static async Task TracePhaseStartedAsync(
        ITraceSession trace,
        string phase,
        IDistillationModelClient modelClient,
        CancellationToken cancellationToken)
    {
        await trace.EmitAsync(
            $"{phase}.started",
            new Dictionary<string, object?>
            {
                ["phase"] = phase
            },
            cancellationToken);
        await trace.EmitAsync(
            "model.called",
            new Dictionary<string, object?>
            {
                ["phase"] = phase,
                ["provider"] = modelClient.ProviderName
            },
            cancellationToken);
    }

    private static async Task TracePhaseCompletedAsync(
        ITraceSession trace,
        string phase,
        string? topic,
        ModelInvocationMetadata metadata,
        CancellationToken cancellationToken)
    {
        await trace.EmitAsync(
            "model.completed",
            new Dictionary<string, object?>
            {
                ["phase"] = phase,
                ["topic"] = topic,
                ["model"] = metadata.Model,
                ["responseId"] = metadata.ResponseId,
                ["inputTokens"] = metadata.InputTokens,
                ["outputTokens"] = metadata.OutputTokens,
                ["cachedInputTokens"] = metadata.CachedInputTokens,
                ["reasoningTokens"] = metadata.ReasoningTokens
            },
            cancellationToken);
        await trace.EmitAsync(
            $"{phase}.completed",
            new Dictionary<string, object?>
            {
                ["phase"] = phase
            },
            cancellationToken);
    }

    private async Task RecordFailureAsync(
        ITraceSession trace,
        RunArtifactPaths artifacts,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            await trace.EmitAsync(
                "run.failed",
                new Dictionary<string, object?>
                {
                    ["exceptionType"] = exception.GetType().Name,
                    ["message"] = exception.Message
                },
                cancellationToken);
        }
        catch
        {
            // The original exception remains authoritative.
        }

        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.InputManifestPath,
            "# Input Manifest\n\nRun failed before the corpus manifest was completed.\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.RunManifestPath,
            "{}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.SourceRiskPath,
            "{\"findings\":[]}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.EvidencePath,
            "{\"documents\":[],\"chunks\":[]}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.EvidenceMatrixPath,
            "{\"claims\":[],\"pillars\":[]}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.ModelUsagePath,
            "{\"invocations\":[],\"totals\":{}}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.DraftPath,
            "{}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.CritiquePath,
            "{}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.AnalysisPath,
            "{}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.ReportPath,
            "# Distillation Failed\n\nNo report was produced.\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.HtmlReportPath,
            "<!doctype html><html><body><h1>Distillation failed</h1></body></html>");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.RunSummaryPath,
            $"# Run Summary\n\nStatus: **FAILED**\n\n{exception.Message}\n");
        await WriteFailureArtifactIfMissingAsync(
            artifacts,
            artifacts.EvalReportPath,
            "# Evaluation Report\n\nOverall: **FAIL**\n\n" +
            "- [ ] run completed: The runtime failed before evaluation completed.\n");
    }

    private async Task WriteFailureArtifactIfMissingAsync(
        RunArtifactPaths artifacts,
        string path,
        string content)
    {
        if (File.Exists(path))
        {
            return;
        }

        try
        {
            await _artifactWriter.WriteTextAsync(artifacts, path, content);
        }
        catch
        {
            // Failure artifacts are best effort.
        }
    }

    private static string RenderInputManifest(
        IReadOnlyList<SourceDocument> documents)
    {
        var builder = new StringBuilder()
            .AppendLine("# Input Manifest")
            .AppendLine();

        foreach (var document in documents)
        {
            builder
                .Append("- **")
                .Append(document.Id)
                .Append("**: `")
                .Append(document.RelativePath)
                .Append("` (")
                .Append(document.Content.Length)
                .Append(" characters, SHA-256 `")
                .Append(document.Sha256)
                .AppendLine("`)");
        }

        return builder.ToString();
    }

    private static string RenderRunSummary(
        string runId,
        string provider,
        IngestionResult ingestion,
        DistillationDraft analysis,
        EvidenceMatrix evidenceMatrix,
        SourceRiskReport sourceRiskReport,
        IReadOnlyList<ModelInvocationMetadata> modelInvocations,
        RunArtifactPaths artifacts) =>
        $"""
        # Run Summary

        - Run ID: `{runId}`
        - Pattern: `analyze-critic-revise`
        - Provider: `{provider}`
        - Documents: {ingestion.Documents.Count}
        - Evidence chunks: {ingestion.Chunks.Count}
        - Inferred topic: {analysis.Topic}
        - Pillars: {analysis.Pillars.Length}
        - Atomic claims: {CountClaims(analysis)}
        - Cited source coverage: {evidenceMatrix.CorpusSourceCoverage:P0}
        - Mean lexical grounding signal: {evidenceMatrix.AverageLexicalGroundingScore:P0}
        - Source-risk findings: {sourceRiskReport.Findings.Count}
        - Input tokens: {modelInvocations.Sum(item => item.InputTokens)}
        - Output tokens: {modelInvocations.Sum(item => item.OutputTokens)}
        - Cached input tokens: {modelInvocations.Sum(item => item.CachedInputTokens)}
        - Reasoning tokens: {modelInvocations.Sum(item => item.ReasoningTokens)}
        - Output directory: `{artifacts.RunDirectory}`

        Deterministic evaluation is recorded in `eval_report.md`.
        """;

    private RunManifest CreateRunManifest(
        string runId,
        DistillationRunRequest request,
        IngestionResult ingestion,
        PromptSet prompts,
        IDistillationModelClient modelClient) =>
        new(
            2,
            runId,
            _timeProvider.GetUtcNow(),
            modelClient.ProviderName,
            modelClient.ModelName,
            "analyze-critic-revise",
            ComputeSha256(
                string.Join(
                    "\n",
                    ingestion.Documents.Select(
                        document => $"{document.Id}:{document.Sha256}"))),
            ComputeSha256(
                $"{prompts.Analyze}\n---CRITIC---\n{prompts.Critic}" +
                $"\n---REVISE---\n{prompts.Revise}"),
            ingestion.Documents.Count,
            ingestion.Chunks.Count,
            request.MaxInputCharacters,
            request.ChunkSizeCharacters,
            request.ChunkOverlapCharacters);

    private static string ComputeSha256(string content) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static int CountClaims(DistillationDraft analysis) =>
        analysis.Pillars.Sum(pillar => pillar.Claims.Length);

    private static void ValidateRequest(DistillationRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PromptsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Provider);
        if (request.ChunkOverlapCharacters < 0 ||
            request.ChunkOverlapCharacters >= request.ChunkSizeCharacters)
        {
            throw new ArgumentException(
                "Chunk overlap must be non-negative and smaller than chunk size.",
                nameof(request));
        }

        var inputRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(request.InputDirectory));
        var outputRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(request.OutputRoot));
        var inputPrefix = inputRoot + Path.DirectorySeparatorChar;
        if (outputRoot.Equals(inputRoot, StringComparison.OrdinalIgnoreCase) ||
            outputRoot.StartsWith(inputPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The output root must not be inside the input corpus directory.",
                nameof(request));
        }
    }
}
