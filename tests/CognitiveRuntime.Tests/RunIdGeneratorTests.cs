using CognitiveRuntime.Core.Runtime;

namespace CognitiveRuntime.Tests;

public sealed class RunIdGeneratorTests
{
    [Fact]
    public void GenerateRunId_ReturnsRandomGuidWithoutSeparators()
    {
        var generator = new GuidRunIdGenerator();

        var first = generator.GenerateRunId();
        var second = generator.GenerateRunId();

        Assert.True(Guid.TryParseExact(first, "N", out _));
        Assert.True(Guid.TryParseExact(second, "N", out _));
        Assert.NotEqual(first, second);
    }
}
