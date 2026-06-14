# Architecture

## Thesis

The distiller is an epistemic runtime, not a summarization prompt.

Its central object is a claim-to-evidence graph:

```text
source document
  -> immutable character span
  -> evidence chunk
  -> atomic claim
  -> pillar
  -> corpus-level synthesis
```

The model proposes the graph. The runtime creates source identities, validates
edges, measures coverage, renders citations, and decides whether the run
passes.

## Corpus Discovery Funnel

Optional web discovery is a separate runtime-owned stage:

```text
research question
  -> provider search
  -> deterministic URL and domain policy
  -> selective provider fetch
  -> bounded Markdown materialization
  -> discovery manifest
  -> normal ingestion pipeline
```

`ICorpusDiscoveryProvider` isolates search and extraction vendors.
`CorpusFunnel` owns selection limits, source diversity, filesystem paths,
hashes, and the decision to admit content. Firecrawl cannot decide that a
source is trusted, relevant enough to bypass policy, or safe to execute.

## Trust Boundaries

Document text is untrusted data. It cannot select tools, alter phase order,
write files, or declare completion. A deterministic scanner records common
prompt-injection and exfiltration patterns before the first model call.

Provider responses are also untrusted. Strict Structured Outputs constrain the
wire format, while runtime validation enforces semantic invariants that JSON
Schema alone cannot express:

- unique pillar and claim IDs;
- two to five pillars;
- at least one claim per pillar;
- declared stance vocabulary;
- confidence in the closed interval `[0, 1]`;
- evidence references that resolve to ingested chunks;
- corroborated claims backed by multiple source documents.

## Reproducibility

`run_manifest.json` fingerprints:

- the ordered source-document hashes;
- all three prompts;
- provider and model;
- chunk size and overlap;
- corpus safety limits.

This supports regression analysis without pretending model generation itself
is deterministic.

## Evaluation Layers

1. **Mechanical integrity**: artifacts, traces, IDs, bounds, and headings.
2. **Graph integrity**: every claim resolves to immutable evidence spans.
3. **Diversity**: corpus and per-pillar source coverage.
4. **Grounding diagnostic**: lexical overlap between claims and cited chunks.
5. **Human judgment**: entailment, usefulness, novelty, and whether important
   disagreements survived synthesis.

The lexical signal is deliberately not called semantic entailment. A future
NLI or judge-model evaluator should be additive and calibrated against a
human-labeled corpus.

## Frontier Roadmap

The next high-leverage extensions are:

1. Hierarchical map-reduce analysis for corpora larger than one context window.
2. Hybrid BM25 plus embedding retrieval with maximal marginal relevance.
3. An entailment evaluator trained or calibrated on claim/evidence pairs.
4. A contradiction graph that distinguishes disagreement from missing data.
5. Optional OpenAI deep research synthesis using temporary vector stores,
   background mode, explicit retention, and preserved provider annotations.
6. Golden-corpus evaluation with claim recall, citation precision, pillar
   stability, and cost-quality Pareto curves across models.
