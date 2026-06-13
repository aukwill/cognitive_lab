using CognitiveRuntime.Cli;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Tests;

public sealed class ExitCodesTests
{
    [Theory]
    [InlineData(RunOutcome.Success, ExitCodes.Success)]
    [InlineData(RunOutcome.EvalFailed, ExitCodes.EvalFailed)]
    [InlineData(RunOutcome.RuntimeFailed, ExitCodes.RuntimeFailed)]
    [InlineData(RunOutcome.Cancelled, ExitCodes.Cancelled)]
    public void FromOutcome_MapsEachOutcomeToItsExitCode(RunOutcome outcome, int expectedExitCode)
    {
        Assert.Equal(expectedExitCode, ExitCodes.FromOutcome(outcome));
    }

    [Fact]
    public void FromOutcome_ThrowsForUnknownOutcome()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ExitCodes.FromOutcome((RunOutcome)999));
    }
}
