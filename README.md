# Cognitive Agent Runtime Lab

A local-first C#/.NET runtime for running reusable cognitive modes from files.
It is intentionally not a chatbot, web app, or autonomous agent framework.

> The LLM is not the product. The loop is the product.

## Requirements

- .NET 10 SDK
- No credentials for mock mode

## Build And Test

```powershell
dotnet restore --configfile NuGet.Config
dotnet test CognitiveRuntime.slnx --no-restore
```

## Run Mock Mode

```powershell
dotnet run --project src/CognitiveRuntime.Cli -- `
  --mode frame `
  --input examples/agent_runtime_goal.txt `
  --run-mode mock
```

Each run creates a timestamped directory under `outputs/` containing:

- `input.md`
- `result.md`
- `trace.json`
- `run_summary.md`
- `eval_report.md`

## Runtime Shape

The CLI parses arguments, loads environment configuration, builds dependency
injection, calls the orchestrator, and maps the result to an exit code.

The core runtime owns mode loading, phase order, model calls, tool policy,
artifact paths, trace events, evaluation, and completion. Mode-specific prose
lives under `modes/`.

`run.completed` marks completion of the cognitive phase loop. Deterministic
post-run evaluation follows, and `run.finalized` marks the fully persisted run.
This allows the eval to verify that the cognitive loop completed while keeping
eval events in the same trace.

## Model Providers

The default provider is `mock`. It is deterministic and requires no credentials.

GitHub Models uses:

```text
MODEL_PROVIDER=github-models
GITHUB_TOKEN=
GITHUB_MODELS_ENDPOINT=https://models.github.ai/inference
GITHUB_MODELS_MODEL=
```

The Azure Foundry client is an intentional MVP stub with a real provider
boundary. It reports a clear error rather than pretending to be a production
integration:

```text
MODEL_PROVIDER=azure-foundry
AZURE_FOUNDRY_ENDPOINT=
AZURE_FOUNDRY_API_KEY=
AZURE_FOUNDRY_DEPLOYMENT=
AZURE_FOUNDRY_API_VERSION=
```

## Tool Safety

Modes do not call tools in the MVP. The `IToolProvider`, `ToolPolicy`, and
`ToolExecutor` boundaries are present for later wiring. Tools are blocked unless
allowlisted, writes require runtime approval and an in-run target path, execute
tools stay blocked, and external tools require detailed tracing.
