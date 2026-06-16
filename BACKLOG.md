# Cognitive Runtime Backlog

This backlog develops the runtime thesis:

> The LLM is not the product. The loop is the product.

Cognitive Runtime Lab is a local-first C#/.NET environment for running,
comparing, and inspecting bounded agent orchestration patterns, where the
runtime owns the loop and the model only performs reasoning inside assigned
steps.

The core object is the `OrchestrationPattern`. The implemented catalog is
single-pass, critic-revision, and linear-pipeline. Each pattern is
runtime-owned, bounded, inspectable, and mockable. `Mode` describes what a
phase reasons about; `OrchestrationPattern` describes how phases relate and
who runs next. The bounded revision loop already built is the
`CriticRevisionPattern`, not the whole project.

New pattern shapes should begin as research items. Do not expand the catalog
until all existing patterns share one typed plan, one runtime executor, and
one verification path.

Items are ordered to strengthen runtime-owned orchestration, state, policy,
traces, evals, and artifacts before expanding integrations. Trace, artifact,
and eval hardening exist to make orchestration patterns inspectable -- they
are not ends in themselves.

## Backlog Rules

- `P0` is the next milestone or a prerequisite for it.
- `P1` strengthens the runtime after the next milestone.
- `P2` is valuable, but should not displace core loop work.
- `Research` requires a short design note or spike before implementation.
- The runtime, not the model, owns phase order, completion, policy, evals, and
  artifact paths.
- Tests must not require external credentials.
- New provider and tool behavior must stay behind existing abstractions.
- Backlog completion still requires the repository definition of done.

## Suggested Sequence

1. Complete the baseline, bounded revision loop, and initial pattern catalog.
2. Unify all patterns behind one typed execution plan and runtime executor.
3. Make run state, node state, and lifecycle explicit.
4. Harden traces and artifacts.
5. Verify declared plans and context flow deterministically.
6. Improve mode authoring and validation.
7. Add an offline regression and pattern-comparison harness.
8. Harden provider and tool boundaries.
9. Improve CLI inspection and verification.

## Next Implementation Queue

This is the proposed order for the next implementation cycle. All other items
remain open, but should not interrupt this queue without an explicit priority
change. Each item is intended to be one reviewable pull request.

The initial pattern catalog exposed an important boundary problem:
`linear-pipeline` has a separate orchestration path and directly coordinates
runtime services. The next cycle removes that exception before adding another
pattern shape.

- [x] `OP-011` Define a typed pattern execution plan.
- [x] `OP-012` Route every pattern through one runtime executor.
- [x] `OP-013` Validate pattern plans before model execution.
- [x] `CF-001` Define the canonical agent information model.
- [x] `RS-001` Introduce immutable run state.
- [x] `RS-003` Add stable execution-node state.
- [x] `RS-002` Add a lifecycle state machine.
- [x] `RS-004` Inject run ID generation.
- [x] `TA-002` Centralize trace event names.
- [x] `TA-001` Version the trace schema and add sequence numbers.
- [x] `TA-003` Record durations.
- [x] `TA-004` Add model call correlation IDs.
- [x] `TA-005` Add sanitized failure events.
- [x] `TA-006` Add `run.json`.
- [x] `EV-003` Evaluate declared plan execution and cardinality.
- [x] `EV-004` Evaluate model call pairing.
- [x] `EV-005` Evaluate terminal trace integrity.

### Completed - Orchestration Pattern Cycle

- [x] `OP-001` Define `IOrchestrationPattern`.
- [x] `OP-002` Implement `SinglePassPattern`.
- [x] `OP-003` Implement `CriticRevisionPattern` on top of `IOrchestrationPattern`.
- [x] `OP-004` Implement `LinearPipelinePattern`.
- [x] `OP-005` Add pattern selection to the CLI.
- [x] `OP-006` Trace pattern lifecycle.
- [x] `OP-007` Write a `pattern.md` artifact.
- [x] `OP-008` Show the pattern graph in static HTML.
- [x] `OP-009` Add mock outputs for each pattern.
- [x] `OP-010` Add tests proving the runtime, not the model, controls the
      pattern.

### Completed - Loop Efficacy Cycle

- [x] `EV-001` Parse Markdown headings structurally. Prerequisite for the rest.
- [x] `EV-002` Validate content under each heading.
- [x] `EFF-001` Add a deterministic loop-efficacy eval.

### Deferred From The Previous Cycle

These were deferred from the loop-efficacy cycle. The state and verification
items now appear in the next implementation queue:

- [x] `RS-001` Introduce immutable run state.
- [x] `RS-007` Define a typed run outcome.
- [x] `TA-002` Centralize trace event names.
- [x] `EV-003` Evaluate declared plan execution and cardinality.
- [x] `EV-005` Evaluate terminal trace integrity.

### Completed - Milestones 0 and 1

- [x] `BL-001` Finish the current working-tree slice.
- [x] `BL-002` Make finalization the true terminal event.
- [x] `BL-003` Define terminal failure semantics.
- [x] `RL-001` Add a typed revision phase.
- [x] `RL-002` Replace single previous output with typed phase context.
- [x] `RL-003` Add revision prompts to every mode.
- [x] `RL-004` Make mock revision deterministic.
- [x] `RL-005` Trace revision lifecycle.
- [x] `RL-006` Make the revision authoritative.
- [x] `RL-007` Evaluate revision completion.
- [x] `RL-009` Cover revision failure paths.
- [x] `RL-010` Update the end-to-end documentation.
- [x] `RL-008` Show revision in the static run view.
- [x] `BL-004` Document lifecycle vocabulary.

## Cross-Cutting Foundations

These items prevent the implementation and research tracks from inventing
incompatible representations or producing runs that cannot be reproduced,
shared, or evaluated responsibly.

### CF-001 - Define the canonical agent information model

Priority: `P0`

Status: Complete. See
[`docs/architecture/ADR-001-canonical-agent-information-model.md`](docs/architecture/ADR-001-canonical-agent-information-model.md).

Define the relationships among:

- observation: immutable data received from an environment, tool, file, or
  provider
- evidence: an observation admitted for a declared reasoning purpose
- claim: a proposition produced by a model or deterministic transformation
- belief: runtime-managed epistemic state about one or more claims
- blackboard entry: run-scoped shared structured state
- context projection: the exact values disclosed to one execution node

Acceptance:

- Each concept has a typed identity, provenance, lifecycle, and ownership
  boundary.
- The model may propose claims and content but cannot relabel observations as
  trusted evidence or commit beliefs and blackboard state directly.
- `RB-007`, `RB-009`, `AR-004`, `AR-010`, and `AR-013` use this vocabulary
  rather than defining parallel stores.
- A short architecture decision record explains which concepts are persisted
  in traces, artifacts, and run state.
- The first version remains run-scoped and does not introduce long-term
  memory.

Relevant research:

- [From Agent Traces to Trust: Evidence Tracing and Execution Provenance in
  LLM Agents](https://arxiv.org/abs/2606.04990)
- [Agent-BRACE: Decoupling Beliefs from Actions in Long-Horizon Tasks via
  Verbalized State Uncertainty](https://arxiv.org/abs/2605.11436)

### CF-002 - Add a run reproducibility envelope

Priority: `P1`

Acceptance:

- `run.json` records runtime assembly version, source commit when available,
  dirty-worktree status when available, .NET version, operating system,
  effective non-secret configuration, mode and prompt hashes, provider API
  version, exact model identifier, and deterministic seeds where applicable.
- Missing provenance is represented as unknown, not omitted or guessed.
- Secrets, tokens, machine usernames, and unrelated environment variables are
  excluded.
- Replay and comparison reports identify which envelope fields differ.
- Tests inject deterministic provenance rather than depending on the local
  Git repository or machine.

### CF-003 - Define the agent threat and data-governance model

Priority: `P1`

Produce a versioned design note covering assets, actors, trust boundaries,
attack surfaces, data classes, retention, redaction, deletion, artifact
sharing, and expected incident evidence.

Acceptance:

- The model distinguishes user input, mode instructions, provider responses,
  tool observations, external content, runtime policy, and human approval.
- Data classes define what may appear in model requests, traces, artifacts,
  eval fixtures, and exported bundles.
- The design includes indirect prompt injection, confused-deputy behavior,
  secret exfiltration, unsafe tool arguments, trace poisoning, and artifact
  tampering.
- `AR-001`, tool policy, context projection, and export behavior reference the
  same trust and data classifications.
- The document names residual risks that the runtime does not claim to solve.

Relevant research:

- [AgentDojo: A Dynamic Environment to Evaluate Prompt Injection Attacks and
  Defenses for LLM Agents](https://arxiv.org/abs/2406.13352)
- [Agent-SafetyBench: Evaluating the Safety of LLM
  Agents](https://arxiv.org/abs/2412.14470)

### CF-004 - Define an evaluation validity protocol

Priority: `P1`

Research and document how an eval demonstrates that it measures the intended
runtime behavior rather than an exploitable proxy.

Acceptance:

- Every eval declares its construct, observable proxy, known blind spots,
  negative controls, mutation fixtures, and expected failure modes.
- Benchmark inputs, provider fixtures, and acceptance thresholds are versioned
  separately from implementation code.
- The protocol addresses leakage, evaluator dependence, metric gaming,
  Goodhart effects, repeated tuning on the same corpus, and nondeterministic
  provider variance.
- New research claims include a baseline, an ablation or negative control, and
  reproducible commands.
- An eval cannot become a release gate until its false-positive and
  false-negative behavior is demonstrated on planted fixtures.

Relevant research:

- [ToolSandbox: A Stateful, Conversational, Interactive Evaluation Benchmark
  for LLM Tool Use Capabilities](https://arxiv.org/abs/2408.04682)
- [Benchmark Test-Time Scaling of General LLM
  Agents](https://arxiv.org/abs/2602.18998)

## Milestone 0: Stabilize The Baseline

Outcome: the current GitHub Models and static HTML work is a coherent,
well-tested baseline before the loop changes.

### BL-001 - Finish the current working-tree slice

Priority: `P0`

- Review the GitHub Models client, CLI option, and static HTML changes as one
  coherent slice.
- Keep provider details isolated from orchestration and evaluation.
- Confirm `dotnet test` and the documented mock command pass without
  credentials.

Acceptance:

- All tests pass.
- A mock `frame` run passes its evals.
- `--html` remains optional.
- README commands match actual CLI behavior.

### BL-002 - Make finalization the true terminal event

Priority: `P0`

`index.html` is currently written after `run.finalized`. Move all optional
artifact work before finalization and make the terminal event authoritative.

Acceptance:

- No successful artifact writes occur after `run.finalized`.
- HTML generation is traced as an artifact write.
- A successful trace ends in exactly one terminal success event.
- Tests cover runs with and without `--html`.

### BL-003 - Define terminal failure semantics

Priority: `P0`

Acceptance:

- A failed run records one terminal failure outcome.
- Failure during evaluation or optional artifact generation cannot leave a
  misleading successful terminal event.
- Required failure artifacts remain best-effort and preserve the original
  exception.
- Tests cover model, eval, trace, and view-writing failures.

### BL-004 - Document lifecycle vocabulary

Priority: `P1`

Clarify the meanings of `run.completed`, `run.finalized`, and `run.failed`.

Acceptance:

- The README and trace tests use the same vocabulary.
- `run.completed` means the cognitive phase loop completed.
- Finalization means all configured post-run work completed.

## Milestone 1: Bounded Revision Loop

Outcome: the critic materially changes the answer through a single,
runtime-controlled revision:

```text
main -> critic -> revision -> deterministic eval
```

The cycle is fixed at one revision. The model cannot request another pass,
skip a phase, or declare completion.

### RL-001 - Add a typed revision phase

Priority: `P0`

Acceptance:

- `PhaseKind` supports `Revision`.
- Mode validation requires main, critic, and revision phases in that order.
- Duplicate or out-of-order required phases fail with clear mode errors.
- Existing phase-order tests are expanded.

### RL-002 - Replace single previous output with typed phase context

Priority: `P0`

The revision phase needs both the initial draft and critic output. Avoid adding
more positional strings to `ModelRequest`.

Acceptance:

- Model requests receive immutable prior phase results by name and kind.
- Provider clients remain unaware of orchestration policy.
- Main receives no prior results, critic receives main, and revision receives
  main plus critic.
- Tests verify the exact context visible to each phase.

### RL-003 - Add revision prompts to every mode

Priority: `P0`

Add:

```text
modes/frame/prompts/revision.md
modes/challenge/prompts/revision.md
modes/synthesize/prompts/revision.md
```

Acceptance:

- Revision prose stays in mode files.
- Each revision prompt instructs the model to preserve the declared output
  contract while addressing material critic findings.
- `MODE.md` and `mode.json` describe the three-phase loop.
- Mode loading requires the revision prompt.

### RL-004 - Make mock revision deterministic

Priority: `P0`

Acceptance:

- `MockModelClient` returns a contract-satisfying revision for all three modes.
- Revision output is visibly derived from the draft and critic context.
- Tests do not depend on timestamps, random content, or external credentials.

### RL-005 - Trace revision lifecycle

Priority: `P0`

Add `revision.started` and `revision.completed`.

Acceptance:

- Revision model calls still emit `model.called` and `model.completed`.
- Revision events contain phase name and useful content metadata.
- Trace order is asserted in integration tests.

### RL-006 - Make the revision authoritative

Priority: `P0`

The revised response should be the authoritative result. Keep the critique
available as an appendix for inspectability.

Acceptance:

- `result.md` clearly labels the revised response as authoritative.
- The initial draft and critic remain inspectable without obscuring the final
  answer.
- Result composition is deterministic.
- The output contract is evaluated against the authoritative revision, not
  text accidentally present in an appendix.

### RL-007 - Evaluate revision completion

Priority: `P0`

Acceptance:

- Evals require `revision.completed`.
- Evals fail when revision output is empty.
- Evals validate the revision against the mode output contract.
- A critic cannot make a malformed revision pass by containing missing
  headings itself.

### RL-008 - Show revision in the static run view

Priority: `P1`

Acceptance:

- The phase list shows main, critic, and revision distinctly.
- The revised response is identified as authoritative.
- Model content remains HTML encoded.
- Static HTML remains read-only, script-free, and server-free.

### RL-009 - Cover revision failure paths

Priority: `P0`

Acceptance:

- Tests cover empty revision output.
- Tests cover a provider exception during revision.
- Tests cover a revision that violates the output contract.
- Failure traces and required artifacts remain valid.

### RL-010 - Update the end-to-end documentation

Priority: `P0`

Acceptance:

- README describes the three-phase loop.
- The mock CLI example produces a passing revised result.
- The distinction between model reasoning and runtime completion remains
  explicit.

## Milestone 1.1: Orchestration Patterns

Outcome: the runtime exposes multiple bounded orchestration patterns through a
named catalog, and the CLI can select and inspect them.

Status: Complete. `Milestone 1.2` closes the remaining split between simple
patterns and composite pipeline execution.

```text
dotnet run --project src/CognitiveRuntime.Cli -- \
  run \
  --pattern critic-revision \
  --mode frame \
  --input examples/agent_runtime_goal.txt \
  --run-mode mock \
  --html
```

```text
outputs/<timestamp>_critic-revision_frame/
  input.md
  result.md
  pattern.md
  trace.json
  run_summary.md
  eval_report.md
  index.html
  phases/
    01-main.md
    02-critic.md
    03-revision.md
```

### OP-001 - Define `IOrchestrationPattern`

Priority: `P0`

Acceptance:

- A pattern declares its name, the ordered phases or stages it runs, and how
  each step's context is assembled from prior results.
- The runtime, not the pattern implementation, drives execution: the
  abstraction returns a plan or the next step rather than calling the model
  itself.
- Existing orchestration concepts (phase, phase context, revision) are
  expressible as an `IOrchestrationPattern` without breaking current tests.

### OP-002 - Implement `SinglePassPattern`

Priority: `P0`

Acceptance:

- A single main phase runs and its output is the authoritative result.
- The pattern is the trivial case used to validate the abstraction before
  porting the more complex critic-revision loop.

### OP-003 - Implement `CriticRevisionPattern` on top of `IOrchestrationPattern`

Priority: `P0`

Acceptance:

- The existing bounded `main -> critic -> revision -> eval` loop is expressed
  as an `IOrchestrationPattern` implementation.
- All existing revision-loop tests and behavior (RL-001 through RL-010)
  continue to pass unchanged.
- The pattern name `critic-revision` is used in traces and artifacts.

### OP-004 - Implement `LinearPipelinePattern`

Priority: `P0`

Acceptance:

- The pattern runs a runtime-configured, ordered sequence of modes (for
  example `frame,challenge,synthesize`), each a complete phase or
  pattern run in its own right.
- The model cannot add, remove, reorder, or repeat stages.
- Each stage's artifacts are written under `stages/NN-<mode>/`.

### OP-005 - Add pattern selection to the CLI

Priority: `P0`

Acceptance:

- `--pattern <name>` selects the orchestration pattern; `critic-revision`
  remains the default to preserve current behavior.
- `--pipeline <mode,mode,...>` configures `LinearPipelinePattern` stages.
- Unknown pattern names fail as a usage error before any model call.

### OP-006 - Trace pattern lifecycle

Priority: `P0`

Acceptance:

- The trace records which pattern ran and its declared step/stage plan.
- Pattern-level start and completion events bound the existing phase-level
  events.
- Tests assert the pattern lifecycle events for at least two patterns.

### OP-007 - Write a `pattern.md` artifact

Priority: `P0`

Acceptance:

- `pattern.md` describes the selected pattern, its steps or stages, and how
  context flowed between them for this run.
- Content is generated from typed pattern data, not free-form model output.

### OP-008 - Show the pattern graph in static HTML

Priority: `P1`

Acceptance:

- The static run view renders the pattern's steps/stages and their
  relationships (e.g. critic-revision vs. linear pipeline) distinctly.
- Static HTML remains read-only, script-free, and server-free.

### OP-009 - Add mock outputs for each pattern

Priority: `P0`

Acceptance:

- `MockModelClient` produces contract-satisfying output for every step of
  `SinglePassPattern`, `CriticRevisionPattern`, and `LinearPipelinePattern`.
- A mock run of each pattern passes its evals without credentials.

### OP-010 - Add tests proving the runtime, not the model, controls the pattern

Priority: `P0`

Acceptance:

- Tests show that model output cannot alter step order, add steps, skip
  steps, or change which pattern ran.
- Tests cover at least `SinglePassPattern`, `CriticRevisionPattern`, and
  `LinearPipelinePattern`.

## Milestone 1.2: Unified Pattern Execution

Outcome: every pattern is an immutable runtime plan executed through the same
orchestration path. Pattern implementations describe work; they do not load
modes, invoke models, write artifacts, emit traces, or evaluate results.

Status: Complete.

This milestone is intentionally narrower than a generic graph engine. It only
needs the fixed sequences and nested stages already used by the built-in
patterns.

### OP-011 - Define a typed pattern execution plan

Priority: `P0`

Acceptance:

- A plan declares stable node IDs, node kind, mode source, dependencies,
  context inputs, stage grouping, and the authoritative output.
- A plan declares the deterministic eval profile required by the pattern
  instead of inferring it from the final phase or pattern name.
- The complete bounded plan is known before the first model call.
- The plan contains data only and exposes no runtime services.
- `single-pass`, `critic-revision`, and `linear-pipeline` are representable
  without pattern-name conditionals.
- Plan serialization is deterministic enough for traces, `pattern.md`, and
  tests to consume the same structure.

### OP-012 - Route every pattern through one runtime executor

Priority: `P0`

Acceptance:

- The orchestrator resolves every pattern through one registry or factory;
  it has no `linear-pipeline` string branch.
- One runtime-owned executor handles mode loading, phase execution, context
  assembly, tracing, artifact writing, and stage boundaries.
- Pattern implementations do not depend on `IModelClient`, `PhaseRunner`,
  `IArtifactWriter`, `ITraceSession`, or provider-specific types.
- Existing CLI behavior, stage artifact layout, traces, and mock outputs
  remain compatible.
- Tests prove that all registered patterns use the shared executor.

### OP-013 - Validate pattern plans before model execution

Priority: `P0`

Acceptance:

- Node IDs are unique and dependencies reference declared nodes.
- Context edges cannot reference future or undeclared nodes.
- At execution time, context assembly rejects failed or incomplete source
  nodes.
- The plan has exactly one authoritative output and at least one executable
  node.
- Unsupported cycles, unbounded expansion, and unknown phase kinds fail with
  clear errors before the first model call.
- Mutation tests cover duplicate IDs, missing dependencies, invalid
  authority, and illegal context edges.

## Milestone 2: Explicit Runtime State And Lifecycle

Outcome: orchestration operates on typed state and validated transitions rather
than local variables and implicit event ordering.

### RS-001 - Introduce immutable run state

Priority: `P1`

Status: Complete.

Acceptance:

- Run state contains run identity, loaded modes, resolved pattern plan,
  execution-node results, artifact paths, lifecycle status, and eval outcome.
- State updates happen through small runtime-owned transitions.
- Provider-specific configuration is not stored in core run state.

### RS-002 - Add a lifecycle state machine

Priority: `P1`

Status: Complete.

Suggested states:

```text
created
running
execution-completed
evaluating
finalizing
succeeded
failed
cancelled
```

Acceptance:

- Invalid transitions throw a clear runtime exception.
- Terminal states cannot transition again.
- Trace terminal events are derived from lifecycle transitions.
- Transition tests cover success, failure, and cancellation.

### RS-003 - Add stable execution-node state

Priority: `P1`

Status: Complete.

Acceptance:

- Each planned execution node has pending, running, completed, failed, or
  cancelled status.
- Pipeline stages group node states without inventing a second execution
  model.
- The runtime records node ID, phase, mode, provider, model, start time, end
  time, and output length.
- The model cannot modify node or stage status.

### RS-004 - Inject run ID generation

Priority: `P1`

Status: Complete.

Acceptance:

- Production uses random collision-resistant IDs.
- Tests can inject fixed IDs.
- Run directory collision behavior is deterministic and tested.

### RS-005 - Formalize cancellation

Priority: `P1`

Acceptance:

- Cancellation before, during, and after a model call has a defined outcome.
- User cancellation is distinguishable from provider timeout.
- A cancelled run records a terminal cancellation event and useful partial
  artifacts.
- CLI exit code `130` remains documented and tested.

### RS-006 - Add runtime-owned execution budgets

Priority: `P2`

Start with fixed, typed limits:

- maximum input characters
- maximum model calls
- maximum phase output characters
- maximum run duration

Acceptance:

- Budget checks occur outside model clients.
- Budget failures are traced with the limit and observed value.
- Defaults preserve current mock behavior.
- Tests cover each exceeded limit.

Token and cost budgets are a later extension of this item and depend on
`MP-003`. Unknown provider usage must never be treated as zero.

### RS-007 - Define a typed run outcome

Status: Complete.

Priority: `P1`

Acceptance:

- Replace loosely related booleans with a success, eval-failed, runtime-failed,
  or cancelled outcome.
- CLI exit-code mapping is centralized.
- `RunResult` cannot represent contradictory states.

## Milestone 3: Trace And Artifact Integrity

Outcome: a run can be understood and verified from its output directory without
console logs or hidden process state.

### TA-001 - Version the trace schema

Priority: `P1`

Status: Complete.

Acceptance:

- `trace.json` includes a schema version.
- Every event includes a monotonically increasing sequence number.
- Readers reject unsupported future versions with a clear error.

### TA-002 - Centralize trace event names

Priority: `P1`

Status: Complete.

Acceptance:

- Core event names are defined in one inspectable location.
- Producers and evals do not repeat string literals for required events.
- Event payloads remain plain JSON-compatible data.

### TA-003 - Record durations

Priority: `P1`

Status: Complete.

Acceptance:

- Phase, model-call, evaluation, and total-run durations are recorded.
- Durations use `TimeProvider`.
- Tests use fixed or controlled time.

### TA-004 - Add model call correlation IDs

Priority: `P1`

Status: Complete.

Acceptance:

- Each `model.called` event pairs with one `model.completed` or
  `model.failed` event.
- Correlation does not depend on event position alone.
- Each call is attributed to one execution node and retry attempt.
- Tests detect missing and duplicate completion events.

### TA-005 - Add sanitized failure events

Priority: `P1`

Status: Complete.

Acceptance:

- Failures record category, phase, provider, exception type, and safe message.
- Tokens, authorization headers, and configured secrets are never traced.
- Provider response excerpts are length bounded.

### TA-006 - Add `run.json`

Priority: `P1`

Status: Complete.

Create a machine-readable run manifest containing:

- run ID and outcome
- mode name and version
- provider and model names
- start and end times
- resolved pattern plan and authoritative node
- execution-node and stage outcomes
- artifact inventory
- eval summary

Acceptance:

- The manifest is written inside the run directory.
- It contains no credentials.
- It is covered by deterministic serialization tests.

### TA-007 - Add artifact hashes and sizes

Priority: `P1`

Status: Complete.

Acceptance:

- `run.json` records SHA-256 and byte length for completed artifacts.
- A verifier can detect modified or missing artifacts.
- Hashes are computed after final artifact content is written.

`RB-004` researches whether event hash chaining adds value beyond these
artifact-level integrity checks.

### TA-008 - Persist individual phase outputs

Priority: `P1`

Suggested layout:

```text
phases/
  01-main.md
  02-critic.md
  03-revision.md
```

Acceptance:

- Paths are runtime-generated and constrained to the run directory.
- Phase artifacts are traced and listed in `run.json`.
- `result.md` remains the human-facing authoritative composition.

### TA-009 - Snapshot mode provenance

Priority: `P2`

Acceptance:

- The run records hashes for `mode.json` and all prompts used.
- A reader can tell when a mode changed between two runs.
- Snapshotting cannot read outside the loaded mode directory.

`RB-005` builds a deterministic drift report on this provenance.

### TA-010 - Replace the eval placeholder artifact

Priority: `P1`

The current flow reserves `eval_report.md` with placeholder content so the eval
can check its own existence.

Acceptance:

- Artifact planning and artifact completion are represented explicitly.
- Required-artifact evaluation does not depend on temporary placeholder text.
- A partial run clearly distinguishes planned, written, and failed artifacts.

### TA-011 - Verify atomic write behavior

Priority: `P2`

Acceptance:

- Tests cover replacement of existing artifact content.
- Interrupted writes do not leave a partially serialized trace.
- Temporary files remain inside the run directory and are cleaned up on
  success.

### TA-012 - Add typed trace payload contracts

Priority: `P1`

Centralizing event names does not prevent payload-key drift between producers,
evals, views, and future readers.

Acceptance:

- Core trace events have small typed payload records or builders with required
  fields.
- Producers and consumers do not repeat magic strings for required payload
  keys such as node ID, phase, call ID, stage, and outcome.
- Serialized trace data remains plain JSON and does not expose implementation
  type names.
- Compatibility tests cover the payloads used by evals and static views.

## Milestone 4: Deterministic Evaluation

Outcome: evals check runtime invariants and output structure precisely without
using an LLM.

### EV-001 - Parse Markdown headings structurally

Priority: `P1`

Replace whole-line set membership with a small deterministic heading parser.

Acceptance:

- Required headings must have the expected Markdown level and spelling.
- Heading order is validated.
- Duplicate required headings are reported.
- Headings inside fenced code blocks do not count.

### EV-002 - Validate content under each heading

Priority: `P1`

Acceptance:

- Required sections cannot be empty.
- Per-section failures name the affected heading.
- Minimum total length remains supported.

### EFF-001 - Evaluate loop efficacy deterministically

Priority: `P1`

The structural evals confirm the revision is well-formed, but nothing confirms
the loop did any work. A run can pass every check while the revision ignored the
critic or made the answer worse. This item adds a deterministic, non-LLM check
that the revision materially responded to the critic. It is the only eval that
requires a mode change: the critic must emit findings in a parseable form so the
runtime can correlate them with revision edits.

Builds on `EV-001` and `EV-002` for heading and section parsing.

Acceptance:

- The critic output contract declares a parseable findings format (for example,
  one finding per list item under a required `## Findings` heading). Critic
  prose still lives in mode files.
- The runtime computes a deterministic diff between the draft and the
  authoritative revision at section granularity.
- The eval fails when the revision is byte-identical to the draft, or differs
  by less than a declared minimum, treating an unchanged revision as a loop that
  did no work.
- The eval reports, per critic finding, whether the section it referenced was
  changed in the revision, and fails when no flagged section changed.
- No semantic or quality judgment is made and no model is used. The check
  measures whether the loop responded, not whether the answer improved.
- The mock provider produces a revision that satisfies this eval for all three
  modes, and a failure fixture proves the eval fails for an inert revision.
- Thresholds are runtime-owned configuration, not model output.

### EV-003 - Evaluate declared plan execution and cardinality

Priority: `P1`

Status: Complete.

Acceptance:

- The trace contains exactly one terminal execution outcome for each declared
  plan node.
- Required dependency and stage ordering matches the resolved pattern plan.
- Each node's recorded context source IDs match its declared context inputs.
- Missing, duplicate, or undeclared node executions fail the eval.
- The authoritative node completed successfully and matches the result used
  for output-contract evaluation.
- The check works for single-pass, critic-revision, and linear-pipeline
  without switching on pattern name.

### EV-004 - Evaluate model call pairing

Priority: `P1`

Status: Complete.

Acceptance:

- Every model call has one correlated completion or failure.
- A successful run has no unclosed model calls.
- The report identifies unmatched call IDs.

### EV-005 - Evaluate terminal trace integrity

Priority: `P1`

Status: Complete.

Acceptance:

- A successful run has exactly one final success event.
- A failed or cancelled run cannot contain a later success event.
- No trace events appear after a terminal event.

### EV-006 - Produce `eval_report.json`

Priority: `P2`

Acceptance:

- JSON and Markdown reports are rendered from the same typed report.
- Check IDs are stable across runs.
- Automation can distinguish pass, fail, and not-run.

### EV-007 - Add eval severity

Priority: `P2`

Support `error` and `warning` without allowing warnings to silently become
success criteria.

Acceptance:

- Errors determine pass/fail.
- Warnings are visible in Markdown, JSON, trace, and static HTML.
- Severity is declared by runtime eval definitions.

### EV-008 - Add artifact integrity evals

Priority: `P2`

Acceptance:

- Required artifacts exist and match their recorded hash.
- Artifact paths remain inside the run directory.
- The eval report identifies the exact missing or modified file.

### EV-009 - Add eval execution tracing

Priority: `P1`

Acceptance:

- Individual checks emit stable check ID, status, and duration.
- Eval trace payloads do not duplicate large artifact contents.
- Eval failures remain deterministic.

## Milestone 5: Mode Authoring And Validation

Outcome: modes are easy to inspect and difficult to misconfigure.

### MO-001 - Version the mode manifest schema

Priority: `P1`

Acceptance:

- `mode.json` declares a schema version distinct from the mode content version.
- Unsupported schema versions fail clearly.
- Existing modes migrate together.

### MO-002 - Reject unknown manifest properties

Priority: `P1`

Acceptance:

- Misspelled fields fail loading instead of being ignored.
- Errors include the manifest path and offending property.
- Tests cover malformed JSON and valid comments/trailing commas behavior.

### MO-003 - Strengthen phase validation

Priority: `P1`

Acceptance:

- Phase names use a safe, documented character set.
- Prompt paths are unique, relative, and contained by the mode directory.
- Required phases cannot be omitted or repeated.
- Unreferenced required prompt files are detected.

### MO-004 - Add mode validation to the CLI

Priority: `P1`

Suggested command:

```text
cognitive-runtime modes validate frame
```

Acceptance:

- Validation performs no model calls and writes no run output.
- Success and failure have documented exit codes.
- Errors are concise and actionable.

### MO-005 - Add mode listing and description

Priority: `P2`

Acceptance:

- The CLI lists valid mode names, versions, descriptions, and phase order.
- Invalid mode directories are identified without preventing valid modes from
  being listed.
- The CLI delegates loading and validation to Core.

### MO-006 - Add per-phase contracts

Priority: `P2`

Acceptance:

- Main, critic, and revision may declare different structural contracts.
- Final-result evaluation still targets the authoritative revision contract.
- Missing contracts receive explicit runtime defaults.

### MO-007 - Validate mode documentation consistency

Priority: `P2`

Acceptance:

- `MODE.md` exists and is non-empty.
- Documentation names the configured phases and intended output.
- Validation remains deterministic and does not judge prose quality.

### MO-008 - Validate every built-in mode and prompt variant

Priority: `P1`

Acceptance:

- `frame`, `challenge`, `synthesize`, and each checked-in `lens` prompt
  variant load and validate without a model call.
- Validation covers manifests, prompt containment, required phases, and output
  contracts.
- Test data is shared without hiding mode- or lens-specific assertions.

Full mock execution remains covered by `RT-001`.

## Milestone 6: Offline Regression Harness

Outcome: changes to modes and runtime behavior can be evaluated across a small,
credential-free corpus.

### RG-001 - Add deterministic eval cases

Priority: `P1`

Suggested layout:

```text
examples/evals/
  frame/
  challenge/
  synthesize/
```

Each case should declare input, expected contract outcome, and expected runtime
invariants.

Acceptance:

- Cases are data-driven and human-readable.
- No case requires network access.
- The corpus includes passing and intentionally failing cases.

### RG-002 - Add a Core eval-suite runner

Priority: `P1`

Acceptance:

- The runner executes cases through the normal orchestrator.
- Case ordering and output are deterministic.
- One case failure does not prevent later cases from running.

### RG-003 - Add a CLI eval command

Priority: `P2`

Suggested command:

```text
cognitive-runtime eval --mode frame --cases examples/evals/frame
```

Acceptance:

- CLI code only parses options, configures services, and invokes Core.
- Suite output is written under a timestamped output folder.
- Exit status reflects suite pass/fail.

### RG-004 - Produce a suite summary

Priority: `P2`

Acceptance:

- Markdown and JSON summarize cases, checks, durations, and artifact paths.
- Failures link to the individual run directory.
- Summary generation uses typed results.

### RG-005 - Add contract mutation fixtures

Priority: `P1`

Acceptance:

- Fixtures cover missing headings, wrong order, duplicate headings, empty
  sections, malformed traces, and absent artifacts.
- Tests prove each deterministic eval can fail for the intended reason.

### RG-006 - Add recorded-response replay

Priority: `P2`

Create an `IModelClient` that replays sanitized, checked-in responses.

Acceptance:

- Replay is deterministic and credential-free.
- Requests are matched by mode, phase, and fixture ID.
- Missing or unexpected calls fail loudly.
- Recorded responses contain no tokens or private inputs.
- A recorded run can be replayed without contacting the original provider.

### RG-007 - Compare runtime regressions

Priority: `P2`

Acceptance:

- A comparison reports changed eval status, trace shape, artifact hashes, and
  phase output lengths.
- It does not attempt semantic quality scoring.
- Comparison output is an artifact, not a web service.

### RG-008 - Compare orchestration patterns on fixed cases

Priority: `P2`

Acceptance:

- A data-driven matrix runs selected cases through multiple registered
  patterns with the mock or replay provider.
- The summary compares model-call count, executed nodes, eval status,
  durations, output lengths, and artifact paths.
- Each underlying run remains independent and inspectable.
- The report does not rank answer quality or use an LLM judge.

## Milestone 7: Model Provider Hardening

Outcome: external providers fail clearly and expose useful common metadata
without leaking provider details into the loop.

### MP-001 - Add typed provider failure categories

Priority: `P1`

Suggested categories:

```text
configuration
authentication
authorization
rate-limit
timeout
transport
malformed-response
empty-response
cancelled
```

Acceptance:

- `ModelProviderException` carries a category.
- CLI, trace, and tests use the common category.
- Provider-specific response details stay inside each client.

### MP-002 - Separate timeout from caller cancellation

Priority: `P1`

Acceptance:

- GitHub Models tests cover both cases.
- Timeout is reported as a provider failure.
- User cancellation preserves cancellation semantics.

### MP-003 - Add common response metadata

Priority: `P2`

Acceptance:

- `ModelResponse` may report request ID, token usage, finish reason, and
  latency when available.
- Missing metadata is valid.
- The orchestrator does not parse provider payloads.
- Budget consumers distinguish unknown usage from measured zero usage.

### MP-004 - Add explicit retry policy

Priority: `P2`

Keep retries small and inspectable.

Acceptance:

- Default retry count is zero or a documented conservative value.
- Only explicitly transient categories can retry.
- Attempts are traced separately and count against runtime budgets.
- Cancellation never retries.

### MP-005 - Expand GitHub Models protocol tests

Priority: `P1`

Acceptance:

- Fake-handler tests verify URI, headers, API version, model, messages, and
  cancellation.
- Tests cover 401, 403, 429, malformed JSON, missing choices, and empty text.
- Error messages remain bounded and secret-free.

### MP-006 - Add provider configuration validation

Priority: `P2`

Acceptance:

- Configuration can be checked without making a model call.
- Validation returns typed errors.
- The CLI may expose validation without moving provider rules into CLI code.

### MP-007 - Implement a minimal Azure Foundry HTTP adapter

Priority: `P2`

This is a minimal provider client, not a full Azure deployment feature.

Acceptance:

- Uses `HttpClient` and existing provider boundaries.
- Has fake-handler tests and no credential-dependent test.
- Configuration, auth, response parsing, and failures remain isolated.
- README clearly labels the supported protocol and limitations.

### MP-008 - Add opt-in provider smoke tests

Priority: `Research`

Acceptance:

- Smoke tests are excluded from normal `dotnet test`.
- They require explicit environment variables and user invocation.
- They never print credentials.
- Their failure cannot make credential-free CI fail.

## Milestone 8: Tool Boundary And Policy Exercises

Outcome: tool infrastructure is proven through bounded, explicit tests before
any model-driven tool loop is considered.

### TP-001 - Add phase attribution to tool traces

Priority: `P1`

The static view expects phase data, but current tool events do not carry it.

Acceptance:

- `ToolRequest` identifies the requesting phase.
- Policy, call, completion, and failure events include that phase.
- Tests verify attribution.

### TP-002 - Add a typed tool registry

Priority: `P2`

Acceptance:

- Tool descriptors come from registered providers.
- Duplicate tool names fail startup or validation clearly.
- The runtime can inspect category and provider before execution.

### TP-003 - Make allowlists explicit run policy

Priority: `P2`

Acceptance:

- Effective permission is the intersection of runtime configuration and mode
  declaration.
- An absent allowlist blocks all tools.
- The model cannot widen permissions.
- Effective policy is recorded in `run.json`.

### TP-004 - Harden write-path containment

Priority: `P1`

Acceptance:

- Tests cover sibling-prefix paths, `..`, rooted paths, alternate separators,
  and platform case behavior.
- Reparse-point or symbolic-link escapes are blocked or explicitly documented
  as unsupported.
- Writes remain constrained to the run output directory.

### TP-005 - Record explicit approval provenance

Priority: `P2`

Acceptance:

- Approval includes source, timestamp, tool name, target, and scope.
- Approval is runtime input, not model output.
- Approval decisions are traced without exposing secret arguments.

### TP-006 - Add tool execution limits

Priority: `P2`

Acceptance:

- Runtime policy can limit argument size, result size, call count, and timeout.
- Exceeded limits are traced and fail predictably.
- Execute-category tools remain blocked.

### TP-007 - Add tool argument redaction

Priority: `P2`

Acceptance:

- Tool descriptors can identify sensitive argument names.
- Trace records redacted placeholders instead of sensitive values.
- Tests cover nested structured arguments.

### TP-008 - Exercise `MockToolProvider` through orchestration

Priority: `P2`

Use an explicit runtime-authored test plan rather than model-selected tools.

Acceptance:

- Every call passes through `ToolPolicy` and `ToolExecutor`.
- Allowed, denied, and failed calls are traced.
- The exercise cannot write outside its run directory.

### TP-009 - Add a read-only evidence phase experiment

Priority: `Research`

Research status: `complete` on 2026-06-13. Implementation is deferred until the
tool policy and run-state prerequisites land.

Design note:
[`docs/research/TP-009-read-only-evidence-phase.md`](docs/research/TP-009-read-only-evidence-phase.md)

Explore a fixed phase where the runtime supplies allowlisted read results to a
model. Do not add autonomous tool selection yet.

Questions:

- How are requested evidence items represented as typed data?
- Which component selects the tool and arguments?
- How is untrusted tool output delimited in model context?
- How are evidence artifacts and citations traced?

### TP-010 - Keep MCP as a placeholder

Priority: `P2`

Acceptance:

- `McpToolProvider` fails clearly when invoked.
- No production MCP transport is added in this roadmap.
- The boundary remains testable and replaceable.

### TP-011 - Add policy simulation

Priority: `P2`

Evaluate proposed tool requests through the real policy boundary without
executing a provider.

Acceptance:

- Simulation uses the same typed requests and `ToolPolicy` as execution.
- Reports include allow or deny, matched rule, phase, provider, reason, and
  redacted arguments.
- No `IToolProvider` method is called.
- `RX-009` may render the results as a local inspection artifact.

## Milestone 9: CLI Inspection And Verification

Outcome: the CLI remains thin while making runs and modes easier to operate.

### CL-001 - Introduce explicit CLI commands

Priority: `P2`

Potential commands:

```text
run
modes list
modes validate
inspect
verify
eval
```

Acceptance:

- Parsing stays in CLI.
- Behavior stays in Core services.
- Existing invocation remains supported or receives a documented migration.
- No heavy command framework is required unless complexity proves it useful.

### CL-002 - Add machine-readable run output

Priority: `P2`

Acceptance:

- `--format json` reports run ID, outcome, output directory, and artifact paths.
- Human output remains the default.
- Logs do not corrupt JSON stdout.

### CL-003 - Support standard input

Priority: `P2`

Acceptance:

- `--input -` reads UTF-8 input from stdin.
- Empty stdin fails as a usage error.
- `input.md` is still written normally.
- Input source is recorded as `stdin`.

### CL-004 - Document and test exit codes

Priority: `P1`

Acceptance:

- Success, runtime failure, usage error, eval failure, and cancellation have
  stable codes.
- CLI tests cover each mapping.
- Provider-specific errors do not invent new exit codes.

### CL-005 - Add a dry validation command

Priority: `P2`

Acceptance:

- Validates mode, input accessibility, output-root safety, and provider
  configuration.
- Makes no external request.
- Creates no run directory.

### CL-006 - Add run inspection

Priority: `P2`

Acceptance:

- Reads `run.json`, eval reports, and trace through Core readers.
- Reports outcome, phase status, provider, durations, and integrity status.
- Does not mutate the run.

### CL-007 - Add run verification

Priority: `P2`

Acceptance:

- Verifies trace schema, terminal invariants, required artifacts, and hashes.
- Returns a nonzero exit code when tampering or corruption is detected.
- Works without loading model providers.

### CL-008 - Export an artifact bundle

Priority: `P2`

Acceptance:

- Exports one completed run as a deterministic local archive.
- The archive contains a manifest with paths, hashes, and byte lengths.
- Export verifies the run before packaging and fails on integrity errors.
- No upload or remote destination behavior is added.

## Milestone 10: Reliability And Test Depth

Outcome: common interruption and filesystem edge cases have deterministic
behavior.

### RT-001 - Add all-mode end-to-end tests

Priority: `P1`

Acceptance:

- Every built-in mode and checked-in lens variant runs through its compatible
  mock pattern, eval, and artifact finalization.
- Assertions cover mode-specific headings.

### RT-002 - Add cancellation boundary tests

Priority: `P1`

Acceptance:

- Tests cancel during mode load, model call, artifact write, and eval.
- Partial outputs remain understandable.

### RT-003 - Add concurrent run tests

Priority: `P2`

Acceptance:

- Concurrent runs never share directories or trace sessions.
- Artifact contents stay associated with the correct run ID.
- No global mutable runtime state is introduced.

### RT-004 - Add filesystem failure tests

Priority: `P2`

Acceptance:

- Tests cover unwritable output roots, failed atomic replacement, and missing
  run directories where practical.
- Original exceptions are not hidden by cleanup failures.

### RT-005 - Add malformed trace reader tests

Priority: `P2`

Acceptance:

- Truncated JSON, unknown schema versions, duplicate sequence numbers, and
  invalid terminal ordering produce useful errors.

### RT-006 - Add culture and timezone tests

Priority: `P2`

Acceptance:

- Machine-readable timestamps use invariant ISO 8601 formatting.
- Numeric metadata is culture invariant.
- Output directory timestamps remain UTC.

### RT-007 - Add architecture boundary tests

Priority: `P2`

Acceptance:

- Core does not reference CLI.
- Orchestration does not reference provider implementation types.
- Eval and artifact code do not reference GitHub or Azure configuration.
- Pattern implementations do not coordinate runtime services directly.
- After `OP-012`, the orchestrator does not branch on a pattern name.

### RT-008 - Add credential-free CI

Priority: `P1`

Acceptance:

- CI restores and runs `dotnet test`.
- No secrets are required.
- Generated `outputs/` content is not committed.
- The documented local commands match CI.

### RT-009 - Add a pattern conformance test kit

Priority: `P1`

Acceptance:

- Every registered pattern runs through shared tests for deterministic
  planning, unique node IDs, valid dependencies, bounded execution, and one
  authoritative output.
- The kit verifies that a model response cannot mutate the remaining plan.
- Pipeline configurations are covered as data, not one-off test code.
- Adding a registered pattern without conformance coverage fails tests.

## Milestone 11: Static Inspection Improvements

Outcome: generated inspection remains an artifact, not an application.

### SI-001 - Link phase artifacts

Priority: `P2`

Acceptance:

- The static page links to main, critic, revision, result, trace, and eval
  artifacts.
- Links are relative and remain inside the run directory.

### SI-002 - Render an event timeline

Priority: `P2`

Acceptance:

- The timeline is generated from typed trace events.
- It shows event sequence, timestamp, phase, and duration where available.
- It uses no JavaScript or external assets.

### SI-003 - Show provenance and integrity

Priority: `P2`

Acceptance:

- The page shows trace schema, mode version, prompt hashes, and artifact
  verification status.
- Sensitive configuration is omitted.

### SI-004 - Improve accessibility and printing

Priority: `P2`

Acceptance:

- Semantic landmarks and table headings are present.
- Status is not conveyed by color alone.
- Print output remains readable.

## Research Backlog

These items fit the runtime thesis, but should stay out of implementation until
their prerequisites land. Each item requires a short design note or spike
before implementation.

### Research Governance

The research-intake freeze through `OP-011`, `OP-012`, and `OP-013` is now
satisfied. New research should still replace, merge, or explicitly outrank an
existing item rather than only expanding the backlog.

These rules apply to both `Research Backlog` and `Agent Systems Research`.

No more than two research items should be active at once. Every active item
must have a design note under `docs/research/` containing:

- a falsifiable hypothesis or concrete design question
- prerequisites and assumptions
- a timebox
- the smallest useful experiment
- baseline and negative control
- required fixtures, traces, and artifacts
- reproduction commands
- evidence and limitations
- a final disposition: promote, defer, merge, or reject

Research status values are `proposed`, `active`, `complete`, `deferred`,
`merged`, and `rejected`. An unlabeled research item is `proposed`. `Complete`
means the question produced a documented decision; it does not mean
implementation is approved or finished.

Research priority:

1. `RB-008` Bounded scatter-gather orchestration.
2. `RB-009` Context projection and information barriers.
3. `RB-010` Static boundedness analysis.
4. `RB-011` Speculative execution with deterministic commit.
5. `RB-012` Model portfolio scheduling.

### RB-001 - One bounded contract-repair pass

If deterministic eval fails only for repairable output structure, the runtime
may run one explicit repair phase. The runtime decides eligibility and allows
at most one pass.

Prerequisites:

- deterministic heading parsing
- content-under-heading validation
- terminal trace integrity checks
- authoritative revision output

Guardrails:

- No open-ended loop.
- No LLM-based pass/fail decision.
- Original failure and repaired output remain inspectable.
- The repair output is evaluated deterministically.

### RB-002 - Resume an interrupted run

Research status: `complete` on 2026-06-13. Safe resume is limited to verified
checkpoints with no ambiguous external model call.

Design note:
[`docs/research/RB-002-resume-interrupted-run.md`](docs/research/RB-002-resume-interrupted-run.md)

Research whether a persisted lifecycle state machine can safely resume from
the next unstarted phase.

Prerequisites:

- immutable run state
- lifecycle state machine
- stable execution-node state
- atomic artifact writes
- model call correlation IDs

Guardrails:

- Do not resume from an ambiguous in-flight model call.
- Do not mutate the original trace in a misleading way.
- Resumed runs must be visibly marked as resumed.

### RB-003 - Candidate generation with deterministic selection

Research status: `merged` into `RB-011`.

Candidate generation is the simplest experimental configuration of
speculative execution with deterministic commit. Do not implement it as a
separate orchestration mechanism.

### RB-004 - Trace hash chain

Research whether chaining event hashes adds meaningful value beyond artifact
hashes and run verification.

Prerequisites:

- trace schema versioning
- event sequence numbers
- artifact hashes and sizes
- run verification command

Guardrails:

- Prefer artifact hashes unless trace mutation detection clearly requires a
  chain.
- Hashing must not include unstable serialization artifacts.

### RB-005 - Mode drift report

Research a deterministic report that compares mode and prompt hashes across
runs and reports exactly what changed.

Guardrails:

- Do not attempt semantic prompt evaluation.
- Do not use an LLM to judge whether the new mode is better.
- Report file-level and prompt-level provenance only.

### RB-006 - Bounded conditional routing

Research whether a pattern may choose among predeclared branches using only a
runtime-owned deterministic predicate.

Example predicates:

- a structural eval failed with a declared repairable check ID
- a provider failed with a typed transient or terminal category
- a runtime budget has enough capacity for a predeclared fallback

Guardrails:

- Every possible branch and maximum call count is declared before execution.
- Free-form model content cannot create a branch or choose a route.
- The decision, inputs, matched predicate, and skipped nodes are traced.
- No generic router, supervisor, or dynamic graph engine is introduced.
- `RB-001` remains the first concrete use case to evaluate.

### RB-007 - Runtime-owned blackboard

Research a run-scoped, typed blackboard that lets declared execution nodes
share structured intermediate facts without passing every value through
free-form prompt context.

Vocabulary: extend the canonical `BlackboardEntry`, typed information
references, provenance, lifecycle, and runtime commit authority from `CF-001`.
Do not create a parallel shared-state record.

Questions:

- Are entries immutable and append-only, or can the runtime replace a value
  under a declared key?
- How do pattern plans declare which nodes may read or propose writes to each
  blackboard section?
- Which component validates and commits a model-proposed entry?
- How are entry schema, producer node, source evidence, timestamp, and
  revision history represented?
- How are conflicts, missing entries, and size limits handled
  deterministically?
- Which blackboard snapshots belong in traces and artifacts without
  duplicating sensitive or large content?

Prerequisites:

- typed pattern execution plans
- immutable run state
- stable execution-node state
- typed trace payload contracts

Guardrails:

- The blackboard is scoped to one run and is not long-term memory.
- The runtime owns keys, schemas, permissions, validation, and commits.
- Model output may propose content but cannot create sections, widen access,
  delete history, or commit state directly.
- Reads and accepted or rejected write proposals are attributable to an
  execution node and traceable.
- Blackboard contents cannot select tools, approve actions, alter the plan, or
  declare completion.
- Persistence stays inside the run output directory.
- Do not introduce a generic tuple-space, message bus, or multi-agent
  framework.

### RB-008 - Bounded scatter-gather orchestration

Research a pattern that runs a fixed set of independent reasoning nodes in
parallel and gathers their typed outputs into one declared synthesis node.

Questions:

- How does a plan prove that scatter nodes are independent and safe to run
  concurrently?
- How are output contracts declared for each branch and for the gather node?
- Does the gather node receive full outputs, structured projections, or
  artifact references?
- How are branch failures, timeouts, cancellation, and partial completion
  represented?
- What ordering is used when concurrent results are assembled into context
  and artifacts?

Prerequisites:

- typed pattern execution plans
- one runtime executor
- stable execution-node state
- model call correlation IDs
- runtime-owned execution budgets

Guardrails:

- Branch count and maximum model calls are fixed before execution.
- The model cannot spawn branches or extend the gather phase.
- Concurrency does not change deterministic artifact or context ordering.
- The gather node cannot hide missing or failed branches.
- No worker may directly invoke another worker.

### RB-009 - Context projection and information barriers

Research typed projections that give each execution node only the input,
prior results, evidence, and blackboard entries declared by the pattern plan.

Vocabulary: extend the canonical `ContextProjection` and
`ContextProjectionItem` contracts from `CF-001`. Projection inputs refer to
canonical observations, evidence, claims, beliefs, and blackboard entries.

Scope boundary: this item owns node-specific disclosure and information-flow
policy. `AR-004` owns retention, compaction, and eviction across the run.

Questions:

- How are projections declared without embedding prompt prose in runtime code?
- Which metadata accompanies projected values so provenance survives
  summarization or transformation?
- How are absent, redacted, oversized, or unauthorized values represented?
- Can the runtime prove that a node did not receive undeclared context?
- How should projections apply to model requests, tool requests, traces, and
  artifacts?

Prerequisites:

- typed pattern execution plans
- stable execution-node state
- typed trace payload contracts
- `RB-007` runtime-owned blackboard research

Guardrails:

- Default visibility is deny.
- A model cannot request broader context or discover hidden entry names.
- Projection is performed by runtime code before provider serialization.
- Redaction and truncation are explicit, traceable transformations.
- Provider-specific clients receive only the completed projection.

### RB-010 - Static boundedness analysis

Research a plan analyzer that computes hard upper bounds before execution.

Candidate bounds:

- execution nodes and stages
- model calls and retry attempts
- tool calls by category
- parallel width
- input and output characters
- estimated duration, tokens, and cost when metadata is available

Questions:

- Which bounds can be proven exactly and which are conservative estimates?
- How are nested patterns and predeclared conditional branches expanded?
- How are unknown provider token usage and latency represented?
- Which plan properties make analysis impossible and should therefore be
  rejected?

Prerequisites:

- typed pattern execution plans
- pattern plan validation
- runtime-owned execution budgets
- common model response metadata for token and cost extensions

Guardrails:

- Unknown usage is never treated as zero.
- A plan that exceeds a hard configured limit fails before its first model
  call.
- Runtime counters still enforce limits because static analysis is not a
  substitute for execution-time checks.
- Analysis does not permit open-ended loops or model-created nodes.

### RB-011 - Speculative execution with deterministic commit

Research running a fixed set of alternative nodes concurrently while allowing
the runtime to commit exactly one authoritative result through predeclared,
deterministic criteria.

This item subsumes `RB-003`.

Potential commit criteria:

- output-contract satisfaction
- explicit deterministic score
- provider failure category
- measured latency or cost within a declared quality tier
- stable tie-breaking by plan order

Questions:

- Are losing alternatives allowed to finish, or should they be cancelled?
- How are completed but uncommitted outputs retained for inspection?
- Can commit occur early without making results timing-dependent?
- How are provider retries distinguished from speculative alternatives?

Prerequisites:

- bounded scatter-gather orchestration research
- deterministic evaluation
- model call correlation IDs
- runtime-owned execution budgets
- typed run outcome and lifecycle state

Guardrails:

- Alternatives and commit rules are fixed before execution.
- No LLM judge or model-reported confidence selects the winner.
- Exactly one result is committed, and every alternative remains traceable.
- Timing cannot silently change selection unless latency is an explicit
  declared criterion.
- Speculation cannot increase the precomputed execution bound.

### RB-012 - Model portfolio scheduling

Research runtime policy that assigns a registered model provider and model to
each execution node using declared requirements and measured operational
metadata.

Candidate policy inputs:

- required capabilities and context size
- allowed providers and data-handling restrictions
- historical structural eval pass rate
- typed provider health and failure categories
- latency, token usage, and estimated cost
- deterministic fallback order

Questions:

- Which metadata is configuration, which is measured locally, and which may be
  trusted from providers?
- How are stale or insufficient measurements represented?
- Can scheduling remain reproducible when health and pricing change?
- How are fallback attempts budgeted and traced?
- Should a run snapshot the effective portfolio policy and measurements used?

Prerequisites:

- common model response metadata
- typed provider failure categories
- provider configuration validation
- offline regression harness
- static boundedness analysis research

Guardrails:

- The runtime selects the provider and model; model output cannot influence
  scheduling.
- Allowlisted providers, privacy constraints, and maximum cost are hard
  policy, not optimization preferences.
- Unknown measurements never become favorable defaults.
- The effective decision inputs, selected target, fallback order, and reason
  are recorded without credentials.
- Tests use fake providers and deterministic metrics; no external credentials
  are required.

## Agent Systems Research

These items study agent reliability beyond orchestration topology. They focus
on how an agent receives information, represents uncertainty, interacts with
tools and people, and is evaluated as a stateful system.

Agent-systems research priority:

This ranks frontier research attention, not implementation order. Security,
state, trace, eval, and tool-policy prerequisites remain authoritative. The
linked articles are starting points for design notes; many are recent
preprints and their claims should be reproduced or independently validated
before shaping runtime policy.

1. `AR-009` Behavioral contracts and temporal runtime shields.
2. `AR-010` Structured belief state under partial observability.
3. `AR-017` Explicit environment state and causal world models.
4. `AR-011` Causal replay and counterfactual responsibility.
5. `AR-012` Verifier-guided generative optimization.
6. `AR-013` Claim-evidence provenance graph.
7. `AR-014` Value-of-information sensing and clarification.
8. `AR-015` Information fidelity and periodic re-grounding.
9. `AR-016` Adaptive test-time compute policy.
10. `AR-001` Untrusted observation and prompt-injection boundaries.
11. `AR-002` Stateful trajectory evaluation environments.
12. `AR-003` Abstention and escalation policy over belief state.
13. `AR-004` Run-scoped context lifecycle and loss-accounted compaction.
14. `AR-005` Typed tool effects and state transition contracts.
15. `AR-006` Metamorphic and adversarial robustness testing.
16. `AR-007` Offline policy improvement from run traces.
17. `AR-008` Human intervention and control transfer.

### AR-001 - Untrusted observation and prompt-injection boundaries

Research how the runtime distinguishes instructions from untrusted data
returned by tools, files, retrieval, and external providers.

Questions:

- How are origin, trust level, sensitivity, and allowed use represented on
  every context value?
- Can taint-like labels survive projection, summarization, blackboard writes,
  and artifact references?
- Which tool requests must be rejected when their arguments derive from
  untrusted instructions?
- How can deterministic fixtures measure task success and attack resistance
  separately?
- Which defenses belong in runtime policy rather than provider prompts?

Guardrails:

- External content is data and cannot grant authority.
- Prompt instructions are not treated as a security boundary.
- Untrusted content cannot widen tool permissions, disclose hidden context,
  approve actions, or alter the execution plan.
- Security traces record provenance and policy decisions without copying
  sensitive payloads.
- Research uses mock tools and local adversarial fixtures only.

### AR-002 - Stateful trajectory evaluation environments

Research small deterministic environments for evaluating agents across
multiple observations, tool calls, and state transitions rather than grading
only the final text.

Questions:

- How are initial state, allowed actions, hidden state, milestones, and final
  invariants represented?
- Which trajectory variations should be accepted when they reach the same
  valid end state?
- How are invalid intermediate side effects distinguished from harmless
  alternative paths?
- Can the same environment exercise mock, replay, and external providers?
- How are environment state and run artifacts kept isolated?

Guardrails:

- Environments are local, deterministic, credential-free, and resettable.
- The runtime, not the model, applies state transitions.
- Evaluation checks explicit milestones and end-state invariants.
- No autonomous shell, browser, or production-service access is introduced.
- The first environments should be narrow enough to inspect by hand.

### AR-003 - Abstention and escalation policy over belief state

Research when the runtime should abstain, select a fallback, or request human
input based on the structured belief state defined by `AR-010`.

Scope boundary: `AR-010` owns belief representation and updates. This item owns
runtime decisions triggered by that state.

Candidate states:

- supported
- contradicted
- insufficient-evidence
- stale
- unknown

Questions:

- Which states are computed deterministically and which may only be model
  proposals?
- How are contradictory claims and evidence represented without forcing
  premature consensus?
- Which declared thresholds trigger abstention, fallback, or escalation?
- How can calibration be evaluated without trusting self-reported confidence?
- How does epistemic state interact with the blackboard and evidence phases?

Guardrails:

- Model confidence is never treated as proof.
- The runtime owns state transitions and escalation thresholds.
- Abstention is a valid typed outcome, not a provider failure.
- Unsupported claims remain inspectable and cannot silently become facts.
- No LLM judge is required for MVP evaluation.

### AR-004 - Run-scoped context lifecycle and loss-accounted compaction

Research how a long run admits, retains, projects, compresses, and evicts
context while preserving provenance and making information loss visible.

Vocabulary: compacted values remain canonical information records with
`InformationProvenance` inputs and explicit lifecycle state from `CF-001`;
this item does not introduce a second context store.

Scope boundary: `RB-009` owns which values a node may receive. This item owns
how admitted values are retained, compacted, and evicted over time.

Questions:

- Which values must remain lossless and which may be summarized?
- How does a compacted value reference its source entries and transformation?
- What deterministic checks can detect missing required facts or citations?
- When should the runtime reject compaction and exceed a budget instead?
- How are raw and compacted context retained for replay and evaluation?

Guardrails:

- This is run-scoped context management, not long-term memory.
- The runtime decides when compaction is allowed and which schema it must
  satisfy.
- The model may propose a summary but cannot delete source context or hide
  reported loss.
- Compaction records input size, output size, provenance, and validation
  result.
- Tests compare full-context and compacted-context behavior on fixed cases.

### AR-005 - Typed tool effects and state transition contracts

Research tool descriptors that declare more than names and argument schemas:
preconditions, read and write sets, side effects, idempotency, reversibility,
and expected postconditions.

The resulting effect model is an input to the behavioral contracts in
`AR-009`; this item does not define a second enforcement system.

Questions:

- Which effects can be verified before execution and which require observing
  the result?
- How are stale preconditions and conflicting writes handled?
- Can dry-run simulation use the same contracts as real execution?
- How are retries restricted for non-idempotent operations?
- What evidence proves that a postcondition was satisfied?

Guardrails:

- Tool contracts do not authorize execution; `ToolPolicy` remains
  authoritative.
- Execute-category tools remain blocked by default.
- Unknown effects receive the most restrictive policy.
- Model output cannot redefine a tool's effects or claim a postcondition
  succeeded.
- MVP research uses `MockToolProvider` and in-run state only.

### AR-006 - Metamorphic and adversarial robustness testing

Research deterministic transformations that should preserve, constrain, or
predictably change agent behavior.

Candidate transformations:

- reorder irrelevant context
- add benign distractors
- vary formatting and equivalent wording
- inject stale, conflicting, or malicious observations
- fail or delay one tool result
- change culture, timezone, or path representation

Questions:

- Which invariants should remain stable under each transformation?
- How are expected differences distinguished from regressions?
- Can tests localize susceptibility to a model, mode, pattern, tool, or
  runtime policy?
- Which robustness metrics remain meaningful without semantic judging?

Guardrails:

- Every mutation and expected invariant is declared before execution.
- Tests compare typed outcomes, traces, decisions, and artifacts.
- No mutation may access external services or credentials.
- Robustness failures remain inspectable as ordinary run artifacts.

### AR-007 - Offline policy improvement from run traces

Research a controlled process that uses completed run traces and eval results
to propose changes to prompts, budgets, routing thresholds, or provider
policy without modifying live behavior automatically.

Counterfactual evidence from `AR-011` should be preferred over heuristic blame
when proposing changes from failed runs.

Questions:

- Which trace fields are suitable inputs without exposing private content?
- How are proposals linked to the failures or regressions they address?
- What replay or regression evidence is required before accepting a change?
- How are overfitting and evaluation leakage detected?
- Which policy surfaces must never be model-generated?

Guardrails:

- No online self-modification or autonomous deployment.
- Proposed changes are artifacts requiring explicit human review.
- Acceptance requires credential-free replay or regression evidence.
- Training and fine-tuning infrastructure remain out of scope.
- Historical runs remain immutable.

### AR-008 - Human intervention and control transfer

Research explicit runtime semantics for pausing, inspecting, approving,
correcting, or terminating a run at predeclared checkpoints.

Questions:

- What state and artifacts must be persisted before yielding control?
- Which fields may a human amend without rewriting run history?
- How is a correction distinguished from approval or cancellation?
- Does continuation create a new run, a linked segment, or a resumed
  checkpoint?
- How are stale approvals invalidated when inputs or plans change?

Guardrails:

- Intervention points and allowed actions are declared by runtime policy.
- Human input cannot silently mutate prior trace events or artifacts.
- Approval is scoped, attributable, expiring, and revocable before use.
- A paused run performs no background work.
- Mock tests cover approval, correction, rejection, timeout, and cancellation.

### AR-009 - Behavioral contracts and temporal runtime shields

Research machine-checkable agent contracts containing preconditions,
invariants, temporal constraints, governance rules, and declared recovery
behavior.

Example properties:

- authenticate before reading protected data
- obtain scoped approval before a write tool
- never use untrusted content as tool authority
- do not finalize while required evidence remains unresolved
- after a denied action, do not retry it through an equivalent tool

Questions:

- Which contract language is expressive enough without becoming a general
  theorem-proving platform?
- Which properties can be checked before a model call, before a tool call, or
  only after observing an effect?
- How are contracts composed across nested patterns and tool providers?
- What is the difference between a hard violation, a recoverable violation,
  and an eval failure?
- Can the runtime propose a compliant alternative without delegating policy
  decisions to the model?

Guardrails:

- Runtime enforcement is authoritative; prompt instructions are advisory.
- Hard constraints block execution before side effects occur.
- Recovery paths are predeclared and bounded.
- Contract violations, blocked actions, and recovery attempts are traced.
- Initial work uses deterministic predicates and state machines; an SMT
  solver is optional research, not an MVP dependency.

Relevant research:

- [Enforcing Temporal Constraints for LLM Agents](https://arxiv.org/abs/2512.23738)
- [Agent Behavioral Contracts: Formal Specification and Runtime Enforcement
  for Reliable Autonomous AI Agents](https://arxiv.org/abs/2602.22302)

### AR-010 - Structured belief state under partial observability

Research a typed belief state that separates observations, candidate
hypotheses, uncertainty, contradictions, and action policy.

Vocabulary: extend the canonical `Observation`, `Evidence`, `Claim`, and
runtime-committed `Belief` contracts from `CF-001`. This item owns belief
transition rules, not a replacement representation.

Questions:

- How are atomic beliefs represented without pretending natural-language
  probabilities are calibrated?
- How does new evidence support, weaken, supersede, or leave a belief
  unresolved?
- Should alternative hypotheses remain visible until deterministic evidence
  eliminates them?
- Which belief fields may be model-proposed, and which are runtime-computed?
- How do belief state, blackboard entries, evidence provenance, and
  abstention interact?

Guardrails:

- Observations are immutable and distinct from inferred beliefs.
- The runtime owns belief identity, provenance, lifecycle, and update rules.
- Model-reported confidence is metadata, not proof.
- Contradictions are retained rather than silently overwritten.
- This remains run-scoped and does not introduce long-term personal memory.

Relevant research:

- [Agent-BRACE: Decoupling Beliefs from Actions in Long-Horizon Tasks via
  Verbalized State Uncertainty](https://arxiv.org/abs/2605.11436)
- [Belief Memory: Agent Memory Under Partial
  Observability](https://arxiv.org/abs/2605.05583)

### AR-017 - Explicit environment state and causal world models

Research a typed separation among actual environment state, observable
signals, runtime state, agent belief state, actions, and state transitions.
Explore whether a declared or learned causal world model can simulate proposed
actions and explain observed state changes.

Questions:

- Which environments have an authoritative state that the runtime can inspect,
  and which expose observations only?
- How are action preconditions, transition rules, exogenous events, and hidden
  variables represented?
- Can a simulator reject impossible actions or estimate counterfactual
  outcomes before commitment?
- How is model mismatch detected when predicted and observed transitions
  diverge?
- Which causal structures are declared by humans, inferred from traces, or
  merely proposed by a model?
- How do world models support belief updates, value-of-information decisions,
  causal replay, and verifier-guided optimization?

Guardrails:

- Runtime state, belief state, and environment state remain distinct types.
- An inferred world model is advisory until validated against deterministic
  transition fixtures.
- Simulated success never substitutes for observing a real postcondition.
- Model-generated causal edges do not become authoritative without runtime
  validation.
- Initial experiments use small deterministic environments and perform no
  production side effects.
- Training a general learned world model is outside the project scope.

Relevant research:

- [Language Agents Meet Causality: Bridging LLMs and Causal World
  Models](https://arxiv.org/abs/2410.19923)
- [CausalPlan: Empowering Efficient LLM Multi-Agent Collaboration Through
  Causality-Driven Planning](https://arxiv.org/abs/2508.13721)

### AR-011 - Causal replay and counterfactual responsibility

Research whether controlled replay with explicit interventions can identify
which earlier decisions caused a failed run.

Candidate interventions:

- replace one model response with a known-good fixture
- remove one observation or blackboard write
- substitute one tool result
- force one policy decision or context projection
- hold all other recorded inputs constant where possible

Questions:

- What does it mean to hold a stochastic provider policy constant?
- Which effects can be established with one deterministic replay, and which
  require repeated samples and uncertainty intervals?
- How are interacting causes distinguished from one pivotal step?
- How can replay avoid mutating or misrepresenting the original run?
- Which counterfactual repairs are safe to turn into regression fixtures?

Guardrails:

- The original run and trace remain immutable.
- Every intervention is explicit, typed, and recorded in a new run.
- Causal claims include assumptions and uncertainty; correlation is not
  labeled as causation.
- No production side effects are replayed.
- MVP work uses mock or recorded providers and deterministic environments.

Relevant research:

- [Causal Agent Replay: Counterfactual Attribution for LLM-Agent
  Failures](https://arxiv.org/abs/2606.08275)
- [CausalFlow: Causal Attribution and Counterfactual Repair for LLM Agent
  Failures](https://arxiv.org/abs/2605.25338)

### AR-012 - Verifier-guided generative optimization

Research status: `complete` on 2026-06-14. Promote the bounded Flare dungeon
builder experiment described in
[`docs/research/AR-012-flare-dungeon-builder.md`](docs/research/AR-012-flare-dungeon-builder.md).

Research a bounded loop where the model proposes a candidate artifact, an
executable verifier returns structured feedback, and the runtime permits a
fixed number of revisions under a declared objective.

Potential verifier types:

- compiler or parser
- deterministic simulator
- constraint solver
- unit-test harness
- schema and invariant checker
- numeric objective with hard feasibility constraints

Questions:

- How are feasibility, objective score, and diagnostic feedback separated?
- What termination rule handles diminishing returns under a fixed budget?
- How are multiple feasible candidates compared without an LLM judge?
- Which verifier outputs are safe and useful to return to the model?
- How are best-so-far, latest, and authoritative candidates distinguished?

Guardrails:

- The verifier, objective, iteration limit, and budget are fixed before
  execution.
- The runtime owns candidate selection and termination.
- A model cannot alter the verifier, objective, or success threshold.
- Every candidate and verifier result remains inspectable.
- No arbitrary shell execution is introduced; initial verifiers are in-process
  deterministic components or tightly controlled test fixtures.

Relevant research:

- [Frontier-Eng: Benchmarking Self-Evolving Agents on Real-World Engineering
  Tasks with Generative Optimization](https://arxiv.org/abs/2604.12290)

### AR-013 - Claim-evidence provenance graph

Research a typed graph connecting observations, sources, tool results,
blackboard entries, intermediate claims, transformations, actions, and final
claims.

Vocabulary: graph nodes use `AgentInformationReference` and the canonical
information identities from `CF-001`. Graph edges extend existing provenance;
they do not duplicate content into a separate truth store.

Candidate relations:

- derived-from
- supports
- contradicts
- supersedes
- transformed-by
- used-to-authorize
- cited-by

Questions:

- What is the smallest useful provenance unit: artifact, section, claim, or
  span?
- How are claims normalized without assigning semantic authority to a model?
- Can deterministic checks find unsupported, stale, circular, or
  contradicted claims?
- How are privacy, redaction, and deleted source material represented?
- How does provenance support debugging, replay, verification, and recovery?

Guardrails:

- Provenance records lineage, not truth by itself.
- Source content remains immutable and separately addressable.
- Model-generated citations cannot create support edges without runtime
  validation.
- Graph serialization is bounded and stored inside the run directory.
- Initial evaluation uses planted fixtures with known support relationships.

Relevant research:

- [From Agent Traces to Trust: Evidence Tracing and Execution Provenance in
  LLM Agents](https://arxiv.org/abs/2606.04990)
- [WorldReasoner: Evaluating Whether Language Model Agents Forecast Events
  with Valid Reasoning](https://arxiv.org/abs/2606.11816)

### AR-014 - Value-of-information sensing and clarification

Research runtime policy for deciding whether another observation, tool call,
or human clarification is worth its cost before committing to an action.

Questions:

- How are uncertainty reduction, expected utility, action risk, latency,
  monetary cost, and human interruption cost represented?
- Which quantities can be measured, bounded, or only configured?
- How does the policy choose between asking a human, calling a tool,
  abstaining, or acting?
- How are repeated low-value questions prevented?
- Can fixed synthetic environments provide known value-of-information ground
  truth?

Guardrails:

- The runtime decides whether information gathering is permitted.
- Model-proposed uncertainty and utility are untrusted inputs.
- High-impact actions may require information regardless of estimated value.
- Every clarification or sensing decision records its inputs and reason.
- Initial work uses deterministic tables or simulators, not learned policies.

Relevant research:

- [Value of Information: A Framework for Human-Agent
  Communication](https://arxiv.org/abs/2601.06407)

### AR-015 - Information fidelity and periodic re-grounding

Research how factual distortion accumulates as information passes through
tools, summaries, projections, handoffs, blackboard updates, and model
responses.

Questions:

- Which deterministic fidelity metrics work for structured facts, citations,
  and state transitions?
- How is semantic fidelity evaluated without making an LLM judge
  authoritative?
- When should the runtime re-read original evidence instead of trusting a
  derived context value?
- Can each transformation declare an expected loss budget?
- How do fidelity budgets compose across a run?

Guardrails:

- Derived context retains links to lossless source material.
- Unknown fidelity is not treated as perfect fidelity.
- Re-grounding triggers and maximum intervals are runtime policy.
- A fidelity failure cannot be repaired by merely increasing model confidence.
- MVP experiments use planted facts and deterministic transformations.

Relevant research:

- [Information Fidelity in Tool-Using LLM Agents: A Martingale Analysis of the
  Model Context Protocol](https://arxiv.org/abs/2602.13320)

### AR-016 - Adaptive test-time compute policy

Research runtime allocation of additional candidates, revisions, tool calls,
verification, or model capacity according to expected benefit and remaining
budget.

Questions:

- Which observable signals justify more depth, more width, a stronger model,
  or early termination?
- How is the verification gap measured when generating alternatives is easier
  than selecting among them?
- Can a policy remain deterministic for a fixed trace and configuration?
- How are quality tiers and diminishing returns represented?
- What offline evidence is required before changing allocation policy?

Guardrails:

- Hard maximum calls, duration, tokens, and cost are fixed before execution.
- The runtime owns allocation and termination decisions.
- Model requests for more compute are advisory only.
- More sampling is not assumed to improve quality without a valid verifier.
- Initial policies are explicit rules evaluated through replay and fixed
  suites, not online reinforcement learning.

Relevant research:

- [Benchmark Test-Time Scaling of General LLM
  Agents](https://arxiv.org/abs/2602.18998)
- [Frontier-Eng: Benchmarking Self-Evolving Agents on Real-World Engineering
  Tasks with Generative Optimization](https://arxiv.org/abs/2604.12290)

## Runtime Experiments

These are small, local-first experiments that make the runtime thesis visible.
They should not displace the bounded revision loop, lifecycle state, trace
integrity, deterministic evals, or provider and tool boundary work.

The bar for this section:

- the runtime owns the loop
- the model never controls completion
- the output is an artifact, not an app
- the experiment can run locally
- the result can be inspected after the run
- the demo teaches something about orchestration, policy, evals, or traces

### RX-001 - Loop Microscope

Generate a static, read-only inspection artifact showing the run as a sequence
of runtime-owned decisions:

```text
input accepted
mode loaded
main started
main completed
critic started
critic completed
revision started
revision completed
eval started
eval passed
artifacts finalized
run succeeded
```

Why it matters:

Shows that the product is the loop, not the final answer.

Non-goal:

No JavaScript, server, dashboard, or live UI.

### RX-002 - Failure Zoo

Create a small catalog of intentionally bad model outputs and provider
behaviors:

- empty output
- malformed Markdown
- missing or duplicated required heading
- critic passes but revision fails
- provider timeout or malformed JSON
- model output exceeds budget
- trace or optional artifact writing fails

Each case should produce a failed run with useful artifacts.

Why it matters:

Makes runtime ownership visible through failure, not only success.

Non-goal:

No LLM judge or semantic quality scoring.

### RX-003 - Phase Surgery Replay

Replay a completed run while replacing exactly one phase output with an
explicit fixture:

```text
original main -> original critic -> replaced revision -> eval
```

The runtime reuses recorded context and regenerates downstream artifacts.
This experiment builds on `RG-006`.

Guardrails:

- Replay is visibly marked.
- The original run remains immutable.
- No provider calls are required.

### RX-004 - Mode Pipeline Lab

Superseded by `OP-004` (`LinearPipelinePattern`), which implements this
experiment as a first-class orchestration pattern rather than a standalone
lab. Retained here for the artifact-layout idea.

Run a configured multi-mode pipeline:

```text
frame -> challenge -> synthesize
```

Each mode remains a real mode. The runtime owns the plan.

```text
outputs/<timestamp>_pipeline/
  input.md
  pipeline_result.md
  runs/
    01_frame/
    02_challenge/
    03_synthesize/
  trace.json
  eval_report.md
```

Non-goal:

The model cannot add, remove, reorder, or repeat modes.

### RX-005 - Runtime Decision Ledger

Create a compact artifact listing every decision the runtime made:

- selected mode and provider
- loaded phase order and transitions
- blocked or allowed tool
- accepted or rejected phase output
- eval and terminal outcome

Why it matters:

Separates runtime decisions from model text.

### RX-006 - Contract Mutation Lab

Given a valid result, generate deterministic mutations and prove the evals
fail for the intended reason:

- remove, reorder, or duplicate a required heading
- empty a required section
- move heading text inside a code block
- append a fake successful terminal trace event

This experiment builds on `RG-005`.

Non-goal:

No semantic grading.

### RX-007 - Provider Weather Report

Run the same fixed prompt corpus against configured providers and produce a
structural report:

- success or failure
- empty or malformed output rate
- latency and token usage when available
- contract pass or fail
- provider failure category

Why it matters:

Makes provider swapping inspectable without leaking provider details into the
orchestrator.

Non-goal:

No answer-quality judgment and no LLM judge.

### RX-008 - Artifact Autopsy

Given a failed run directory, generate a deterministic report from the trace,
run manifest, artifacts, and evals.

The report should identify the failure point, last valid runtime state,
planned and written artifacts, failed eval, and terminal-event validity.

Non-goal:

No model-generated debugging advice.

### RX-009 - Policy Courtroom

Render `TP-011` policy simulation results as `policy_report.md`.

Each proposed call receives:

- allow or deny
- matched rule
- phase and provider
- reason
- redacted arguments

Non-goal:

No real tool execution.

### RX-010 - Cognitive Mode Compiler

Treat a mode directory as a compiled unit and write
`mode_validation_report.md`.

Checks:

- manifest schema and prompt existence
- phase order and duplicate phase names
- output contract and unsafe paths
- documentation consistency
- mock-run compatibility

Why it matters:

A mode becomes a validated runtime artifact, not just a prompt folder.

### RX-011 - Run Museum

Generate a static local index of selected runs grouped by mode, outcome,
provider, eval status, date, and failure category.

Non-goal:

No web app or server. Only static generated files.

### RX-012 - Loop Recipe DSL

Research a tiny declarative format for runtime-owned loop plans:

```yaml
name: bounded-revision
steps:
  - phase: main
  - phase: critic
    receives: [main]
  - phase: revision
    receives: [main, critic]
terminal:
  eval: deterministic
  authoritative: revision
```

Why it matters:

Sharpens the distinction between a mode, a phase, and a runtime plan.

Non-goal:

No generic agent framework, arbitrary graph execution, or model-controlled
flow.

## Explicitly Out Of Scope

Do not add these to the implementation backlog without changing the project
intent:

- chat or conversational UI
- ASP.NET or Blazor
- generic agent framework abstractions
- autonomous shell execution
- autonomous file editing
- model-controlled tool approval
- model-controlled completion or phase skipping
- long-term memory
- background jobs
- production MCP integration
- full Azure deployment automation
- LLM-based MVP evals
- prompt prose hardcoded into orchestration

## Backlog Hygiene

- Split an item before implementation if it cannot be reviewed comfortably in
  one pull request.
- Every behavior change includes focused tests.
- Update this file when an item changes priority or scope.
- Keep one explicit implementation queue and finish or reprioritize it before
  starting work from later milestones.
- Do not promote a research item into implementation without a completed
  design note and an explicit `promote` disposition.
- When two items converge, mark one `merged` and name the surviving owner
  rather than maintaining duplicate acceptance criteria.
- Review the research priority lists after each completed milestone; do not
  treat their numbering as permanent.
- Mark an item complete only when tests, mock execution, artifacts, trace, eval,
  provider isolation, and README behavior remain valid.
