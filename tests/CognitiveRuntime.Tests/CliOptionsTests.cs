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
}
