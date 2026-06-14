using CognitiveRuntime.Core.Abstractions;

namespace CognitiveRuntime.Core.Runtime;

public sealed class GuidRunIdGenerator : IRunIdGenerator
{
    public string GenerateRunId() => Guid.NewGuid().ToString("N");
}
