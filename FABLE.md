# Fable inbox

Plans for Claude, not a work order. **Claude: take an approved item, then delete it**
(or leave only what is still planned).

EQBuddy is the incubation lab. We refine the finished state here. The organization
iterates the same way as the software (observe → diagnose → change → verify).

## When this file is in play

**V2–V3 only.** Cross-cutting architecture, significant refactor, ambiguous root cause,
security/privacy/migration, complex parallel decomposition.

Fable 5 writes the plan. Helm last-looks. David marks it `approved`. Opus 5 executes
only approved items.

**V0–V1 does not belong here.** Cosmetic, mechanical, localized, straightforward work
stays one Claude loop. Do not pay a planning-handoff tax without reason.

This is not a fourth gate on Scribe intake or Bevel critique. Those files stay their
own inboxes. Org-level proposals do not go in this file.

There is no Fable Grok Bot. Point Fable 5 at this file.

## Item shape

- **Priority:** `approved` (David said build it) · `waiting` (needs Helm QA or David) · `someday`
- **Class:** `V2` or `V3` (if you cannot say why it is not V0–V1, it does not go here)
- **Source:** discussion/issue, Bevel/Scribe item, or David’s words
- **Plan:** architecture, risks, decomposition, verification, what is out of scope
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`.

---

## Plane of Sky: spawns that are not timed at all need a spawn TYPE, not a data patch

- **Priority:** `approved` — David, 2026-08-21, in session ("please mark them approved"),
  after reading the plan below (Fable 5, 2026-08-21). PR 0 in the plan is V1 and independent.
  **Stub written by Claude (Opus 5), 2026-08-21.** Investigation only; nothing implemented.
- **Class:** `V2`
- **Source:** #109 follow-up (Frankthetankk, 2026-08-19), with a screenshot from a personal
  Plane of Sky instance.

### What he reported

In an instanced Sky: **The Spiroc Guardian marked DUE**, and **Bzzazzt / Bzzzt / Bazzzazzt
each carrying ~1:01 countdowns** that are really just how long each kill took. Neither is
timer-driven at all — the bees are a chain (each spawns the moment the previous dies) and the
Spirocs are player-triggered (kill specific trash to summon). DUE is worse than imprecise
here: it says a passive timer just elapsed when in fact nothing happens until somebody acts.

### What I verified (and where he is wrong, which is the useful part)

His hypothesis was *"Plane of Sky isn't in the achievements-dump raid-boss list #109's
suppression keys off."* **It is.** `RaidTargets.json` carries `"The Plane of Sky"` with
eleven bosses. What is missing is narrower and it lines up exactly with his screenshot:

| In the spawn catalog's Sky zone | In the raid-boss list? |
|---|---|
| `The Spiroc Lord` | **yes** → suppressed |
| `The Spiroc Guardian` | **no** → gets `namedDefaultSeconds` |
| `Bazzt Zzzt` | **yes** → suppressed |
| `Bzzzt` | **no** → gets `namedDefaultSeconds` |

So the two names showing wrong countdowns are precisely the two absent from the list. That is
a confirmed mechanism for the per-entry half.

**Still unknown, and it needs HIS log:** whether the zone-level fallback should have caught
them anyway. `SpawnTimers` also suppresses when `_currentZoneInstanced && zone.RaidZone`, and
`SpawnCatalog.IsInstancedZoneName` only recognises `"<zone> N (Awakened|Adaptive|Fused|Refined)"`
and `"<zone> - Solo|Group"`. Nobody here knows what a personal Sky instance actually prints on
zone-in. He has offered the line; it has been asked for on the thread.

### Why this is not V0–V1, and why the obvious fix is a trap

The obvious fix is to add `The Spiroc Guardian` and `Bzzzt` to `RaidTargets.json`. **Do not.**
That list also drives the Raids card and the achievements cross-reference, so it means "this
is a raid target you can clear", and these are trigger-spawned adds. Using it to mean "do not
show a countdown" is trap 4 — one entry, two meanings — and the Raids card would start
listing trash as raid bosses.

What the domain actually lacks is a **spawn type**. Frankthetankk names the two honestly:
`chained` (spawns on the previous one's death, no interval) and `player-triggered` (requires
an action, never time). Each wants its own non-countdown presentation rather than being forced
through the timer UI. That is a new concept in `SpawnCatalog`/`SpawnTimers`, a curated-catalog
schema change, and a display decision on the widget, the map, the breakout AND EQBuddy Mobile.
It also has to be reconciled with #185's auto-discovery, which assumes an interval exists to
learn — a chained spawn would teach it a number that means nothing.

**And it is catalog data about the game**, so CLAUDE.md's rule applies: match the wiki, and
departing from it needs a confirmed observation rather than an expectation. He is explicit
that he has no overworld Sky data and is only describing instances.

### Already shipped (must not be fought)

- `#109` raid-instance suppression: per-entry `RaidInstanced` (from `RaidTargets`) plus the
  zone-level `_currentZoneInstanced && zone.RaidZone` rule. A player-TYPED duration outranks
  both, everywhere — that is deliberate and must survive.
- `SpawnCatalog.EffectiveSeconds` returns null for raid-instanced entries.
- Persisted pre-#109 countdowns heal at load through `SuppressedRaidInstance`.
- `#185` (bjstrange) auto-discovery of named mobs from combat text — still open, and the
  interaction above is real.

### The wiki already publishes this distinction, and we flatten it

**Added 2026-08-21 after David said he cannot field-verify any of this — his highest character
is level 29, so Plane of Sky is out of reach for him and one reporter was the only source.**
CLAUDE.md's rule covers exactly that case: eqlwiki is the tie-breaker. It corroborates him,
and it changes what the plan should probably do.

| Page | What eqlwiki says |
|---|---|
| `Bzzzt` | **"Respawn Time: Triggered"** — an explicit field value. Plus: *"One spawns immediately after killing Bazzzazzt (#3)"* and *"Killing this mob immediately spawns Bazzt Zzzt, the boss of Island 6"*. |
| `The Spiroc Guardian` | *"Instantly respawns if killed while ANY a spiroc vanquisher lives."* |
| Island 5 page (in our own harvest cache) | *"Killing a spiroc banisher, a spiroc walker, or a spiroc revolter will spawn the miniboss The Spiroc Guardian, which in turn spawns The Spiroc Lord"* |
| `The Spiroc Lord` | **No respawn information at all.** |

So the reporter is right, and on the Guardian he is right about the category while differing on
the mechanic — the wiki gives a vanquisher-conditional respawn *and* a trash-kill trigger, not
the "manually summoned" he described. Either way a countdown is wrong.

**The important consequence for the plan: `chained` / `player-triggered` is not a taxonomy we
would be inventing. eqlwiki already carries "Respawn Time: Triggered" as a value.** Importing an
upstream distinction is a very different piece of work from designing one, and it is the version
that stays true as the wiki is corrected.

**But there is no spawn harvester.** `scripts/harvests/eqlwiki/` covers AAs, buffs, charms,
debuffs, fades, items and quests — not mobs. `SpawnCatalog.json` is hand-curated, so the wiki
value has nowhere to land today even if it were read. Whether this item includes building that
harvester, or only the schema plus a curated edit, is a scope decision for the plan — and
curated catalogs are never auto-written, so a harvester here can only *flag*.

**One more thing the plan should not miss:** `The Spiroc Lord` is currently suppressed only
because it happens to be in the raid-boss list. It is chained, not a raid-instance boss. It is
getting the right behaviour for the wrong reason, and a fix that cleans up the raid list would
silently break it.

### Checked

Read: `SpawnCatalog.MarkRaidInstanced`, `MatchesZoneName`, `IsInstancedZoneName`,
`InstanceTier`, `SpawnTimers` lines 250–310 and `SuppressedRaidInstance`, plus the Sky entries
in `SpawnCatalog.json` and `RaidTargets.json`. Fetched the three eqlwiki pages above and
grepped the local harvest cache. Confirmed `Plane of Sky` has `namedDefaultSeconds` **28800**
(8 h), which is what any Sky named with no respawn of its own inherits.

**Not checked / open:** what a personal Sky instance prints on zone-in (asked the reporter — it
decides whether the zone-level rule can ever fire for Sky); whether overworld Sky bosses have
real timers; and why his screenshot shows **~1:01** rather than the 8 h default — a plausible
reading is that `Bzzazzt` and `Bazzzazzt` are **not in the catalog at all** (only `Bzzzt` and
`Bazzt Zzzt` are), so they arrive through discovery as "killed N ago" chips with no duration,
and 1:01 is how long the chain took. **That is a hypothesis, not verified.**

### Plan — Fable 5, 2026-08-21

**Classification:** V2, confirmed — and for two reasons the stub did not have, both read in the
code. One V1 slice inside it (PR 0 below) should not wait for this item.

**Evidence chain, stated per the rule:** eqlwiki first (live API reads, 2026-08-21, quoted
below), the reporter's screenshot second. David cannot field-verify any of it; no family log
has ever been in Plane of Sky. Nothing in this plan departs from the wiki.

#### What I read, and what it changes

1. **The stub's table holds.** `The Spiroc Lord` and `Bazzt Zzzt` are in `RaidTargets.json`;
   `The Spiroc Guardian` and `Bzzzt` are not. `RaidZone` IS true for Sky (`StartsWith("Plane
   of ")` and `MatchesZoneName` matches "The Plane of Sky" by containment). `namedDefaultSeconds`
   is 28800.
2. **Live wiki, read today:** `Bzzzt` and `Bazzt Zzzt` carry `| respawn_time = Triggered` as a
   template field. **`The Spiroc Guardian` has NO `respawn_time` field** — its mechanic is in
   `description` prose ("Instantly respawns if killed while ANY a spiroc vanquisher lives").
   `The Spiroc Lord` says nothing. The Island 5 paragraph on the zone page (in our harvest
   cache, `lsth-Plane_of_Sky.wikitext:153`) states the banisher/walker/revolter → Guardian →
   Lord chain. So the stub's "import an upstream distinction" is true for the bees and NOT for
   the Spirocs, which are curated from zone-page prose. Both are wiki; the plan cites which.
3. **NEW — the #109 zone gate does not cover discovered or custom named.** `_currentZoneInstanced`
   is consulted at exactly one line, `SpawnTimers.cs:264`, inside the catalog-entry loop.
   `DiscoverNamed` and the `CustomFor` loop never see it. A proper-named kill the catalog does
   not list, inside an instanced raid zone, is discovered and timed — the #185 path walks around
   the #109 fence. `KillsInsideAnInstancedRaidZoneStartNoCountdownEvenForUnlistedNamed` tests a
   CATALOG entry missing from the dump, not a discovered one. This is a confirmed V1 bug with a
   one-condition fix and its own test, independent of everything else here.
4. **NEW — suppressing the COUNTDOWN is not enough, because LEARNING is what manufactured the
   numbers.** `Bzzzt` has a null respawn over an UNtrusted 8 h zone default, so `IsTrusted` is
   false and `LearnFromRekill` accepts any same-stay re-kill gap in [90 s, 8 h) as the cycle.
   Bee Island spawns several `Bzzzt` per clear (the zone page's split chain); two kills three
   minutes apart "measure" a three-minute respawn, write it to `spawn-overrides.json` as
   `Learned`, and every later kill counts three minutes down to DUE. The Guardian's instant
   respawn while a vanquisher lives produces the same thing. **Hypothesis, labelled:** that is
   his DUE, and the ~1:01 is time REMAINING on such a learned clock — or the chip's "killed 1m
   1s ago" for a discovered first kill. The 179×144 screenshot cannot separate the two and the
   plan does not depend on which. What it cannot be is the stub's literal reading:
   `MinLearnSeconds` refuses a 61 s gap, so no learned duration is ever 1:01.
5. **Consequence:** a `Learned` value already sits in his overrides file for these names. The
   type must HEAL it (the existing trusted self-heal at `SpawnTimers.cs:~277` is the pattern,
   and `SuppressedRaidInstance` at load is the other half), and `ZoneShare.cs:215–217` must
   stop importing a stranger's duration onto a typed entry, or one shared archive re-poisons it.
6. **Surfaces.** A typed entry creates no `SpawnTimerState` at all (exactly as `RaidInstanced`
   does today), so the chip stack, the map, and EQBuddy Mobile show nothing — parity is
   automatic and no wire field is needed. The ONE surface that changes is the Spawns window's
   catalog row, and both desktops already route it through `TimerView.For(…, suppressed)`
   (`EQBuddy/SpawnsWindow.xaml.cs:276`, `EQBuddy.Avalonia/SpawnsWindow.cs:448`). The display
   decision is one shared place.
7. **The zone-enter line is still wanted but nothing waits on it.** Because catalog names
   (Guardian, Bzzzt) showed countdowns, the zone gate did not fire — so either the personal-Sky
   line has a shape `IsInstancedZoneName` does not know, or the kills were open-world. That
   decides only whether the zone gate helps in Sky. The spawn type is a property of the
   CREATURE (the wiki puts "Triggered" on the creature page, not on an instance), so it holds in
   the overworld too and does not depend on the line. That is the clean split this item
   introduces: **`RaidInstanced` says WHERE; the spawn type says HOW.**

#### Architecture

**Schema (`SpawnCatalog.json`, hand-edited; `SpawnEntry`)**
- `spawnType`: absent/`"timed"` (default) or `"triggered"`. **One value, the wiki's word.** The
  stub's `chained` vs `player-triggered` is a real distinction in the world and NOT one eqlwiki
  records; inventing a two-value taxonomy would depart from the tie-breaker for no engine
  benefit — both mean "no clock, no learning". The difference is display, carried by:
- `triggeredBy`: free text, `/`-separated like `placeholder` — "Bazzzazzt", "a spiroc banisher /
  a spiroc walker / a spiroc revolter". Read by the row detail (trap 20: a field nothing reads
  is a lost capability).
- Store `spawnType` as a **string**, not an enum, with a lenient parser and a catalog test that
  every value is one we know — a typo in a curated file must fail a test, never catalog load.
- Entries to mark, each with its citation in `note`: **Bzzzt** and **Bazzt Zzzt** (creature
  page field); **The Spiroc Guardian** and **The Spiroc Lord** (zone page, Island 5; Guardian's
  vanquisher rule from its description). `The Spiroc Lord` keeps `RaidInstanced` — it IS in the
  achievements dump, which is the dump's fact to state — and gains the type, so it is now quiet
  for the right reason as well. The catalog's other Sky notes that say "triggered" (Protector of
  Sky, Keeper of Souls, Sister of the Spire, Hand of Veeshan, Overseer of Air, Sirran) are
  candidates: mark each only after re-reading its live page, cite it, and leave unmarked
  anything the wiki does not state. **Ask Frankthetankk for the exact kill lines** for the bee
  names — he wrote "Bzzazzt", the wiki has "Bizazzt"/"Bazzzazzt" — and add the confirmed ones
  as typed entries so discovery never runs on them.

**Engine (`SpawnCatalog.cs`, `SpawnTimers.cs`, `ZoneShare.cs`)**
- `EffectiveSeconds` → null for triggered, as for `RaidInstanced`.
- `OnKill` catalog loop: triggered and no player-typed duration → heal any `Learned` override,
  start no timer, return. Placed before learning so a re-kill gap is never measured.
- Load-time heal: generalise `SuppressedRaidInstance` to "suppressed by catalog" so persisted
  clocks from before this change drop, as #109's did.
- `ZoneShare` import: skip incoming durations for triggered entries (flag them in the preview
  the way wild timers are flagged).
- `DiscoverNamed` and the custom loop: honour `_currentZoneInstanced && zone.RaidZone` (PR 0).

**Presentation (`UI.Shared`, then both desktops)**
- `SpawnRow.Suppressed` (bool) becomes `SpawnRow.Suppression` — `None | RaidInstance |
  Triggered` — and `TimerView.State` gains `Triggered`. Reusing the bool for two meanings is
  trap 4; the word on the row must be "triggered", not "instance", because the player's next
  action differs (go kill the trigger vs. wait for the instance clock).
- `TimerView.Text` → "triggered"; dim ink; no track.
- `SpawnsViewModel.BuildRow` detail: "Triggered spawn — there is no clock to count down: it
  appears when {triggeredBy} dies (eqlwiki). Type a respawn time if you want your own reminder
  anyway." A typed duration still outranks the type everywhere, as it outranks `RaidInstanced`.
- Both `SpawnsWindow`s compile against the new enum; nothing else in either UI changes.
- Chip stack, map, Mobile: unchanged by construction (no timer exists). A "now up: Bazzt Zzzt"
  successor chip is a real idea and **out of scope** — it needs a wire field and trap 32's page
  reload path.

#### Risks and the traps they touch

- **Trap 4:** never put Guardian/Bzzzt in `RaidTargets.json`; never make `Suppressed` mean two
  things.
- **Trap 20/26:** `triggeredBy` has a reader from the first commit; the Spawns row is it.
- **Trap 22/23:** the `spawns-window` shot stages Runnyeye; it needs a Sky row staged, and the
  prediction written down first: "triggered", no track, detail names the trigger.
- **Trap 30:** `TimerView.State` grows — grep `scripts/` and both `SpawnsWindow`s for anything
  that enumerates states by hand.
- **Match-the-wiki:** every typed entry cites its page in `note`; the Spirocs cite the zone
  page explicitly because their creature pages are silent.
- **#185 interplay:** typed entries are catalog entries, so discovery never runs on them;
  learned values heal; PR 0 closes the discovery hole in instanced raid zones generally.
- **A typed duration must keep winning.** `IsManual` already distinguishes typed from learned;
  the new branch must use it, not `RespawnSeconds is not null`.
- **Unknown enum string** in a curated file: lenient parse + sanity test, as above.

#### Decomposition

- **PR 0 (V1, independent — does not need this item's approval, only David's ordinary go):**
  discovery and custom named honour the instanced-raid-zone gate.
  `DiscoveredNamedTests.AProperNamedKillInsideAnInstancedRaidZoneDiscoversNothing`, and the
  open-world twin still discovers. No UI.
- **PR 1 (Core only):** schema fields + lenient parse + catalog sanity test; `EffectiveSeconds`;
  the `OnKill` branch with heal; load-time heal; `ZoneShare` skip; the four Sky entries with
  citations. `SpawnTimerTests`: a triggered kill starts no countdown; a re-kill gap teaches
  nothing; a pre-existing `Learned` override heals at kill and at load; a typed duration still
  runs; a share payload cannot import a duration onto it. `docs/TestPlan.md` §3 rows.
- **PR 2 (presentation, both desktops):** `Suppression` enum, `TimerView.State.Triggered`,
  `SpawnsViewModel` detail, both `SpawnsWindow`s, staged `spawns-window` shot, Avalonia
  `WidgetRenderTests` row assertion. `WhatsNew.json` entry crediting Frankthetankk (#109 and
  the 2026-08-19 follow-up). Reply on #109 signed, after reading the last comment's signature.
- **Someday, NOT this item:** a `mobs-harvest.py` that reads `respawn_time` off every catalog
  name's `{{Namedmobpage}}` and **flags** field-level disagreements in the refresh report. The
  curated-catalog rule means it can only ever flag; `refresh.py` already flags by page hash,
  and a per-field diff is the useful version of that.

#### Verification

- Unit tests above; `scripts/check.ps1`.
- **David cannot field-verify this, and the plan says so.** Acceptance is: the suite, the staged
  screenshot on both desktops (prove the binary first — trap 18), and the reporter's
  confirmation on #109 after release. Ask him on the thread for three things at once: does the
  Guardian row now read "triggered"; do the bees stop counting; and the zone-enter line.
- A manual check that needs no Sky character: seed a `spawn-overrides.json` with a `Learned`
  value for `Bzzzt`, launch, confirm the row shows "triggered" with no duration and the file no
  longer carries the value.

#### Out of scope

Overworld timers for Sky's genuinely timed bosses (Noble Dojorn's 7 d, Thunder Spirit
Princess's 6 h stay); reading the game's Instance Maintenance screen (not in the log — log-only
rule); a chain "next up" chip; the mob harvester (someday, above); editing `RaidTargets.json`
in any direction; Mobile wire or page changes; the reporter's "D0–D4 naming as a broader
signal" (that is `IsInstancedZoneName`, and it waits on his zone line, not on this item).

---

*No other items yet.*
