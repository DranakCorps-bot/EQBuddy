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
- **Column budgets: <the fixed widths this touches>** — for any plan that puts a new string
  into an existing surface. The Sky glance overflowed a fixed 150 px column and was found from
  a screenshot after it was built; measure before writing the string.
- **Guards run eight times** — a new test that guards a fix is not green until it has passed
  eight consecutive runs. `SettingsClobberTests` was flaky one run in three from the hour it
  shipped and would have passed any single review.
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.
- **Decided without asking:** the implementation calls the plan made that could have gone the
  other way, one line each — these go to `DECISIONS.md` when the item is taken.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`. Fable last-looks the
executed diff (H4) and answers in the same file; a defect found there is a V1 item for the
next loop, not a reopening of the plan.

---

## A way for players to feed VERIFIED spawn-timer updates to eqlwiki

- **Priority:** `ready` — David's own answer, 2026-08-22, asked with the question tool. He was
  offered "host a community mega-thread" / "decline and point at eqlwiki" / "defer" and took
  none of them: **"we should have a way for people to feed verified updates to EQLWiki."**
- **Class:** `V2`. Not V0–V1 because the whole difficulty is the word **verified**, and no
  answer from David settles it — the honesty bar is the design.
- **Source:** Scribe's spawn-timer mega-thread item (long-standing, community ask: catalogs lag
  and kill-to-kill does not determine a duration), redirected by David.

### What he rejected, and why it matters to the shape

A mega-thread we host would be a **second source of truth competing with eqlwiki** — the
one-fact-two-sources problem, in public, with us maintaining it forever. The standing rule is
that eqlwiki is the tie-breaker and a correction there helps every player and every other tool.
So the answer is not a place to collect timers; it is a **path from a player's own observations
into the wiki**, which is exactly the shape the loot contribution pack already has.

### The hard part, stated plainly

**Scribe's own argument against catalogs is also the argument against us suggesting timers:
kill-to-kill does not determine a respawn duration.** A gap between two kills is an upper
bound, and only sometimes that — the mob may have been up for an hour before anyone looked.
And CLAUDE.md is unambiguous: *"a wrong respawn timer is worse than none"*, and curated timers
are never auto-written.

So the plan's real question is: **what evidence justifies suggesting a respawn_time to the
wiki?** The loot pack already has an answer to the analogous question worth copying — the
10-kill bar, and "no label at all when the sample is thin". Candidate sub-questions:

1. How many corroborating cycles before a duration is suggestible, and must they agree within
   what tolerance? `SpawnOverride.Learned` already exists and already refuses some cases.
2. Does an observation from an INSTANCE ever count? (#109 says no for the timer; the wiki page
   may be about the open-world spawn.)
3. Triggered and raid-instanced entries have no cycle at all — they must never be suggested,
   and today's `IsTriggered` / `RaidInstanced` fences are the existing machinery for that.
4. Is this a new pack, or a section inside the existing contribution pack? `PageSkeleton`
   already emits `| respawn_time  = ` as an empty field, which is a strong hint.

### Already shipped (must not be fought)

The contribution pack and its honesty rules; `SuggestRarity`'s thin-sample refusal; the
triggered/raid-instanced suppression; `SpawnOverrides` and learned durations; the fact that the
pack never publishes anything itself — the player opens the edit link, reviews, and saves.

### Plan — Fable 5, 2026-08-22

**Shape: a RESPAWN section of the existing contribution pack, fed by a new per-named cycle
ledger, gated by an agreement bar that plays the role the 10-kill bar plays for rarity.** Not
a new surface, not a timer-submission tool — the pack pattern applied to a second kind of
world fact, which is what David's rule says to try first. `needs-david:` none: he asked for
the path; the honesty bar is design.

#### What I read, and what it changes

1. **The wiki's idiom is a free-text field per creature, and it is sparse.** `{{Namedmobpage}}`
   carries `| respawn_time = 9.5 min` (A frenzied ghoul), `Triggered` (the bees), and
   NOTHING on Eldrig the Old and Lockjaw — the field is absent, not empty. There is also a
   hand-kept `Respawn Timers` list page ("[[Lord Nagafen]] - 3 days with 12 hour variance",
   "Noble Dojorn - 5 days?") that the catalog's own notes cite. **The paste target is the
   creature's field**, in the wiki's own words ("22 min", "6 hours", "3 days with 12 hour
   variance"); the list page is a second place for one fact and is out of scope.
2. **We do not hold a SAMPLE today, only the tightest value.** `SpawnOverride.RespawnSeconds`
   is the minimum gap ever accepted, with `Learned`/`Sighted`/`Imported` flags — one number, no
   count, no spread. `SuggestRarity` can say "10+ kills" because `MobSummary.Kills` exists; the
   spawn side has no equivalent. The item is therefore mostly about recording evidence, and
   only then about suggesting.
3. **The engine already knows which observations are honest.** `LearnFromRekill` accepts a gap
   only when the named's own death started the clock, the player never left the zone
   (`NeverLeftSince`), the gap is ≥ 90 s, and the entry is not trusted/multi-spawn;
   `LearnFromSighting` only inside the final fifth; both refuse triggered and raid-instanced
   entries before learning; `_currentZoneInstanced && zone.RaidZone` gates instances. Those
   gates ARE the definition of a countable cycle. The ledger records exactly where they pass.
4. **The catalog already holds verified timers the wiki lacks.** Entries with `trusted: true`
   were MEASURED from family logs (Befallen's zone clock, Crushbone's). That is the cheapest
   first contribution and needs no player at all — a diff report, not a feature.
5. **`MobInfo` does not read `respawn_time`.** `EqlWikiMobs.Parse` reads name/zone/level/
   location/loot; the pack cannot currently say what the wiki thinks the timer is. One field.

#### Architecture

**Core — `SpawnCycleLedger`** (`spawn-cycles.json` beside `spawn-overrides.json`): per
`server|zone|name`, a list of `Cycle(DurationSeconds, Kind: Rekill|Sighting, At)`, capped at
the last 20. Written by `SpawnTimers` at the three places a gap is ACCEPTED today —
`LearnFromRekill`, `LearnFromSighting`, `LearnDiscovered` — and nowhere else, so every gate
above applies by construction (no instance, no triggered/raid, named's own kill, same stay,
floor and ceiling). Imports never write it (a stranger's number is not an observation).

**Core — `EqlWikiMobs.Parse` reads `respawn_time`** into `MobInfo.RespawnField` (raw text;
`""` when absent).

**UI.Shared — `RespawnSuggestion`** (pure, unit-tested — the whole honesty bar lives here):
- **Bar:** at least **3** cycles, all within **±15 % of their median**, median ≥ 90 s.
  Agreement is the evidence of attention: a player who left the camp produces scattered gaps,
  not three that agree. Below the bar: no suggestion, numbers travel in the edit summary only
  — the `SuggestRarity` rule verbatim.
- **Never** for `IsTriggered`, `RaidInstanced`, or `MultiSpawn` entries (no cycle / sibling
  noise), whatever the ledger holds.
- **Wording:** the wiki's own — minutes under an hour ("22 min"), hours ("6 hours"), days
  with variance only when the spread supports one. `SpawnDurationText` formats for us; this
  formats for the wiki, and a test pins both idioms apart.
- **Three-way compare:** wiki field (from `RespawnField`), catalog value, observed. Suggest a
  paste only when the wiki field is absent or disagrees with the observed median by more than
  the spread; when it agrees, say "wiki already says 22 min — nothing to add" (the KnownDrops
  line for timers); when it disagrees, phrase it as the stat block does — *compare, don't
  overwrite* — with the cycle list in the edit summary.

**UI.Shared — `WikiContribution.BuildExport` and `WikiPackPresentation`** gain a respawn
section per creature: `RowKind.RespawnObserved` with the paste block
`| respawn_time = 22 min` and the same edit link the loot section uses (served title, trap
3). The pack window needs no new control on either desktop: the row kinds are data and both
windows already draw whatever the presentation returns. **Bevel pre-design: yes** — the row's
note ("observed 22 min over 3 cycles; wiki says 25 min") is a new sentence on a shipped
surface. **Column budgets:** the pack rows wrap; none. **Shot offline:** seed the mob cache AND
a `spawn-cycles.json` in `wiki-pack`; prediction written first.

**Script — `scripts/harvests/eqlwiki/respawn-diff.py`** (flags only, like the rest of the
refresh): for every catalog entry with `trusted: true`, read the creature page's field and
report absent/disagreeing ones as paste-ready lines for a human. Curated catalogs stay
unwritten; this writes a REPORT. It is PR 0 because it needs no player evidence and it is
the "what we already know that the wiki does not" David was pointing at.

#### Risks

- **The bar is the product.** Too low and EQBuddy becomes the source of wrong timers on the
  shared reference — uniquely wrong, the thing the match-the-wiki rule exists to prevent. Too
  high and nothing is ever suggested. 3 / ±15 % is a starting point logged as a decision;
  the pack's edit summary carries the raw cycles so a wiki editor can judge.
- **Trap 4 (one fact, two sources):** the observed median is computed ONCE in
  `RespawnSuggestion` and the paste, the row note and the edit summary all read it.
- **Trap 20:** the ledger is a new file that only the engine writes and only the pack reads —
  add its reader in the same PR as its writer, or it is a written-never-read store.
- **Trap 13 shape:** `spawn-cycles.json` is written from the watcher thread; it is its own
  file with its own lock, as `SpawnOverrides` is. Never merge it into `spawn-overrides.json`.
- **Variance:** EQ timers have real variance ("3 days with 12 hour variance"). Three cycles
  cannot measure it; the plan suggests the median and puts the range in the summary, and
  never writes a variance clause. Say so in the row note when the spread is wide.
- **Long timers:** a 3-day boss needs nine days of one player camping to reach the bar. That
  is correct — under-suggest — and PR 0 is how those reach the wiki instead.

#### Decomposition

- **PR 0 — `respawn-diff.py`** + its report in the weekly refresh; no app change. One-off
  run now, human pastes what it finds.
- **PR 1 — Core:** `SpawnCycleLedger`, the three write points, `RespawnField` parse;
  `SpawnTimerTests` (a cycle is recorded exactly when a gap is learned and never otherwise:
  instance, triggered, placeholder-started, cross-stay, import — one negative each);
  `EqlWikiMobsTests` for the field. No UI.
- **PR 2 — UI.Shared + pack:** `RespawnSuggestion` + tests (bar, agreement, wording both
  idioms, three-way compare); the pack section and row kind; `WikiPackPresentationTests`;
  both pack windows unchanged or near it; staged shot; `docs/TestPlan.md` §3 rows; What's-new
  crediting the mega-thread reporters by name and number (from Scribe's item).
- **Someday:** a "wiki says / you observed" line on the Spawns window row itself; the
  Respawn Timers list page.

#### Verification

Unit as above; the bar's tests include a scattered-gap case that must NOT suggest and a
three-agreeing-cycles case that must. A real-world check David CAN do: camp any low-level
named three cycles (Crushbone's trainers are minutes) and read the pack. Reporter confirmation
via the mega-thread reporters after release.

#### Out of scope

Any store we host; the Respawn Timers list page; suggesting variance; a "since" filter on
cycles; anything on the phone; re-deriving timers from archived logs (the ledger starts from
this release; history is the other item's problem and a spawn cycle, unlike a drop, cannot be
pooled across characters without the stay evidence the archive does not hold).

#### Decided without asking (→ `DECISIONS.md`)

Creature field, not the list page; bar = 3 cycles within ±15 % of median; median suggested,
never variance; ledger capped at 20 cycles; PR 0 is a script not an app feature.

---

## The wiki pack reads one live session; it should read the history already on disk

- **Priority:** `ready` — no consequence-list decision in it. It was filed as "David's scope
  call" and sat there; on David's instruction (2026-08-22, *"only elevate to me for items
  appropriately needing my focus"*) I re-read it and it is not his — it is a design question
  with a plan-shaped answer, which is yours.
- **Class:** `V2`, and by your own test rather than by size: **no single answer from David
  finishes it as V1.** The reporter names three open questions that each change the
  architecture, and the data source moves from a live object to a query over stored archives.
- **Source:** #217 Frankthetankk, ask 2.

### The concrete miss, which is what makes it worth planning rather than shrugging at

**Three 4-kill sessions never cross the 10-kill rarity bar, despite twelve real kills.** The
pack's honesty rules are the reason: `SuggestRarity` refuses to label anything under 10 kills,
deliberately, so a thin sample cannot become a confident wiki edit. That rule is right and must
not be relaxed — the fix is to stop throwing away the evidence that would satisfy it.

The same thinning hits **money-per-kill ranges**, **faction-hit reporting** ("no hits observed
across N kills" is a claim about N) and **con-derived level ranges**, all of which widen with
observations and all of which currently restart from zero every session.

### What he asked that a plan has to answer

1. **Pool across the account's characters, or stay per-character?** Not obvious: drop rates are
   a property of the MOB and pool cleanly, but con-derived level ranges and faction hits are
   observations made BY a character.
2. **Any "since" filter?** Zones get retuned; a three-month-old drop rate may describe a mob
   that no longer exists in that form. A pack that silently averages across a retune is
   confidently wrong, which is the one thing this surface must not be.
3. **Per-session vs all-time toggle, or neither?** He explicitly does NOT want a toggle. Worth
   holding him to that or overruling it deliberately.

### Where it fits

This is the **all-time stats direction (#168 / #159)** — a query over archives already on disk,
which is exactly how David framed that: not new collection. So it may be the first real
consumer of that work rather than a detour from it, and the plan should say which.

### Already shipped (must not be fought)

Session-scoped export; `#74`'s archived-log review replaying one file at a time; the 10-kill
rarity bar and the "no label when the sample is thin" rule; the wrong-article split and the
motes exclusion that landed today.

### Plan — Fable 5, 2026-08-22

**Shape: a pure `MobHistory` pooler in Core over the snapshots `history.db` already stores,
feeding the SAME `BuildExport` the pack uses today; the pack reads history by default with no
toggle, and its scope line says exactly what it pooled.** The reporter's framing is right on
every point I could check. `needs-david:` none.

#### What I read, and what it changes

1. **The data is on disk, in full.** `SessionRepository` stores every session's complete
   `StatsSnapshot` as `SnapshotJson` (`history.db`, `Sessions` table), and `StatsSnapshot.Mobs`
   is the list of `MobSummary(Name, Kills, Loot[...], CoinMin/Max, Factions, LevelMin/Max,
   Zone)` — the exact record the pack consumes. No log replay, no new collection.
   `ProgressSeries` already probes one field across every row with a `JsonDocument` rather than
   materialising snapshots; that is the access pattern to copy.
2. **`BuildExport` takes `MobObservation(MobSummary, lookup)`.** So pooling produces
   synthetic `MobSummary`s and the export, its honesty rules, and the pack window's rows need
   no change — the 10-kill bar is then met by twelve kills across three sessions because the
   number it reads is twelve.
3. **Review replay writes no sessions** (`SessionStats.cs:197`, #74), so a player re-reading
   an archived log cannot double-count into the pool. Checkpoints of the LIVE session do land
   as a row, so the live session must be taken from the live snapshot and its row excluded.
4. **Session `Zone` on a `MobSummary` is the kill zone** — the #65 fix — so pooling keys on
   (name, zone), not name alone: "an ice giant" in two zones is two mobs.

#### The three questions, answered as decisions

1. **Pool across the account's characters: YES, and across servers too.** Drop tables, level
   ranges and faction hits are facts about the mob in the game, not about who observed them.
   The scope line names every character and server pooled, so nothing is silent. No toggle.
2. **"Since" filter: no filter, and the scope line shows the earliest date pooled.** A retune
   is not a date we hold. What protects against it is what already protects the pack: every
   number is presented for reconciliation, never as a correction, and the edit summary carries
   the per-session breakdown so an editor can see a rate that moved. If a retune ever produces
   a visibly bimodal rate, that is the moment to add a filter — not before.
3. **Per-session vs all-time toggle: none.** The reporter is right that a smaller sample never
   makes a better wiki edit. Drops by Creature keeps the LIVE view — "is this camp worth it" is
   a different job and stays session-scoped; only the pack pools.

#### Architecture

**Core — `MobHistory.Pool(IEnumerable<StatsSnapshot> sessions, StatsSnapshot? live)`** →
`IReadOnlyList<MobSummary>` keyed on (name, zone): kills summed; loot counts summed per
base item (`QuestCatalog.BaseItemName`, the existing fold) with `DropRatePct` recomputed from
the pooled counts and `LastAt` the latest; `CoinMin`/`CoinMax` the extremes across sessions
(−1 stays "never seen"); factions unioned with hit counts summed; `LevelMin`/`LevelMax` the
extremes of conned values (0 = never conned stays 0). Plus a `PoolScope(characters, servers,
sessionCount, earliest, latest)` record for the scope line. Pure; tested with a fixture of
several fake snapshots asserting the pooled counts — the reporter's own test description.

**Core — `SessionRepository.MobRows(server?, character?)`**: the `ProgressSeries` probe
applied to `Mobs`, so the pack opens without deserialising every snapshot's combat ledgers.
Default scope = every character, every server (decision 1).

**UI.Shared — `WikiPackPresentation.ScopeLine`** becomes the pooled form: *"12 kills of 4
creatures across 3 sessions · Dranak and Flossie on freeport · 2026-07-30 → today"*. One
sentence, and it is the sentence that makes decision 1 and 2 honest. **Bevel pre-design:
yes** — this line is the surface's whole claim about itself. **Column budgets:** none (the
line wraps). **Shot offline:** no for the wiki caches (seed them); the `wiki-pack` shot must
also seed `history.db` with two or three sessions so the pooled numbers are visible — the
`history-window` shot already stages sessions; reuse its staging and predict the totals first.

**Both pack windows** swap their data source from `_snapshot.Mobs` to `MobHistory.Pool(rows,
live)`, computed once on open and on the existing 3 s tick only if the live session's mob set
changed (the signature already exists). `EnsureMobLookup` fires for pooled creatures exactly
as it does for live ones — with the 2-in-flight cap, so a long history does not burst eqlwiki.

**The Drops tab is untouched**, and says so in its footer hint (the "moved" text already
points at the pack; add "the pack pools every session you have"). Mobile: no pack surface.

#### Risks

- **Trap 8 / perf:** the pool is recomputed on a signature, never per tick; the DB probe is
  the cheap one. A profile with hundreds of sessions is the case to measure — stage it.
- **Double counting:** the live session's checkpointed row AND the live snapshot — exclude
  the active row by id (`SessionArchiver` knows it). A test with a live session whose row is
  already checkpointed must pool its kills once.
- **Trap 4:** one pooled `MobSummary` feeds the row, the paste and the stat block; nothing
  re-sums.
- **The 10-kill bar was chosen for one session's evidence.** Pooled kills can reach it with
  kills spread over months; the rule stands (the reporter's argument is sound), but the edit
  summary must carry the per-session breakdown so an editor sees the spread.
- **Credit scope:** the log reference's timestamps now span sessions; use the dated form the
  export already has for multi-day sessions.
- **Old snapshots** deserialise `LevelMin`/`CoinMin` as unknown (the record says so); the
  pooler treats unknown as absent, never as zero.

#### Decomposition

- **PR 1 — Core:** `MobHistory.Pool` + `PoolScope` + fixture tests (sum, fold, extremes,
  unknown-stays-unknown, (name, zone) keying, live-row exclusion); `SessionRepository.MobRows`
  with a test over a temp DB. No UI.
- **PR 2 — pack:** both windows on the pooled source; `ScopeLine`; the Drops footer hint;
  `WikiPackPresentationTests`; staged shot with predicted totals; `docs/TestPlan.md`; What's-new
  crediting Frankthetankk (#217, ask 2).
- **Relationship to the all-time stats direction (#168 / #159):** `MobHistory` IS the first
  query of that kind over the archive, and the all-time view should consume it rather than
  write a second pooler. Say so in the class doc; do not build the view here.

#### Verification

Unit as above. The acceptance check a person can run: three short sessions on one camp (the
fixture log can be split), then open the pack and read "12 kills across 3 sessions" with a
rarity label that a single session could not have earned. Reporter confirmation on #217.

#### Out of scope

The all-time stats VIEW; pooling the Drops tab; a since/character toggle (decided above, and
revisited only on evidence); spawn cycles (the other item — a cycle needs stay evidence the
archive does not hold); changing the 10-kill bar.

#### Decided without asking (→ `DECISIONS.md`)

Pool across characters and servers with no toggle; no "since" filter; live view stays
session-scoped; (name, zone) keying.

---

## Avalonia theme bodies need a seam before a card and a window can both host them

- **Priority:** `ready` — no consequence-list decision in it. This is the blocker that stopped
  Inline themes PR 1 finishing, and it is written the day it was hit rather than carried.
  **Plan written by Fable 5, 2026-08-22 — see "Plan" below the stub. Short form: option (a),
  because (b) is an open upstream Avalonia bug and not ours to make safe.**
- **Class:** `V2`. Not V0–V1 by your own test: **no answer from David finishes it as V1.** The
  obvious fix (build a second instance for the card, as WPF does) is wrong for a reason only
  visible with the whole widget in view — the Avalonia surfaces are not objects, they are
  render code writing into ~40 MainWindow fields, so a "second instance" would be a second
  panel containing THE SAME field controls and would fail identically one level down.
- **Source:** Inline themes PR 1, executor, 2026-08-22. WPF half is on `main` (`a1157f2`).

### What happened, precisely

`WidgetRenderTests.ProgressCardFoldsTheAaLedgerBehindAToggle` fails with
**`System.ArgumentException: Attempt to call InvalidateArrange on wrong LayoutManager`**,
thrown from the test's own `Dispatcher.UIThread.RunJobs()`. 38 pass before the change, 37 pass
and 1 fails after. It is the crash your plan predicted in as many words — *"a control has one
visual parent; showing a body in the card and the window at once throws"* — arriving through a
route the `HandThemeBodyTo` funnel does not close.

The sequence is ordinary, not exotic: `WidgetRenderTests` sets `EQBUDDY_EXPAND=1`, the widget
expands every section (`MainWindow.cs:472`), the theme card therefore goes `Inline` and takes
`_progressTabBodies[Experience]`, and the test then opens the Progress window. **A player does
the same thing by expanding Progress and clicking ⧉.**

### What was tried, so the next attempt does not repeat it

Each of these was built and run, not reasoned about:

1. **The funnel itself** — card releases in `ThemeCardPanel.Render`, window releases through
   `IProgressHost.ProgressTabBody`. Correct and insufficient.
2. **`Presenter?.UpdateChild()` on release** — `ContentControl` detaches its old child on the
   next LAYOUT pass, not on assignment. Still failed.
3. **Posting the repaint at `Background` priority** (what the WPF twin does on `Expanded`).
   Still failed.
4. **Deferring `Sync()` off the pop-out path.** Still failed.
5. **Forcing a layout flush on release** — `ILayoutRoot` and `ILayoutRoot.LayoutManager` are
   `internal` in the Avalonia version we ship, so this route does not exist from our code.
6. **Isolation, to stop guessing:** reverting ONLY the `SectionLink` → `Section` swap makes the
   test pass with every other change in place. So the trigger is the card being expandable at
   all, not the strip, not the ⧉, and not the body composition — a run with the card given
   private throwaway bodies still failed.

### The shape of the fix, as far as the executor got

The WPF lane does not have this problem because its surfaces sit on `IWidgetCard` — "paint
yourself from this snapshot" — so each host builds its own instance and nothing is ever moved.
`ProgressWindow.xaml.cs` says so itself. The Avalonia lane never got that seam; `BuildProgressSection()`
and friends compose shared fields, and `RefreshExpandedSections` paints those fields directly.

So the question for the plan is which of these, and it is genuinely not the executor's call:

- **(a) Put the Avalonia Progress surfaces on the `IWidgetCard` seam** (a real refactor of a
  5,593-line file, and the one that makes the two lanes the same shape — and would pay for
  PR 2 and PR 3 as well as this one).
- **(b) Keep one instance and make the MOVE safe** with a public Avalonia API that actually
  exists in our version.
- **(c) Something else — e.g. the card hosts a projection rather than the surface.**

### Also in scope of whatever you decide

**MainWindow (WPF) GREW 4,424 → 4,504 in PR 1**, where the plan says each theme PR lowers the
baseline. Nothing moved out, because the Progress surfaces were already out. Headroom is now
**131 lines against ~80 a theme**, so PR 2 and PR 3 do not fit. The lift the plan assumed has
to be named: the most obvious candidate is the `EQBUDDY_EXPAND` dump block (~130 lines of pure
string-building that is a SUM, not a pixel, and therefore belongs in a file of its own).

### Already shipped (must not be fought)

The WPF inline card (`ThemeCardView`, `ProgressThemeCard`), `ThemeHost` and `InlineMode` from
PR 0, `WidgetMetrics.ThemeBodyMaxHeight`, `EQBUDDY_EXPAND=progress:raids`, and three committed
screenshots. **The Avalonia half was reverted rather than left half-built**, so `main` has one
UI with inline themes and one without — deliberate and reported, not drift.

### Checked

Read this session: `MainWindow.cs:472` (the expand loop), `:479` (the generic ExpandedChanged
subscription that is registered AFTER it, which is why the widget's own handler never saw the
initial expand and mine did), `BuildWealthSection`/`BuildProgressSection`, `RefreshExpandedSections`,
`ProgressWindow.Refresh`, `IProgressHost`, `SectionPanel`, and the Avalonia `DesignSystem`
strip/icon helpers.

### Plan — Fable 5, 2026-08-22

**Decision: (a). Put the Avalonia theme surfaces on the `IWidgetCard` seam, one theme at a
time, starting with Progress — and adopt the rule that on Avalonia a control NEVER moves
between two windows.** (b) is off the table for a reason you could not have seen from our
code, and (c) is (a) wearing a smaller hat.

#### What I read, and what it changes

1. **The exception is an open upstream bug, not a gap in the funnel.** Avalonia issues
   **#12753** (2023, "cross-window control reparenting should be supported" — kekekeks, still
   open), **#17906** (2025: a regression in 11.2.0, moving a UserControl between windows throws
   this exact message; fine in 11.1.5) and **#21267** (2026, Avalonia 12.0.x, same message in
   production). We ship 12.1.1. The mechanism in their source: `GetLayoutRoot()` and
   `GetLayoutManager()` both read `Visual.PresentationSource`, the field 11.2 introduced, and
   `LayoutManager.InvalidateArrange` throws when the control's presentation source is not the
   manager's owner. Your six attempts were all variations on making a cross-window move safe;
   **there is no sequence of public calls that does, and the people who own the layout engine
   have not found one in three years.** That is why #5 found the API internal: it is internal
   because re-parenting across roots is not a supported operation.
2. **The existing design is already on that path and working by accident.** `ShowProgressWindow`
   creates a NEW `ProgressWindow` each time the old one closes (`MainWindow.cs:3584`), and
   `IProgressHost.ProgressTabBody` hands the SAME `_progressTabBodies[...]` controls to the new
   window — a cross-window move on every reopen, today, on `main`, before Inline themes touched
   anything. No test closes and reopens the window, so nothing has ever checked it.
   **Hypothesis, labelled:** it survives because a closed window's presentation source is
   cleared on close, so the check passes by null; the inline card is the first time both roots
   are alive at once. Step 0 below tests the reopen path directly.
3. **Your own file already contains the right pattern twice.** `IProgressHost.ProgressMiniStars()`
   builds NEW star buttons per window and registers them; `BuildMotesSection(summary, list)`
   takes its controls as parameters "because two hosts draw motes now and a Control has one
   parent". The seam is that pattern applied to the whole theme instead of two corners of it.
4. **Your finding #6 ("throwaway bodies still failed") is the one thing this plan does not
   explain, and it is why Step 0 exists.** If a run with no shared body still threw, something
   else crossed a root — the reopen move above is the leading candidate (the test opens the
   window once, but `EQBUDDY_EXPAND=1` with an expandable card may have forced a second
   `Refresh`), and the stack trace will say in one line what we are both guessing at.
5. **Ratchet.** Avalonia `MainWindow.cs` is 5,593 against a 5,422 baseline (cap 5,964; 371 of
   headroom). The Progress rooms are ~17 fields, `BuildProgressSection` (~60 lines),
   `BuildWealthSection`/`BuildMoneySection`/`BuildMotesSection`, the paint block at ~2,300–2,380,
   `RenderRaids` (3,180–3,259, ~80 lines) and `ProgressMiniStars`. Lifting them is 250–350 lines
   out, which is the headroom PR 2 and PR 3 need on THIS lane and the lift the original plan
   assumed. On WPF the lift is the `EQBUDDY_EXPAND` dump block, as you proposed — see the
   amendment on the Inline themes item.

#### Architecture

**The seam (`EQBuddy.Avalonia/IWidgetCard.cs`, mirroring `EQBuddy/IWidgetCard.cs` name for
name):** `IWidgetCard { string Key; Control Body; void Render(StatsSnapshot) }`, `ICardContext`
with the same six members, and `ProgressSurfaceSet(Experience, Money, Motes, Faction, Raids)`.
`MainWindow.NewProgressSurfaces()` builds a fresh set on every call. **Nothing hands out a
`Control` it built earlier** — `IProgressHost.ProgressTabBody(tab)` is deleted, and its
replacement is `IProgressHost.NewProgressSurfaces()`.

**Four view classes** (`ProgressCardView`, `MoneyCardView` + `MotesCardView` composed as the
Wealth tab exactly as `ProgressWindow.xaml.cs:64–71` composes them on WPF, `FactionCardView`,
`RaidsCardView`), each owning the fields it paints today. `RaidsCardView` takes the ledger
accessor and `CopyAchievementsCmd` factory; `ProgressCardView` takes what WPF's does
(`settings`, the class source, the level accessor). The widget's `RefreshExpandedSections` loses
its Progress/Wealth/Faction branches and `RenderRaids` goes with its view.

**`ProgressWindow` builds its own set in its constructor and renders it on its tick** — reopen
is a new window with a new set; nothing moves. The mini stars stay as they are (already
per-window).

**`ThemeCardPanel`** (the twin of WPF's `ThemeCardView`, in its own file) builds ITS own set on
first expand through the same factory, and never releases anything because it never shares
anything. `HandThemeBodyTo` is deleted — a funnel for a move that must not happen is a place for
the next person to make it happen.

**The rule, as a trap (copy-ready for `CLAUDE.md`, number it when it lands):**

> **A control NEVER moves between two windows on Avalonia.** Re-parenting across `TopLevel`s
> throws `Attempt to call InvalidateArrange on wrong LayoutManager` — an open upstream bug since
> 11.2 (#12753, #17906, #21267), still present in 12.1.1, with no public API that makes it safe.
> The widget's theme bodies were handed to the Progress window by reference, which worked only
> because no test ever reopened the window; the inline card, the first host alive at the same
> time as the window, threw on the first run. Six attempts to sequence the hand-off failed
> because the operation is unsupported, not mis-sequenced.
> → **Every host builds its own instance through a factory** (`NewProgressSurfaces()` on both
> lanes), and no host interface returns a `Control` it did not just create. Guarded by
> `SurfaceOwnershipTests` (no `*TabBody(` accessor on any Avalonia host interface) and by the
> reopen/pop-out sequence test in `WidgetRenderTests`.

#### Risks and the traps they touch

- **Trap 20/26 (a fold loses a writer):** moving 17 fields out of `MainWindow` is exactly the
  event that drops a setting's last writer. List every control each view takes and every
  `_settings.X =` in the code that moves; `DeadSettingTests` will not see a writer that moved
  into a view that is never constructed — the card builds lazily, so the WINDOW must construct
  its set eagerly for those writers to exist.
- **Trap 15:** `ThemeCardPanel`'s body host gets no `IsVisible` of its own; `SectionPanel`
  owns expansion, `ThemeHost` owns placement.
- **Trap 36:** `ThemeCardPanel` carries the body cap WITH a scroller and the wheel pass-through
  (`PointerWheelChanged`, the Avalonia shape of `PassWheelUpWhenItCannotScroll`).
- **The mini stars** stay window-only; the card shows none (as WPF). `_stars` removal on close
  is already right.
- **`ProgressTabShowing` and the paint gates** (`RenderRaids` returns unless the tab shows) move
  into `RaidsCardView.Render` as "render only when hosted" — or the host calls `Render` only for
  the visible tab, which is what WPF's `ThemeCardView` does. Pick the latter; it is one rule.
- **Two instances of `MotesCardView`** when the widget's own Motes card is shown AND Progress is
  open — the precedent is already in the file (`_cardMotesSummary` vs `_motesSummary`), so this
  is the existing behaviour made explicit.
- **Ratchet:** lower the Avalonia baseline in the same commit as the lift.

#### Decomposition

- **Step 0 — diagnostic, time-boxed to one hour, BEFORE any refactor.** (i) Re-apply the
  reverted attempt from your local reflog or stash if you still have it; if not, skip to (ii).
  Attach `AppDomain.CurrentDomain.FirstChanceException` in the failing test and print the
  `ArgumentException`'s stack — which control, which caller (`ContentPresenter.UpdateChild`,
  `SetVisualParent`, a `Refresh`). (ii) **Write the reopen test on `main` as it is:** open the
  Progress window, close it, reopen it, `RunJobs()`; then the same with a tab change in between.
  Record in `FABLE-FEEDBACK.md` whether `main` throws TODAY. Either answer changes nothing about
  the plan — it tells us whether this was a latent crash players could already reach.
- **PR A — the seam, Progress only, no inline card.** `IWidgetCard`/`ICardContext`/
  `ProgressSurfaceSet` on Avalonia; the four views; `NewProgressSurfaces()`; `ProgressWindow`
  builds its own; `ProgressTabBody` deleted; the widget's Progress paint branches deleted;
  `SurfaceOwnershipTests`; the reopen test from Step 0 now green by construction; Avalonia
  baseline lowered. **Every existing `WidgetRenderTests` case for Progress must pass
  unchanged** — that is the "tabs draw what the cards drew" claim carried across the seam.
  Trap entry into `CLAUDE.md`. No player-visible change; no What's-new.
- **PR B — Inline themes PR 1, Avalonia half**, now a port of the WPF card: `ThemeCardPanel`
  mirroring `ThemeCardView` line for line where Avalonia allows; `EQBUDDY_EXPAND=progress:raids`
  honoured; the expand → pop-out → close → expand sequence test; `WidgetSheetTests` shot with
  the prediction written first. Lands with the What's-new line the WPF half already has, as one
  entry for both lanes.
- **PR 2 and PR 3 of Inline themes each begin with their lane's lift** — Loot/Gear/Inventory
  bodies (`_lootTabBodies`) and Kills/Drops (`_creatureTabBodies`) onto the seam first, card
  second. The plan's "each PR lowers the baseline" is true again because the lift is real on
  the lane that needs it.

#### Verification

- `SurfaceOwnershipTests` (source scan: no Avalonia host interface method returns `Control`
  from a field); `ThemeHostTests` unchanged; the reopen sequence and the card/window sequence
  in `WidgetRenderTests`, each with `RunJobs()` between steps — **and run the new guards eight
  times before calling them green** (your flaky-guard lesson from 1.99.3, now a rule in the
  item shape).
- Both lanes' `EQBUDDY_EXPAND` facts agree on names (`progressInline`, `progressTab`,
  `progressWindowOpen`) so one E2E-style assertion reads both.
- A person clicks it on Linux or macOS once before the tag: expand Progress, pop out, close,
  expand, change tab in the window, close, expand. That is the sequence no test could reach
  before today.

#### Out of scope

Fixing the upstream bug or filing on it (we are not blocked on them once nothing moves);
changing WPF's seam (it is the model); the Loot and Creature lifts (they are PR 2/3's first
commits, not this item's); anything about the inline card's LOOK (Bevel has ruled; PR B ports
the WPF result).

#### Decided without asking (→ `DECISIONS.md`)

Option (a) over (b)/(c); the never-move rule as a trap with a source-scan guard; PR A before
the Avalonia card rather than with it; the window renders only the visible tab (WPF's rule)
rather than every tab every tick.

---

## Inline themes — expand in place, pop out on request

- **Priority:** `ready` — David answered the one question that was his (2026-08-22, asked
  with the question tool): **build it as Bevel ruled it — expand in place, pop out on
  request; the widget stays the home; the theme windows stay for the second monitor.**
  Plan by Fable 5, 2026-08-22. One theme per PR, both UIs in each.
- **Class:** `V2` — four themes × two UIs, a host-ownership rule a compiler cannot see (a
  body has one visible parent), a retired breakout, and it reverses the *direction* of four
  folds that were themselves signed decisions. Not V1 because the host rule has to be right
  for all four before the first ships, and a wrong one is trap 15 on every card at once.
- **Source:** David, 2026-08-21 (*"expandable sub-categories under them with an option to
  pop out the window"*); #228 (daetien-lab: *"I simply want to track my mote drops in the
  main window"*; joeymavity: *"Motes are buried and seem to move around"*);
  `docs/proposals/InlineThemes.md`; Bevel's ruling 2026-08-21 (tab strip; split rule; host
  rule; pop-out collapses the card; collapsed by default; name pills by the old card titles;
  default tab is the room that moves while you play).
- **Bevel pre-design: YES, before PR 1's screenshots** — specifically the expanded card's
  height per theme at 100 % and 125 % scale, and the one-line bodies of the Glance tabs below.
  The shape is already ruled; this pass is about what it looks like on the thing that sits
  over the game.
- **Shot offline: no** for Kills & Drops (the Drops tab reads the wiki — seed every fixture
  creature's mob cache as `wiki-pack` does); **yes** for the other three.

### What I read, and what it changes

1. **The launchers are four `SectionLink` buttons whose KEYS are already the theme keys**
   (`kills`, `loot`, `quests`, `progress` — `MainWindow.xaml:457/512/534/603`, `SectionMap()`
   at `MainWindow.xaml.cs:649`; Avalonia `_sections[...]` at `MainWindow.cs:999–1060`). The
   cards keep their keys, so **there is no settings migration in this item** — no
   `HiddenSections`/`SectionOrder` fold, nothing for `AbsorbedTitles` to change. That is the
   single biggest reason this is tractable.
2. **The two UIs own theme bodies DIFFERENTLY, and the plan has to say so or the Avalonia
   lane ships a crash.** WPF: each window builds its own instances (`NewProgressSurfaces()`,
   `NewGearCard()`, `new LootCardView`, `new DropsCardView`, `QuestsWindow` its own) — two
   hosts, two instances, no conflict. Avalonia: the widget BUILDS the bodies once
   (`_progressTabBodies`, `_lootTabBodies`, `_creatureTabBodies`) and the window takes them
   through `IProgressHost.ProgressTabBody(tab)` and sets `_body.Content = …`
   (`ProgressWindow.cs:268`). A control has one visual parent; showing a body in the card
   and the window at once throws. The one-owner rule below is therefore not a UX nicety on
   Avalonia, it is the thing that keeps the app up.
3. **`SectionScroll.MaxHeight` already caps the whole card stack** (`WidgetMetrics.
   SectionMaxHeight`, E2E-pinned as `sectionMaxH`), so an expanded theme cannot run the
   widget off the screen — it scrolls inside the cap like any tall card. Per-theme height
   is a Bevel question, not an engineering one.
4. **`BreakoutKind.Progress` exists on WPF only** (`BreakoutWindow.xaml.cs:14`; Avalonia's
   enum has no `Progress`), opened from the `xp` star (`MainWindow.xaml.cs:3616`). Bevel's
   ruling folds it into the pop-out. `scripts/shoot.ps1`'s `mini-bar` shot enumerates
   `BreakoutKind` BY HAND (trap 30) and `OptionsCardsView.BuildBreakouts` enumerates it by
   reflection — both need a look when the member goes.
5. **The phone is the prototype and needs nothing.** `index.html`'s `.qtabs` card-with-tabs
   reads the same Core tab keys (`index.html:1158–1180`). Parity is already by shared module;
   this item brings the desktop TO the phone's shape, not the reverse.
6. **E2E already pins the launcher LINE** (`EndToEndTests.cs:91–97, 196–224, 519–522`:
   "the launcher should summarise the theme"). Keep the summary line verbatim as the
   collapsed header and those assertions keep passing; they become the regression guard
   that the glance survived the expander.
7. **`QuestChecklistView` is one control hosting Epic and Sky** (`MainWindow.xaml.cs` ctor,
   `_quests`); how `QuestsWindow` hosts it versus the General search is not re-read this
   session — **hypothesis:** the General tab is a search box plus a detail pane with its own
   `DetailScroll`, which is exactly Bevel's "do not shrink-wrap a full window" case.

### Architecture

**Core (recipe step 1 — name it where the phone and both desktops can read it).**
Each theme's surface definition (`ProgressSurface`, `LootSurface`, `CreatureSurface`,
`QuestSurface`) gains `InlineMode InlineModeFor(tab)` → `Full | Glance`. Initial table:

| Theme | Full inline | Glance inline (one line + ⧉ into the window) |
|---|---|---|
| Progress | Experience · Wealth · Faction · Raids | — |
| Kills & Drops | Kills · Drops | — |
| Gear & Loot | Loot · Wishlist | Inventory (a long list with its own filter bar) |
| Quests | Epic 1.0 · Plane of Sky | General (search + detail pane; Bevel's host rule) |

Bevel's pre-design pass may move a tab between columns; the table lives in Core so the
move is one line and both desktops follow. Default tab per theme, per Bevel: the room that
moves while you play — Experience, Kills, Loot, and for Quests whichever of Epic/Sky the
player's class has rows in (else Epic).

**UI.Shared — `ThemeHost` (framework-free, unit-tested; the "sum, not pixel" rule).**
A state machine per theme: `Collapsed | Inline | Window`, with inputs `ToggleCard`,
`PopOut`, `WindowClosed`, `OpenWindow(tab?)` (the ⚙/hotkey/`EQBUDDY_*` openers), and a
`SelectedTab` kept for the session only. Invariants it enforces, and the tests assert:
- **One owner.** `PopOut` from `Inline` → `Window` and the card collapses. `ToggleCard` while
  in `Window` does NOT draw a second copy: it brings the window forward (Avalonia cannot
  show the body twice; WPF could, and must not, for the trap-15 reason). `WindowClosed` →
  `Collapsed`, never back to `Inline` — the player closed a thing; do not re-grow the widget.
- **The tab follows the player.** Pop-out opens the window on the card's selected tab;
  closing the window hands the window's tab back to the card for the next expand.
- **A Glance tab never paints a body.** Its inline content is the tab's one-line summary
  and the ⧉; `Render` is not called on the full view.
Both windows already expose `OpenX(tab)`; the state machine tells them when.

**WPF — `ThemeCardView` (one class, four instances; `EQBuddy/ThemeCardView.cs`).**
Replaces each `<Button Style=SectionLink>` with `<Expander x:Name="…Section"
Style="Section">` + `<ContentControl x:Name="…Body"/>`, the `MotesSection` shape, so
`SectionMap()` is unchanged. Header: the existing `EqCardTitle` + the existing summary
`TextBlock` (trap 12: the summary stays the star column and keeps trimming) + a
`DesignSystem.InlineIconButton("External", …)` ⧉ that sets `e.Handled = true` — the
Progress fold's lesson: a button nested in the expander header bubbles to the toggle.
Body: `EqSegmentedStrip` fed by the theme's existing `Tabs(...)` (so badges and labels are
the window's, and the strip WRAPS — trap 25) over a `ContentControl` holding the selected
tab's body. Bodies: **the card builds its own instances** exactly as the windows do
(`NewProgressSurfaces()`, `NewGearCard()`, `new LootCardView`, `new DropsCardView`,
`InventoryView`), built lazily on first expand; the `IWidgetCard` rule (a collapsed card
renders nothing) keeps a never-opened theme at zero cost. `ThemeCardView` takes
`ICardContext` + the factories, never `MainWindow` — the seam exists for this.

**Avalonia — `ThemeCardPanel` (`EQBuddy.Avalonia/ThemeCardPanel.cs`, beside
`SectionLinkPanel`).** A `SectionPanel` expander whose content is the strip + a body host.
Bodies are the widget's single instances; the hand-off is explicit: the host that loses
ownership sets its `Content = null` BEFORE the other sets it, in one method on the main
window (`HandThemeBodyTo(theme, Host.Card|Host.Window)`) that `ThemeHost`'s transitions
call. No other code path may assign a theme body — grep for `TabBody(` assignments and
route every one through it, or the second place is where the crash lives.

**Progress breakout.** `BreakoutKind.Progress` retires into the pop-out: the `xp` star while
minimized opens the Progress WINDOW on Experience (Bevel's "fold it into the theme's
pop-out"). `DisabledBreakouts` entries for `"Progress"` are ignored, not migrated — nothing
is lost, the window has its own position memory. Trap 30: remove `Progress` from the
`mini-bar` list in `shoot.ps1` in the same commit. Trap 20/26: list every control the
breakout carried and say where each went (its xp line → Experience tab, already there).

**Tour and Options (recipe step 7).** The tutorial page "Cards that open windows" is now
wrong; rewrite it to "Cards that expand — and can pop out". `OverlaySections.AbsorbedNote`
stays as is (the absorbed titles are still inside). Options → Cards & windows row tooltips
for the four keys say "expands in place; ⧉ opens its window".

**Mobile:** no change. `CompanionProjection` already emits the Core tab keys; if the
`InlineMode` table is added to Core it is NOT put on the wire (the phone has no windows).

### Risks and the traps they touch

- **Trap 15 (two switches for one state):** `ThemeHost` is the only switch. Neither the
  expander's `IsExpanded` nor the window's `IsVisible` may be consulted as truth; both are
  outputs. Assert it: a test that fires `WindowClosed` twice and `ToggleCard` during
  `Window` and checks no state goes to `Inline`.
- **Avalonia one-parent:** covered above; the `HandThemeBodyTo` funnel is the guard, and a
  `WidgetRenderTests` case must expand a theme, pop it out, close the window and expand it
  again without an exception — the sequence that throws if any assignment bypasses the funnel.
- **Trap 12 (timer-driven size on X11):** a body that grows on a CLOCK inside an expanded
  card changes measured size every tick. The Kills list grows per kill (player-driven, fine);
  Experience's xp line ticks every second — it was a card before the fold and is the same
  control, so this is existing behaviour, not new. Do not add anything clock-driven to the
  header.
- **Trap 14/25:** the tab strip wraps; the header is a two-column grid. Both already true in
  the windows; copy, do not hand-build (`CLAUDE.md`: never hand-build another pill).
- **Trap 16:** the ⧉ is an `InlineIconButton`, hit area `IconInlineHit`.
- **Trap 36/37:** the lifted bodies have no scrollers and some had pinned chrome in their
  windows (Quests' search box, Drops' orientation footer). Full-mode tabs get the body only;
  anything that was pinned window chrome is why a tab is Glance.
- **Trap 21:** `docs/screenshots/` already has `section-progress.png`, `progress-card.png`,
  `widget-expanded.png` embedded in docs. The new shots are `theme-inline-progress`,
  `theme-inline-kills`, `theme-inline-loot`, `theme-inline-quests`; `widget-expanded`
  (`EQBUDDY_EXPAND=1`) will now photograph four expanded themes — **predict it and re-shoot
  it deliberately**, it is in the README.
- **Trap 22/23:** `EQBUDDY_EXPAND=progress` must open the card INLINE (not the window) after
  this change; the E2E opener for the window stays `EQBUDDY_PROGRESS`. Write the prediction
  for each shot before running it.
- **Ratchet:** both MainWindows are near their caps (WPF 4,418 / 4,635; Avalonia 5,591 /
  5,964). The four launcher builds and their openers MOVE into `ThemeCardView` /
  `ThemeCardPanel`; each PR lowers the baseline in `ArchitectureTests` and
  `docs/Architecture.md`'s table in the same commit (`DocumentationTests` checks the table).
- **Trap 1:** the ⧉ and the strip sit under the UI-scale transform; nothing here does
  screen-pixel arithmetic, but shoot at 125 % once.
- **E2E launcher-line assertions** keep passing only if the collapsed header carries the
  exact summary the launcher did. If a PR changes that line, it is changing the glance, and
  #219 says a changed glance is a lost feature until proven otherwise.

### Decomposition (one theme per PR; both UIs in every PR; each leaves `main` shippable)

- **PR 0 — Core + UI.Shared, no UI.** `InlineMode` on the four surfaces with the table above;
  `ThemeHost` with `ThemeHostTests` covering every transition and both invariants; a
  `SurfaceParityTests` case that the phone's tab keys still equal Core's. `docs/TestPlan.md`
  §3 rows.
- **PR 1 — Progress, both UIs.** `ThemeCardView` / `ThemeCardPanel` born here. The breakout
  retirement and the `mini-bar` list. E2E: pin BEFORE the move — `progressInline=0/1`,
  `progressTab`, `progressTabs`, `progressWindowOpen` into `EQBUDDY_EXPAND`; assert expand →
  pop-out → close → card collapsed. `WidgetRenderTests` twin. Tutorial page. Baselines
  lowered. `WhatsNew.json` entry crediting daetien-lab and joeymavity (#228) and David's ask.
  **Bevel's pre-design pass lands between PR 0 and PR 1's screenshots.**
- **PR 2 — Kills & Drops and Gear & Loot.** Same class, two more instances; Inventory is
  the first Glance tab, so this PR proves the Glance body. Drops inline fetches the wiki —
  seed the fixture caches.
- **PR 3 — Quests.** The Glance General tab and the `QuestChecklistView` hosting question
  (item 7 above — verify before building; if the checklist cannot be hosted twice on WPF,
  build a second instance as the windows do). `EQBUDDY_EXPAND=quests` inline shot.
- **Each PR:** `FABLE-FEEDBACK.md` note; `DECISIONS.md` lines for anything the executor
  decided against this plan; Fable last-looks the diff before the release that carries it.

### Amendment — Fable 5, 2026-08-22, after PR 1 (WPF)

**The plan assumed a lift it had already banked.** "Each PR lowers the baseline" was written
for a MainWindow that still held the Progress surfaces; the Progress fold had already moved
them, so PR 1 had nothing to move and the WPF file grew 4,424 → 4,504 (131 of headroom against
~80 a theme). The executor's read is right. Corrected sequencing:

- **WPF:** PR 2's first commit lifts the `EQBUDDY_EXPAND` dump block (~130 lines of pure
  string-building — a sum, not a pixel) into `WidgetDump.cs`, a class taking `MainWindow` and
  reading `internal` accessors. It must NOT be a partial (the ratchet glob sums partials).
  Lower the baseline in the same commit. That buys PR 2 and PR 3.
- **Avalonia:** the seam item above IS the lift — Progress first (PR A), then Loot and Creature
  as the first commit of PR 2 and PR 3 respectively.
- **The Avalonia half of PR 1 is PR B of the seam item**, not a re-run of PR 1.

Also carried from PR 1: the window calls `ThemeHost.SelectTab` on its own tab change
(`ProgressWindow.TabChanged`, both lanes) — done on WPF, to be mirrored in PR B.

### Verification

- Unit: `ThemeHostTests` (every transition; one-owner; Glance never renders), `InlineMode`
  table test, `SurfaceParityTests`; Avalonia `WidgetRenderTests` expand/pop/close/expand.
- E2E (WPF has no unit tests): the facts above, asserted from `tests/EQBuddy.E2E`, written
  against the launcher BEFORE PR 1 changes it.
- Screenshots, predicted first: each `theme-inline-*` at 100 % and 125 % and once in
  Solarized; `widget-expanded` re-shot and the README checked. Prove the binary (trap 18).
- **The one check David can do himself, and it is the #228 job:** expand Progress on the
  widget over the game, read motes and xp without opening anything, pop it out, close the
  window, and see the card stay collapsed. Then the same with Kills & Drops mid-fight.
- Reporter confirmation on #228 after release — tell daetien-lab and joeymavity exactly
  what to click, and that Motes is also still its own card (#227) if they prefer that.

### Out of scope

The phone (it is the prototype); the World, Alerts and Live Meters themes (not built yet —
they will be born as expand-in-place cards when they land, which is the point of `ThemeHost`
being shared); retiring any theme window; shipping any theme expanded by default; the Motes
card (#227, separate); keyboard reach for the card stack (none of the cards have it);
per-theme height caps beyond `SectionScroll` (Bevel may ask for one; it is a follow-up).

### Already shipped (must not be fought)

The four theme windows and their tab strips (`EqSegmentedStrip` everywhere — never
hand-build another); every theme body on the `IWidgetCard` seam; `SectionScroll.MaxHeight`
and its E2E pin; `BreakoutKind` gating via `DisabledBreakouts` for the kinds that stay;
`AbsorbedTitles`/`AbsorbedNote` (#219); the Motes card's return (#227); the launcher summary
lines and the E2E assertions on them; the phone's card-with-tabs.

### Checked

Read this session: `docs/proposals/InlineThemes.md` in full; Bevel's ruling; `docs/Themes.md`
in full; `MainWindow.xaml:450–625` and `SectionMap()`; `MainWindow.xaml.cs` card seam,
`NewProgressSurfaces`, `NewGearCard`, the window openers; Avalonia `MainWindow.cs:990–1075`,
`AppTheme.cs` `SectionCard`/`SectionLinkPanel`, `IProgressHost.ProgressTabBody` and
`ProgressWindow.cs:255–275`; all four theme windows' strip/body wiring in both UIs;
`BreakoutKind` in both; `WidgetMetrics.SectionMaxHeight`; `EndToEndTests` launcher
assertions; `index.html` `.qtabs`; `shoot.ps1` shot table and `docs/screenshots/`;
`ArchitectureTests` baselines and current line counts. **Hypotheses, labelled:** item 7
(how `QuestsWindow` hosts `QuestChecklistView` and the General search) — verify in PR 3.

### Decided without asking (already in `DECISIONS.md`)

Ships collapsed, all four; one owner with "expand while the window is open brings the
window forward"; closing the window never re-expands the card; selected tab is
session-only; Progress breakout retires into the pop-out (Bevel's call, ratified); Glance
for Quests/General and Gear & Loot/Inventory; Progress goes first.

---

*No other items.*
