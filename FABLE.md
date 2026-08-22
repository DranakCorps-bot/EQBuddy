# Fable inbox

Plans for Claude, not a work order. **Claude: take a `ready` item, then delete it**
(or leave only what is still planned).

EQBuddy is the incubation lab. We refine the finished state here. The organization
iterates the same way as the software (observe → diagnose → change → verify).

## When this file is in play

**V2–V3 only.** Cross-cutting architecture, significant refactor, ambiguous root cause,
security/privacy/migration, complex parallel decomposition.

Fable 5 writes the plan. Helm last-looks. **Claude executes it** — unless the plan carries a
`needs-david:` line, which names a decision from the consequence list in `CLAUDE.md`
("What needs David, and what does not") and waits for him to answer THAT. David reads this
file as a digest he can veto; the release gate is where anything he dislikes is caught.

**Approval by exception, not by gate** (David, 2026-08-22). The old shape — Fable plans,
David marks `approved`, Claude executes — had him reading every plan in full to say yes to
work the release gate already protected him from. The first two plans through here were
approved without a word changed.

**V0–V1 does not belong here.** Cosmetic, mechanical, localized, straightforward work
stays one Claude loop. Do not pay a planning-handoff tax without reason. The test before
stubbing: *if David answered one question right now, could this be V1?* If yes, ask the
question instead.

This is not a fourth gate on Scribe intake or Bevel critique. Those files stay their
own inboxes. Org-level proposals do not go in this file.

There is no Fable Grok Bot. Point Fable 5 at this file.

## Item shape

- **Priority:** `ready` (plan written; Claude may take it) · `needs-david: <the decision>`
  (names ONE consequence-list decision; waits for his answer, never for a generic "approve") ·
  `someday`. David may still write `approved` as an explicit mark; it means `ready`.
- **Class:** `V2` or `V3` (if you cannot say why it is not V0–V1, it does not go here)
- **Source:** discussion/issue, Bevel/Scribe item, or David's words
- **Plan:** architecture, risks, decomposition, verification, what is out of scope
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.
- **Decided without asking:** the implementation calls the plan made that could have gone the
  other way, one line each — these go to `DECISIONS.md` when the item is taken.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`.

---

*No items yet.*
