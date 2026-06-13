using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Runs a runtime-configured, ordered sequence of modes as pipeline stages.
/// Each stage is a complete <see cref="CriticRevisionPattern"/> run of one
/// mode, fed by the previous stage's authoritative revision (or the
/// pipeline's initial input for the first stage). The stage list is fixed at
/// construction; the model cannot add, remove, reorder, or repeat stages.
/// </summary>
public sealed class LinearPipelinePattern
{
    private static readonly CriticRevisionPattern StagePattern = new();

    public LinearPipelinePattern(IReadOnlyList<string> stageModeNames)
    {
        ArgumentNullException.ThrowIfNull(stageModeNames);

        if (stageModeNames.Count == 0)
        {
            throw new ArgumentException(
                "A pipeline requires at least one stage.",
                nameof(stageModeNames));
        }

        if (stageModeNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Pipeline stage mode names cannot be null or blank.",
                nameof(stageModeNames));
        }

        StageModeNames = stageModeNames;
    }

    public string Name => "linear-pipeline";

    public IReadOnlyList<string> StageModeNames { get; }

    public async Task<IReadOnlyList<PipelineStageResult>> RunAsync(
        string runId,
        string initialInput,
        IModeLoader modeLoader,
        IModelClient modelClient,
        PhaseRunner phaseRunner,
        IArtifactWriter artifactWriter,
        RunArtifactPaths rootArtifacts,
        ITraceSession trace,
        CancellationToken cancellationToken = default)
    {
        var stageResults = new List<PipelineStageResult>(StageModeNames.Count);
        var stageInput = initialInput;

        for (var index = 0; index < StageModeNames.Count; index++)
        {
            var stageIndex = index + 1;
            var modeName = StageModeNames[index];

            await trace.EmitAsync(
                "stage.started",
                new Dictionary<string, object?>
                {
                    ["stageIndex"] = stageIndex,
                    ["mode"] = modeName
                },
                cancellationToken);

            var mode = await modeLoader.LoadAsync(modeName, cancellationToken: cancellationToken);

            var steps = StagePattern.Plan(mode);
            var phaseResults = new List<PhaseResult>(steps.Count);

            foreach (var step in steps)
            {
                var context = StagePattern.SelectContext(
                    step,
                    Array.AsReadOnly(phaseResults.ToArray()));

                var phaseResult = await phaseRunner.RunAsync(
                    runId,
                    mode,
                    step.Phase,
                    stageInput,
                    context,
                    modelClient,
                    trace,
                    cancellationToken);

                phaseResults.Add(phaseResult);
            }

            await trace.EmitAsync(
                "stage.completed",
                new Dictionary<string, object?>
                {
                    ["stageIndex"] = stageIndex,
                    ["mode"] = modeName,
                    ["phaseCount"] = phaseResults.Count
                },
                cancellationToken);

            var resultContent = ResultComposer.Compose(mode, phaseResults);
            var revisionContent = phaseResults
                .Single(result => result.PhaseKind == PhaseKind.Revision)
                .Content;

            var stageArtifacts = await artifactWriter.PrepareStageAsync(
                rootArtifacts,
                stageIndex,
                modeName,
                cancellationToken);

            await artifactWriter.WriteAsync(
                stageArtifacts,
                ArtifactKind.Input,
                stageInput,
                cancellationToken);
            await artifactWriter.WriteAsync(
                stageArtifacts,
                ArtifactKind.Result,
                resultContent,
                cancellationToken);

            stageResults.Add(
                new PipelineStageResult(
                    stageIndex,
                    modeName,
                    mode,
                    phaseResults,
                    resultContent,
                    revisionContent,
                    stageArtifacts.RunDirectory));

            stageInput = revisionContent;
        }

        return stageResults;
    }
}
