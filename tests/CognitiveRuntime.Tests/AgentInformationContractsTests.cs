using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Tests;

public sealed class AgentInformationContractsTests
{
    private static readonly DateTimeOffset RecordedAt =
        new(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TypedIds_RejectBlankValues()
    {
        Assert.Throws<ArgumentException>(() => new ObservationId(""));
        Assert.Throws<ArgumentException>(() => new EvidenceId(" "));
        Assert.Throws<ArgumentException>(() => new ClaimId(""));
        Assert.Throws<ArgumentException>(() => new BeliefId(" "));
        Assert.Throws<ArgumentException>(() => new BlackboardEntryId(""));
        Assert.Throws<ArgumentException>(() => new ContextProjectionId(" "));
    }

    [Fact]
    public void CommittedConcepts_AreRunScopedAndRuntimeAuthoritative()
    {
        var observation = CreateObservation();
        var evidence = CreateEvidence(observation);
        var claim = CreateClaim(evidence);
        var belief = CreateBelief(claim, evidence);
        var blackboardEntry = CreateBlackboardEntry(claim);
        var projection = CreateProjection(evidence, blackboardEntry);

        IRunScopedAgentInformation[] committed =
        [
            observation,
            evidence,
            claim,
            belief,
            blackboardEntry,
            projection
        ];

        Assert.All(
            committed,
            item =>
            {
                Assert.Equal("run-001", item.RunId);
                Assert.Equal("run-001", item.Provenance.RunId);
                Assert.Equal(
                    InformationCommitAuthority.Runtime,
                    item.CommitAuthority);
                Assert.Equal(
                    InformationLifecycleState.Active,
                    item.Lifecycle.State);
                Assert.False(string.IsNullOrWhiteSpace(item.Reference.Id));
            });
    }

    [Fact]
    public void ClaimProposal_IsNotCommittedRunState()
    {
        var proposal = new ClaimProposal(
            "The runtime controls execution.",
            "revision",
            [new EvidenceId("evidence-001")]);

        Assert.Equal(
            InformationCommitAuthority.ModelProposal,
            proposal.CommitAuthority);
        Assert.False(
            typeof(IRunScopedAgentInformation).IsAssignableFrom(
                typeof(ClaimProposal)));
    }

    [Fact]
    public void Relationships_PreserveTypedLineageWithoutRelabelingContent()
    {
        var observation = CreateObservation();
        var evidence = CreateEvidence(observation);
        var claim = CreateClaim(evidence);
        var belief = CreateBelief(claim, evidence);

        Assert.Equal(observation.Id, evidence.ObservationId);
        Assert.True(claim.EvidenceIds.SequenceEqual([evidence.Id]));
        Assert.True(belief.ClaimIds.SequenceEqual([claim.Id]));
        Assert.True(belief.EvidenceIds.SequenceEqual([evidence.Id]));
        Assert.True(
            evidence.Provenance.Inputs.SequenceEqual([observation.Reference]));
        Assert.True(
            claim.Provenance.Inputs.SequenceEqual([evidence.Reference]));
    }

    [Fact]
    public void StructuredValue_ClonesJsonAndProjectionRecordsExactDisclosure()
    {
        StructuredInformationValue value;
        using (var document = JsonDocument.Parse("""{"decision":"continue"}"""))
        {
            value = new StructuredInformationValue(
                "runtime.decision.v1",
                document.RootElement);
        }

        var claim = CreateClaim(CreateEvidence(CreateObservation()));
        var entry = CreateBlackboardEntry(claim, value);
        var projection = CreateProjection(
            CreateEvidence(CreateObservation()),
            entry);

        Assert.Equal(
            "continue",
            entry.Value.Value.GetProperty("decision").GetString());
        var projected = Assert.Single(projection.Items);
        Assert.Equal(entry.Reference, projected.Source);
        Assert.Equal("""{"decision":"continue"}""", projected.Content);
    }

    [Fact]
    public void Contracts_RoundTripAsPlainJson()
    {
        var observation = CreateObservation();
        var evidence = CreateEvidence(observation);
        var claim = CreateClaim(evidence);
        var entry = CreateBlackboardEntry(claim);
        var projection = CreateProjection(evidence, entry);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        var json = JsonSerializer.Serialize(projection, options);
        var roundTripped = JsonSerializer.Deserialize<ContextProjection>(
            json,
            options);

        Assert.NotNull(roundTripped);
        Assert.Equal(projection.Id, roundTripped.Id);
        Assert.Equal(projection.NodeId, roundTripped.NodeId);
        Assert.True(projection.Items.SequenceEqual(roundTripped.Items));
        Assert.Equal(projection.Provenance.RunId, roundTripped.Provenance.RunId);
        Assert.Equal(
            projection.Provenance.SourceKind,
            roundTripped.Provenance.SourceKind);
        Assert.Equal(
            projection.Provenance.SourceId,
            roundTripped.Provenance.SourceId);
        Assert.True(
            projection.Provenance.Inputs.SequenceEqual(
                roundTripped.Provenance.Inputs));
    }

    private static Observation CreateObservation() =>
        new(
            new ObservationId("observation-001"),
            "run-001",
            "The trace ends with run.finalized.",
            "text/plain",
            Provenance(
                InformationSourceKind.File,
                "trace.json",
                inputs: []),
            ActiveLifecycle());

    private static Evidence CreateEvidence(Observation observation) =>
        new(
            new EvidenceId("evidence-001"),
            "run-001",
            observation.Id,
            "Verify terminal trace integrity.",
            "[E1]",
            Provenance(
                InformationSourceKind.Runtime,
                "evidence-admission",
                [observation.Reference]),
            ActiveLifecycle());

    private static Claim CreateClaim(Evidence evidence) =>
        new(
            new ClaimId("claim-001"),
            "run-001",
            "The run finalized successfully.",
            ClaimOrigin.ModelProposal,
            [evidence.Id],
            Provenance(
                InformationSourceKind.Model,
                "revision-response",
                [evidence.Reference],
                "revision"),
            ActiveLifecycle());

    private static Belief CreateBelief(Claim claim, Evidence evidence) =>
        new(
            new BeliefId("belief-001"),
            "run-001",
            [claim.Id],
            [evidence.Id],
            BeliefAssessment.Supported,
            "The cited terminal trace event is present.",
            Provenance(
                InformationSourceKind.Runtime,
                "belief-transition",
                [claim.Reference, evidence.Reference]),
            ActiveLifecycle());

    private static BlackboardEntry CreateBlackboardEntry(
        Claim claim,
        StructuredInformationValue? value = null)
    {
        value ??= CreateStructuredValue("""{"decision":"continue"}""");
        return new BlackboardEntry(
            new BlackboardEntryId("blackboard-001"),
            "run-001",
            "runtime.decision",
            value,
            Provenance(
                InformationSourceKind.Runtime,
                "blackboard-commit",
                [claim.Reference]),
            ActiveLifecycle());
    }

    private static ContextProjection CreateProjection(
        Evidence evidence,
        BlackboardEntry entry) =>
        new(
            new ContextProjectionId("projection-001"),
            "run-001",
            "stage-01.main",
            [
                new ContextProjectionItem(
                    "runtime.decision",
                    entry.Reference,
                    "application/json",
                    """{"decision":"continue"}""")
            ],
            Provenance(
                InformationSourceKind.Runtime,
                "context-projection",
                [evidence.Reference, entry.Reference]),
            ActiveLifecycle());

    private static StructuredInformationValue CreateStructuredValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new StructuredInformationValue(
            "runtime.decision.v1",
            document.RootElement);
    }

    private static InformationProvenance Provenance(
        InformationSourceKind sourceKind,
        string sourceId,
        ImmutableArray<AgentInformationReference> inputs,
        string? producerNodeId = null) =>
        new(
            "run-001",
            sourceKind,
            sourceId,
            producerNodeId,
            inputs,
            RecordedAt);

    private static InformationLifecycle ActiveLifecycle() =>
        new(InformationLifecycleState.Active, RecordedAt);
}
