# AGENTS.md

## Product Intent

This project is an evidence-first document distillation runtime.

The product is the pipeline:

> ingest -> scan -> chunk -> analyze -> critique -> revise -> validate -> evaluate

The model proposes structured research content. The runtime owns phase order,
evidence identifiers, citations, artifact paths, traces, evaluation, and
completion.

Do not turn this into a chatbot, generic agent framework, or web application.

## Stack

- C# and .NET 10
- `System.Text.Json`
- `HttpClient`
- xUnit

Prefer standard library primitives and inspectable code.

## Boundaries

### `DocumentDistiller.Cli`

CLI parsing, configuration, composition, exit codes, and console output only.

### `DocumentDistiller.Core`

Ingestion, chunking, model-provider boundaries, orchestration, rendering,
tracing, artifacts, and deterministic evaluation.

### `DocumentDistiller.Tests`

All tests must pass without API keys. Use the deterministic mock provider.

## Runtime Rules

- The runtime fixes the phase sequence.
- The runtime creates source and evidence IDs.
- The runtime creates atomic claim IDs and validates claim stance/confidence.
- The runtime fingerprints the corpus, prompts, model, and chunking policy.
- The runtime writes all files.
- The runtime renders Markdown citations from typed evidence IDs.
- The runtime decides whether the run passed.
- Providers return typed content only.
- No writes are allowed outside the timestamped run directory.

## Required Artifacts

Every successful run writes:

- `input_manifest.md`
- `run_manifest.json`
- `source_risk.json`
- `evidence.json`
- `evidence_matrix.json`
- `model_usage.json`
- `draft.json`
- `critique.json`
- `analysis.json`
- `report.md`
- `index.html`
- `trace.json`
- `run_summary.md`
- `eval_report.md`

## Testing

Run:

```powershell
dotnet test DocumentDistiller.slnx
```

External credentials must never be required for tests.
