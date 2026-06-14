# Document Distiller

An evidence-first .NET runtime that infers a corpus topic, identifies central
pillars, critiques its own structure, and renders a cited research brief.

This is intentionally not a chatbot. The model reasons inside a fixed loop;
the runtime owns evidence IDs, phase order, artifacts, traces, evaluation, and
completion.

## Why This Is A Portfolio Project

The interesting ML engineering work is not a single prompt. It is the system
around the prompt:

- deterministic ingestion and chunk identity;
- overlapping chunks with exact character spans and SHA-256 provenance;
- typed structured outputs;
- atomic claims with stance and calibrated corpus confidence;
- provider isolation;
- bounded critic/revision orchestration;
- citation-graph validation;
- prompt-injection risk scanning over untrusted source text;
- corpus, prompt, model, and configuration fingerprints;
- provider-neutral token, reasoning, and cache telemetry;
- reproducible mock runs;
- inspectable traces and intermediate artifacts;
- evals that fail broken reports even when the prose sounds convincing.

## Pipeline

```text
documents
  -> deterministic ingestion, overlap chunking, and risk scanning
  -> topic and pillar analysis
  -> atomic claim and evidence graph
  -> evidence-aware critique with source content
  -> constrained revision
  -> runtime validation and evidence matrix
  -> runtime Markdown rendering
  -> deterministic evaluation
```

Supported MVP inputs are UTF-8 `.md`, `.markdown`, and `.txt` files.

## Discover A Corpus With Firecrawl

The optional discovery funnel uses Firecrawl v2 search followed by selective
scraping. Search results do not flow directly into the model. The runtime
applies HTTPS-only fetching, domain allowlists, URL deduplication, per-domain
caps, source-count limits, and per-source character limits first. It writes a
timestamped `discovery_manifest.json` beside the materialized Markdown corpus.

```powershell
$env:FIRECRAWL_API_KEY = "..."
$env:OPENROUTER_API_KEY = "..."

dotnet run --project projects/document-distiller/src/DocumentDistiller.Cli -- `
  --discover "meaningful danger in tabletop and isometric fantasy RPGs" `
  --include-domain dndbeyond.com `
  --include-domain aonprd.com `
  --include-domain gemrb.org `
  --include-domain github.com `
  --max-sources 8 `
  --max-sources-per-domain 2 `
  --provider openrouter
```

Optional discovery settings:

```text
FIRECRAWL_ENDPOINT=https://api.firecrawl.dev
FIRECRAWL_API_KEY=...
```

Firecrawl is deliberately a source provider, not the research loop. Selection,
artifact writing, risk scanning, evidence identity, validation, and evaluation
remain runtime responsibilities.

For a reproducible no-Firecrawl showcase corpus:

```powershell
./scripts/materialize-gm-shaped-hole.ps1
```

This creates a timestamped corpus from D&D, Pathfinder, GemRB, Flare, and
OpenTemple sources while retaining original URLs, retrieval URLs, license
notes, selection limits, and hashes.

## Build And Test

From `projects/document-distiller`:

```powershell
dotnet restore --configfile ../../NuGet.Config
dotnet test DocumentDistiller.slnx --no-restore
```

## Run Without Credentials

From the repository root:

```powershell
dotnet run --project projects/document-distiller/src/DocumentDistiller.Cli -- `
  --input projects/document-distiller/examples/agent-systems `
  --provider mock
```

The command prints the timestamped output directory. Every run writes:

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

## Run With OpenAI

The OpenAI provider uses the Responses API with strict Structured Outputs.
As of June 13, 2026, OpenAI's current general model guidance names `gpt-5.5`;
override the model rather than changing runtime code.

```powershell
$env:OPENAI_API_KEY = "..."
$env:OPENAI_MODEL = "gpt-5.5"

dotnet run --project projects/document-distiller/src/DocumentDistiller.Cli -- `
  --input projects/document-distiller/examples/agent-systems `
  --provider openai
```

Optional settings:

```text
OPENAI_ENDPOINT=https://api.openai.com/v1
OPENAI_MODEL=gpt-5.5
OPENAI_REASONING_EFFORT=medium
```

## Run With OpenRouter

The OpenRouter provider uses its OpenAI-compatible Responses API and the same
strict claim/evidence schema:

```powershell
$env:OPENROUTER_API_KEY = "..."
$env:OPENROUTER_MODEL = "openai/gpt-5"

dotnet run --project projects/document-distiller/src/DocumentDistiller.Cli -- `
  --input projects/document-distiller/examples/agent-systems `
  --provider openrouter
```

Optional settings:

```text
OPENROUTER_ENDPOINT=https://openrouter.ai/api/v1
OPENROUTER_REASONING_EFFORT=medium
```

The OpenAI adapter records response IDs and available usage fields for every
phase, including input tokens, output tokens, cached input tokens, and
reasoning tokens. The runtime writes them to `model_usage.json` and the trace.
OpenAI prompt caching is automatic for eligible repeated prefixes; cached-token
telemetry makes the effect measurable rather than assumed.

## Evidence Model

Each pillar contains atomic claims rather than one coarse citation list. Every
claim carries:

- a stable claim ID;
- one of `corroborated`, `single-source`, `contested`, or `inference`;
- a confidence value representing support within the corpus;
- one or more exact chunk IDs.

`evidence_matrix.json` resolves those links, counts independent source
coverage, and calculates a deterministic lexical-grounding signal. That signal
is intentionally documented as a diagnostic, not an entailment model.

`source_risk.json` records source text that resembles instruction overrides,
role impersonation, citation manipulation, or data-exfiltration requests.
Documents remain evidence, never executable instructions.

`index.html` is a static, script-free inspection dashboard generated from the
same typed data as the Markdown report. It surfaces claims, stance, confidence,
exact source spans, quality metrics, source risks, and links to raw artifacts
without introducing a web application.

## Deep Research Direction

OpenAI's deep research models currently run through the Responses API and
require at least one data source such as web search, remote MCP, or file search
over vector stores. The next integration should provision a temporary vector
store from the corpus, run `o4-mini-deep-research` in background mode, preserve
its source annotations as a separate artifact, and delete the temporary store
according to an explicit retention policy.

That feature should remain an optional synthesis provider. Local evidence
chunk IDs and deterministic citation checks stay authoritative, so enabling
hosted research does not move orchestration or evaluation into the model.

Official references:

- [Deep research](https://developers.openai.com/api/docs/guides/deep-research)
- [File search](https://developers.openai.com/api/docs/guides/tools-file-search)
- [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs)
- [Prompt caching](https://developers.openai.com/api/docs/guides/prompt-caching)
- [Using GPT-5.5](https://developers.openai.com/api/docs/guides/latest-model)
- [Firecrawl Search](https://docs.firecrawl.dev/features/search)
- [Firecrawl Scrape](https://docs.firecrawl.dev/features/scrape)

## Human Evaluation

1. Run the example corpus in mock mode and open `report.md`.
2. Pick three evidence IDs from the report and verify each resolves in
   `evidence.json` to an exact source span.
3. Confirm `trace.json` shows the fixed
   `scan -> analysis -> critic -> revision -> validate -> eval` lifecycle.
4. Read `draft.json`, `critique.json`, and `analysis.json` to judge whether the
   revision addressed the critic without inventing citations.
5. Add one document that disagrees with the others and verify the tension is
   preserved rather than flattened into consensus.
6. Add the phrase "ignore previous instructions" to a test source and confirm
   it appears in `source_risk.json` without changing runtime control.
7. Compare `run_manifest.json` across two identical runs. Corpus and prompt
   fingerprints should match even though run IDs and timestamps differ.
