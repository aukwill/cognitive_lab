using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;

namespace CognitiveRuntime.Tests;

public sealed class OutputContractValidatorTests
{
    private static readonly OutputContractValidator Validator = new();

    [Fact]
    public void Validate_PassesWhenHeadingsPresentInOrderWithLevelAndSpelling()
    {
        var content = """
            ## Problem

            Body text that is long enough to satisfy the minimum length.

            ## Objective

            More body text.
            """;
        var contract = new OutputContract
        {
            RequiredHeadings = ["## Problem", "## Objective"],
            MinimumLength = 10
        };

        var result = Validator.Validate(content, contract);

        Assert.True(result.Passed);
    }

    [Fact]
    public void Validate_FailsWhenRequiredHeadingHasWrongLevel()
    {
        var content = """
            # Problem

            ## Objective
            """;
        var contract = new OutputContract
        {
            RequiredHeadings = ["## Problem", "## Objective"],
            MinimumLength = 1
        };

        var result = Validator.Validate(content, contract);

        Assert.False(result.Passed);
        Assert.Contains("## Problem", result.Details);
    }

    [Fact]
    public void Validate_FailsAndReportsDuplicateRequiredHeadings()
    {
        var content = """
            ## Problem

            ## Objective

            ## Problem
            """;
        var contract = new OutputContract
        {
            RequiredHeadings = ["## Problem", "## Objective"],
            MinimumLength = 1
        };

        var result = Validator.Validate(content, contract);

        Assert.False(result.Passed);
        Assert.Contains("Duplicate required headings: ## Problem", result.Details);
    }

    [Fact]
    public void Validate_FailsWhenRequiredHeadingsAreOutOfOrder()
    {
        var content = """
            ## Objective

            ## Problem
            """;
        var contract = new OutputContract
        {
            RequiredHeadings = ["## Problem", "## Objective"],
            MinimumLength = 1
        };

        var result = Validator.Validate(content, contract);

        Assert.False(result.Passed);
        Assert.Contains("out of order", result.Details);
    }

    [Fact]
    public void Validate_FailsAndNamesHeadingWithEmptyRequiredSection()
    {
        var content = """
            ## Problem

            ## Objective

            Body text that is long enough.
            """;
        var contract = new OutputContract
        {
            RequiredHeadings = ["## Problem", "## Objective"],
            MinimumLength = 1
        };

        var result = Validator.Validate(content, contract);

        Assert.False(result.Passed);
        Assert.Contains("Empty required sections: ## Problem", result.Details);
    }

    [Fact]
    public void Validate_FailsWhenLastRequiredSectionHasNoTrailingContent()
    {
        var content = """
            ## Problem

            Body text that is long enough.

            ## Objective
            """;
        var contract = new OutputContract
        {
            RequiredHeadings = ["## Problem", "## Objective"],
            MinimumLength = 1
        };

        var result = Validator.Validate(content, contract);

        Assert.False(result.Passed);
        Assert.Contains("Empty required sections: ## Objective", result.Details);
    }

    [Fact]
    public void Validate_IgnoresHeadingsInsideFencedCodeBlocks()
    {
        var content = """
            ```text
            ## Problem
            ```

            ## Objective

            Body text.
            """;
        var contract = new OutputContract
        {
            RequiredHeadings = ["## Problem", "## Objective"],
            MinimumLength = 1
        };

        var result = Validator.Validate(content, contract);

        Assert.False(result.Passed);
        Assert.Contains("Missing headings: ## Problem", result.Details);
    }
}
