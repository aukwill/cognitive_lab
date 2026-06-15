using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Experiments.Dungeons;

namespace CognitiveRuntime.Core.Models;

public sealed class MockModelClient : IModelClient
{
    public string ProviderName => "mock";

    public Task<ModelResponse> CompleteAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = request.PhaseKind switch
        {
            PhaseKind.Main => CreateMainResponse(request),
            PhaseKind.Critic => CreateCriticResponse(request),
            PhaseKind.Revision => CreateRevisionResponse(request),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PhaseKind,
                "Unsupported phase kind.")
        };

        return Task.FromResult(
            new ModelResponse(content, ProviderName, "deterministic-template-v2"));
    }

    private static string CreateMainResponse(ModelRequest request)
    {
        var inputSummary = Summarize(request.Input);

        return request.ModeName.ToLowerInvariant() switch
        {
            "dungeon-builder" => CreateDungeonProposal(),
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
            "lens" => $"""
                ## The Concept

                {inputSummary}

                ## The Lens

                Treat this as a raid encounter: a boss with a handful of mechanics and a wipe condition.

                ## The Mapping

                The objective maps to the encounter's enrage timer, the constraints map to mechanics that must be respected on every pull, and the unknowns map to mechanics not yet revealed at the current difficulty.

                ## Where It Breaks Down

                Real-world constraints do not reset on a weekly lockout, and no addon highlights the next failure mode.

                ## Plain Translation

                Without the raid framing: {inputSummary}
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
        var mainResult = GetPriorResult(request, PhaseKind.Main);
        var previousLength = mainResult.Content.Trim().Length;

        var findings = request.ModeName.ToLowerInvariant() switch
        {
            "frame" => """
                - [Problem] The problem statement does not yet bound scope to a single inspectable runtime decision.
                - [Next Actions] The next actions are not concrete or testable enough to verify completion.
                """,
            "challenge" => """
                - [Target Claim] The target claim does not name the specific runtime guarantee being challenged.
                - [Tests] The tests do not yet describe how to force and observe a failure.
                """,
            "synthesize" => """
                - [Shared Ground] The shared ground does not reference the concrete signals from the initial draft.
                - [Recommendation] The recommendation is generic and does not commit to a specific next step.
                """,
            "lens" => """
                - [The Mapping] The mapping leans on generic raid language instead of a specific encounter or mechanic.
                - [Plain Translation] The plain translation repeats lens language instead of standing on its own.
                """,
            _ => throw new InvalidOperationException(
                $"Mock critic findings are not defined for mode '{request.ModeName}'.")
        };

        return $"""
            ### Coverage

            The main phase produced {previousLength} characters and addressed the declared structure.

            ### Revision Guidance

            Preserve the required headings, replace generic claims with evidence when using a real provider, and keep runtime decisions outside model output.

            ## Findings

            {findings}
            """;
    }

    private static string CreateRevisionResponse(ModelRequest request)
    {
        if (string.Equals(
                request.ModeName,
                "dungeon-builder",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateDungeonRevision();
        }

        var mainResult = GetPriorResult(request, PhaseKind.Main);
        var criticResult = GetPriorResult(request, PhaseKind.Critic);
        var inputSummary = Summarize(request.Input);
        var draftSignal = Summarize(mainResult.Content);
        var criticSignal = Summarize(criticResult.Content);

        return request.ModeName.ToLowerInvariant() switch
        {
            "frame" => $"""
                ## Problem

                Refine the original intent into an inspectable runtime problem: {inputSummary}

                ## Objective

                Produce a testable cognitive runtime outcome that incorporates the initial draft signal ({draftSignal}) and the critic guidance ({criticSignal}).

                ## Constraints

                Keep phase order, completion, tool policy, artifacts, traces, and deterministic evaluation under runtime control; model output supplies reasoning content only.

                ## Unknowns

                Real-provider quality, domain evidence, and future tool requirements remain explicit unknowns rather than assumed facts.

                ## Next Actions

                Verify the authoritative revision against the declared headings, inspect the trace and artifacts, and refine mode prose only when deterministic evidence shows a gap.
                """,
            "challenge" => $"""
                ## Target Claim

                The runtime should strengthen this claim without surrendering loop control to the model: {inputSummary}

                ## Assumptions

                The initial draft signal ({draftSignal}) is a proposal rather than evidence, and the runtime contract remains the authority for phase order and completion.

                ## Failure Modes

                The critic identified this material revision signal: {criticSignal}. Failure remains possible through hidden requirements, provider coupling, malformed revisions, or incomplete traces.

                ## Counterarguments

                A fixed three-phase loop can still produce useful challenge work because inspectable context and deterministic checks constrain rather than replace model reasoning.

                ## Tests

                Confirm main, critic, and revision context ordering; force a revision provider failure; submit a malformed revision; and verify the runtime produces valid terminal traces and artifacts.
                """,
            "synthesize" => $"""
                ## Shared Ground

                The original input and initial draft agree on this concern: {inputSummary}. The draft signal was {draftSignal}

                ## Tensions

                Flexible reasoning still competes with deterministic control, and the critic adds this caution: {criticSignal}

                ## Synthesis

                Use one runtime-owned revision that receives typed draft and critic context, then treat only that revision as authoritative for deterministic evaluation.

                ## Tradeoffs

                The fixed loop gives up open-ended self-revision in exchange for traceability, bounded cost, reproducible tests, and clear ownership of completion.

                ## Recommendation

                Keep mode prose replaceable and provider-specific transport isolated while the runtime enforces the three phases, writes artifacts, evaluates the revision, and records the terminal outcome.
                """,
            "lens" => $"""
                ## The Concept

                {inputSummary}

                ## The Lens

                Treat this as a specific raid encounter: a boss with a small number of named mechanics and a defined wipe condition, building on the initial framing ({draftSignal}).

                ## The Mapping

                Map the objective to the encounter's enrage timer, the constraints to mechanics that must be respected on every pull, and the unknowns to mechanics not yet revealed at the current difficulty, addressing the critic's guidance ({criticSignal}).

                ## Where It Breaks Down

                Real-world constraints do not reset on a weekly lockout, and no addon highlights the next failure mode before it happens.

                ## Plain Translation

                Restated on its own, independent of the raid framing: {inputSummary}
                """,
            _ => throw new InvalidOperationException(
                $"Mock revision is not defined for mode '{request.ModeName}'.")
        };
    }

    private static PhaseResult GetPriorResult(
        ModelRequest request,
        PhaseKind phaseKind) =>
        request.PriorPhaseResults.SingleOrDefault(
            result => result.PhaseKind == phaseKind)
        ?? throw new InvalidOperationException(
            $"Mock {request.PhaseKind.ToString().ToLowerInvariant()} phase " +
            $"requires a prior {phaseKind.ToString().ToLowerInvariant()} result.");

    private static string CreateDungeonProposal() =>
        DungeonJson.Serialize(new DungeonPlan(
            SchemaVersion: 1,
            Title: "Mock Proposed Crypt",
            Width: 16,
            Height: 12,
            Rooms:
            [
                new DungeonRoom("entrance", 1, 3, 3, 3),
                new DungeonRoom("hall", 6, 3, 3, 3),
                new DungeonRoom("sanctum", 11, 3, 3, 3)
            ],
            Corridors:
            [
                new DungeonCorridor("corridor-entrance-hall", "entrance", "hall", 1),
                new DungeonCorridor("corridor-hall-sanctum", "hall", "sanctum", 1)
            ],
            Doors: [],
            Markers:
            [
                new DungeonMarker(DungeonMarkerKind.Entrance, "entrance"),
                new DungeonMarker(DungeonMarkerKind.Objective, "sanctum"),
                new DungeonMarker(DungeonMarkerKind.Exit, "entrance")
            ]));

    private static string CreateDungeonRevision() =>
        DungeonJson.Serialize(new DungeonPlan(
            SchemaVersion: 1,
            Title: "Mock Revised Crypt",
            Width: 34,
            Height: 14,
            Rooms:
            [
                new DungeonRoom("entrance", 1, 5, 3, 3),
                new DungeonRoom("hall", 6, 5, 4, 3),
                new DungeonRoom("crossing", 12, 4, 3, 4),
                new DungeonRoom("annex", 17, 5, 5, 3),
                new DungeonRoom("sanctum", 24, 5, 3, 3),
                new DungeonRoom("vault", 12, 9, 4, 4)
            ],
            Corridors:
            [
                new DungeonCorridor("corridor-entrance-hall", "entrance", "hall", 2),
                new DungeonCorridor("corridor-hall-crossing", "hall", "crossing", 2),
                new DungeonCorridor("corridor-crossing-annex", "crossing", "annex", 2),
                new DungeonCorridor("corridor-annex-sanctum", "annex", "sanctum", 2),
                new DungeonCorridor("corridor-crossing-vault", "crossing", "vault", 2),
                new DungeonCorridor("corridor-vault-sanctum", "vault", "sanctum", 2)
            ],
            Doors:
            [
                new DungeonDoor("door-sanctum", "annex", "sanctum", DungeonDoorKind.Guarded)
            ],
            Markers:
            [
                new DungeonMarker(DungeonMarkerKind.Entrance, "entrance"),
                new DungeonMarker(DungeonMarkerKind.Objective, "sanctum"),
                new DungeonMarker(DungeonMarkerKind.Exit, "entrance")
            ]));

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
