# RB-008: Bounded Scatter-Gather Orchestration

Status: Research complete; implementation deferred (gated on `RS-006`)

Date: 2026-06-17

## Decision

Promote `scatter-gather` to the pattern catalog as a new registered
`IOrchestrationPattern`, implemented entirely as an immutable
`PatternExecutionPlan` plus one additive change to the shared executor:
runtime-owned **bounded concurrent execution of independent nodes**. Gate
implementation on `RS-006` (execution budgets), because a fixed fan-out without
a hard ceiling on total model calls is the only genuinely new risk this pattern
introduces; everything else is already expressible with today's typed plan,
single executor, node state, and model-call correlation IDs.

No new runtime service, no model-controlled branching, and no second execution
model are required.

## Design Question

Falsifiable hypothesis: a fixed-fan-out scatter-gather can be expressed as an
immutable plan and run through the existing shared executor such that

1. the model cannot change the branch count or add/skip the gather,
2. concurrent execution produces byte-identical artifacts and context to a
   sequential execution of the same plan, and
3. the only new runtime capability needed is bounded concurrency plus a
   thread-safe trace and node-state path.

The hypothesis is refuted if deterministic artifact/context ordering cannot be
preserved under concurrency, if independence cannot be proven from the plan
before execution, or if expressing the pattern forces a pattern-name branch back
into the orchestrator (violating `OP-012`/`RT-007`).

## Prerequisites And Assumptions

Already satisfied:

- `OP-011` typed pattern execution plan
- `OP-012` one runtime executor (no pattern-name branching)
- `OP-013` plan validation before the first model call
- `RS-003` stable execution-node state
- `TA-004` model-call correlation IDs
- `TA-008` individual phase outputs (extends to per-branch artifacts)

Required before implementation:

- `RS-006` runtime-owned execution budgets — fan-out must count against a fixed
  maximum model-call budget known before execution.

Assumptions:

- Branches are independent *by construction*: the plan author declares them with
  no context or dependency edges between them. Independence is a static property
  the validator can check, not a runtime guess.
- The mock provider can serve concurrent calls deterministically; real providers
  may rate-limit, which the concurrency cap and `RS-006` budget bound.

## Timebox

One spike of roughly two to three days: add the pattern + plan, extend the
executor with wave-based bounded concurrency, make the trace session and
node-state tracker thread-safe, and prove determinism and boundedness with the
experiment below. If thread-safety of the trace session proves larger than the
spike, stop and reduce scope to a documented concurrency cap of 1 (correct but
not yet parallel) and re-timebox the concurrency work separately.

## Proposed Plan Shape

```text
nodes:
  branch-01 (main)   dependencies: []           context: []        input: run input
  branch-02 (main)   dependencies: []           context: []        input: run input
  branch-03 (main)   dependencies: []           context: []        input: run input
  gather    (synthesis) dependencies: [branch-01, branch-02, branch-03]
                        context:      [branch-01, branch-02, branch-03]
authoritative: gather
eval profile: requires gather; optionally requires each branch
stage group: one "scatter" group for the branches (reuses stage grouping;
             no second execution model)
```

Branch count and each branch's mode are fixed at plan construction (for example
`--scatter frame,challenge,synthesize` or N copies of one mode). The model
cannot read or alter the count.

## Answers To The Open Questions

**How does a plan prove that scatter nodes are independent and safe to run
concurrently?** Independence is a static plan property: scatter nodes share no
dependency or context edge with each other and write to disjoint artifact paths
(`scatter/NN-<mode>/`). `PatternExecutionPlanValidator` (`OP-013`) gains one
rule: nodes the plan marks as concurrent may not reference one another. Safety
to run concurrently is therefore proven before any model call, not asserted at
runtime.

**How are output contracts declared for each branch and for the gather node?**
Each branch uses its mode's existing output contract; the gather node uses its
mode's contract and is the authoritative output evaluated against that contract.
The eval profile requires the gather node and may require each branch.

**Does the gather node receive full outputs, structured projections, or artifact
references?** v1 passes the full typed branch `PhaseResult`s as context, exactly
like critic-revision passes prior results — no new mechanism. Structured
projections (to bound context size) are explicitly deferred to `RB-009` (context
projection and information barriers); this note assumes full outputs and flags
context blow-up as a limitation.

**How are branch failures, timeouts, cancellation, and partial completion
represented?** v1 is strict: a failed branch fails the run. This reuses the
existing rule that context assembly rejects failed or incomplete source nodes
(`OP-013`/`EV-003`), so the gather literally cannot run on a missing branch.
Timeouts are an `RS-006` per-call budget breach reported as a node failure.
Cancellation cancels all in-flight branches via the existing node-cancellation
path. The gather cannot hide a missing or failed branch because the declared
plan execution eval (`EV-003`) requires every declared node to terminate and the
gather's recorded context source IDs to equal its declared branch IDs.

**What ordering is used when concurrent results are assembled into context and
artifacts?** Declared plan order, never completion order. The gather's context is
assembled by the plan's branch ID order, and branch artifacts are written under
`scatter/NN-<mode>/` by declared index. Determinism is independent of which
branch finishes first.

## Concurrency Model

This is the only real new runtime work. The executor currently runs plan nodes
sequentially. It gains runtime-owned, dependency-aware concurrency:

- Compute execution waves by dependency level. Nodes whose dependencies are all
  complete and that share no edges run together.
- Run a wave with `Task.WhenAll`, capped by a runtime concurrency limit and the
  `RS-006` maximum-model-call budget. The cap is runtime configuration, not
  model output.
- The gather node is a later wave that depends on all branches, so it naturally
  runs after the scatter wave.

Two components must become concurrency-safe:

- `JsonTraceSession`: serialize event appends so `sequence` stays contiguous and
  monotonic even when concurrent branches emit interleaved events. Each event
  already carries `nodeId`/`callId`, so interleaving is readable.
- `ExecutionNodeStateTracker`: guard state transitions so concurrent
  `Start`/`Complete` updates do not race.

This stays within `OP-012`: the executor is shared and pattern-agnostic; the
pattern only declares which nodes are independent.

## Smallest Useful Experiment

Add a `scatter-gather` pattern with a fixed three-branch fan-out over the mock
client, executed through the wave-based executor, and assert:

1. All three branches and the gather complete exactly once (`EV-003`).
2. The gather's recorded context equals the three branch IDs.
3. Two runs with a fixed run ID and fixed `TimeProvider` produce byte-identical
   `run.json` (deterministic ordering under concurrency).
4. An adversarial mock that "asks" to spawn another branch or skip the gather
   does not change the executed plan.
5. A forced branch failure fails the run, and the gather does not run.

## Baseline And Negative Control

Baseline: a `linear-pipeline` over the same modes (sequential). Model-call count
must match scatter-gather; wall-clock should drop under concurrency; eval
outcomes for independent branches must agree.

Negative controls:

- A plan whose scatter nodes reference one another must be **rejected** by the
  validator before any model call — proving independence is enforced, not
  assumed.
- Running the scatter plan with the concurrency cap set to 1 must produce
  byte-identical artifacts to the concurrent run — proving concurrency changes
  only timing, never output.

## Required Fixtures, Traces, And Artifacts

- Mock outputs for the branch and gather modes (deterministic, credential-free).
- A deterministic-ordering fixture: two runs, fixed run ID and time, compared
  `run.json`.
- An adversarial fixture (model output requesting more branches / skipping
  gather).
- A branch-failure fixture.
- Trace: reuse `node.started`/`node.completed`/`node.failed`/`node.cancelled`
  and `stage.started`/`stage.completed` for the scatter group; no new event
  names required beyond the existing contract (`TraceEventNames`).
- Artifacts: `scatter/NN-<mode>/` per branch, mirroring the pipeline stage
  layout and the `TA-008` `phases/` convention; gather output is the
  authoritative `result.md`.

## Reproduction Commands

```powershell
dotnet run --project src/CognitiveRuntime.Cli -- `
  --pattern scatter-gather `
  --scatter frame,challenge,synthesize `
  --input examples/agent_runtime_goal.txt `
  --run-mode mock `
  --html

dotnet test CognitiveRuntime.slnx --no-restore
```

## Evidence And Limitations

- The dominant unknown is the cost and risk of making the trace session and
  node-state tracker concurrency-safe. If that proves larger than the timebox,
  ship with a concurrency cap of 1 (correct, deterministic, not yet parallel)
  and treat true parallelism as a follow-up.
- The wall-clock benefit only materializes when branches are genuinely
  independent and the provider serves concurrent calls; for a single
  rate-limited provider the win may be small. The value is also architectural:
  it proves the typed-plan + one-executor design generalizes to fan-out.
- Full-output gather context can grow large with many branches; `RB-009`
  (context projection) is the intended mitigation and is deliberately out of
  scope here.
- Without `RS-006`, fan-out has no hard ceiling on total model calls; this is
  why implementation is gated on it.

## Rejected Alternatives

- Model-chosen fan-out (the model decides how many branches): violates the
  runtime-owns-the-loop thesis and unbounded-expansion guardrail.
- A separate parallel execution engine: duplicates the executor and breaks
  `OP-012`; waves inside the one executor are sufficient.
- Completion-order assembly: fast but nondeterministic; rejected in favor of
  declared-order assembly.
- Lenient gather over partial branches in v1: hides failures; deferred until
  there is an explicit typed "branch outcome" contract the gather must surface.
- Worker-to-worker calls: explicitly disallowed; all coordination is the
  runtime's via the plan.

## Research Sources

- [ADR-001 canonical agent information model](../architecture/ADR-001-canonical-agent-information-model.md)
- [RB-009 context projection and information barriers](../../BACKLOG.md) (intended
  follow-up for bounding gather context)
