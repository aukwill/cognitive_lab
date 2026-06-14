using CognitiveRuntime.Core.Abstractions;

namespace CognitiveRuntime.Tests;

internal sealed class FixedRunIdGenerator(string runId) : IRunIdGenerator
{
    public string GenerateRunId() => runId;
}
