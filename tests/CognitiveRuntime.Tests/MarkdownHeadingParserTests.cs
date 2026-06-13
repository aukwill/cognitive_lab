using CognitiveRuntime.Core.Evaluation;

namespace CognitiveRuntime.Tests;

public sealed class MarkdownHeadingParserTests
{
    [Fact]
    public void Parse_ReturnsHeadingsWithLevelAndLineNumber()
    {
        var content = """
            # Title

            ## Problem

            Body text.

            ## Objective
            """;

        var headings = MarkdownHeadingParser.Parse(content);

        Assert.Equal(3, headings.Count);
        Assert.Equal(new MarkdownHeading(1, "Title", 1), headings[0]);
        Assert.Equal(new MarkdownHeading(2, "Problem", 3), headings[1]);
        Assert.Equal(new MarkdownHeading(2, "Objective", 7), headings[2]);
    }

    [Fact]
    public void Parse_IgnoresHeadingsInsideFencedCodeBlocks()
    {
        var content = """
            ## Problem

            ```text
            ## Not A Heading
            ```

            ## Objective
            """;

        var headings = MarkdownHeadingParser.Parse(content);

        Assert.Equal(2, headings.Count);
        Assert.Equal("Problem", headings[0].Text);
        Assert.Equal("Objective", headings[1].Text);
    }

    [Fact]
    public void Parse_StripsOptionalTrailingHashes()
    {
        var content = "## Problem ##";

        var headings = MarkdownHeadingParser.Parse(content);

        Assert.Equal("Problem", Assert.Single(headings).Text);
    }

    [Fact]
    public void Parse_IgnoresIndentedCodeBlockLines()
    {
        var content = "    ## Not A Heading";

        var headings = MarkdownHeadingParser.Parse(content);

        Assert.Empty(headings);
    }

    [Theory]
    [InlineData("## Problem", 2, "Problem")]
    [InlineData("# Title", 1, "Title")]
    [InlineData("###Not A Heading", 0, "")]
    [InlineData("####### Too Deep", 0, "")]
    public void TryParseHeading_ParsesLevelAndText(string line, int expectedLevel, string expectedText)
    {
        var ok = MarkdownHeadingParser.TryParseHeading(line, out var level, out var text);

        if (expectedLevel == 0)
        {
            Assert.False(ok);
        }
        else
        {
            Assert.True(ok);
            Assert.Equal(expectedLevel, level);
            Assert.Equal(expectedText, text);
        }
    }
}
