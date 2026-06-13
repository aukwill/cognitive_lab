# Lens

Lens explains a concept by mapping it onto a hobby or fandom the reader
already has deep intuition for.

The runtime executes one fixed loop:

```text
main -> critic -> revision
```

The revision preserves the lens output contract while addressing material
critic findings. The runtime remains responsible for phase order, evaluation,
artifact paths, and completion.

## Choosing a lens

Unlike other modes, `lens` does not ship a default prompt set under
`prompts/`. The runtime selects a lens via `--lens <name>`, which loads
`prompts/<name>/{main,critic,revision}.md` instead. The output contract,
`mode.json`, and this document are shared by every lens.

Built-in lenses:

- `warcraft` - explain the concept to a Warcraft player using factions,
  raids, classes, and gear.

## Adding a new lens

Add `prompts/<name>/main.md`, `prompts/<name>/critic.md`, and
`prompts/<name>/revision.md`. Each prompt must:

- Produce (or critique, or revise) Markdown with exactly the headings
  declared in `mode.json`'s output contract.
- Keep the hobby-specific framing inside `## The Lens` and `## The Mapping`;
  the other headings stay legible without the hobby's vocabulary.
- End the critic prompt with a `## Findings` heading in the shared
  `- [Heading Text] description` format.

No runtime code changes are required to add a lens.
