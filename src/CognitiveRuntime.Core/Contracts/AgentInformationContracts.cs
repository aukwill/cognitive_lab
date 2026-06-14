using System.Collections.Immutable;

namespace CognitiveRuntime.Core.Contracts;

public sealed record Observation(
    ObservationId Id,
    string RunId,
    string Content,
    string MediaType,
    InformationProvenance Provenance,
    InformationLifecycle Lifecycle) : IRunScopedAgentInformation
{
    public AgentInformationReference Reference =>
        new(AgentInformationKind.Observation, Id.Value);

    public InformationCommitAuthority CommitAuthority =>
        InformationCommitAuthority.Runtime;
}

public sealed record Evidence(
    EvidenceId Id,
    string RunId,
    ObservationId ObservationId,
    string Purpose,
    string Citation,
    InformationProvenance Provenance,
    InformationLifecycle Lifecycle) : IRunScopedAgentInformation
{
    public AgentInformationReference Reference =>
        new(AgentInformationKind.Evidence, Id.Value);

    public InformationCommitAuthority CommitAuthority =>
        InformationCommitAuthority.Runtime;
}

public sealed record ClaimProposal(
    string Proposition,
    string ProducerNodeId,
    ImmutableArray<EvidenceId> CitedEvidenceIds)
{
    public InformationCommitAuthority CommitAuthority =>
        InformationCommitAuthority.ModelProposal;
}

public sealed record Claim(
    ClaimId Id,
    string RunId,
    string Proposition,
    ClaimOrigin Origin,
    ImmutableArray<EvidenceId> EvidenceIds,
    InformationProvenance Provenance,
    InformationLifecycle Lifecycle) : IRunScopedAgentInformation
{
    public AgentInformationReference Reference =>
        new(AgentInformationKind.Claim, Id.Value);

    public InformationCommitAuthority CommitAuthority =>
        InformationCommitAuthority.Runtime;
}

public sealed record Belief(
    BeliefId Id,
    string RunId,
    ImmutableArray<ClaimId> ClaimIds,
    ImmutableArray<EvidenceId> EvidenceIds,
    BeliefAssessment Assessment,
    string Rationale,
    InformationProvenance Provenance,
    InformationLifecycle Lifecycle) : IRunScopedAgentInformation
{
    public AgentInformationReference Reference =>
        new(AgentInformationKind.Belief, Id.Value);

    public InformationCommitAuthority CommitAuthority =>
        InformationCommitAuthority.Runtime;
}

public sealed record BlackboardEntry(
    BlackboardEntryId Id,
    string RunId,
    string Key,
    StructuredInformationValue Value,
    InformationProvenance Provenance,
    InformationLifecycle Lifecycle) : IRunScopedAgentInformation
{
    public AgentInformationReference Reference =>
        new(AgentInformationKind.BlackboardEntry, Id.Value);

    public InformationCommitAuthority CommitAuthority =>
        InformationCommitAuthority.Runtime;
}

public sealed record ContextProjectionItem(
    string Name,
    AgentInformationReference Source,
    string MediaType,
    string Content);

public sealed record ContextProjection(
    ContextProjectionId Id,
    string RunId,
    string NodeId,
    ImmutableArray<ContextProjectionItem> Items,
    InformationProvenance Provenance,
    InformationLifecycle Lifecycle) : IRunScopedAgentInformation
{
    public AgentInformationReference Reference =>
        new(AgentInformationKind.ContextProjection, Id.Value);

    public InformationCommitAuthority CommitAuthority =>
        InformationCommitAuthority.Runtime;
}
