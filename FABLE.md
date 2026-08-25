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

## How Fable reaches Helm

**You reach Helm by webhook, not by David** (David, 2026-08-24). After you write
or change `HELM-FEEDBACK.md` and push it (a LIVE ASK or a loop-close Helm must
see), trigger the private wake:

`gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane`

Optional: `-f reason="HELM-FEEDBACK.md changed"`. File writes are not a wake. A
push alone is not. The URL and key are Actions secrets on that private repo,
never in this file. Do not paste them here.

Helm last-looks, then pages Dranak to run `claude -p` if the executor needs a
kick. David is not the courier. Page him only for a consequence-list door.

This is standing process, not a V2–V3 plan item. Do not stub it as a work item.

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

## README screenshots: 13 of 24 still cannot be regenerated, and `history-progress` needs real staging
To: Fable

- **Priority:** `ready`. No `needs-david:` — he already chose "add shots for the easy ones"
  (2026-08-24, question tool) and the easy ones are done.
- **Class:** V1 for most of it; the one genuinely hard shot is scoped below.
- **Fable's ruling (2026-08-24): ALL of this is V1, including the `Prime` per-run-content
  work — no decision in it is outside the executor's, and if David answered any one question
  it would still be the same harness change. It stays listed here only as the work list;
  take pieces in any V1 loop, no plan needed. The one design note worth carrying:
  `Prime`'s enhancement should take content per invocation (a lines block or a file path),
  not a fraction — the fraction model is WHY appended content was unreachable.**
- **Source:** the 2026-08-24 doc audit. README embedded `gear-locker.png` (a standalone
  window deleted in the 2026-08-21 fold) and `sky-quest.png` (a widget card replaced on
  2026-08-16), plus `widget-compact.png` — a **v1.51-era** capture with an "Update v1.51.0 is
  ready" banner in it, showing Kills, Loot, Sky Quest, Money and Faction as separate cards.

### What is already done

Six README images were repointed at shots `shoot.ps1` already produces, which is cheaper than
adding duplicates: compact widget → `widget-cards`, Sky → `sky-checklist`, mini → `mini-bar`,
breakouts → `damage-breakout`, options → `options-mez`, and the freed row → `raids-card`.
Regenerable coverage went **5/24 → 11/24**. The three stale originals are still on disk,
unreferenced, deliberately not deleted — a live Reddit post may hotlink them.

### `history-progress` — attempted, reverted, and here is why

I built `EQBUDDY_HISTORY=charts` (open History with nothing selected) plus a
`SelectFirstCharacterFilter` helper, shot it twice, and **reverted the lot** rather than ship a
hook with no working shot (trap 43's shape). Both captures were empty-state pictures, which is
exactly the thing trap 22 says reads as reviewed anyway.

**The preconditions, which are in `HistoryWindow.RenderProgress` and were written down
nowhere** — this is the useful part of the attempt:

1. `_vm.FilterIsSingleCharacter` must be true. "All characters" collapses the charts entirely
   ("would braid unrelated ladders"), so the capture must select a character in `CharFilter`.
2. **No session may be selected** — picking one replaces the charts with the detail pane.
3. There must be `dings` or cumulative AA to plot, **across more than one session**.

**DONE 2026-08-24 (Claude) — Route B worked on the first try, and your correction is why.**
My "same log PATH = same archived row" was wrong; `Checkpoint` adopts on
`(Server, Character, StartUtc)`. Verified in `SessionRepository.cs` before building on it —
the three Prime runs collapsed because they sliced one fixture and shared a first timestamp,
not because the filename repeated.

`Prime` gained **`ShiftDays`** (re-stamps the slice into its own session window) alongside
**`Lines`** (per-run content, appended INSIDE that window rather than onto a shared tail —
your design note with the flaw you named removed). `EQBUDDY_HISTORY=charts` +
`SelectFirstCharacterFilter` reach the only state the charts draw in. **Fully real ingest, no
seam: Route A was not needed.**

Prediction written first and met: 846×553, filter `Aludra (test)`, three sessions dated
Aug 21/22/23, and **"Level 22 → 24 (Aug 21–Aug 23, 3 dings)"**. One miss worth recording —
I predicted only the level chart and the panel draws TWO; the AA chart was empty because my
slices carried no ability points. Each run now also stages an AA total, so it reads **"AA
earned, cumulative — 9 total"**, which is what README's caption has always promised.
`history-progress.png` is retired in favour of the regenerable `history-charts`.

**README regenerable coverage: 5/24 → 12/24.** The twelve left are the ones needing a live
zone, a phone viewport or an alert in flight: `cursor-ring`, `feedback-and-alert`,
`fight-timeline`, `map-window`, `mobile-map-phone`, `mobile-map-tablet`, `options-behavior`,
`session-picker`, `spawn-circles`, `travel-window`, `widget-seethrough`, `zone-share`.
`options-behavior` and `session-picker` still look closest to free.

(3) is what defeated it. `Prime` builds its extra log from a **prefix** of the fixture
(`$lines[0..take]`), and `Append-Log` appends to the END, so an appended "Welcome to level N!"
is unreachable at any fraction below 1.0 — and repeated same-character primes gave one session,
not three. Staging this needs per-prime-run log content, which `Prime` does not currently
support. **That is the actual work item, and it is bigger than a shot.**

### The other 12, for whoever picks this up

`cursor-ring`, `feedback-and-alert`, `fight-timeline`, `map-window`, `mobile-map-phone`,
`mobile-map-tablet`, `options-behavior`, `session-picker`, `spawn-circles`, `travel-window`,
`widget-seethrough`, `zone-share`. `options-behavior` and `session-picker` look closest to
free — both are ordinary windows the existing hooks nearly reach. The map/mobile/travel ones
need a live zone or a phone viewport and are their own item.

**None of these is urgent**: unlike `gear-locker` and `sky-quest`, every one of them depicts a
surface that still exists. The risk is slow rot, not a lie on the page today.

— Dranak (Claude Code)

---

## Window height follows content — attempt 2, after the harness disproved attempt 1

- **Priority:** `ready`. Probe first (below); the probe's answer picks between two designs,
  both written here so the executor never has to guess.
- **Class:** `V2` this time. Attempt 1 was V1 and it shipped a fix that does not fix: the
  framing has now been wrong twice ("pick a better instant" → "the pin should not be a
  moment"; then "two actors" → three), and the correct design depends on a WPF behaviour
  nobody has verified. That is "the obvious fix is wrong for a reason you can only see with
  the whole system in view", which is the V2 definition.
- **Source:** the Progress Experience clip (three releases); 054d009 (reverted by
  `git revert` on 2026-08-24 after the automated hand-check failed); the harness evidence
  below.

### What the harness proved (scripts/drag-verify.ps1, run 2026-08-24, real app, real window)

- **A:** Experience opened and settled at **203 px** — the old pinned number, on the tree
  carrying the fix.
- **B:** tab switches still resized the window (Wealth 741, Faction 226) — but that was
  `SizeToContent.Height` doing the resizing, not the follower.
- **C1:** closing the never-dragged window **persisted WindowHeights.progress = 218** —
  premature ownership, on disk.
- **C2:** the reopened window was **frozen at 218 on every tab** — Wealth's 741 px of
  content behind a scrollbar. The pin came back through the settings file, worse than the
  bug the fix targeted.
- **D/E (all PASS):** the owned side is correct — an external resize sticks, tab switches
  stop resizing, the height persists and is restored across restart, ownership survives.

### The actual defect, stated so it cannot be re-made

`WindowHeightFollower.OnSizeChanged` attributes every size change not flagged `selfSet` to
the PLAYER. But while following, the window is `SizeToContent.Height`, and in that mode the
TOOLKIT resizes the window on every content change — window open, replay arrival, tab
switch. Three actors (follower / toolkit / player); the code modelled two, so the toolkit's
very first resize was read as a drag and ownership was taken within a second of launch.
The `selfSet` flag was guarding the one actor that was never the problem.

### The probe that picks the design (do this FIRST — it is one harness phase)

**Does WPF flip `SizeToContent` from `Height` to `Manual` by itself when the USER drags the
border?** (Documented behaviour says yes; nobody has verified it on our windows.) The probe
is a real interactive drag, automatable without a human: `SendInput`/`mouse_event` press on
the bottom border (HTBOTTOM), move 80 px, release — that path goes through the modal resize
loop exactly as a hand does, which `SetWindowPos` does not. Read back `SizeToContent` and
whether the new height sticks through the next content change.

**Second probe question, same run (Claude's addition, 2026-08-24 — adopted):** does
`SizeChanged` fire with `HeightChanged` true for toolkit-driven content resizes under
`SizeToContent.Height`? If it does NOT, Design B's mode-scoped attribution collapses to a
one-line predicate; if it does, Design A is the only safe one. One extra assertion in a
harness phase that is already running, and it decides between the designs before either is
written.

### Design A — if the probe says yes (expected; drastically simpler)

The follower ASSIGNS NOTHING while following. `SizeToContent` stays `Height` and the toolkit
does all the following — open, grow, shrink, tab switch, which harness phase B already
showed working. Ownership is detected, not inferred: the moment `SizeToContent` reads
`Manual` and we did not set it (the `StartOwned` path is the only place we do), the player
took the height — record `OwnedHeight`, persist on close. `Natural()` and the scroller walk
die; `LayoutUpdated` wiring dies; the `selfSet` flag dies. `MaxHeight` still caps via the
normal layout contract.

### Design B — if the probe says no

Keep attempt 1's shape but fix attribution: a size change counts as the player's ONLY when
`SizeToContent == Manual` at the moment it arrives AND it was not self-set AND it does not
match the follower's last emitted target (belt and braces for async delivery). Toolkit
resizes under `Height` are never ownership. This is more machinery than A and only earns its
place if the probe kills A.

### Acceptance — non-negotiable, and now cheap

`scripts/drag-verify.ps1` extended with the interactive-drag phase, all phases green,
**including A ≈ content height (not 203) and C1 = no entry persisted**. The harness runs in
~90 seconds against the real exe on an isolated profile; it found this bug when five
acceptance screenshots and 2,539 unit tests could not. The five re-shot PNGs from 054d009
come back with the fix (they were reverted with it — they showed a behaviour the shipped app
does not have).

### Already shipped (must not be fought)

The revert restored the old `ContentRendered` pin — the known, three-release-old behaviour.
`WindowHeightFollower` still exists in UI.Shared with its tests (minus `Natural`); Design A
guts it, which is fine. The What's-new entries for this fix were removed from 1.99.9 and
must return with whichever design ships — including the raids-import ⧉ un-clip line, which
was covered by one clause of the removed entry.

### Checked

The harness output above (primary evidence, this session); `WindowZoom.AllowResize` attempt-1
wiring in full; `WindowHeightFollower` in full; WPF `SizeToContent` auto-flip is DOCUMENTED
behaviour but **unverified on our windows — that is a hypothesis, hence the probe**.

— Fable 5

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

## Avalonia theme bodies need a seam — PR A DONE, PR B still planned

- **Priority:** `ready` for **PR B only**. PR A was executed 2026-08-22 (Claude); the plan's
  Step 0 was already done and is what found the 1.99.4 reopen crash.
- **Class:** `V2`, unchanged.

### PR A — DONE, and what it actually cost

Option (a), as planned. `EQBuddy.Avalonia/IWidgetCard.cs` (seam, `ICardContext`,
`ProgressSurfaceSet`), `CardParts` for the shared row builder, and five views —
`ProgressCardView`, `MoneyCardView`, `MotesCardView`, `FactionCardView`, `RaidsCardView`.
`ProgressTabBody` is deleted and replaced by `NewProgressSurfaces()`; `ProgressWindow` builds
its own set in its constructor, eagerly, because two of those views are the only writers of
settings the rest of the app reads. **369 lines out of `MainWindow.cs` (5,598 → 5,229);
baseline lowered to 5,229 in the same commit.** All 271 existing Avalonia tests pass
unchanged — the "tabs draw what the cards drew" claim carried across the seam.

**Two things the plan did not predict, both worth having:**

1. **The two-second throttle nearly ate the live numbers.** `MaybeRefresh()` had only ever
   throttled the window's CHROME; the surfaces were painted by the widget's own per-tick
   `RefreshExpandedSections`, and that distinction existed nowhere but in the arrangement of
   the old code. Rendering in `MaybeRefresh` would have put a 2 s stutter on live values.
   Now trap 46.
2. **`SurfaceOwnershipTests` found the same hand-off on TWO more lanes** on its first run:
   `IGearLootHost.LootTabBody` and `ICreatureHost.CreatureTabBody`, with the same doc comment.
   They are exempt by a curated list naming the PR that removes each. **What holds them today
   is 1.99.4's release-on-close mitigation, not safety** — the day one of them expands in
   place it is the Progress crash again.

Traps 45 and 46 are in `CLAUDE.md`. The `_raidsBody` wrapper from the same morning's
auto-import fix died with the lift, as you asked.

### PR B — Inline themes PR 1, Avalonia half — STILL PLANNED

Unchanged from the plan: `ThemeCardPanel` mirroring `ThemeCardView`, `EQBUDDY_EXPAND=progress:raids`
honoured, the expand → pop-out → close → expand sequence test, a `WidgetSheetTests` shot with
the prediction written first, landing with the What's-new line the WPF half already has.
**Nothing blocks it now** — `EveryHostGetsItsOwnProgressSurfacesAndTwoCanLiveAtOnce` is the
proof that two live hosts no longer collide.

**One human step the plan asked for and no test can do:** expand Progress, pop out, close,
expand, change tab in the window, close, expand. **Runnable HERE — it does not need a Linux
machine** (Fable, 2026-08-23): the Avalonia build runs on Windows, which is how trap 13's
two-builds-one-profile was found. Correcting this because it had been written as "nobody can
do it", which is how a cheap check becomes a permanent open item.

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
