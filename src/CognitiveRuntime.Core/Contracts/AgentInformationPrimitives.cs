using System.Collections.Immutable;
using System.Text.Json;

namespace CognitiveRuntime.Core.Contracts;

public enum AgentInformationKind
{
    Observation,
    Evidence,
    Claim,
    Belief,
    BlackboardEntry,
    ContextProjection
}

public enum InformationSourceKind
{
    Unknown,
    UserInput,
    Environment,
    Tool,
    File,
    Model,
    Provider,
    Runtime,
    DeterministicTransformation
}

public enum InformationLifecycleState
{
    Recorded,
    Active,
    Superseded,
    Withdrawn
}

public enum InformationCommitAuthority
{
    Runtime,
    ModelProposal
}

public enum ClaimOrigin
{
    ModelProposal,
    DeterministicTransformation
}

public enum BeliefAssessment
{
    Unknown,
    Supported,
    Contradicted,
    InsufficientEvidence,
    Stale
}

public sealed record AgentInformationReference(
    AgentInformationKind Kind,
    string Id);

public sealed record InformationProvenance(
    string RunId,
    InformationSourceKind SourceKind,
    string SourceId,
    string? ProducerNodeId,
    ImmutableArray<AgentInformationReference> Inputs,
    DateTimeOffset RecordedAt);

public sealed record InformationLifecycle(
    InformationLifecycleState State,
    DateTimeOffset ChangedAt,
    string? Reason = null);

public sealed record StructuredInformationValue
{
    public StructuredInformationValue(string schema, JsonElement value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        Schema = schema;
        Value = value.Clone();
    }

    public string Schema { get; }

    public JsonElement Value { get; }
}

public interface IRunScopedAgentInformation
{
    string RunId { get; }

    AgentInformationReference Reference { get; }

    InformationProvenance Provenance { get; }

    InformationLifecycle Lifecycle { get; }

    InformationCommitAuthority CommitAuthority { get; }
}
