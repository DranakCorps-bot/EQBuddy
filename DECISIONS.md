# Decisions made without asking

**For David to skim, not to approve.** Every line here is a call an agent made under the
pre-authorization in `CLAUDE.md` ("What needs David, and what does not"): a decision that
could plausibly have gone another way, did not touch the consequence list, and was made
rather than asked. David vetoes from here; a veto while the work is unreleased is cheap,
which is why the release gate is the only hard one.

**One line each, newest first.** *What was decided · the other way it could have gone · where
it landed (commit, file, or thread).* If a line needs a paragraph, it was probably a question.

**A veto goes in the same line**, prefixed `VETOED (David, date):` with the replacement, so the
history of the call stays readable. If vetoes become common, the consequence list in
`CLAUDE.md` is too short; if there are never any, it is too long.

---

## 2026-08-22

- **eqlwiki request etiquette: 2 lookups in flight per process, 30 s before the same page may
  be re-checked, pack re-check bounded to flagged creatures** · could have been 1/60 s or
  uncapped as today · wiki re-check plan, `FABLE-FEEDBACK.md` 2026-08-21. *Put in front of
  David as "adjust at approval" at the time; it should have been this line instead.*
- **One spawn type, `triggered`, with a free-text `triggeredBy` — not a `chained` /
  `player-triggered` pair** · the reporter's two-word taxonomy was real in the world · eqlwiki
  records one value, and the engine treats both the same; `SpawnEntry.SpawnType`.
- **A re-check in flight keeps the OLD wiki answer on screen rather than showing "not checked
  yet"** · could have nulled the memo entry · the #217 rule (pending ≠ nothing new), wiki
  re-check plan.
