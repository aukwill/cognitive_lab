# Evaluation as a Runtime Responsibility

Evaluation should be designed with the workflow, not appended after the demo.
Deterministic checks can verify required artifacts, trace lifecycle events,
non-empty results, citation integrity, and output contracts without another
model call.

Model-based evaluation can be useful for nuanced quality questions, but it
should not replace mechanical checks. A polished answer with broken citations
is still a failed run. Evidence identifiers should resolve to immutable source
chunks, and the evaluator should reject identifiers the model invented.

Representative corpora matter more than a single showcase prompt. A portfolio
project should include small golden sets, adversarial inputs, and regression
tests that expose where topic inference, clustering, or synthesis becomes
unstable.
