using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;

namespace CognitiveRuntime.Tests;

public sealed class LoopEfficacyEvaluatorTests
{
    private static readonly OutputContract Contract = new()
    {
        RequiredHeadings = ["## Problem", "## Objective"],
        MinimumLength = 1
    };

    private static readonly string Draft = """
        ## Problem

        Original problem statement.

        ## Objective

        Original objective statement.
        """;

    private static readonly string Critic = """
        ## Findings

        - [Problem] The problem statement is too vague.
        """;

    [Fact]
    public void Evaluate_PassesWhenFlaggedSectionChanged()
    {
        var revision = """
            ## Problem

            Revised, more specific problem statement.

            ## Objective

            Original objective statement.
            """;
        var evaluator = new LoopEfficacyEvaluator();

        var result = evaluator.Evaluate(Draft, Critic, revision, Contract);

        Assert.True(result.Passed);
        Assert.Contains("## Problem", result.Details);
    }

    [Fact]
    public void Evaluate_FailsWhenRevisionIsByteIdenticalToDraft()
    {
        var evaluator = new LoopEfficacyEvaluator();

        var result = evaluator.Evaluate(Draft, Critic, Draft, Contract);

        Assert.False(result.Passed);
        Assert.Contains("did not change any required section", result.Details);
    }

    [Fact]
    public void Evaluate_FailsWhenNoFlaggedSectionChanged()
    {
        var revision = """
            ## Problem

            Original problem statement.

            ## Objective

            Revised objective statement.
            """;
        var evaluator = new LoopEfficacyEvaluator();

        var result = evaluator.Evaluate(Draft, Critic, revision, Contract);

        Assert.False(result.Passed);
        Assert.Contains("No flagged section changed", result.Details);
    }

    [Fact]
    public void Evaluate_FailsWhenCriticHasNoFindings()
    {
        var revision = """
            ## Problem

            Revised problem statement.

            ## Objective

            Original objective statement.
            """;
        var criticWithoutFindings = "## Coverage\n\nNo findings declared.";
        var evaluator = new LoopEfficacyEvaluator();

        var result = evaluator.Evaluate(Draft, criticWithoutFindings, revision, Contract);

        Assert.False(result.Passed);
        Assert.Contains("no parseable findings", result.Details);
    }

    [Fact]
    public void ParseFindings_ParsesListItemsUnderFindingsHeading()
    {
        var critic = """
            ## Findings

            - [Problem] First finding.
            - [Objective] Second finding.

            ## Other

            - [Ignored] Not under findings.
            """;

        var findings = LoopEfficacyEvaluator.ParseFindings(critic);

        Assert.Equal(2, findings.Count);
        Assert.Equal("Problem", findings[0].SectionHeading);
        Assert.Equal("First finding.", findings[0].Description);
        Assert.Equal("Objective", findings[1].SectionHeading);
        Assert.Equal("Second finding.", findings[1].Description);
    }
}
