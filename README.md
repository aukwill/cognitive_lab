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

Command Prompt:

```bat
dotnet run --project src/CognitiveRuntime.Cli -- --mode frame --input examples/agent_runtime_goal.txt --run-mode mock --html
```

PowerShell:

```powershell
dotnet run --project src/CognitiveRuntime.Cli -- `
  --mode frame `
  --input examples/agent_runtime_goal.txt `
  --run-mode mock `
  --html
```

Each run creates a timestamped directory under `outputs/` containing:

- `input.md`
- `result.md`
- `trace.json`
- `run_summary.md`
- `eval_report.md`
- `index.html` when `--html` is present

## Static HTML Inspection

The runtime may emit static HTML inspection artifacts for completed runs. These
files are artifacts, not a web application. They make the loop inspectable
without moving orchestration into the browser.

`index.html` is opt-in with `--html`. It opens directly from the filesystem and
contains read-only summaries of the run, mode, phases, tool policy decisions,
evals, trace, and artifact links. It has no JavaScript, external assets, server,
editing controls, approval controls, or rerun controls.

## Runtime Shape

The CLI parses arguments, loads environment configuration, builds dependency
injection, calls the orchestrator, and maps the result to an exit code.

The core runtime owns mode loading, phase order, model calls, tool policy,
artifact paths, trace events, evaluation, and completion. Mode-specific prose
lives under `modes/`.

Every mode runs one bounded sequence:

```text
main -> critic -> revision
```

The main phase receives no prior results. The critic receives the typed main
result. The revision receives typed main and critic results, preserves the
mode's output contract, and becomes the authoritative answer. `result.md`
places that revision first, with the initial draft and critic review retained
as inspectable supporting context. Deterministic evals validate the revision
itself, so headings in an appendix cannot make a malformed revision pass.

The model produces reasoning content inside each phase. It does not choose
phase order, request another revision, skip evaluation, write artifacts, or
declare the run complete.

`run.completed` marks completion of the cognitive phase loop. Deterministic
post-run evaluation and configured optional artifacts follow.
`run.finalized` is the single terminal success event and means all configured
post-run work was persisted. `run.failed` is the single terminal failure event
and follows best-effort required failure artifacts. A trace does not contain
both terminal outcomes.

## Lenses

The `lens` mode explains a concept by mapping it onto a hobby the reader
already has intuition for. Unlike other modes, it has no default prompt set;
`--lens <name>` selects which `modes/lens/prompts/<name>/` directory supplies
the main, critic, and revision prompts. `mode.json`, `MODE.md`, and the output
contract are shared by every lens.

```text
dotnet run --project src/CognitiveRuntime.Cli -- --mode lens --lens warcraft --input examples/agent_runtime_goal.txt --run-mode mock --html
```

Adding a hobby lens means adding three prompt files under
`modes/lens/prompts/<name>/` — no runtime code changes are required. See
[`modes/lens/MODE.md`](modes/lens/MODE.md) for the contract each lens prompt
set must satisfy.

## Model Providers

The default provider is `mock`. It is deterministic and requires no credentials.

GitHub Models calls the versioned GitHub REST inference API. Create a
fine-grained personal access token with the `models:read` permission. Keep the
token out of files and set it only in the current shell:

Command Prompt:

```bat
set "GITHUB_TOKEN=YOUR_NEW_TOKEN"
dotnet run --project src/CognitiveRuntime.Cli -- --mode frame --input examples/agent_runtime_goal.txt --run-mode github-models
```

PowerShell:

```powershell
$secureToken = Read-Host "GitHub PAT (models:read)" -AsSecureString
$env:GITHUB_TOKEN = [System.Net.NetworkCredential]::new("", $secureToken).Password

dotnet run --project src/CognitiveRuntime.Cli -- `
  --mode frame `
  --input examples/agent_runtime_goal.txt `
  --run-mode github-models
```

The default model is `openai/gpt-4.1`. These optional settings override the
GitHub Models defaults:

```text
GITHUB_MODELS_TOKEN=
GITHUB_MODELS_MODEL=openai/gpt-4.1
GITHUB_MODELS_ENDPOINT=https://models.github.ai/inference
GITHUB_MODELS_API_VERSION=2026-03-10
```

`GITHUB_MODELS_TOKEN` takes precedence over `GITHUB_TOKEN` when both are set.
GitHub's free API usage is rate limited.

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
