# AGENTS.md

## Project Intent

This repo is a small C#/.NET cognitive agent runtime lab.

Do not turn it into a chatbot, generic agent framework, or web app.

The thesis:

> The LLM is not the product. The loop is the product.

The runtime owns orchestration, state, policy, traces, evals, and artifact writing.

The model is only one reasoning policy inside the loop.

## Language and Stack

Use C# and .NET.

Prefer boring, inspectable code.

Use standard .NET primitives unless a dependency clearly improves the runtime.

Preferred libraries:

* `System.Text.Json`
* `HttpClient`
* `Microsoft.Extensions.Configuration`
* `Microsoft.Extensions.DependencyInjection`
* `Microsoft.Extensions.Logging`
* `xUnit`

Do not add heavy agent frameworks.

Do not add:

* Semantic Kernel
* LangChain
* LangGraph
* AutoGen
* CrewAI
* Microsoft Agent Framework
* ASP.NET
* Blazor
* background job frameworks

## Architecture Priorities

Optimize for:

1. Clear runtime boundaries
2. Typed contracts
3. Small files
4. Testable behavior
5. No external credentials required for tests
6. Traceability
7. Artifact discipline
8. Provider isolation

## Required Solution Shape

Use this structure:

```text
src/
  CognitiveRuntime.Cli/
  CognitiveRuntime.Core/

tests/
  CognitiveRuntime.Tests/

modes/
  frame/
  challenge/
  synthesize/

examples/
outputs/
```

## Project Boundaries

### `CognitiveRuntime.Cli`

CLI only.

Allowed responsibilities:

* parse args
* load configuration
* setup dependency injection
* call orchestrator
* return exit code

Do not put orchestration logic here.

### `CognitiveRuntime.Core`

Runtime implementation.

Owns:

* mode loading
* phase running
* model calls
* tool policy
* trace events
* artifact writing
* eval execution

### `CognitiveRuntime.Tests`

All tests must pass without external credentials.

Mock mode must be deterministic enough to test.

## Runtime Rules

The runtime owns the loop.

The model should not decide:

* what files to write
* where files are written
* whether evals pass
* whether a tool is allowed
* whether the run is complete
* whether a phase is skipped

The model can produce reasoning content.

The runtime controls execution.

## Model Client Rules

All model providers must implement `IModelClient`.

Required clients:

* `MockModelClient`
* `GitHubModelsClient`
* `AzureFoundryModelClient`

Provider-specific logic must stay inside provider-specific clients.

Do not leak Azure Foundry or GitHub Models details into:

* `Orchestrator`
* `PhaseRunner`
* mode loading
* eval logic
* artifact writing

External provider failures should produce clear errors.

Tests must not require external model providers.

## Tool Provider Rules

All tools must go through `IToolProvider`.

All tool calls must go through `ToolPolicy`.

No direct tool calls from the orchestrator without policy evaluation.

Initial providers:

* `MockToolProvider`
* `McpToolProvider`

`McpToolProvider` can be a placeholder in the MVP, but the boundary should be real.

## Tool Safety Rules

Do not implement autonomous shell execution.

Do not allow writes outside the run output folder.

Block tools by default unless allowlisted.

Tool categories:

```text
read      -> allowed only if allowlisted
write     -> requires explicit runtime approval
execute   -> blocked by default
external  -> trace heavily
```

For MVP, modes should not call tools unless explicitly wired later.

## Mode Rules

Modes live under `/modes`.

A mode is a reusable way of thinking, not a task automation.

Initial modes:

* `frame`
* `challenge`
* `synthesize`

Each mode must include:

```text
MODE.md
mode.json
prompts/main.md
prompts/critic.md
```

Do not hardcode mode behavior in C# if it belongs in mode files.

The runtime may enforce contracts, but mode-specific prose should live in mode files.

## Trace Rules

Every run must produce `trace.json`.

Trace events should include:

```text
run.started
mode.loaded
phase.started
model.called
model.completed
critic.started
critic.completed
artifact.written
eval.started
eval.completed
run.completed
```

Trace files must be valid JSON.

Trace should be useful for debugging without reading console logs.

## Artifact Rules

Every run creates a timestamped output folder.

Required artifacts:

```text
input.md
result.md
trace.json
run_summary.md
eval_report.md
```

Do not write outside the output folder except for normal test/build artifacts.

## Eval Rules

Every run should produce `eval_report.md`.

Minimum evals:

* required artifacts exist
* trace contains `run.started`
* trace contains `run.completed`
* critic phase ran
* result is not empty
* output contract was satisfied

Evals should be simple and deterministic.

Do not use an LLM for MVP evals.

## Testing Rules

Use xUnit.

Tests must pass with:

```bash
dotnet test
```

Add or update tests when changing:

* mode loading
* orchestration
* trace writing
* eval logic
* tool policy
* model provider selection
* artifact writing

Do not require API keys in tests.

## Style

Use modern C#.

Use nullable reference types.

Use records for simple immutable contracts.

Prefer constructor injection.

Prefer async APIs for model/tool clients.

Keep methods small.

Avoid clever abstractions until the simple version works.

## Non-Goals

Do not build:

* web UI
* chat UI
* long-term memory
* background jobs
* autonomous file editing
* autonomous shell execution
* production MCP integration
* full Azure deployment
* full GitHub Models production client

## Definition of Done

A task is done only when:

1. `dotnet test` passes.
2. Mock mode works with no credentials.
3. CLI can run `frame` mode.
4. Required artifacts are written.
5. Trace file is valid JSON.
6. Eval report is written.
7. Provider-specific code is isolated.
8. README instructions still work.

## Working Principle

When in doubt, choose the boring runtime primitive over the impressive agent demo.

The product is the loop.
