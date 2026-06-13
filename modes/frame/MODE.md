# Frame

Frame turns a messy intent into an explicit problem definition.

Use it when the runtime should surface objectives, constraints, unknowns, and
next actions before solution work begins.

The runtime executes one fixed loop:

```text
main -> critic -> revision
```

The revision preserves the frame output contract while addressing material
critic findings. The runtime, not the model, decides phase order, evaluation,
artifact paths, and completion.
