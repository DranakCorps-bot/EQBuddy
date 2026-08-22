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

- **v1.99.4 review: the motes "stays off" promise is fixed by WORDING, not by a
  "player-touched" flag** · could have added a setting recording the player's own toggle ·
  one day of exposure and a one-toggle cost do not earn a setting; `FABLE-FEEDBACK.md`.
- **BOM/whitespace churn across fifteen files is next-loop hygiene, not a pre-tag block** ·
  could have held the tag for a normalisation commit · nothing breaks and a renormalisation
  is its own diff; `.gitattributes` is the executor's V1 call.
- **Avalonia theme bodies: option (a) — the `IWidgetCard` seam, every host builds its own
  instance; a control never moves between windows, as a trap with a source-scan guard** ·
  (b) make the move safe, or (c) a projection · the move is an open Avalonia bug since 11.2
  (#12753, #17906, #21267), still in 12.1.1; `FABLE.md` plan, Fable 5.
- **The Avalonia seam lands as its own PR (A) BEFORE the Avalonia inline card (B)** · could
  have shipped both in one · the seam is a refactor with no player-visible change and must pass
  every existing Progress render test unchanged; mixing it with the card hides which half broke.
- **On Avalonia the window renders only its visible tab, as WPF does** · could have rendered
  every tab every tick as the widget's paint block did · one rule on both lanes.
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
- **Reverted the Avalonia half of Inline themes PR 1 rather than shipping it half-working or
  forcing it.** Default it could have gone the other way: keep pushing (it was six fixes deep),
  or leave the failing test and file it. Landed: the blocker is that Avalonia's theme surfaces
  are shared field-backed instances with no `IWidgetCard`-style seam, which is a V2 refactor of
  a 5,593-line file — and CLAUDE.md says stop and stub when work turns out V2 mid-session, not
  finish it and label it. `main` now has inline themes on one widget and not the other, which
  is a parity gap that is REPORTED rather than quiet, and no What's-new claims it.
- **Did not write the `WhatsNew.json` entry for PR 1.** Could have gone: write it now so it is
  not forgotten. Landed: "the Progress card expands in place" is false on Linux and macOS
  today, and the rule is that entries are TRUE, not that they are early. It is recorded as owed
  in `HANDOFF.md`.
- **Took Helm's #228 ruling as WORK, and restored the Motes card from the mini-dashboard star
  rather than from `hadFile`.** Could have gone: show the card to every existing profile (the
  maximal reading of "restore"). Landed: the fold destroyed the real preference — it removes the
  key from `SectionOrder` AND `HiddenSections` — so the star is the only surviving evidence, and
  restoring what can be proven beats growing everyone's widget. It under-restores on purpose,
  and that limit is written into the code.
- **Did NOT write the What's-new entry, and asked David instead** (he chose "ask Helm, hold the
  entry"). The two rules collide: every player-visible change needs an entry, and Helm's hold
  says "we do not tell players motes are back". A shipped fix does not lift a hold, and reading
  the hold as thread-only would have been me lifting it. Note filed in `SCRIBE-FEEDBACK.md` for
  Helm; David is the courier. Version deliberately NOT bumped, so nothing can ship by accident.
- **`all cleared` rather than `0 left` for a finished raid ledger**, and an over-counted ledger
  says the same rather than `-2 left`. Bevel named the two states; the wording of the empty one
  was left to me. Landed: a zero is a number to read, and that state is an achievement.
- **Changed the Wealth CHIP only, not the Progress window's Wealth body.** Bevel's ruling was
  justified with "window Wealth is coin too", which is not true — the window's Wealth tab still
  draws Coin, Sold and Motes. Default it could have gone the other way: strip the body to match
  the justification. Landed: the Motes card ships hidden, so for most profiles that block is the
  only place mote rows appear, and removing a surface uninvited is exactly how #204/#210/#212
  happened. Handed back to Bevel with the screenshot.
- **Corrected my own framing of #228 rather than letting it stand.** I told David repeatedly that
  a fix was built and we were held back from telling the reporter. False: both reporters were
  told on 2026-08-21 and 1.99.0's notes announced it. I had read Helm's hold and Scribe's item
  and never opened the threads. The lesson is narrow and worth keeping: **an agent's hold text
  describes an intention, not the state of a thread** — check the thread before describing what
  a player has been told.
- **Staged 1.99.4 with a Windows-scoped entry for the inline Progress card**, rather than
  releasing it unannounced or reverting it off `main`. Default it could have gone the other way:
  no entry until Avalonia has it (what I said this morning). Landed: it is already on `main`, so
  any tag ships it, and a player-visible change with no note is the defect the What's-new rule
  exists to prevent. The entry names Windows explicitly and says why the other build lags, which
  is the opposite of quiet. **This is the one judgement call in the release for David to veto.**
- **Corrected the motes What's-new sentence and its code comment rather than building a flag**
  (Fable, v1.99.4 review). `HiddenSections` has no provenance, so the restore cannot tell a
  deliberate hide from the blanket one and DOES un-hide a starred player who re-hid the card.
  Default it could have gone the other way: add a "player touched it" flag. Landed: one day of
  exposure, one toggle to undo, and a setting that remembers a single day is a setting forever.
- **PATTERN, not a one-off: I write comments from the INTENT and not from the code.** Fable
  caught the same shape twice in one day — the `AppSettings.Load` "never saves" claim and the
  motes "stays off" claim. Both times the tests were right and the prose asserted a safety
  property the code lacks. Worth checking for deliberately in review.
- **Stripped the BOMs and rebuilt `WhatsNew.json` from the tag's own bytes.** The cause was my
  Python `encoding='utf-8-sig'` on WRITE (right for reading, wrong for writing), not a
  PowerShell `Set-Content` as the review guessed; 19 files, not 15, measured against `v1.99.3`
  rather than counted by eye. The file uses three-space array elements and `indent=2` emits six,
  which is what made a 13-line addition a 2,387-line diff. Now 13 added, 0 removed.
- **Did NOT add a `.gitattributes`.** Fable framed it as a one-time renormalisation and a V1
  call; a whole-repo line-ending commit in the middle of a staged release is the wrong moment.
  Logged so the next loop can take it deliberately.
- **Gave Helm its own inbox and feedback file (David asked), and MOVED the holds into it rather
  than leaving them in `SCRIBE.md`.** Default it could have gone the other way: create the two
  files and leave the holds where they are, which is the smaller change. Landed: the holds were
  wrong all three at once this morning precisely because their author and their list-maintainer
  were different, and two lists would be strictly worse than one — the one a session reads would
  be the stale one. `SCRIBE.md` now carries a pointer and an explicit "do not restore holds
  here"; Scribe and Bevel were both told why, and Scribe's `## Holds` / `Retired` conventions
  moved wholesale rather than being rewritten.
- **`HELM.md` is documented as STATE, not a work queue.** The other three inboxes are
  take-an-item-and-delete-it; a hold is never taken, it binds until Helm lifts it. Writing that
  distinction down is the point — treating a hold like a work item is how "a shipped fix lifts
  the hold" gets invented.

## 2026-08-22 — clearing the `waiting (David's call)` pile

David: *"only elevate to me for items appropriately needing my focus."* Six items sat on him;
one genuinely does. Each line below is a decision I made instead of a question I asked.

- **Motes are excluded from what the wiki pack SUGGESTS.** Could have gone: leave it for David
  as filed. Landed: it FOLLOWS eqlwiki (its own Mote Guide says motes are not creature-specific),
  and "departing from the wiki" is the thing on his list — matching it is not. Kept distinct
  from the admins' ruling that common drops stay IN: a cheap gem really did drop from that
  creature, a mote did not. `WikiContribution.SuggestableToWiki`, with the negative asserted.
- **"What's-new should cover skipped versions" was CLOSED, not decided** — it was already built
  before it was filed. `EntriesBetween` returns every entry between two versions and
  `WhatsNewTests` covers a multi-version hop. The item's "Already shipped" line was wrong, which
  is the rot Scribe's own SSC promises to sweep. Verified rather than assumed.
- **The pack's session-vs-history scope went to `FABLE.md` as a V2, not to David.** It reads as
  a scope call and is a design one: three open sub-questions the reporter named, and the data
  source moves from a live object to a query over archives.
- **Two UX items went to Bevel, not David** — the slow chip's icon (an overlay-space call) and
  Mobile "New at level" using the played class rather than the Quest Tracker filter (a
  which-surface-owns-this-state call, the #212 shape).
- **Two stay with David, and both belong to him:** the spawn-timer mega-thread (public posture
  under the project's name, consequence-list item 3) and the `/consider` park, which is his own
  decision — worth telling him only that #217 has since answered its destination question.

## 2026-08-22 — David's three calls, asked with the question tool

- **#228: star-only IS enough.** He ruled the lifting condition met. Recorded for Helm rather
  than acted on: Helm's condition named him, but the LIFT is still Helm's, and he answered the
  question rather than telling me to post. Nothing posted; he is carrying the note.
- **Spawn-timer mega-thread: he took none of the three options.** *"We should have a way for
  people to feed verified updates to EQLWiki."* So: no thread we host, and a new V2 in
  `FABLE.md` instead. The redirect is better than any option I offered — a thread we host is a
  second source of truth competing with the wiki, maintained by us forever.
- **`/consider`: unparked, wiki half only.** The spawn-chip half stays parked. The wiki half now
  has a reporter-confirmed, admin-backed destination; the chip half has neither.
