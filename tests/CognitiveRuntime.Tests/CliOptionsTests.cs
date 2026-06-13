using CognitiveRuntime.Cli;
using Microsoft.Extensions.Configuration;

namespace CognitiveRuntime.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void Parse_AcceptsHtmlAsValueLessSwitch()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = CliOptions.Parse(
            [
                "--mode",
                "frame",
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--html"
            ],
            configuration);

        Assert.True(options.WriteHtmlView);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Parse_AcceptsLensOption()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = CliOptions.Parse(
            [
                "--mode",
                "lens",
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--lens",
                "warcraft"
            ],
            configuration);

        Assert.Equal("warcraft", options.Lens);
    }

    [Fact]
    public void Parse_DefaultsLensToNull()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = CliOptions.Parse(
            [
                "--mode",
                "frame",
                "--input",
                "input.txt",
                "--run-mode",
                "mock"
            ],
            configuration);

        Assert.Null(options.Lens);
    }

    [Fact]
    public void Parse_DefaultsHtmlViewToDisabled()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = CliOptions.Parse(
            [
                "--mode",
                "frame",
                "--input",
                "input.txt",
                "--run-mode",
                "mock"
            ],
            configuration);

        Assert.False(options.WriteHtmlView);
    }

    [Fact]
    public void Parse_DefaultsPatternToCriticRevision()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = CliOptions.Parse(
            [
                "--mode",
                "frame",
                "--input",
                "input.txt",
                "--run-mode",
                "mock"
            ],
            configuration);

        Assert.Equal("critic-revision", options.Pattern);
        Assert.Null(options.PipelineStages);
    }

    [Fact]
    public void Parse_AcceptsPatternOption()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = CliOptions.Parse(
            [
                "--mode",
                "frame",
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--pattern",
                "single-pass"
            ],
            configuration);

        Assert.Equal("single-pass", options.Pattern);
    }

    [Fact]
    public void Parse_RejectsUnknownPattern()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<CliUsageException>(() => CliOptions.Parse(
            [
                "--mode",
                "frame",
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--pattern",
                "bogus"
            ],
            configuration));

        Assert.Contains("bogus", exception.Message);
    }

    [Fact]
    public void Parse_AcceptsPipelineOptionAndDefaultsMode()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = CliOptions.Parse(
            [
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--pattern",
                "linear-pipeline",
                "--pipeline",
                "frame,challenge"
            ],
            configuration);

        Assert.Equal("linear-pipeline", options.Pattern);
        Assert.Equal(["frame", "challenge"], options.PipelineStages);
        Assert.Equal("pipeline", options.Mode);
    }

    [Fact]
    public void Parse_RequiresPipelineForLinearPipelinePattern()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<CliUsageException>(() => CliOptions.Parse(
            [
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--pattern",
                "linear-pipeline"
            ],
            configuration));
    }

    [Fact]
    public void Parse_RejectsPipelineWithoutLinearPipelinePattern()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<CliUsageException>(() => CliOptions.Parse(
            [
                "--mode",
                "frame",
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--pipeline",
                "frame,challenge"
            ],
            configuration));
    }

    [Fact]
    public void Parse_RejectsEmptyPipelineStageNames()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<CliUsageException>(() => CliOptions.Parse(
            [
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--pattern",
                "linear-pipeline",
                "--pipeline",
                "frame,,challenge"
            ],
            configuration));
    }

    [Fact]
    public void Parse_RejectsHtmlWithLinearPipelinePattern()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<CliUsageException>(() => CliOptions.Parse(
            [
                "--input",
                "input.txt",
                "--run-mode",
                "mock",
                "--pattern",
                "linear-pipeline",
                "--pipeline",
                "frame,challenge",
                "--html"
            ],
            configuration));
    }
}
