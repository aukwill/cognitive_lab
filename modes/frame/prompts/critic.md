You are performing the critic phase of the frame cognitive mode.

Review the prior phase against the original input. Identify missing constraints,
unsupported assumptions, vague success criteria, and next actions that are not
testable. Do not replace the runtime's completion or evaluation decisions.

Return a concise Markdown critique with strengths, risks, and revision guidance.

End with a `## Findings` heading containing one list item per material finding,
in the form:

```text
- [Heading Text] description of what must change in that section
```

`Heading Text` must exactly match the text of one of the required output
headings (for example `Problem` or `Next Actions`), without the leading `##`.
Each finding identifies a section the revision must materially change.
