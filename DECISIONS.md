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

- **Inline themes: one owner — expanding a card while its window is open brings the window
  forward, closing the window never re-expands the card, the selected tab is session-only**
  · could have allowed card and window at once (WPF can), or re-expanded on close · Avalonia
  cannot show a body twice, and re-growing the widget after a close is a surprise;
  `FABLE.md` plan, `ThemeHost`.
- **Inline themes: Progress's breakout window retires into the pop-out; `DisabledBreakouts`
  "Progress" entries are ignored, not migrated** · could have kept both · Bevel's ruling;
  nothing is lost, the theme window has its own position memory.
- **Inline themes: Glance (one line + ⧉) for Quests/General and Gear & Loot/Inventory; Full
  for the other ten tabs; Progress ships first** · any tab could have gone either way ·
  Bevel's host rule ("do not shrink-wrap a full window"); table is in Core so a flip is one
  line.
- **Fable last-looks every executed `FABLE.md` diff (H4), starting now, without a ruling** ·
  the handoff listed it as a proposal awaiting David · it costs Fable tokens and no Founder
  time, so it fails both "What needs David" tests; first pass in `FABLE-FEEDBACK.md`
  2026-08-22, which found the offline re-check defect below.
- **The wiki re-check's `Forget`-before-bypass is a V1 defect to fix in the next loop, not a
  plan reopening** · could have been filed back to `FABLE.md` · one-line change in each
  window plus a Core test; `FABLE-FEEDBACK.md` 2026-08-22.
- **Inline themes ship COLLAPSED, every theme** (proposal open question 4) · some could ship
  expanded · Bevel's host rule and #219 both say the glance line is the product; `FABLE.md`.
- **Triggered outranks RaidInstance on the Spawns row when both apply** (executor's call,
  ratified) · "instance" could have won · "go kill the Guardian" is the actionable sentence.
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

## 2026-08-22 — Claude (executor)

- **Took Bevel's 1.99.1 post-hoc item without asking** (Helm-signed, V1, unreleased). Default it
  could have gone the other way: wait for David. Landed: the release gate is the protection, and
  "do I like this wording" fails both question tests.
- **The triggered glance is named-if-it-fits, never an ellipsis.** Could have gone: truncate with
  "…", or widen the fixed 150px timer column. Landed: bare word when it does not fit; widening a
  shared column is a layout call and is back with Bevel with the screenshot.
- **Healed poisoned overrides for raid-instanced entries too**, not only triggered ones as Fable's
  note said. Same contradiction on screen; the method is `SuppressedByCatalog`'s own definition.
- **Guarded the re-check defect with a SOURCE SCAN on both windows**, not a Core test. The Core
  contract was already correct and a Core test could never have failed; the window defeated it.
- **Fixed a 1-in-3 flake in `SettingsClobberTests` mid-PR rather than filing it.** Default it
  could have gone the other way: leave it, it is pre-existing and unrelated to inline themes.
  Landed: it is the guard Fable asked for in the v1.99.3 release review, it has been flaky since
  the hour it shipped, and I was about to run the gates repeatedly against it — a gate that lies
  one run in three trains you to re-run until green. Cause: `CompanionHost` and
  `OutputfileAutoImport` write the shared profile's settings.json from a different xUnit
  collection, and collections run in parallel. Cost 2,350 tests and still 2 s, because the fix
  is a serial collection of four files rather than disabling parallelism (which was 2 s → 8 s).
- **Guarded it with a source scan, not just the four attributes.** Could have gone: add
  `[Collection]` and move on. Landed: the file's old comment *claimed* nothing else touched
  settings.json, and that claim is what let the flake exist — trap 34, a comment standing in for
  a guard. `SettingsFileCollectionTests` fails the build when a fifth writer appears.
- **Inline themes PR 1 ships `ThemeBodyMaxHeight = 320` with the screenshot unable to choose.**
  Bevel offered 280 or 320 and delegated the pick. Landed: 320, because `GearCardView` already
  uses it and a second nearby constant is two answers to one question — and the shot is recorded
  as NOT deciding it, since no Progress room is tall enough to reach either cap. PR 2's Loot and
  Drops rows are where the number is actually tested.
- **Extended `EQBUDDY_EXPAND` to name a room (`progress:raids`) instead of adding a variable.**
  Could have gone: a new `EQBUDDY_THEMETAB`. Landed: an inline theme has four bodies behind one
  key, and three of them were unreachable by a test or a screenshot — trap 22, a surface that
  cannot be reviewed reads as reviewed.
