# Product Notes

## Validated Showcase: The GM-Shaped Hole

**Signal:** Strong positive user response.

**Working title:** *The GM-Shaped Hole: How Fantasy RPGs Manufacture
Meaningful Danger*

Use a curated corpus spanning:

- Dungeons & Dragons SRD rules for play, exploration, combat, hazards, and
  monsters;
- Pathfinder rules for action economy, degrees of success, encounters,
  conditions, hazards, and game building;
- open isometric CRPG architecture and implementation documentation from
  projects such as GemRB and OpenTemple.

The central research question is:

> How do tabletop RPGs and isometric CRPGs create meaningful danger when
> software must replace human judgment?

This is a particularly strong Document Distiller showcase because it combines
prose, formal rules, design guidance, architecture documentation, and
implementation evidence. The sources should produce real tensions rather than
simple agreement: flexible adjudication versus explicit mechanics, dramatic
failure versus repetition, and tabletop intent versus computer-enforced
behavior.

Prefer a curated set of 8-12 documents over whole repositories. Preserve source
licenses and attribution in the input manifest.

## Run Results (2026-06-14)

No `FIRECRAWL_API_KEY` was present, so the corpus was materialized with
`scripts/materialize-gm-shaped-hole.ps1` (8 documents: D&D SRD, Pathfinder 2e
GM Core, GemRB, Flare, and OpenTemple) and distilled via OpenRouter
(`openai/gpt-5-2025-08-07`).

Outcome: 8 documents, 50 evidence chunks, 4 pillars, 23 atomic claims, 100%
source coverage, mean lexical grounding 62%, 0 source-risk findings, all
evaluations passed (`dotnet test`: 19/19).

Artifacts from this run are in
`outputs/20260614T005928226Z_distill_d96b63cf/` (`report.md`, `index.html`,
`eval_report.md`), with the curated corpus and discovery manifest in
`outputs/20260614T005701775Z_curated_223609cc/`.

**Honest read:** the result is technically strong but one level too literal.
The pipeline inferred a topic of "encounter mechanics and engine mappings"
rather than fully pursuing the poetic thesis above — *why* danger is
enjoyable and what software substitutes for a human GM's judgment, rather
than just cataloguing how mechanics and engines implement it.

**Next step:** add a `--lens` option. Topic inference stays automatic, but
the lens steers the analysis phase toward a specific angle — e.g. danger,
fairness, agency, legibility, and what software substitutes for a human GM —
so the synthesis argues *why*, not just *what*.
