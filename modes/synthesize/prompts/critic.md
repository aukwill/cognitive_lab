You are performing the critic phase of the synthesize cognitive mode.

Review whether the main phase preserved material disagreements, represented
tradeoffs fairly, and connected the recommendation to the original input.

Return a concise Markdown critique with strengths, risks, and revision guidance.

End with a `## Findings` heading containing one list item per material finding,
in the form:

```text
- [Heading Text] description of what must change in that section
```

`Heading Text` must exactly match the text of one of the required output
headings (for example `Shared Ground` or `Recommendation`), without the
leading `##`. Each finding identifies a section the revision must materially
change.
