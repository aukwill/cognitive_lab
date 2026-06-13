# RB-002: Resume An Interrupted Run

Status: Research complete; implementation deferred

Date: 2026-06-13

## Decision

Do not implement general automatic resume yet.

A future MVP may explicitly resume a run only when all completed work is
represented by a verified durable checkpoint and no external model call has an
unknown outcome. Resume should continue from the first pending runtime step,
reuse recorded phase results, and never repeat a completed model call.

If a run stopped while an external model call might have been accepted but its
result was not durably recorded, classify the run as
`interrupted-ambiguous`. Do not retry that call automatically. Preserve the run
for inspection and require a new run unless the provider exposes a real
idempotency or response-recovery contract.

This is narrower than "resume from the next unstarted phase," but it is the
strongest claim the runtime can make honestly.

## Current Gaps

The current runtime cannot resume safely:

- Run state exists only in local variables.
- A new run ID and output directory are always created.
- `JsonTraceSession` starts with an empty in-memory event list.
- Trace and artifact writes are separate filesystem operations.
- Phase outputs are not persisted individually.
- `IModelClient` has no idempotency key, request lookup, or recovery contract.
- GitHub Models uses a plain HTTP `POST` and does not persist a request
  identity before dispatch.
- Atomic file replacement avoids partially serialized target files, but the
  current helper does not explicitly flush data to stable storage.

Most importantly, a process can stop after a provider accepts a request but
before `model.completed` or the phase response reaches durable local state.
The local runtime then cannot know whether retrying means a first call or a
duplicate call.

## Recovery Principle

The recovery authority should be a typed checkpoint, not console output and
not `trace.json`.

Trace remains the audit artifact. A checkpoint answers:

- What run is this?
- Which exact input, mode, prompts, and phase plan were used?
- Which runtime transitions completed durably?
- Which phase results are authoritative?
- Is any external call in an ambiguous state?
- What is the first safe next transition?

Resume must rebuild runtime state from this authority. It must not infer
completion merely because a human-facing artifact exists.

## Proposed State

The exact contract should follow `RS-001` through `RS-003`, but it needs at
least:

```csharp
public enum InvocationStatus
{
    Pending,
    Prepared,
    Completed,
    Failed
}

public sealed record ModelInvocationState(
    string InvocationId,
    string RequestHash,
    InvocationStatus Status,
    string Provider,
    string? Model,
    string? Content,
    string? ContentHash,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record RunCheckpoint(
    int SchemaVersion,
    long Generation,
    string RunId,
    string InputHash,
    string ModeHash,
    IReadOnlyList<string> PromptHashes,
    IReadOnlyList<string> PhaseOrder,
    string LifecycleStatus,
    IReadOnlyList<PhaseExecutionState> Phases,
    IReadOnlyList<ArtifactExecutionState> Artifacts,
    DateTimeOffset UpdatedAt);
```

Provider credentials and provider-specific payloads must not be stored.
Provider and model identity, request hashes, response content, and bounded
response metadata are sufficient for recovery.

For this small runtime, storing completed phase content inside the checkpoint
is acceptable. Human-readable phase files can be regenerated from it. This
keeps the authoritative phase completion and its response in one atomic state
update rather than pretending two independent files form a transaction.

## Phase Commit Protocol

For each model phase:

1. Create a runtime-owned invocation ID and deterministic request hash.
2. Transition the phase from `Pending` to `Prepared`.
3. Persist the checkpoint before contacting the provider.
4. Call the provider.
5. Validate the response.
6. Transition the phase to `Completed`, including response content and hash.
7. Persist the completed checkpoint atomically.
8. Write or repair derived phase, trace, result, and summary artifacts.

There is still an unavoidable ambiguity window between steps 3 and 7 for an
external provider without idempotency or response lookup. A surviving
`Prepared` invocation therefore means "the call may have happened," not "the
call did not happen."

For a deterministic mock or recorded-response client, the runtime may define
`Prepared` as replay-safe because another call has no external side effect and
returns the same response. That capability must be explicit rather than
inferred from the provider name.

## Model Client Recovery Boundary

Do not leak provider details into the orchestrator. If recoverable provider
calls are added later, expose a narrow capability such as:

```csharp
public enum ModelCallRecoveryKind
{
    None,
    ReplaySafe,
    IdempotentRetry,
    ResponseLookup
}

public sealed record ModelClientCapabilities(
    ModelCallRecoveryKind RecoveryKind);
```

`IdempotentRetry` is valid only when the remote provider accepts a
caller-supplied request identity and guarantees semantically equivalent
handling for retries. Hashing identical request parameters locally is not an
idempotency contract.

`ResponseLookup` is valid only when the provider can retrieve the original
result by a durable request identity.

The current external clients should report `None` until their supported
protocols provide and test one of these guarantees.

## Recovery Classification

On an explicit resume request, acquire an exclusive run-directory lock, load
the checkpoint, verify it, and classify the run:

| State | Classification | Action |
| --- | --- | --- |
| `succeeded`, `failed`, or `cancelled` | terminal | refuse resume |
| all prior phases completed, next phase pending | resumable | continue |
| phases completed, eval or finalization pending | resumable | continue without a model call |
| invocation `Prepared`, provider is replay-safe | resumable | replay same invocation ID |
| invocation `Prepared`, provider supports idempotent retry | resumable | retry with the same key |
| invocation `Prepared`, provider supports lookup | reconcilable | fetch and persist original result |
| invocation `Prepared`, no recovery capability | ambiguous | refuse automatic resume |
| checkpoint missing, corrupt, or inconsistent | unverifiable | refuse resume |
| mode, prompt, input, or phase-plan hash changed | drifted | refuse resume |

An operator override for an ambiguous call should not mutate the interrupted
run. It should create a new run with a new run ID and record the previous run
as provenance.

## Artifact And Trace Reconciliation

The checkpoint is authoritative; Markdown, HTML, eval, summary, and public
trace files are derived or auditable artifacts.

After loading a valid checkpoint:

- Verify every completed artifact with its recorded hash when available.
- Regenerate missing derived artifacts from checkpointed state.
- Never accept an artifact whose content conflicts with the checkpoint.
- Preserve unexpected files for inspection instead of silently deleting them.
- Add `run.resume_requested`, `run.resumed`, and reconciliation events.
- Mark recovered trace events explicitly rather than pretending they were
  persisted before interruption.

`trace.json` cannot be the sole recovery authority because a process can stop
between a state transition and a trace rewrite. Resume may repair trace from
checkpoint metadata, but that repair must itself be visible.

## Concurrency

Resume must be explicit; no background worker or startup scan is needed.

Use an exclusive open file handle such as `run.lock` with no sharing while a
run or resume command is active. The operating system releases the handle when
the process exits, avoiding stale timestamp heuristics. A second process must
fail clearly without modifying the run.

The checkpoint `Generation` must increase on every durable transition. A
writer that observes an unexpected generation fails rather than overwriting a
newer state.

## Durability Scope

The first implementation should claim process-crash recovery, not guaranteed
survival of sudden power loss on every filesystem.

For stronger local durability, checkpoint persistence needs:

1. A temporary file in the same directory.
2. A complete write and `FileStream.Flush(flushToDisk: true)`.
3. Atomic replacement of the checkpoint.
4. Platform-specific evidence that the directory entry is durable.

.NET exposes flushing intermediate file buffers, and Windows exposes a
write-through move option, but the current `File.Move` helper does not state
that it uses a durable rename contract. Cross-platform power-loss guarantees
should remain an explicit non-goal until a storage abstraction can test and
document them.

A transactional store such as SQLite could improve this guarantee, but adding
it is not justified for the initial small runtime until process-crash recovery
proves valuable.

## Compatibility Rules

Resume should require exact matches for:

- checkpoint schema version
- run ID and output directory identity
- input hash
- mode manifest and prompt hashes
- ordered phase plan
- provider and configured model identity
- runtime behavior version when transition semantics change

Do not resume through arbitrary mode or orchestration code changes. A future
migration tool may transform old checkpoints, but migration is separate from
resume.

## Failure And Cancellation

Normal caught failures and user cancellation should reach explicit terminal
states and are not resumable in the first version.

Resume is for interruption where the process did not record a terminal state,
for example:

- process termination
- host restart
- runtime crash outside normal exception handling

This distinction prevents resume from becoming an implicit retry command for
known failures.

## Fault-Injection Test Plan

All tests use mock or fake clients and no credentials.

1. Stop before the first phase call; resume runs the first phase once.
2. Stop after `Prepared` with a replay-safe client; resume reuses the same
   invocation ID.
3. Stop after `Prepared` with a non-recoverable client; resume reports
   `interrupted-ambiguous` and makes no call.
4. Stop after response validation but before completed checkpoint persistence;
   external calls remain ambiguous.
5. Stop after completed checkpoint persistence but before phase artifact
   writing; resume regenerates the artifact without a model call.
6. Stop after all phases but before eval; resume runs eval only.
7. Stop during finalization; resume completes only unfinished deterministic
   work.
8. Corrupt or truncate the checkpoint; resume refuses without modifying files.
9. Change a prompt, mode manifest, input, provider, or phase order; resume
   reports drift.
10. Open the run lock from another process; concurrent resume fails clearly.
11. Resume a terminal run; no state changes.
12. Reconcile a missing trace event from checkpoint data and mark it recovered.
13. Inject a checkpoint generation conflict; the stale writer fails.
14. Leave a temporary checkpoint file; recovery ignores or quarantines it
    deterministically.

Tests should use explicit fault-injection hooks at transition boundaries.
Killing tests based on timing would be flaky and would not prove the intended
state.

## Prerequisites

Implement only after:

- `RS-001` immutable run state
- `RS-002` lifecycle state machine
- `RS-003` stable phase execution state
- `RS-004` injectable run ID generation
- `RS-005` formal cancellation semantics
- `TA-001` trace schema versioning
- `TA-006` machine-readable run state or manifest
- `TA-007` artifact hashes and sizes
- `TA-008` individual phase outputs
- `TA-009` mode and prompt provenance
- `TA-011` atomic write verification
- `RG-006` recorded-response replay for credential-free recovery tests

The bounded revision loop should land first so the checkpoint schema models
the intended main, critic, and revision lifecycle.

## Rejected Alternatives

- Retry every incomplete phase: duplicates ambiguous external calls and cost.
- Trust the last trace event: trace persistence can lag state persistence.
- Trust an existing Markdown file: presence alone does not establish which
  request produced it.
- Resume with changed prompts or phase order: rebuilds a different execution.
- Infer idempotency from identical request content: identical intent can still
  represent two desired calls.
- Add Temporal, Durable Task, or a background job framework: disproportionate
  to this lab and contrary to project constraints.
- Claim power-loss safety from atomic rename alone: durability requires
  explicit flush and filesystem guarantees.

## Research Sources

- [AWS Builders' Library: Making retries safe with idempotent APIs](https://aws.amazon.com/builders-library/making-retries-safe-with-idempotent-APIs/)
- [Microsoft: Durable orchestrations](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-orchestrations)
- [Temporal: Workflow replay and resume](https://docs.temporal.io/workflows)
- [.NET `FileStream.Flush(Boolean)`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
- [.NET `FileOptions.WriteThrough`](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileoptions?view=net-10.0)
- [Windows `MoveFileExW`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw)
- [SQLite atomic commit](https://www.sqlite.org/atomiccommit.html)

These sources support replay from durable history, deterministic orchestration,
caller-supplied idempotency identities, and explicit disk-flush requirements.
They are design references only; this proposal does not add their frameworks
or dependencies.
