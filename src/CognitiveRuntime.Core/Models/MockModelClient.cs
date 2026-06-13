using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Models;

public sealed class MockModelClient : IModelClient
{
    public string ProviderName => "mock";

    public Task<ModelResponse> CompleteAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = request.PhaseKind == PhaseKind.Critic
            ? CreateCriticResponse(request)
            : CreateMainResponse(request);

        return Task.FromResult(
            new ModelResponse(content, ProviderName, "deterministic-template-v1"));
    }

    private static string CreateMainResponse(ModelRequest request)
    {
        var inputSummary = Summarize(request.Input);

        return request.ModeName.ToLowerInvariant() switch
        {
            "frame" => $"""
                ## Problem

                Clarify and bound the intent: {inputSummary}

                ## Objective

                Produce an inspectable runtime outcome with explicit success criteria.

                ## Constraints

                Keep orchestration, policy, traces, artifacts, and evaluation under runtime control.

                ## Unknowns

                Provider quality, future tool needs, and domain-specific acceptance thresholds remain open.

                ## Next Actions

                Validate the frame, run the critic, inspect artifacts, and refine the mode files when needed.
                """,
            "challenge" => $"""
                ## Target Claim

                The current intent can be challenged as follows: {inputSummary}

                ## Assumptions

                The input is sufficiently specific, the selected mode is appropriate, and deterministic checks are meaningful.

                ## Failure Modes

                Hidden requirements, provider coupling, weak output contracts, or untraceable runtime decisions could invalidate the result.

                ## Counterarguments

                A small runtime can remain useful when its boundaries are explicit and its artifacts make limitations visible.

                ## Tests

                Attempt counterexamples, inspect trace ordering, verify artifacts, and force a contract failure.
                """,
            "synthesize" => $"""
                ## Shared Ground

                The input establishes this common concern: {inputSummary}

                ## Tensions

                Flexibility competes with inspectability, while richer model behavior competes with deterministic runtime control.

                ## Synthesis

                Keep the loop fixed and typed while allowing mode prompts and model providers to vary behind stable boundaries.

                ## Tradeoffs

                The MVP favors explicit files and deterministic evaluation over autonomous behavior and broad integration.

                ## Recommendation

                Adopt the smallest runtime that preserves traceability, provider isolation, and replaceable mode content.
                """,
            _ => $"""
                ## Analysis

                {inputSummary}

                ## Recommendation

                Use the declared mode contract to structure the next decision.
                """
        };
    }

    private static string CreateCriticResponse(ModelRequest request)
    {
        var previousLength = request.PreviousOutput?.Trim().Length ?? 0;

        return $"""
            ### Coverage

            The main phase produced {previousLength} characters and addressed the declared structure.

            ### Risks

            Validate assumptions against the original input and avoid treating deterministic mock prose as domain evidence.

            ### Revision Guidance

            Preserve the required headings, replace generic claims with evidence when using a real provider, and keep runtime decisions outside model output.
            """;
    }

    private static string Summarize(string input)
    {
        var singleLine = string.Join(
            " ",
            input.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return singleLine.Length <= 180
            ? singleLine
            : string.Concat(singleLine.AsSpan(0, 177), "...");
    }
}
