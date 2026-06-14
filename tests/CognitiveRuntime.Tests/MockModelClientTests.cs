using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Models;

namespace CognitiveRuntime.Tests;

public sealed class MockModelClientTests
{
    public static TheoryData<string, IReadOnlyList<string>> ModeContracts =>
        new()
        {
            {
                "frame",
                ["## Problem", "## Objective", "## Constraints", "## Unknowns", "## Next Actions"]
            },
            {
                "challenge",
                ["## Target Claim", "## Assumptions", "## Failure Modes", "## Counterarguments", "## Tests"]
            },
            {
                "synthesize",
                ["## Shared Ground", "## Tensions", "## Synthesis", "## Tradeoffs", "## Recommendation"]
            }
        };

    [Theory]
    [MemberData(nameof(ModeContracts))]
    public async Task CompleteAsync_ProducesContractValidMainAndRevision(
        string modeName,
        IReadOnlyList<string> requiredHeadings)
    {
        var client = new MockModelClient();
        var validator = new OutputContractValidator();
        var contract = new OutputContract
        {
            RequiredHeadings = [.. requiredHeadings],
            MinimumLength = 200
        };

        var main = await client.CompleteAsync(
            CreateRequest(modeName, PhaseKind.Main, []));
        var mainResult = CreateResult(PhaseKind.Main, main);
        var critic = await client.CompleteAsync(
            CreateRequest(modeName, PhaseKind.Critic, [mainResult]));
        var criticResult = CreateResult(PhaseKind.Critic, critic);
        var revision = await client.CompleteAsync(
            CreateRequest(
                modeName,
                PhaseKind.Revision,
                [mainResult, criticResult]));

        Assert.True(validator.Validate(main.Content, contract).Passed);
        Assert.NotEmpty(LoopEfficacyEvaluator.ParseFindings(critic.Content));
        Assert.True(validator.Validate(revision.Content, contract).Passed);
    }

    private static ModelRequest CreateRequest(
        string modeName,
        PhaseKind phaseKind,
        IReadOnlyList<PhaseResult> priorResults) =>
        new(
            "run-001",
            modeName,
            phaseKind.ToString().ToLowerInvariant(),
            phaseKind,
            "Test prompt.",
            "Build a traceable local-first cognitive runtime.",
            priorResults);

    private static PhaseResult CreateResult(
        PhaseKind phaseKind,
        ModelResponse response) =>
        new(
            phaseKind.ToString().ToLowerInvariant(),
            phaseKind,
            response.Content,
            response.Provider,
            response.Model);
}
