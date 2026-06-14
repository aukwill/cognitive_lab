# Runtime Architecture for Reliable LLM Systems

An LLM application becomes inspectable when orchestration is separated from
generation. The runtime should decide phase order, state transitions, retry
limits, artifact paths, and terminal outcomes. A model may propose content,
but it should not decide whether its own answer is complete.

Typed contracts reduce ambiguity between phases. An analysis phase can return
claims and evidence references, while a later critic can inspect those fields
without scraping prose. This makes failures easier to localize and gives tests
a stable surface.

Provider isolation is equally important. Authentication, endpoints, response
parsing, and provider errors belong behind a narrow client interface. The core
orchestrator should not know whether a response came from OpenAI, Azure, a
local model, or a deterministic test double.
