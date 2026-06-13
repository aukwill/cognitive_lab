namespace CognitiveRuntime.Core.Runtime.Orchestration;

public interface IOrchestrationPatternFactory
{
    IOrchestrationPattern Resolve(string patternName);
}

/// <summary>
/// Resolves an <see cref="IOrchestrationPattern"/> by its declared
/// <see cref="IOrchestrationPattern.Name"/>. <c>linear-pipeline</c> is not
/// registered here: <see cref="LinearPipelinePattern"/> is not an
/// <see cref="IOrchestrationPattern"/> and is run directly by the
/// orchestrator.
/// </summary>
public sealed class OrchestrationPatternFactory : IOrchestrationPatternFactory
{
    private readonly IReadOnlyDictionary<string, IOrchestrationPattern> _patterns;

    public OrchestrationPatternFactory(IEnumerable<IOrchestrationPattern> patterns)
    {
        _patterns = patterns.ToDictionary(
            pattern => pattern.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public IOrchestrationPattern Resolve(string patternName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patternName);

        if (_patterns.TryGetValue(patternName, out var pattern))
        {
            return pattern;
        }

        var available = string.Join(", ", _patterns.Keys.Order());
        throw new ArgumentException(
            $"Unknown orchestration pattern '{patternName}'. Available patterns: {available}.");
    }
}
