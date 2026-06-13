# Cognitive Runtime Backlog

This backlog develops the runtime thesis:

> The LLM is not the product. The loop is the product.

Items are ordered to strengthen runtime-owned orchestration, state, policy,
traces, evals, and artifacts before expanding integrations.

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

1. Stabilize the current baseline.
2. Add the bounded revision loop.
3. Make run state and lifecycle explicit.
4. Harden traces and artifacts.
5. Deepen deterministic evaluation.
6. Improve mode authoring and validation.
7. Add an offline regression harness.
8. Harden provider and tool boundaries.
9. Improve CLI inspection and verification.

## Next Implementation Queue

This is the proposed order for the next implementation cycle. All other items
remain open, but should not interrupt this queue without an explicit priority
change.

The cycle targets the two questions a reviewer of a cognitive runtime asks
first: does the loop demonstrably do anything, and is the runtime built on
typed state rather than local variables? Heavy integrity machinery (hash
chains, run manifests, resume, artifact bundles, drift reports) is deliberately
deferred until the simpler version earns it. Each item is intended to be one
reviewable pull request.

- [x] `EV-001` Parse Markdown headings structurally. Prerequisite for the rest.
- [x] `EV-002` Validate content under each heading.
- [x] `EFF-001` Add a deterministic loop-efficacy eval. Cycle centerpiece;
      the only item that touches mode files.
- [ ] `RS-001` Introduce immutable run state.
- [ ] `RS-007` Define a typed run outcome.
- [ ] `TA-002` Centralize trace event names. Pulled earlier than its milestone
      so EV-003 and EV-005 do not repeat event-name string literals.
- [ ] `EV-003` Evaluate phase order and cardinality.
- [ ] `EV-005` Evaluate terminal trace integrity.

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

## Milestone 2: Explicit Runtime State And Lifecycle

Outcome: orchestration operates on typed state and validated transitions rather
than local variables and implicit event ordering.

### RS-001 - Introduce immutable run state

Priority: `P1`

Acceptance:

- Run state contains run identity, loaded mode, phase results, artifact paths,
  lifecycle status, and eval outcome.
- State updates happen through small runtime-owned transitions.
- Provider-specific configuration is not stored in core run state.

### RS-002 - Add a lifecycle state machine

Priority: `P1`

Suggested states:

```text
created
running
phases-completed
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

### RS-003 - Add stable phase execution state

Priority: `P1`

Acceptance:

- Each configured phase has pending, running, completed, or failed status.
- The runtime records provider, model, start time, end time, and output length.
- The model cannot modify phase status.

### RS-004 - Inject run ID generation

Priority: `P1`

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

Acceptance:

- `trace.json` includes a schema version.
- Every event includes a monotonically increasing sequence number.
- Readers reject unsupported future versions with a clear error.

### TA-002 - Centralize trace event names

Priority: `P1`

Acceptance:

- Core event names are defined in one inspectable location.
- Producers and evals do not repeat string literals for required events.
- Event payloads remain plain JSON-compatible data.

### TA-003 - Record durations

Priority: `P1`

Acceptance:

- Phase, model-call, evaluation, and total-run durations are recorded.
- Durations use `TimeProvider`.
- Tests use fixed or controlled time.

### TA-004 - Add model call correlation IDs

Priority: `P1`

Acceptance:

- Each `model.called` event pairs with one `model.completed` or
  `model.failed` event.
- Correlation does not depend on event position alone.
- Tests detect missing and duplicate completion events.

### TA-005 - Add sanitized failure events

Priority: `P1`

Acceptance:

- Failures record category, phase, provider, exception type, and safe message.
- Tokens, authorization headers, and configured secrets are never traced.
- Provider response excerpts are length bounded.

### TA-006 - Add `run.json`

Priority: `P1`

Create a machine-readable run manifest containing:

- run ID and outcome
- mode name and version
- provider and model names
- start and end times
- configured phase order
- artifact inventory
- eval summary

Acceptance:

- The manifest is written inside the run directory.
- It contains no credentials.
- It is covered by deterministic serialization tests.

### TA-007 - Add artifact hashes and sizes

Priority: `P1`

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

### EV-003 - Evaluate phase order and cardinality

Priority: `P1`

Acceptance:

- The trace contains exactly one successful completion for each configured
  phase.
- Main, critic, and revision completion order matches the mode.
- Unexpected duplicate phase executions fail the eval.

### EV-004 - Evaluate model call pairing

Priority: `P1`

Acceptance:

- Every model call has one correlated completion or failure.
- A successful run has no unclosed model calls.
- The report identifies unmatched call IDs.

### EV-005 - Evaluate terminal trace integrity

Priority: `P1`

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

### MO-008 - Run every built-in mode in integration tests

Priority: `P1`

Acceptance:

- `frame`, `challenge`, and `synthesize` all pass with the mock provider.
- Every run writes valid required artifacts and revision traces.
- Test data is shared without hiding mode-specific assertions.

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

Research status: Complete on 2026-06-13. Implementation is deferred until the
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

- Every built-in mode runs through main, critic, revision, eval, and artifact
  finalization.
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

### RT-008 - Add credential-free CI

Priority: `P1`

Acceptance:

- CI restores and runs `dotnet test`.
- No secrets are required.
- Generated `outputs/` content is not committed.
- The documented local commands match CI.

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

Research status: Complete on 2026-06-13. Safe resume is limited to verified
checkpoints with no ambiguous external model call.

Design note:
[`docs/research/RB-002-resume-interrupted-run.md`](docs/research/RB-002-resume-interrupted-run.md)

Research whether a persisted lifecycle state machine can safely resume from
the next unstarted phase.

Prerequisites:

- immutable run state
- lifecycle state machine
- stable phase execution state
- atomic artifact writes
- model call correlation IDs

Guardrails:

- Do not resume from an ambiguous in-flight model call.
- Do not mutate the original trace in a misleading way.
- Resumed runs must be visibly marked as resumed.

### RB-003 - Candidate generation with deterministic selection

Research whether multiple drafts can be useful when selection is based only on
declared deterministic contracts.

Guardrails:

- No LLM judge.
- No model-controlled winner selection.
- Selection criteria must be declared before generation.
- All candidates remain inspectable.

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
- Mark an item complete only when tests, mock execution, artifacts, trace, eval,
  provider isolation, and README behavior remain valid.
