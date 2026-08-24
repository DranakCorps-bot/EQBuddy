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

## 2026-08-24

- **#234 fixed by UNCAPPING the two session-history rollups rather than by carrying a named
  flag through Core** · could have plumbed `KillEvent.ProperName` into `NameCount`/`MobSummary`
  so nameds are always kept regardless of rank · that changes a persisted snapshot schema AND
  the mobile wire for a surface that is a scrollable desktop review pane, where one row per
  creature killed is simply not a lot; uncapping fixes the report with no schema change and no
  heuristic false-positives (`HistoryPresentation`, `GukNamedsRollupTests`).
- **Remaining caps in the session detail now print "... and N more" instead of being raised**
  · could have uncapped loot/pet/damage lists too · a cap that admits itself is honest and
  bounded; uncapping everything makes the text arbitrarily long for no reported complaint.
- **Bumped to 1.99.10 and staged; no reply posted to #234** · could have replied to the
  reporter now the fix is in · Helm's 6:22 AM ruling says explicitly "do not post another
  reply (Claude is in the thread)", and a shipped fix does not lift a Helm instruction.

- **Ran the hand drag/reopen check unattended as an automated harness** · could have waited
  for David at the machine · David authorized it in session while away; `scripts/drag-verify.ps1`
  uses Win32 rects and WindowFromPoint-guarded clicks so nothing could land on his live widget
  (Fable, `498d740`-follow-up).
- **Reverted the window-height fix and shipped 1.99.9 P0-only** · could have fixed forward
  under release pressure · the split was pre-agreed in the review ("if any fail, we split"),
  the redesign needs a probe, and an angry public data-loss thread outranks a self-found clip
  (Fable, `git revert 054d009`; plan re-filed in `FABLE.md`).

- **Auto-empty now only touches files with the exact shape the game writes** · could have kept
  the `eqlog_*.txt` glob and relied on the archive folder as the safety net · **David's call,
  asked with the question tool** — logged here because it is the reasoning, not the decision:
  the discriminator is the character set, not the segment count, since a real server short name
  can contain an underscore (`Core/GameWrittenLog`, `ea2e27d`).
- **Bumped to 1.99.9 and staged rather than asking to ship first** · could have asked David for
  the go before writing the What's-new · the release review goes to Fable first, per the
  standing order (`FABLE-FEEDBACK.md`, `Directory.Build.props`).
- **The window-height fix ships with a What's-new entry even though Fable may yet split it out**
  · could have left the entry until Fable answered · a missing entry is the worse failure and
  the entry comes out with the commit if it splits (`WhatsNew.json`, `054d009`).
- **Re-shot every Progress-window screenshot, not just the one that reported the bug** · could
  have re-shot `progress-card` alone as the item specified · found `raids-import` clipped by
  41px hiding its `⧉ copy` button, which nothing else would have caught (`054d009`).
- **Credited StrIIker-TV by name with no discussion number** · could have opened a GitHub
  discussion to generate one · the report came in on Reddit and David chose "draft it for you,
  you post" over also filing an issue; flagged to Fable for a ruling (`WhatsNew.json`).

## 2026-08-23

- **Class inference (Fable 5): classes are a LIST with a SOURCE — the achievements dump
  first, inference second (every qualifying class within 0.25 of the leader, at most three,
  cited to the wiki's "trio builds"), picks as a lens that widens and never narrows;
  `LeadMargin` deleted** · keep a single inferred class and raise the margin; let picks
  override the dump · the dump is the game's own statement and has been parsed for two
  releases without being used; `FABLE.md` plan.
- **Spells by class, amended after PR 0 (Fable 5): the promote keys on PAGE TITLE, never the
  `spellname` field; "section exists → extras drop" and "no section → derive and flag" are
  two rules, not one** · keep `spellname` as the key · it is a copy-paste artefact that has
  been dropping real spells from the shipped ding list; `FABLE.md` amendment.
- **Spells by class (Fable 5): one catalog with `source` per row, not two; the class page's
  spelling wins and the page title is kept for the link; derived rows are marked dim, never
  hidden; the first promote run (~500-row diff) is human-reviewed before the harvest joins
  the weekly cadence; unlock groups collapse beyond the first class, session-only** · a
  second catalog; hide derived rows; auto-run the promote · trap 4, David's "flagged not
  filtered", and a quarter of a catalog is a review not a cron tick; `FABLE.md` plan.
- **v1.99.6 re-review (Fable 5): the fifth Island 6 bee (`Bizazzzt`) is a pre-tag catalog
  row, not a follow-up** · ship the four and file the fifth · it is discovered-and-learned on
  two kills, the exact defect the release claims to fix; `FABLE-FEEDBACK.md`.
- **VETOED nothing — but recorded because David decided it in session:** when eqlwiki's class
  page and its spell pages disagree about a spell's level, **the class page wins and spell
  pages fill gaps only where the class page has no section**, with anything derived flagged as
  such · class-page-only, or keeping the spell-page harvest and patching the gaps · found from
  Druid 34: class page 5, our catalog 10, missing `Healing Water` and padded with five ports.
  `FABLE.md` stub, 2026-08-23.
- **Bzzazzt is catalogued with eqlwiki's 12-hour clock, NOT as triggered — against what the
  reporter asked for** · mark both new bees triggered as #109 requested · the wiki is the
  tie-breaker and its reason holds independently (a chain's opener cannot itself be
  triggered). His evidence is all from personal instances, which never respawn, so the two
  accounts describe different places rather than disagreeing. `SpawnCatalog.json`,
  `SkyBeeChainTests`.
- **The load-time self-heal now clears learned overrides on multiSpawn entries too** · leave
  it to triggered/raid-instanced only · the learner already refuses multi-spawn names, so a
  `Learned` value on one is a number the current code cannot produce. Bounded cost, named in
  the comment; typed durations untouched, with a negative test.
- **Multi-island Sky steps: default to one row under "Several islands"** · default to
  repeating them under every island · David asked for a toggle rather than a fixed answer,
  and the conservative side matches his wording ("a specific island"). `AppSettings.SkyStepsUnderEveryIsland`.
- **The island toggle lives on the Sky TAB, not in Options** · Options, per the standing rule
  · it is a tab-scoped view lens, and the Epic tab's "Classic-doable only" set that precedent
  in the same control row.
- **`QuestChecklistGroup.Done`/`Total` count DISTINCT steps** · count rows · repeating a step
  under three islands would otherwise turn a 4-step reward into a 6-step one, silently, only
  for players who opted in.
- **The "Isle N:" label is stripped from a row already sitting under that island's heading** ·
  leave the prose alone · the grouping created the redundancy ("Island 6" / "· Isle 6: Bazzt
  Zzzt"), so removing it is part of the same change. Multi-island rows keep every word.

## 2026-08-22

- **PR A: `IProgressHost` and `ProgressWindow` become `internal`** · make `ProgressSurfaceSet`
  public instead · the seam types are implementation, and widening the assembly's public API
  to satisfy an accessibility rule is the tail wagging the dog. Tests already have
  `InternalsVisibleTo`.
- **The Progress window's surface set is built EAGERLY in its constructor, not per tab** ·
  lazily, as the widget does for Gear · two of the five views are the only writers of
  `ShowNextUnlocks` and `ShowAllAAs`, and a writer that only exists once a tab is visited is
  trap 20 waiting to happen (Fable's plan flagged this; it was right).
- **`ProgressWindow.RenderVisible(snapshot)` takes the tick as a parameter** · fetch
  `CurrentSnapshot()` internally · the widget's headless render path hands one in, and
  keeping that possible is what lets `WidgetRenderTests` go on asserting that the tabs draw
  what the cards drew.
- **`SurfaceOwnershipTests` exempts the two lanes that still hand out bodies, by name** ·
  scope the guard to Progress and say nothing about the others · an exemption nobody can see
  is a blind spot; each row names the PR that removes it.
- **The negative test asserts `InvalidOperationException` (visual-parent guard), not the
  `LayoutManager` message from the production crash** · reproduce the exact upstream
  sequence · the simple repro hits a different mechanism reaching the same conclusion, and
  claiming it proved the other one would have been false. The doc comment says which is
  which.
- **The rare-`/consider` fact needs ONE con, not the pack's ten-kill bar** · reuse
  `SuggestRarity`'s threshold for consistency · the evidence is categorically different: the
  game printed the word, so there is no sample to be thin, and a bar would be statistics
  applied to something that was never a measurement. `WikiContribution.RareSpawnNote`.
- **Both con numbers are always printed ("2 of your 7 /considers"), never just "rare"** ·
  print the fact alone · same-named spawns are not all rare, and the person pasting onto
  someone else's wiki is the one who should weigh a 2-of-7.
- **The rare fact is said ONCE, in the contribution block, and NOT in the observed stat
  block** · repeat it there, where the other /consider-derived facts live · the stat block is
  kill-gated and heads itself "thin sample, for your notes rather than the wiki yet" — which
  would put a paste-it instruction and a don't-paste-it-yet caveat on one fact three lines
  apart. Found by reading the real paste block, not from the diff.
- **A rare-conned creature whose loot the wiki already has still earns NO pack section** ·
  give it one · that needs a new `RowKind` on the pack surface, which is a product decision
  about what the surface shows. Asked of Bevel in `BEVEL-FEEDBACK.md` rather than decided.
- **v1.99.6 review: the report's Sky half living on the Raids surface is a Bevel follow-up,
  not a pre-tag block; the three-sentence line ships as written** · hold the tag for a second
  host on the Quest Tracker's Sky tab, or shorten the line to a tooltip now · the rule "the
  report lives where the command is asked for" is already applied; whether a second host helps
  the job is Bevel's, post-hoc, as the 1.99.1 caption was; `FABLE-FEEDBACK.md`, Fable 5.
- **The achievements auto-import report goes on the RAIDS surface, and nowhere else** · the
  Quest Tracker (the other thing the dump feeds), or both · the rule already set by the
  inventory report: the report lives on the surface that ASKS the player to run the command.
  Both UIs' own doc comments already said "read by the Raids surface"; this makes it true.
  Not a design invention, so not a Bevel question. `RaidsCardView.cs`, `MainWindow.cs`.
- **The report sits ABOVE the boss rows, not after them** · below, matching where the
  inventory report sits on Gear · the second screenshot showed it behind a scrollbar under
  21 rows (now trap 44). A notification is read on arrival; a card footer is not.
- **1.99.6 is its own release rather than riding the next feature** · fold it into whatever
  ships next · the What's-new rule — a player-noticeable fix earns the release that ships it,
  and this one has a reporter (#101, Frankthetankk) waiting on the answer.
- **The agent run cadence (Scribe 6am · Bevel 1pm · Helm 8pm) goes in `CLAUDE.md`, beside the
  "inboxes inform you" boundary, not in `HANDOFF.md` or an agent's own section** · a handoff
  note, or three lines one per agent · it changes what you do at the START of every session
  (pull first; Helm rules last), which is what `CLAUDE.md` is for; commit this one.
- **A Bevel item that turns out to be already shipped is marked DONE in place, not deleted**
  · the take-then-delete contract says delete · the wrong-article item's body is mostly
  *do-not* rulings ("two failures must not look alike", Copy stays off), and deleting the
  item would take the standing constraints with it; `BEVEL.md`, verified against
  `DropsCardView.cs` in both UIs.
- **Spawn timers → eqlwiki: the paste target is the creature's `respawn_time` field, not the
  `Respawn Timers` list page; the bar is 3 cycles within ±15 % of the median; the median is
  suggested and variance never is; the ledger keeps the last 20 cycles; PR 0 is a flags-only
  script diffing `trusted` catalog timers against the wiki** · any of those could have gone
  another way · `FABLE.md` plan, Fable 5.
- **Pack history: pool across characters AND servers with no toggle; no "since" filter; the
  live Drops tab stays session-scoped; pooling keys on (name, zone)** · per-character, a since
  filter, or a toggle · the reporter's argument that a smaller sample never makes a better
  edit, and facts about a mob are not about who saw it; `FABLE.md` plan, Fable 5.
- **Dead helpers `IsExcluded`/`IsTimeableNamed`: delete, do not wire; build `DeadHelperTests`
  as V1** · wire them to the pet registry · the suffix rule covers what the log prints, and a
  promise with no caller is worse than none; `FABLE-FEEDBACK.md`.
- **v1.99.5 review: the pet purge must spare `Custom` entries and manual timers — pre-tag**
  · ship as is, it only touches "… pet" names · the file's own principle says a discovery is
  discarded without touching the player's additions; `FABLE-FEEDBACK.md`.
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
- **Deferred `DeadHelperTests` rather than building it in the release loop** (Fable's V1, my
  call on timing). Could have gone: build it now while the shape is fresh. Landed: it is a
  whole-assembly scan whose value lives in a curated `Known` list with a reason per entry — the
  `DeadSettingTests` pattern — and doing that badly under a release is how a guard becomes a
  green tick nobody trusts. Logged so it is a dated decision rather than a thing that quietly
  did not happen.
- **Deleted `IsExcluded`/`IsTimeableNamed` rather than wiring them** (Fable's ruling). The
  suffix rule covers every possessive pet the log prints and `Killer == "You"` closes the
  players case. A promise with no caller is worse than no promise.

## 2026-08-23 — the two features on 1.99.6: next-level spells by class, and motes/hr

- **Motes/hr went to David with the question tool, and he chose the Experience room.** The
  question passed both tests: the Progress WINDOW and the phone already carry that line inside
  their Wealth tab's Motes body, so the only surface missing it was the widget's inline Wealth
  room — which is coin-only by a Helm-signed ruling. My recommendation was the inline Wealth
  body (semantic home, and all three surfaces would then agree); I named the cost of the
  Experience room in the option, which is that the Progress window now states the mote rate in
  two places, an inch apart, on two different tabs. **He chose Experience knowing that.** So
  the line lives in `ProgressPresentation.SummaryLines` and reaches the widget, the window and
  the phone's Experience tab. The Wealth chip is untouched; the window/phone Wealth Motes rows
  are untouched (#227 is still its own item).
- **One formatter, not a fourth.** The app already had three mote-rate strings. The Progress
  line reuses the Motes card's own header via a new `MotesPresentation.RateLine`, and
  `ProgressTheme.MoteRate` now delegates to it. Could have gone: write the Progress line
  inline, which is two lines shorter. Two formatters for one rate is how the Wealth chip and
  the Wealth body came to disagree in the first place.
- **The line is OMITTED, not zeroed, when nothing has dropped.** The same block already omits
  the AA line and the ETA. "0 motes/hr" reads as a measurement of a camp rather than "none
  yet", which is the wording argument `MotesPresentation.Summary` was written to win.
- **No class in play now HIDES the next-level fold** (Bevel's rule, Helm-signed). This removes
  a behaviour: a classless character used to get a preview built from the class-agnostic AA
  categories. It could have gone the other way — those rows are true for everyone — and the
  reason it did not is that `LevelUnlocks.Next` walks forward to the next level with ANY row,
  so the surface offered David "At level 39: 1 new AA ability" about a pet ability five levels
  away, for a character with no pet. Called out in `WhatsNew.json` rather than left to be
  discovered, and asserted in `WidgetRenderTests` so a later refactor cannot restore it quietly.
- **Which group opens is "the first with something to show", not "index 0"** — a decision I
  made, not one Bevel wrote. Its rule says *"first inferred class open"*. A Warrior whose next
  milestone is an Archetype AA produces an empty Warrior group above the shared bucket holding
  the only row, so open-by-index would have shown "nothing new at 15" over a collapsed heading
  with the single row two clicks away. Found from a written prediction BEFORE the screenshot,
  and it is visible in `docs/screenshots/theme-inline-progress.png`. Sent to Bevel as a
  narrowing of its rule rather than assumed to be what it meant.
- **The empty group has no chevron.** Bevel said "keep the class row"; whether it is an
  expander was mine. A fold that opens nothing is an affordance that lies, which is trap 16
  with the switch the other way.
- **The per-class open/shut state is a FIELD on the view, never a setting** (Bevel's rule,
  followed). Worth logging because the neighbouring folds — `ShowNextUnlocks`, `ShowAllAAs` —
  are both settings, so the inconsistency is deliberate rather than an oversight.
- **Fixed a trap-8 violation I was standing next to.** The mobile Progress fingerprint keyed on
  `Wealth.MotesSummary`, which is the RATE — the one value in that record that moves on the
  clock while nothing happens, and also the only thing standing in for "a mote dropped". It now
  keys on the mote tiers. Could have gone: leave it, since it predates this work. It is three
  lines from the block whose comment says exactly why not to do it.
- **The E2E "no class hides the preview" assertion was deleted, not made to pass.** The harness
  always writes the shifted fixture log and that log infers WARRIOR, so the no-class state is
  unreachable there — the test would have been about a state the harness cannot produce. Moved
  to `WidgetRenderTests`, where the class list is a parameter, with a comment in the E2E file
  saying why it is not there. Writing a second class-free fixture for one assertion is a worse
  trade than one test on the other lane.
- **The new screenshot shoots the INLINE card, not the Progress window.** The window restores
  to a height whose body scrolls after ~3 lines, so `progress-card.png` has been photographing
  a panel cut off ABOVE the two lists it is named for — pre-existing, visible in the committed
  file, and filed separately rather than fixed here.

## 2026-08-23 (night) — actioning Fable's third pass

- **Took the blocker at face value only after checking it.** Fable said `ClassName` serialises as
  `className` while the page reads `g.class`, and said plainly it had not run the serialiser.
  Verified all three legs before touching anything — `JsonOpts` is CamelCase,
  `CompanionBuffGroup` is declared `Class`, the page reads `g.class` at five sites — because a
  bot's claim about source is a place to look. It was right in every detail.
- **Renamed the property rather than changing the page.** Both would work and the page side is
  new code too (so trap 32 does not bite either way). The record now matches its siblings, which
  is the property that stops the next group record inventing a third spelling.
- **The guard asserts the emitted JSON, not the record.** `Assert.Contains("\"class\":")` alone
  passes on the broken payload, because `className` contains `class` — so the load-bearing line
  is `Assert.DoesNotContain("className", json)`. Verified by running it on the pre-fix tree: 2 of
  3 fail there.
- **Built `WriteMobileProgressSnapshot` rather than fixing the hand-written snapshot.** The
  cheaper move was to correct the key in my hand-typed JSON and re-run. That would have left the
  next phone change verifiable only by a payload a human typed, which is exactly what hid this
  one. The fixture goes through the real catalogs, `LevelUnlocks` and the real projection, and
  asserts the shape it must carry so it cannot quietly stop carrying it (trap 22).
- **The re-shot `progress-next-classes` drops the level-up append and seeds the ledger LEVEL
  instead.** Fable's fix was "tall enough, or scrolled". Seeding the level is better than either:
  the preview only needs a level to be KNOWN rather than announced, so the six-row ding list goes
  away, all three class groups fit with no scrollbar, and the shot is about one feature instead
  of two. Prediction rewritten before the run and matched line for line.
- **Did NOT hold the release for the Progress window's ~203px Experience tab**, per Fable's
  answer to question 3: it is pre-existing, its body scrolls, and the previous three releases
  carried it. Filed with the Bevel 320-cap question beside it.
- **Carried Fable's forward note into the class-inference V3 stub** rather than leaving it in a
  feedback file: 1.99.6's What's-new tells a player to tick classes in the Quest Tracker, which
  that plan makes wrong rather than merely dated. The stub now says the shipping release owes a
  line saying so.

## 2026-08-23 (night) — #233, and the rule it produced

- **#233 went to David with the question tool, and he chose the guarantee.** It passed both
  tests: the theme fold is his roadmap direction (consequence-list item 5) and a reply would be
  read as a promise (item 3). His answer: keep the roadmap, add the "what moved" commitment, and
  say WHY — *"organizing after rapid initial build out of feature requests… the new homes make
  more logical sense and are intuitive for new users though of course the long term users will
  feel the changes."*
- **Treated as a pattern, not a voice.** #219, #227/#228 and now #233 are one complaint arriving
  three times from three people. That is what moved it from "answer the thread" to "change a
  rule", and it is why the reply concedes it out loud rather than explaining the fold again.
- **The rule is about the ORIGIN, not the destination.** Every one of those releases had a
  truthful What's-new entry describing where a surface had ARRIVED. None named where it had
  LEFT, which is useless to the only person who needs it — someone looking for something. The
  rule is the form "X is now Y", both halves.
- **Built before replying, not promised.** The 1.99.6 What's-new carries the whole current map
  and the promise; `CLAUDE.md` carries the standing rule. Could have gone: post the reply and
  add the rule later. A promise made in a thread and not written into the file every session
  reads first is a promise that lasts one session.
- **NOT posted — routed to Helm.** `HELM.md`'s process line is "new-thread thank-you still comes
  to Helm", and this is a new thread. David has settled the direction, so Helm is being asked
  only about posture and timing; the full draft is in `HELM-FEEDBACK.md` so one carry is enough.
- **#109 gets no reply yet, deliberately.** Its last comment is Frankthetankk's verbatim
  evidence, which is exactly what 1.99.6's bee work was built from — so the honest reply is the
  release itself, and replying before the tag would mean either claiming it shipped (false) or
  saying "soon". The thread is answered by shipping, and the What's-new credits him by name.
- **#233's REPLY is David's; #233's RULE is ours, and they separated cleanly.** He read the draft
  and took the thread himself. The Helm sign-off request is withdrawn in place rather than
  deleted — a live-looking ask that nobody needs answered is the exact shape that made three
  holds describe states that had stopped being true. The two durable outcomes (the "X is now Y"
  rule in `CLAUDE.md`, the WHERE THINGS MOVED map in 1.99.6) shipped and are unaffected by who
  writes the reply.
- **`status.ps1` will keep flagging #233 as awaiting a reply, and that is correct.** Written into
  the handoff so a later session does not read the flag as an unfinished job and post over him.

## 2026-08-23 (afternoon) — the three queued items

- **Progress-window clipping: measured, then FILED rather than fixed.** `AllowResize` releases
  the height on `ContentRendered`, which for a replay-filled body is a frame with nothing in it
  — proven by running the same shot with the pin skipped (203px → 389px), with
  `progress-wealth` as the control at 741px. The FIX is not a V1 call: `AllowResize` wants
  "size to content" and "let the user drag the edge", WPF will not do both, and resolving it
  decides chrome for four windows. Four candidate fixes are in the stub with the cost of each.
  David asked for all three items; this is the one I did not finish, and it is deliberate.
- **PR 1's real number is -98 rows, not -498.** The plan predicted removing ~500; it assumed
  every class page carries every level and PR 0 found none do (all stop at 50 against a cap of
  60). So 362 rows return as DERIVED. Worth logging because the plan's headline number is the
  one a reviewer would check against.
- **`era` is parsed but NOT shipped.** Fixing PR 0's row regex made it come through cleanly —
  and it is "Classic" on all 1,504 rows. One value discriminates nothing, and a harvest field
  no surface reads is trap 43's mirror.
- **`PageTitle` deferred with a reason, not forgotten.** The plan lists it; links work without
  it because the wiki resolves redirects itself, so it buys nothing until something needs the
  served title.
- **The spell hover is ONE LINE because of a rule I nearly tripped over.** Both widgets switch
  a tooltip to monospace when it contains a newline — right for the stat blocks that rule
  exists for, wrong for wiki prose, and invisible to every test and screenshot.
- **1.99.7 exists because 1.99.6 had already shipped.** The first draft of these notes went
  into 1.99.6's block, which would have claimed things the released build does not have.
- **#120's four tests were re-expressed, not relaxed.** They asserted `""` for two comparable
  classes and documented it as a virtue. The protection that actually mattered (a one-off line
  never names a class) lives in the FLOORS and is untouched; what was deleted is a margin that
  could not tell a three-class character from an ambiguous log.
- **`MemberFraction` stays at 0.25 even though it drops a class after two idle half-lives.**
  That is what separates an alt-swap (blocks) from a multi-class character (rotation inside a
  fight), and the dump — which outranks inference — is the answer for anyone it gets wrong.
- **`ClassSourceWritersTests` joins the settings.json collection despite writing nothing.** It
  names `OutputfileAutoImport.cs` as a path string and the flake guard reads that as a call.
  Serialising four file reads is cheaper than teaching that guard to tell a path from a call,
  and a guard with a convenience exception carved into it stops being a guard.

## 2026-08-23 (afternoon) — the V3 presentation half

- **What looked like a labelling job was hiding two functional collapses.** Both Quest windows
  were still reading `CurrentSnapshot().InferredClass` directly — one class, bypassing
  `CharacterClasses.Resolve` — in `BuildClassStrip` and in the filter. The window that most
  needs the multi-class answer was the last place still collapsing it. Renaming a label is what
  took me into the file; the collapse is what I found there.
- **`ClassSourceFor` went ON `IQuestsHost` rather than being reached for.** A seam that window
  must go through cannot drift back to the snapshot's single class.
- **The old `InferredClass` stays on the wire for a release** and the page falls back to it.
  Trap 32: an open phone runs the page it downloaded weeks ago, so removing the field it reads
  would blank the line on every device that has not reloaded.
- **The new wire keys were pinned the same day they were written.** `characterClasses` and
  `classSourceLabel` are in `CompanionWireKeyTests` — the last field added to this wire reached
  the page under the wrong name and the manual check could not see it because the payload was
  hand-typed.
- **Bevel has NOT ruled on this wording** and Fable's plan asked for a pre-design pass. I built
  it as a like-for-like replacement of an existing string rather than a new surface — "(inferred)"
  said one of three things and said nothing when the GAME had told us. Bevel's next run should
  see it; flagged rather than presented as settled.

## 2026-08-23 — self-review pass over 1.99.7

- **The phone fixture could never have tested the thing it was for.** `WriteMobileQuestsSnapshot`
  sets picked classes, and the page suppresses the class-source line whenever picks exist — so
  the state the line lives in was unreachable from the fixture. A second snapshot now covers
  no-picks-plus-a-dump. This is the same shape as the wire-key defect: a check that runs and
  cannot fail. Found by asking what the fixture would show rather than that it passed.
- **Kept `.claude/launch.json`** (a new tracked file in a directory that had none) because
  file:// access to the harness was refused mid-session and serving it over HTTP is what made
  the browser verification possible at all. Small repo-shape call, logged rather than silent.
- **Collapsed the companion quest request to one snapshot and one resolution per tick.** It was
  three `CurrentSnapshot()` calls and two `Resolve()` passes per field-set, each taking the
  ledger lock twice and copying two lists, every second a phone is paired. Nothing was WRONG;
  it is the steady-state allocation perf audit #1 exists to remove.
- **One Avalonia gate run reported 1 failed / 279 total and never reproduced** (seven runs
  since, all 278/278 green). Name unrecoverable — `check.ps1` keeps no log. Ruled out a
  data-driven count (every theory in that project is static `InlineData`), which points at a
  transient host crash rather than a logic flake. **Disclosed to Fable with the reasoning
  labelled as a hypothesis, and the decision of whether to chase it before the tag handed to
  the reviewer rather than taken by me.**
  → Worth considering: `check.ps1` discarding test output is what made this unrecoverable. A
  gate that fails without leaving a name behind costs exactly one incident like this.
