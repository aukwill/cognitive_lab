using DocumentDistiller.Cli;

namespace DocumentDistiller.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void Parse_AcceptsDiscoveryConfiguration()
    {
        var options = CliOptions.Parse(
            [
                "--discover",
                "meaningful danger",
                "--include-domain",
                "dndbeyond.com",
                "--include-domain",
                "aonprd.com",
                "--max-sources",
                "6"
            ]);

        Assert.Equal("meaningful danger", options.DiscoveryQuery);
        Assert.Equal(6, options.MaxSources);
        Assert.Equal(
            ["dndbeyond.com", "aonprd.com"],
            options.IncludeDomains);
    }

    [Fact]
    public void Parse_RejectsInputAndDiscoveryTogether()
    {
        var exception = Assert.Throws<CliUsageException>(
            () => CliOptions.Parse(
                ["--input", "docs", "--discover", "query"]));

        Assert.Contains("cannot be used together", exception.Message);
    }
}
