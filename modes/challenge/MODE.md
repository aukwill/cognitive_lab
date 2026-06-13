# Challenge

Challenge stress-tests a claim or proposed direction.

Use it to expose assumptions, failure modes, counterarguments, and concrete
tests that could disprove the current position.

The runtime executes one fixed loop:

```text
main -> critic -> revision
```

The revision preserves the challenge output contract while addressing material
critic findings. The runtime remains responsible for phase order, evaluation,
artifact paths, and completion.
