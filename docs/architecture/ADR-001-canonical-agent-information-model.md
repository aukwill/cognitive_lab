# ADR-001: Canonical Agent Information Model

Status: Accepted

Date: 2026-06-13

## Context

Future evidence, blackboard, context-management, belief-state, and provenance
work needs one vocabulary. Without shared contracts, each feature can invent a
parallel store with different identity, trust, and lifecycle semantics.

The runtime must remain authoritative. Model output can propose reasoning
content, but it cannot promote source material to evidence, commit shared
state, update beliefs, choose disclosure, or assign trust.

## Decision

Use six run-scoped information concepts:

| Concept | Meaning | Runtime relationship |
| --- | --- | --- |
| `Observation` | Immutable content received from a user, environment, tool, file, or provider. | The runtime records identity and source provenance. |
| `Evidence` | An observation admitted for one declared reasoning purpose. | References an `ObservationId`; admission does not imply truth. |
| `Claim` | A proposition recorded from a model proposal or deterministic transformation. | The runtime assigns identity and records cited evidence. |
| `Belief` | Runtime-managed epistemic state over claims and evidence. | Only runtime transitions may commit or supersede it. |
| `BlackboardEntry` | Run-scoped shared structured state. | The runtime owns keys, schema validation, access, and commits. |
| `ContextProjection` | The exact values disclosed to one execution node. | The runtime constructs it from declared information references. |

Each committed concept implements `IRunScopedAgentInformation` and carries:

- a concept-specific typed ID
- a run ID
- an `AgentInformationReference`
- `InformationProvenance`
- `InformationLifecycle`
- runtime commit authority

`ClaimProposal` is deliberately separate. It is model-proposed content, does
not implement `IRunScopedAgentInformation`, and cannot be treated as a
committed `Claim` without an explicit future runtime transition.

## Provenance

`InformationProvenance` records:

- the run ID
- source kind and source ID
- the producing execution node when applicable
- typed input references
- the runtime-recorded timestamp

Provenance records lineage, not truth. A model citation does not create an
`Evidence` record or prove that a claim is supported.

## Lifecycle

The initial lifecycle vocabulary is:

- `Recorded`
- `Active`
- `Superseded`
- `Withdrawn`

Records are immutable snapshots. Future run-state work may add validated
transitions by appending or replacing snapshots, but it must not mutate prior
trace history or source observations.

## Persistence

This ADR defines persistence responsibility, not a new artifact implementation.

| Concept | Run state | Trace | Artifact |
| --- | --- | --- | --- |
| Observation | Full typed record | ID, source metadata, status, bounded hashes | Raw content only in runtime-owned source artifacts when policy allows |
| Evidence | Full typed record | ID, observation ID, purpose, status | Manifest and referenced observation artifact |
| Claim | Full typed record | ID, producer, cited evidence IDs, status | Existing result or phase artifacts; optional future claim manifest |
| Belief | Full typed record | ID, assessment, transition metadata | Optional bounded belief-state snapshot |
| Blackboard entry | Full typed record | ID, key, schema, status, bounded hash | Optional bounded blackboard snapshot |
| Context projection | Exact disclosed values and source references | Projection ID, node ID, source IDs, status, bounded hashes | Optional projection manifest; raw values only when policy permits |

Trace payloads should not duplicate raw observations, blackboard values, or
projected content. Unknown provenance is represented with
`InformationSourceKind.Unknown`, not silently omitted or guessed.

## Ownership Boundaries

- External systems and users produce source content; the runtime records the
  observation.
- The runtime alone admits an observation as evidence for a purpose.
- A model may emit `ClaimProposal`; the runtime decides whether to record a
  `Claim`.
- The runtime alone commits beliefs and blackboard entries.
- The runtime alone computes context projections before provider
  serialization.
- Providers receive a completed projection and cannot widen it.

## Scope

All identities are unique within one run. Cross-run identity, long-term
memory, personal profiles, semantic truth evaluation, belief-update policy,
blackboard mutation policy, and provenance graph traversal remain future
work.

## Consequences

Future work in `RB-007`, `RB-009`, `AR-004`, `AR-010`, and `AR-013` must extend
these contracts rather than introduce competing observation, evidence, claim,
belief, blackboard, or projection stores.
