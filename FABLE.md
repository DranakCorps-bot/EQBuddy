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
- **Bevel pre-design: yes / no, because…** — required on any plan with a presentation PR.
  Fable plans the architecture; Bevel judges whether the player can still do the job. The
  executor treated a plan as the design pass once (2026-08-22) and should not have had to guess.
- **Shot offline: yes / no** — for any staged screenshot. `shoot.ps1` is NOT offline by
  default, so a "not read yet" prediction for an unseeded wiki page is wrong before it runs.
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.
- **Decided without asking:** the implementation calls the plan made that could have gone the
  other way, one line each — these go to `DECISIONS.md` when the item is taken.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`. Fable last-looks the
executed diff (H4) and answers in the same file; a defect found there is a V1 item for the
next loop, not a reopening of the plan.

---

## Inline themes — expand in place, pop out on request

- **Priority:** `needs-david: is the widget still the right home for four themes at all, or
  does this land as the phone already has it (one page, cards, no windows)?` — open question
  5 in `docs/proposals/InlineThemes.md`. It is roadmap direction (consequence list #5), and
  it is the ONLY open question that is his: everything else is decided or decidable.
- **Class:** `V2` — four themes × two UIs, a host-ownership rule (trap 15: the card and the
  window must not both claim a body), and it reverses a fold direction that was itself a
  signed decision. Not V1 because a "yes" changes what the widget IS, and the shape cannot be
  tried on one theme without committing to the host rule.
- **Source:** David's ask, 2026-08-21 (*"expandable sub-categories under them with an option
  to pop out"*); #228 (daetien-lab, joeymavity); `docs/proposals/InlineThemes.md`; Bevel's
  ruling 2026-08-21 (tab strip; split rule; host rule; pop-out collapses the card).
- **Plan:** NOT written yet, deliberately. The proposal carries the architecture in outline
  and Bevel has settled the shape (questions 1–3). Question 4 (ship any theme expanded?) is
  implementation and is decided here: **collapsed, all of them** — Bevel's host rule and #219's
  lesson both say the glance line is the product. Writing the full decomposition before the
  question above is answered would be planning a thing that may not be built. When David
  answers, Fable writes the plan in one sitting; the proposal is most of it.
- **Bevel pre-design:** yes — already done for the shape; a second pass on the expanded card's
  height per theme before PR 1.
- **Already shipped:** the theme windows (`ProgressWindow`, `QuestsWindow`, `GearLootWindow`,
  `CreatureWindow`, both UIs); every theme body lifted to `IWidgetCard`; `BreakoutKind` and
  `DisabledBreakouts`; the phone's card-with-tabs, which is the working prototype.
- **Checked:** `docs/proposals/InlineThemes.md` in full; Bevel's entry in `BEVEL.md`;
  `HANDOFF.md`'s three mentions. Not re-grepped this session: the `SectionLink` launchers and
  `SectionScroll.MaxHeight` — the proposal's claims about them are hypotheses until the plan is
  written.

---

*No other items.*
