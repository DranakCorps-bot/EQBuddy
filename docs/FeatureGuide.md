# EQBuddy Feature Guide & Manual Verification

A per-feature description of everything UI-surfaced, with how to verify each by hand.
Written for cross-platform parity work (issue #4): if a change touches a feature here,
re-run its verification. Core logic (parser, aggregation, journal, SQLite) is covered
by `tests/EQBuddy.Tests` — this guide covers what the tests can't reach: rendering,
alerts, input, windows, and lifecycle glue.

## Testing without playing: fixture logs & isolated profiles

You don't need the game (or Windows) to exercise almost everything:

- **`EQBUDDY_APPDATA=<dir>`** runs the app against an isolated profile — settings,
  history.db, and error.log all live there. Your real data stays untouched.
- **Fixture log:** take any real `eqlog_*.txt` and rewrite its timestamps
  (format `ddd MMM dd HH:mm:ss yyyy`, e.g. `[Mon Jul 20 16:40:38 2026]`):
  - A block ending **> 60 min ago** becomes a *finished* session, reconstructed into
    history at startup.
  - A block ending **~1 min ago** becomes the *live* session.
  - Append lines to the file while the app runs to simulate live play — the watcher
    polls every 150 ms. The status dot is green only if the file grew in the last 30 s.
- **`EQBUDDY_EXPAND=1`** launches with every section expanded (plus a state dump in
  `<appdata>/debug.txt`) — good for screenshots and layout checks.
- `docs/screenshots/` shows the current WPF rendering of each section for side-by-side
  comparison (regenerated with each release that changes visuals).

## Quick tutorial

A six-page tour shown at every launch until finished or dismissed ("Never show
again", or the Options checkbox "Show quick tutorial at launch"; right-click →
Quick tutorial… reopens it on demand). Page 1 is the **log-truncation consent
question** — while the tour is still enabled, the startup janitor defers log
truncation, so a new user's logs are never emptied before they've answered.
"Skip for now" shows it again next launch; Finish and Never both stop the
auto-show (the last page says how to get it back).

## The widget (main window)

Always-on-top, borderless, draggable anywhere on its surface; position persists.
Title bar: status dot (green = log grew <30 s ago, amber <2 min, red otherwise, with a
"logging looks off" banner after 2 min), character name (follows whichever log file is
growing; switches within a few seconds), gear/reset/minimize/close buttons.

**Verify:** launch with a fixture log ending now → dot green, character name from the
filename. Stop appending → dot decays amber then red with the banner. Point it at a
second character's growing log → title switches, session resets.

### Combat card
Header: session DPS (+ live fight DPS while fighting). Details:
- Summary block: damage dealt (melee/spell split), crits + crit rate, accuracy,
  time-in-combat, recent-window DPS ("Last 15m"), biggest hit, damage taken +
  avoidance %, your spells (over-time vs direct), cast completion, current stance.
- **Your spells: N over time / M direct** — the DoT/nuke split, classified by log-line
  shape (`X has taken N damage from your Y.` is a tick; `You hit X for N points of
  <school> damage by Y.` is direct), not by spell name. Pet damage is excluded, so the
  two need not sum to the spell total.
- **Cast completion** — `Casts N · P% completed (i interrupted · f fizzled · r resisted)`
  from `You begin casting X.` / `Your X spell is interrupted.`. Resists are excluded from
  the failure count: a resisted spell was cast fine, it just did nothing. Logs with no
  cast lines fall back to the old `Fizzles n · resists n` line.
- **Damage by attack** — Details!-style breakdown: each source shows
  `total · ×hits · avg · dps (· crit%)`. The dps follows parser convention:
  **that ability's damage ÷ total time in combat** — its contribution rate, which
  falls the longer you go without using it. The tooltip adds share-of-total and the
  **burst rate** (damage ÷ the ability's own active time: consecutive hits within
  10 s accumulate real spacing, an isolated hit counts ~2.5 s). Sort bar:
  total/dps/hits/avg — the bar behind each row is proportional to whichever column
  is sorted.
  Pet damage appears as "Pet (Name)", or plain "Pet" when the game names it generically
  (`Your pet hits …`) — that form needs no prior identification, since nothing but your own
  pet is ever called "Your pet". **Known gap:** a summoned pet that is never given an
  attack order emits no `Attacking … Master.` line, so if the game also names it by its own
  name in combat lines, its damage goes uncredited until you order an attack. Run with
  `EQBUDDY_CCLOG=1` to capture the real pet chatter and close this properly.
  A charm cast in flight (`You begin casting
  <charm>.` followed by `<creature> blinks.`) confirms the pet outright; a blink with no
  charm cast behind it stays provisional as "Pet? (Name)" until a "Master" tell confirms
  it. An interrupted or fizzled charm claims nothing, and `Your <charm> spell has worn
  off of <pet>.` drops the claim immediately rather than waiting for the creature to turn
  on you. Charm spells outside the seed list are learned from the cast → blink → "Master"
  sequence, so they behave the same on the second cast.
  Pet crits count: third-party damage lines carry the same trailing `(Critical)` your own
  hits do (`Lebn slashes a decaying skeleton for 13 points of damage. (Critical)`), so the
  pet rows show a real crit %. Pet hits stay out of *your* accuracy and crit counters —
  those describe what you swung, and pet misses aren't credited either.
- **Pet abilities** — the pet's row split by what it actually used, in the same
  `total · ×hits · avg · dps (· crit%)` columns and following the damage sort bar above it.
  Melee comes from the attack verb mapped to the skill your own hits use (`bashes` → Bash),
  spells from the name the log gives (`… hit X for N points of magic damage by Lifetap.` /
  `X has taken N damage from Poison Bolt by <pet>.`). Rows are keyed by ability, not by pet,
  so charm swaps stay one readable list — the per-pet totals are the rows above. The
  section is hidden when no pet damage was seen. A generic bucket ("Melee"/"Spell") catches
  any attack whose verb the mapping doesn't recognise, so nothing is lost.
- **Damage taken from** per attacker (total · hits · avg). Self-inflicted damage
  (`You hurt yourself for 27 points.` — HP-cost casting, falls, drowning) counts here
  under a **"Yourself"** row, but is flagged `Self` and deliberately opens **no combat
  window and no encounter**: a swim across a lake is not a fight, and a necromancer's
  own casting must not inflate combat seconds (348 such lines in one real necro session
  would have wrecked the DPS denominator).
- **Recent fights** — last 8 encounters: creature, duration, per-fight DPS, with a
  bar comparing each fight's DPS to the hottest recent fight. A fight opens on
  damage, closes on the kill line or a 20 s timeout ("· ?" marks timeouts).
  Back-to-back same-name kills are distinct fights.
- **Area spells (per cast)** — any spell seen damaging two or more creatures inside a
  2 s burst, reported as `damage/cast · ×casts · avg targets (best N)`. Per-target figures
  understate an AoE: one cast hitting four creatures for 100 each shows as `avg 100` in
  the damage breakdown but is worth 400 per cast, which is the number that decides whether
  pulling a group beats killing them singly. `avg targets` below `best` means later pulls
  were smaller than the best one. Detection is behavioural — no spell list — and works off
  damage lines, so travel spells can't be mistaken for area damage since they deal none.
  Melee and damage shields are excluded.
- **By stance** — damage, combat time, DPS per stance; combat windows close on stance
  change so time lands on the right stance.

**Verify:** replay a combat-heavy fixture; check the share bars are proportional, the
top source's bar spans the full row, the % column sums to ~100, and dps × combat time
≈ total damage. Kill line ordering matters: EQL logs `experience → coin → "You have
slain X!"` in the same second.

### Healing card
HPS (healing ÷ combat time), healing done/received, heals cast per spell with the
same breakdown as Combat: `total · ×casts · avg · hps` per row, sortable by
total/hps/casts/avg with the bar following the sort; per-spell hps = that spell's
healing ÷ total time in combat (burst rate in the tooltip). Who healed you,
regen/hymn tick counts (no amounts — the log gives none).

**Heal-over-time ticks are counted, both directions.** `Aenari healed you over time
for 8 hit points by Echoing Light.` credits healing received (and the healer), and
`You healed X over time for N…` credits healing done, same as a direct heal — these
carry real amounts, unlike hymn/regen ticks. One week of a real cleric-adjacent log
had 223 received-HoT lines that were previously invisible, so pre-1.19 sessions
understate healing received wherever a druid or cleric ran HoTs.

### Kills tab — Kills & Drops card (was the Kills card)
*Folded into the Kills & Drops theme 2026-08-21 (docs/Themes.md). Drops by Creature is the
tab beside it — it moved OUT of Gear & Loot in the same pass, because Gear & Loot is about
your bags and Drops is about the mob.*

Header: your kills (+ group kills). Details: per-creature counts, kills/hour +
recent-window kills, **Farming (per creature)**: avg fight length · coin · xp% per
creature, then each creature's observed drops indented with `×count · drop%`
(drop % can exceed 100 for multi-drops). Group kills by member below.
Coin/XP attribution uses a 3 s window around the kill line **in both directions**
(rewards are logged *before* the kill line in live play). Loot→creature attribution is
by corpse name, which the log always includes (even with the advanced loot window).

**Verify:** fixture with kills + loot + coin. Farming coin/xp must be non-zero for
coin/xp-giving kills; animals and gray cons legitimately show 0.

### Loot tab — Gear & Loot card (was the Loot card)
*Folded into the Gear & Loot theme 2026-08-21 (docs/Themes.md), alongside Items, Wishlist
and Inventory.*

Every looted item with counts (both the `--You have looted…--` form and the auto-sell
form), plus "Created by merging". Auto-sold loot counts as loot AND merchant income.
Selling from the advanced loot window ("You successfully destroyed N X." followed by
"You received … from that item.") is paired into a named merchant sale.

### Watch card (watch rules)
The card is labelled **🎯 Watch**; its persisted key is still `tracked`, so existing
`SectionOrder`/`HiddenSections` settings keep working.

**Ships with one rule already on.** A fresh install (and every existing install, once)
gets a "CC broke" rule — Spell fade + Any crowd control, banner **and** sound enabled —
seeded by `AppSettings.ApplyDefaultRules()` and guarded by `DefaultRulesVersion`, so it
is applied exactly once. Everything about it is editable, and deleting it makes it stay
deleted.

Rules are defined in Options: **Kind** (Loot / Kill / Skill-up / Death / Milestone /
Spell fade / Log text) + name + match text (case-insensitive substring; the name doubles as
match text if the match box is empty; Death/Milestone match everything when empty) + an
optional per-rule **delay**.

**Two rules can share a name.** Every rule carries a persisted `Id` (generated at
construction; rules saved before ids existed get one on first load and it is written
back immediately, so identity is stable across restarts). Alert cooldowns, the 8-cue
in-flight cap, countdowns, alert baselines, and snapshot-row-to-rule matching are all
keyed by id, never by display name — so "Asaka" alerting on sight and "Asaka" running
a respawn timer are fully independent rules. An empty id in a hand-edited
settings.json is regenerated rather than trusted, since several rules sharing `""`
would recreate exactly the collision ids exist to end. Covered by `RuleIdentityTests`.

**Verify (shared names):** two rules with identical names, one immediate and one with a
2 m delay → separate Watch rows, countdown only on the delayed one, separate mini
chips, and the countdown survives an app restart.

**Options → Watch rules → "Show examples"** expands a worked example for every kind, plus the
handful of rules that explain most confusion (match text is a substring, not a whole name;
Kind decides what the text is matched against; empty means "all of them" for some kinds and
"nothing" for Log text). Collapsed by default, remembered per install
(`ShowWatchGuide`). The content lives in `EQBuddy.UI.Shared.WatchGuide` so both UIs show the
same thing, and `WatchGuideTests` checks the examples still describe rules the app can
actually build — including that every `WatchKind` has one, which is how the missing Skill-up
example was caught.

**Log text** matches the raw line instead of a parsed event — the deliberate exception to
WATCH-001. Every other kind can only fire on something EQBuddy has a pattern for, which is
useless for lines nobody can pattern in advance: another player's raid-assist script calling
a heal rotation, a server's custom emotes, a guild's own chat conventions (requested in
[discussion #22](https://github.com/DranakCorps-bot/EQBuddy/discussions/22) for exactly the
first case). Details worth knowing:

- The `[Tue Jul 28 16:55:07 2026] ` prefix is **not** matched, so a pattern like `Jul`
  doesn't hit every line in the log.
- Lines are offered to text rules whether or not EQBuddy parsed them — a raid announcement
  that also happens to be a line we understand still counts for both.
- An empty pattern matches **nothing** here, unlike Death/Milestone where empty means
  match-all. Match-all on raw text would alert on every line in the log.
- Only lines matching an enabled text rule are retained, so with no text rules configured
  this costs one length check per line and nothing is kept. A rule that is disabled, or
  added mid-session, keeps nothing from while it wasn't watching — unlike the other kinds,
  text rules can't recalculate history they never held.
- Rows are keyed by the whole line, so a verbatim repeat groups with a count while a
  different one gets its own row; long lines are trimmed to 64 characters for display.
- Matched text does **not** count towards active-play time. Someone else's macro firing
  while you stand in the bank isn't you playing.

### Alert latency

EQBuddy reads a log file the game writes; it cannot know about a line before the game
flushes it, and there is no callback to hook. So an alert is always *reactive*, and the
delay has three parts:

| Stage | Cost |
| --- | --- |
| Game writes the line and flushes it | outside our control, unmeasured |
| Tailer notices the new bytes | 0–150 ms (poll interval) |
| Match → alert dispatched | ~1 ms |

Measured over 30 appends with the phase swept across the poll cycle: **min 7 ms, median
84 ms, max 195 ms** from the line hitting the file to the alert firing.

Two decisions get it there, both worth preserving:

- **The tailer polls every 150 ms**, not the 500 ms it used before text rules existed. That
  interval *is* the latency floor. Polling an unchanged file is a length check; at 150 ms
  the widget idles at well under 1 % of one core.
- **Text alerts fire from the ingest thread** (`SessionStats.TextMatched`), not from the
  1 s UI refresh that drives every other alert. That refresh alone used to add up to a
  full second, which for a heal rotation is the difference between a cue and a reminder of
  something you missed. Text rules are therefore skipped by the snapshot-driven alert path
  in both UIs — alerting in both places would double-fire.

Per-rule cooldown is **1 s** for text rules rather than the 5 s used elsewhere: a chain
announces every few seconds by design, and the longer cooldown would swallow exactly the
repeats you asked to hear about.

Anything needing sub-frame reaction — casting on a timer you can see coming — is better
served by an in-game trigger. EQBuddy is honest about being a log reader.

**Spell fade** matches "Your X spell has worn off (of Y)." and takes a second dropdown:
- *By name…* — the original substring match against the spell name.
- *Any spell* — every fade, including buffs (which we can't classify).
- *Any CC* / *Charm* / *Mez* / *Root* / *Lull* / *Stun* — matched by category
  via `SpellCatalog`, needing no match text at all. Ranks collapse onto the base name, so
  one rule covers `Befriend Animal` through `Befriend Animal V` and every CC spell the
  character learns later. A damage song wearing off does **not** trigger these.
- *HoT* — heal-over-time spells, the "recast it" cue. Unlike CC (which produces no
  numbers and needs a seed list), HoTs label themselves: their tick lines name the spell
  (`…healed you over time for 8 hit points by Echoing Light.`), so the catalog learns
  them by observation in both directions — your casts and heals landing on you. A small
  seed (Echoing Light, Budding Heal — both log-verified — plus the classic
  Regeneration/Chloroplast/Regrowth line) covers a fade arriving before the first tick
  was seen. A direct heal wearing off does **not** trigger this filter.

**Buff and HoT fades fire too (FADE-001).** Mez/charm fades name their spell, but
buffs and HoTs fade with flavor text that names nothing — `The echo of healing fades
away.` is Echoing Light, `Your speed returns to normal.` is a haste dropping.
`FadeMessageCatalog` (Data/FadeMessages.json, 61 wear-off messages from eqlwiki +
classic spell pages, seeded from lines observed in real Legends logs) maps each
message to its candidate spells; a SpellFade rule fires when ANY candidate satisfies
it, and the row shows the shared label ("Haste") since the log can't say which haste
it was. Spells whose wiki wear-off field is blank (Flowering Heal) appear to fade
silently — no line, no rule; a delay-cue rule is the honest tool there. Found via a
Reddit report from an enchanter whose HoT/haste rules never fired.

Entries show as "Spell (Target)". Each rule shows
total, a **"last: <item> · age ago"** line (the card leads with what just happened —
the full per-item breakdown sits behind a "▸ all N kinds" toggle, session-scoped,
added for the same enchanter drowning in an hour of mez targets), per-hour rates
(wall-clock + active-time).
Rules are evaluated over the whole session journal, so editing a rule mid-session
recalculates history, and alerts never fire during startup ingest or character switch.

**Delay (per rule, up to 30 minutes).** Holds the alert back after the match, so a rule
becomes a *cue* rather than a notification. Entered in seconds by default, with `m` for
minutes — `2.5`, `25`, `8m`, `1:30` — and shown back as minutes when it's a whole number of
them, so an `8m` rule still reads `8m` rather than `480`. Requested in
[discussion #22](https://github.com/DranakCorps-bot/EQBuddy/discussions/22): match the call
in a complete-heal chain and sound at **+2.5 s** to say "cast now", or match your own mez
cast and sound at **+25 s** to say "recast before it breaks". 0 (an empty box) is the
original immediate behaviour, so nothing changes for rules that don't set it.

- **Only the alert waits.** The count, rates and rows update on the match, as always.
- **For both an immediate and a delayed alert, make two rules** with the same match text and
  different sounds — a quiet "heard it" at 0 s and a loud "do it now" at 2.5 s. One rule has
  one sound, so this is strictly better than a toggle would have been.
- **Accuracy:** the cue inherits the detection latency as a bias, not jitter. A 3 s cue
  measured at 3,093 ms end to end — the ~93 ms is the same detection cost described under
  *Alert latency* above. Dial 2.4 if you want 2.5. Log timestamps are 1-second resolution,
  so there's nothing to correct against; cues are scheduled from when the line was *seen*.
- **Overlapping cues are normal** — a chain announces repeatedly and each call gets its own,
  capped at 8 in flight per rule so a chatty pattern can't queue a wall of sounds.
- **Delay works on every rule kind**, not just Log text. Kill + `8m` is a camp timer: kill
  the placeholder, get told when it's due back.
- **A cue in flight shows a live countdown** — on the rule's heading in the Watch card, and on
  its mini-dashboard chip (`⏳ Respawn 7:54`), refreshed every second. While something is
  counting down that's what the chip shows instead of its match count, because when it fires
  is the only thing you want from it. A rule with several cues pending shows the soonest.
- **Cues are abandoned** when they stop making sense — but what "stops making sense" depends
  on the length, because the two uses are different:
  - **Combat cues (≤ 60 s)** — "cast now", "recast before it breaks" — are dropped **when you
    die**. Landing one on your corpse is noise.
  - **Longer cues (> 60 s)** — respawn and camp timers — **survive your death**, because
    dying has no bearing on when a mob pops. Losing an eight-minute timer to a dirt nap
    would defeat the purpose.
  - **Both** are dropped when the session rolls over on an idle gap or the widget follows a
    different character: a timer from the camp you left isn't yours any more.
- The cooldown applies when the alert **fires**, not when it was scheduled — with a delay
  set, what matters is how long since you last heard something.
- The duration has to come from you: EQ Legends never logs how long a spell lasts, so a
  25 s reminder will be wrong once the spell is upgraded.

**📌 per rule** puts that rule on the mini dashboard. The Options checkbox is the master
switch for chips; the pin on each rule row decides which ones appear, so a busy rule list
doesn't turn the mini bar into a wall. Installs that had chips on before this was per-rule
get every enabled rule pinned, matching what they already saw.

**Alerts:** 🔔 banner + a **per-rule sound**, 5 s per-rule cooldown. Each rule's sound box
offers `Off` (silent), `Default` (follow the shared choice), any built-in, or `Custom…`
for that rule's own `.wav`/`.mp3` — the point being that you learn what happened from the
audio without looking at the widget. `TrackedRule.AlertSoundName` holds it; empty means
inherit, so rules saved before this feature keep the shared sound. Resolution lives in
`AlertSoundCatalog.Resolve` and is covered by tests. **What actually gets played, and at
what volume**, is then decided by `AlertSoundPlanner.Plan` — one answer for both UIs. A
custom file that has gone missing is not swapped in silence: Ding stands in *at the
Options volume* and the alert tile names the file that vanished (#153). Nothing EQBuddy
plays bypasses the volume slider. The banner is a
**floating tile**, independent of the widget: always on top, permanently
click-through, never takes focus, auto-dismisses ~6 s. Position it by opening
Options — the tile appears in placement mode ("drag me") and saves its spot on
close; in play, clicks pass straight through it to the game. Sound is global in Options: seven named Windows Media
sounds or a custom .wav/.mp3, with a ▶ preview. (Linux: sound backend TBD.)

**Verify:** create a Loot rule matching an item in your fixture, append the loot line
live → counter increments, banner pops (also while minimized), sound plays once even
if two matches land within 5 s.

**Verify (per-rule sounds):** give two rules different sounds, trigger each, and confirm
you hear two *different* sounds — one sound for everything was the original bug. Set a
third to `Off` and confirm it stays silent rather than falling back to the default.

**Verify (built-in CC alert):** on a fresh profile (`EQBUDDY_APPDATA=<empty dir>`) the
"CC broke" rule is present with 🔔 and 🔊 on. Append
`Your Befriend Animal spell has worn off of a puma.` → banner + sound, counter 1.
Append `Your Chords of Dissonance spell has worn off of a giant spider.` → no alert
(not crowd control). Toggle 🔊 off, delete the rule, restart → it stays deleted.

### Wishlist tab — Gear & Loot card (was the Gear card)
*Folded into the Gear & Loot theme 2026-08-21 (docs/Themes.md) and RENAMED: the tab reads
**Wishlist**, not Gear. `LootSurface.LabelFor(LootTab.Gear)` is the source of that word. The
old Gear Locker and Inventory windows both live on the Inventory tab beside it.*

Import an EQ Legends Tools shopping-list HTML file with **Import gear list…**, which is
on the Wishlist tab itself, directly above the list — *"Options → Cards & windows →
Import gear list…" until 2026-09-05 (SR-2), when the whole block moved to the surface it
fills.* *Open EQ Legends Tools* beside it goes to the page you build the list on. The
Wishlist tab is a persistent manual checklist: each row includes its slot, target item,
drop/quest source, wiki/item URL on hover, and a checkbox. Re-importing replaces the
complete list and keeps every box already ticked; *Clear* removes it, and asks first —
the export is still on the website, but the ticks are not.

Imports separate equipped items under a *Gear* heading from socketed entries under
an *Exaltations* heading. An exaltation that supplies an effect name displays it in
smaller text immediately after the item name, for example `Fishbone Earring
(Enduring Breath)`. Lists without either category omit that heading, and older
shopping-list exports without socketed entries remain ordinary Gear-only lists.

**Verify:** Import a shopping-list export
containing both equipped gear and socketed exaltations. Confirm that Gear comes first,
Exaltations follows, item sources remain with their own rows, effect text is smaller
and parenthetical, and toggling a checkbox updates the Wishlist count and persists
after restart. Import a gear-only export as a compatibility check.

### Wealth tab — Progress window (was the Money card)
*Folded into the Progress theme 2026-08-19 (docs/Themes.md); the widget's Progress card
opens it, and its line carries the coin total and the mote rate while you play.*

Corpse coin vs merchant income, drops count, biggest drop, per-hour rates (wall +
active), everything sold with per-item totals.

### Experience tab — Progress window (was the Progress card)
XP gains + %/hr (session and recent window), AA points + AA/hr, estimated time to
next level (exact after a level-up this session, else an upper bound), level-ups with
**time-in-level**, skill-ups per skill.

### Faction tab — Progress window · Travels tab — World window
*Faction folded into the Progress theme 2026-08-19; Travels & Deaths became the World card's Travels tab in the World fold, 2026-08-27, and the World card itself left the widget on 2026-09-05 — the tab is unchanged, and the way in is right-click → **World…**, or the World room in the new EQBuddy window.*

Net faction standing per faction. A standing at the cap shows **maxed** — the game
says `Your faction standing with X could not possibly get any better.` and EQBuddy
passes that on rather than letting a farmed faction look stuck (a faction that moved
earlier in the session and then capped shows `+120 · maxed`). The formatting lives in
`EQBuddy.UI.Shared.FactionFormat` so both UIs say it the same way. Deaths
(killer + time), zones visited with times, camp markers.

**Verify:** append a faction-cap line for a faction with no prior hits → row appears
as `maxed`; append gain lines then the cap line → `+N · maxed`.

**Both death forms are counted.** EQ Legends logs your death two different ways, each
preceded by `You have been knocked unconscious!`:

| Log line | When | Killer |
| --- | --- | --- |
| `You have been slain by Guard Dunil!` | a direct attack landed the killing blow | named |
| `You died.` | observed when a damage-over-time tick finishes you | **nobody** |

Only the first was parsed originally, so DoT deaths went uncounted — found in a real log
(`eqlog_Hugzee`, four DoTs landing in the same second as the death). Since the plain form
names nobody, the death is blamed on whatever last damaged you within 20 s, which for a DoT
death is the caster of the finishing tick; with nothing to blame it reads "Something". The
`knocked unconscious` line is deliberately not parsed — it precedes both forms and would
double every death.

## Camps tab of the World window (Track Spawns)

**On by default, chicklets first** (`TrackSpawns`, default true; the shape is David's
design, arrived at over three iterations — always-open was noise, pop-the-full-window
was still too much). The ambient face of the feature is the HUD chip row
(`HudChipRowWindow`, which folded `SpawnChipsWindow` and `MezChipsWindow` into one in
Surface A / SA-2): one small chip per running countdown (`⏳ Asaka L`Rei 3:12`), laid out
in a row that sits under the widget and moves with it. **Nothing about its position is
saved** — it is recomputed from the widget every tick, and the old per-stack
`SpawnChipsLeft/Top` and grow-up settings retired with the two windows. The row shows
**every timer on the server regardless of zone** — a Befallen camp timer keeps its
chip while you bank in WC — sorted soonest-first. At zero a chip flips to a
warn-colored **DUE** and a single click acknowledges it (clears the timer); a
double-click on any chip opens the full zone window on that chip's zone. The stack
family exists exactly while timers do and the full window is closed; MainWindow's shared 1 s
tick drives refresh, visibility, and the due sounds (`ConsumeDueAlerts` primes at
startup so a camp that expired while the app was closed doesn't re-alert). Due
notification is **sound-only** — the chip flipping to DUE is the visual, and a banner
on top of it was double notification (David's call).
The **full window never opens by itself**: double-click a chip or right-click →
**Spawn timers…**. **Track spawns** (menu / ⚙ Options checkbox, in lockstep via
`MainWindow.SetTrackSpawns`) disarms everything.

**Zone following** reacts to zone *changes*, not ticks: browsing another zone's list
mid-camp survives until you actually zone. Manual zone picks no longer untick Follow —
1.20.0 unticked it on a selection event the user never made and following silently
died (found on David's machine: `SpawnFollowZone: false`, `SpawnZone: ""` — a
combination no user action produces). Empty selections are ignored outright, and a
one-time repair (`SpawnFollowRepaired`) restores the default for anyone the bug
touched. EQ Legends difficulty tiers — log zone names like `Befallen 1 (Awakened)`,
`Befallen 4 (Refined)` (D0–D4: Awakened/Adaptive/Fused/Refined) — resolve to their
base catalog zone via containment plus `StripTierVariant`.

**The catalog** (`EQBuddy.Core/Data/SpawnCatalog.json`, embedded): 118 zones, 843
named, built from eqlwiki.com (the EQ Legends community wiki — authoritative where it
has data) with classic-EQ sources (p99 wiki, Allakhazam) filling gaps. Per entry:
respawn seconds (null = undocumented → zone's `namedDefaultSeconds` → null means the
player must supply one), variance, placeholder, source, note (surfaced as the row
tooltip). **Player edits never touch the catalog** — they live in
`<appdata>/spawn-overrides.json` (`SpawnOverrides`), so a release that refreshes the
catalog can't eat anyone's corrections. Custom named (player-added) live there too.

**Timers** (`SpawnTimers`, fed by `LogWatcher.Spawns` alongside SessionStats):
- A `KillEvent` matching a named **or its placeholder** starts the countdown, using
  the log's own timestamp — so the startup replay re-derives running countdowns, the
  same way delayed watch cues recover. Longer timers (raid targets outliving the log)
  survive via `<appdata>/spawn-timers.json`.
- Matching is **zone-gated** (names repeat across zones; the current zone comes from
  "You have entered" lines) and **per-server** (`server|zone|name` keys). No zone seen
  yet = no automatic matching; ▶ is the fallback, not a guess.
- Replays are idempotent and an older kill never rewinds a newer timer. A repeat kill
  restarts the clock.
- Due timers show DUE (warn-colored) for **one minute**, then drop on their own
  (`SpawnTimers.DueLinger`) — if nobody clicked it away, they've moved on, and a
  stale DUE tells them nothing (David's call, replacing an earlier one-cycle linger).
- **Timers tighten themselves from play** (`SpawnTimers.LearnFromRekill`): re-killing
  a named (or its PH) sooner than its timer says is possible proves the respawn is at
  most that gap, so the gap becomes a learned override (flagged `Learned`, shown in
  the row tooltip). Manual edits are never touched, learning never loosens, gaps
  under 90 s are multi-spawn noise. Built after a Splitpaw player reported 22-minute
  catalog timers against 2–5-minute Legends reality — the catalog seeds, play
  corrects.
- Durations parse via `SpawnDurationText`: bare number = **minutes** (wiki
  convention — deliberately different from rule delays, where bare = seconds), `90s`,
  `8m`, `12h`, `3d`, `3d 12h`, `6:40` (m:ss), `1:00:00`.

**Alerts:** sound-only — the chicklet's DUE badge is the visual. Per-named: 🔔 opts a
named in (default OFF, like a watch rule's 🔊); its sound picker offers **Default**
(which maps to **Alarm** — spawns deliberately do NOT follow the Options alert sound,
because a camp popping deserves a louder default than a loot ding; both David's
calls, arrived at across two field tests — an earlier spawn-specific shared picker
defaulting to Off made "Default" mean silence, which read as broken), **Off**,
built-ins, and **Custom…**; picking a concrete sound flips the bell on by itself.
`AppSettings.SpawnSound` is dead, kept only for settings round-trip. The view model
primes on first look so a timer that expired while the app was closed shows as due
but never re-alerts at startup; only live transitions fire.

**Verify:** isolated profile, fixture log with `You have entered The Ruins of Old
Guk.` and NO kills → window does NOT open at launch. Append `You have slain a froglok
ghoul lord!` → window pops within ~2 s on Lower Guk with the countdown running. ✕ it,
append another kill → it pops again. Clear the timer (✕ on the row) → the window
closes itself. `You have entered Befallen 1 (Awakened).` must select Befallen; picking
another zone by hand must NOT untick Follow, and zoning afterwards snaps back.

## EQBuddy Mobile

The 📱 title-bar button (also the first entry in the right-click windows list) opens
the pairing window: a QR code, the URL under it, a client count, and **New code**.
The device's browser becomes a second screen. Off until switched on; the server binds
only LAN addresses (never loopback), and the pairing token travels in the URL
*fragment*, so it never appears in an HTTP request line.

Two layers decide what a device sees. The **desktop gate** (Options → Behavior →
EQBuddy Mobile) is a checkbox per surface — the owner deciding what may be sent at
all; an unoffered surface is never even projected. The **device picker** (⚙ on the
page) is that device's own subset and running order, stored in its localStorage, so a
tablet and a phone differ. On the page, one picked screen fills the viewport; several
tile into a grid, with the zone map taking the wide slot.

Writes a device may make: checklist rows (Epic, Sky, Gear) tick as if clicked on the
desktop, and spawn points can be curated from the map — tap a point for confirm /
un-confirm / remove, or ⋯ for a two-step zone reset. Every edit answers with the same
sentence the desktop's status bar would show, including when nothing changed.

**Verify:** switch it on, pair a device, and confirm the client count rises. Untick a
surface on the desktop → the device says "Not shared by the PC". Pick a different
subset on the device → the desktop is unaffected. Tick an Epic row on the device → the
desktop card repaints. On the map, tap a spawn point and remove it → a notice appears
on *every* connected device and the PC's own Map tab drops the circle within a
tick. Press **New code** → paired devices drop and refuse to reconnect. Finally set
`CompanionEnabled=false` in settings.json, restart, and confirm nothing is listening
(`Get-NetTCPConnection -State Listen` shows no EQBuddy port).

**Known limits, all honest failures rather than silent ones:** Windows Firewall
prompts on the first listen and a dismissed prompt means no device can connect;
AP/client isolation on guest Wi-Fi blocks it; and browsers refuse a wake lock over
plain HTTP, so the page tells the user to raise their screen timeout instead of
pretending it can hold the screen awake.

## Mini mode

Minimize (the – button) collapses to a pill: status dot + starred stats (star
toggles live on each section header) + 📌-pinned watch-rule chips. Alert banners
render above the pill. Double-click or ⤢ restores.

## Click-through

Right-click menu → Click-through: the border turns amber and clicks pass through to
the game (WS_EX_TRANSPARENT); a small clickable 🔒 chip parks beside the widget as
the way back — a transparent window can't be clicked to unlock itself. Global
hotkeys were REMOVED (2026-08-06): RegisterHotKey is system-wide, so EQBuddy was
swallowing Ctrl+Shift+T (reopen browser tab) and friends from every app on the
machine — a player caught it in the wild. Linux: click-through re-entry pending
Don's design (issue #7); X11ClickThrough remains.

**Camp marker:** stamps a timestamped marker into the journal; shows under Travels &
Deaths. Intended use: "since I set up camp here" bookkeeping.

## Options window

**Theme** picker: Parchment & Brass (the original look, and the default so upgrades don't
change appearance), Blue Grey, Turquoise, Redish, Grey, Solarized, Solarized Dark, High
Contrast (near-opaque background and pure white text — the translucency that makes the
other themes pretty is what washes them out over a bright game scene), and Custom colors.
Both UIs offer all of them, and the saved `Theme` setting round-trips between them.

Custom colors: the user picks background, text, and accent (swatches or hex) and
`EQBuddy.UI.Shared.CustomTheme` derives the other fourteen keys, auto-correcting the
text color until it clears the same 4.5:1 contrast floor the built-ins are tested to.
The three colors are edited in the WPF app's Options only; the Avalonia app applies
whatever `CustomThemeBg/Text/Accent` hold in settings.json (editor parity: Don's lane).

The colors live once, as data, in `EQBuddy.UI.Shared.ThemePalettes` — 17 brush keys per
theme. WPF composes those into a `ResourceDictionary` at runtime and swaps it into
`Application.Resources`; since `Theme.xaml` holds only structure and reads every brush via
`DynamicResource`, the swap repaints open windows with no restart or reload. Avalonia
builds its UI in code, so it can't swap a dictionary: `AppTheme` keeps one permanent
`SolidColorBrush` per key and mutates `.Color`, which repaints everything still holding
that reference. It implements 14 of the 17 keys, ignoring the scrollbar/toggle ones its
own control themes handle.

Either way, colors baked in at construction can't repaint themselves — `BgWithOpacity`
returns a fresh brush, and the damage rows snapshot a color when built — so both UIs have
a `RefreshTheme()` that re-applies those and forces one rebuild after a switch.

Adding a theme = one row in `ThemePalettes.Values` plus an entry in `ThemeCatalog`.
`ThemePaletteTests` enforces that every catalogued theme has a palette, that every palette
defines every key with a parseable `#AARRGGBB` value, that an unknown theme falls back
instead of throwing, and that text clears 4.5:1 contrast against its background — the
check that caught Solarized's canonical body pairing at 4.1:1.

Sliders: widget size (80–160 %, scales fonts), background see-through (panel only —
text stays opaque), whole-widget opacity. Auto-empty toggle (see Log hygiene).
Recent-rate window (5/15/30 min). Watch-rule editor (kind dropdown, name, spell-class
picker for Spell fade rules, match text, 🔔/🔊 toggles, delete, add). Alert sound picker
+ ▶ test. Overlay cards: per-card up/down reorder and hide/show — hidden cards keep
collecting; layout persists.

**Width is draggable from either side.** The window has custom chrome (`WindowStyle=None`
+ `AllowsTransparency`), so there is no native resize border; transparent `Thumb`s on the
left and right edges drive `Width` instead, clamped to `OptionsWidth` (saved on release).
Dragging the left edge moves `Left` too so the right edge stays put. Both derive size from
the cursor's absolute position rather than accumulated `DragDelta` — the left grip moves
with the window, and accumulating would feed back into itself as jitter.

**Height is bounded by the monitor, and the body scrolls.** `SizeToContent="Height"` still
auto-fits, but `MaxHeight` is clamped at runtime to the work area of whichever monitor the
window is on (recomputed on `LocationChanged`, since monitors differ in size *and* DPI).
Without this, high Windows scaling makes the panel taller than the screen and the bottom is
simply unreachable — a tester running 300 % on a 4K TV could not see the lower half. The
title row sits outside the `ScrollViewer` so ✕ is always reachable.

**Watch-rule columns are labelled** (Watch / Name / Match). The header is a `Grid` sharing
`SharedSizeGroup`s with every rule row inside a `Grid.IsSharedSizeScope` panel, so labels
stay aligned however wide the combos render.

**Verify:** with two rules present (one Loot, one Spell fade set to a class), no field is
clipped at the default width — check "Any CC" in particular, since the class combo shares
the row with the alert toggles. Drag either edge wider, close, reopen: the width is
remembered. Add rules until the content exceeds your screen: the window stops growing at
the work-area height and a scrollbar appears rather than the bottom going off-screen.

## Session history

Automatic SQLite store (`<appdata>/history.db`). Sessions finalize on: 60 min idle
gap, character switch, app exit. Active session checkpoints every 5 min; crash
recovery marks interrupted sessions and re-adopts them on relaunch. Noise-only
sessions are never stored. **Dedup invariant:** the same (server, character,
session-start) never inserts twice — restarts with auto-empty off and repeated
imports update the existing row.

History window: character filter, live search (zone, loot, creature, notes, tags,
snapshot content), full per-session breakdown — Top damage sources and Top heals
render with the same bar rows as the live widget (total · ×hits · avg · dps/hps ·
crit%), falling back to text-only for sessions stored before active-time tracking —
notes + tags, copy summary (plain text with `█` share bars), JSON export, delete
(confirmed), **Ctrl-click two sessions to compare** rates side-by-side,
**Import log…** replays any eqlog into history (ImportedBoundary sessions;
re-importing the same file updates rows instead of duplicating).

**Verify:** fixture with an old block + live block → exactly one finished row + one
in-progress row; relaunch → still exactly those rows (dedup); import the same file
twice → no duplicates.

## What's new popup

Once per update (NOTES-001): at the first launch of a new version, a themed popup
lists the notable changes of every version skipped since the last one seen
(`AppSettings.LastSeenVersion`), newest first, then never again. Content is embedded
(`Core/Data/WhatsNew.json` via `WhatsNewCatalog`) — offline-friendly for the
OneDrive-updated family. Fresh installs skip it entirely (the tutorial owns
onboarding) and just record the baseline; installs from before the feature get one
version's worth rather than the whole history. `release.ps1` refuses to release a
version with no entry, so the popup can't silently rot.

**Verify:** profile with `ShowTutorial=false, LastSeenVersion=<older>` → popup lists
the skipped versions and `LastSeenVersion` advances; relaunch → no popup; fresh
profile → no popup.

## Updates

Checks at startup + every 6 h + on demand. Newer version → green banner; clicking it
installs and restarts, on Windows, from either source below. Nothing downloads or
installs without that click.

Local-first when a shared folder is configured: set `UpdateFolder` in settings (or drop
an `EQBuddyDownload` folder in a synced location, which is auto-discovered). Intended for
guild or LAN setups that would rather not have every machine pull from GitHub.

Otherwise the GitHub release itself is the source: EQBuddy reads the latest release's
assets and downloads `EQBuddySetup.exe` directly. **The published `.sha256` is required
for this path** — no hash, no download; the banner falls back to offering the release
page so the update is still visible and can be fetched by hand. Downloads stream to disk
with a 10-minute timeout (the 15 s used for the version probe covers the whole response
body and would abort a ~45 MB installer on a normal connection). A hash mismatch refuses
the install and deletes the staged file.

Linux always goes to the release page — the staged file is a Windows installer run with
Inno Setup's `/SILENT`, which has no meaning there.

### Linux and macOS: this build is the final one, and it keeps working

EQBuddy v2 ("Evolved") is Windows-only. **The Linux and macOS 1.x builds are not being
taken down** — this copy keeps working exactly as it does today, offline, with your
history and settings untouched. What stops is new work on this line, not this line.

So the update check changes on these two platforms, and only on these two:

- **A further 1.99.x update is still offered**, on every platform, exactly as before.
  Preserved does not mean welded shut.
- **A 2.x release is never offered here.** The automatic six-hourly check says so **once**
  — a short notice that v2 is Windows-only and that this copy stays on v1 and keeps
  working — and then stops. Help → "Check for updates" always answers with the same
  notice, because a check that returns silence would be a bug.
- **Clicking that notice opens the final v1 release page**, pinned to its own tag —
  never `releases/latest`, which becomes the v2 page the moment v2 ships and whose most
  prominent asset is a Windows installer you cannot run.
- **Nothing is ever downloaded, staged, run or overwritten off Windows.** There is no
  forced migration, no self-removal, and no history wipe.

The final Linux tarball and macOS `.app` bundles stay on GitHub permanently, together
with their release notes, tag and source state. The published 1.x tree is MIT, so
continuing it independently is welcome — an invitation, not a promise that anyone here
will maintain, review or host it.

The whole promise, the support matrix and the direct download links live in
**LEGACY-V1.md** in the repository:
<https://github.com/DranakCorps-bot/EQBuddy/blob/main/LEGACY-V1.md>, and the work is
tracked on <https://github.com/DranakCorps-bot/EQBuddy/issues/275>.

## Log hygiene

`Log=1` is forced in `eqclient.ini` ([Defaults] section only, byte-preserving
elsewhere) whenever the game isn't running. With auto-empty ON, logs quiet for
60+ min are truncated at startup/every 10 min — never while the game runs, and
(since the Reddit "this breaks GINA" report, 2026-08-02) **never while GINA or
GamParse is running either** (`EqConfig.IsLogReaderRunning`): those tools keep a
byte offset into the log, and emptying the file under them leaves that offset past
end-of-file, silently killing their triggers until restart. Reading never conflicts
— only truncation ever did. OFF = logs are never touched (uploader/GINA-friendly),
which the history dedup makes safe.

## Known limitations

- Invocations went unparsed until 2026-08-03 (they log nothing until you change one);
  "You begin reciting the &lt;name&gt; invocation." now drives DPS-by-invocation brackets.
  The "You begin to change your invocation." precursor is deliberately ignored.
- The World window's Camps tab is WPF-only so far (Avalonia parity: issue #5); Core/UI.Shared
  hold all the logic, so the port is a thin view.
- The History DPS-over-time graph, the Custom-theme color editor, the History
  fight-by-fight review (expandable per-encounter breakdowns), the mez-target
  chips, and the item-info popup are WPF-only; the data lives in Core/UI.Shared,
  so each is a thin view to port (the Avalonia app already *applies* stored custom
  colors, and its Combat card does show the last-fight incoming breakdown).
- The zone map — and with it the spawn-point circles and the Share zone knowledge
  window — is WPF-only so far (the Avalonia app has no map yet). SpawnPointLedger
  and ZoneShare live in Core, so the archives a Linux player's log builds are
  already there the day the map view is ported.
- Item info (click a loot row, or search in the popup): on-demand eqlwiki lookup —
  stats, vendor value, drops-from, sold-by, quests, recipes — with a 7-day cache
  and LIVE/CACHED/STALE source labels. One fetch per explicit request, nothing in
  the background; in-game "+N" upgrade suffixes are stripped (the wiki has base
  pages only). EqlWikiItemService in Core, fixture-tested against real saved
  wikitext.
- Mez chips: a re-landing REFRESHES the same-name chip (chain-mezzing and bard
  pulse songs both depend on this — issue #32); only same-second landings (an AoE
  catching same-named mobs) create separate, numbered chips, and a break clears
  one of those, not all. Legends' spell ranks ADD mez duration but eqlwiki doesn't
  say how much ("+?") — the caster-side learner (longest observed land→fade gap
  per ranked spell name, persisted to mez-durations.json) fills that in from play,
  and expired chips linger at 0:00 for a few seconds rather than vanishing, since
  a rank-lengthened mez can outlive the base timer.
- Spawn durations are community lore (eqlwiki flags its own timer pages as
  under-review); the edit box exists precisely because the defaults will be wrong
  somewhere.
- Hymn/regen ticks have no amounts in the log — counts only.
- Multi-mob fight attribution is heuristic; timeouts marked "?".
- Per-ability DPS = contribution over combat time (no cast timing in the log).
- Self-signed binaries: SmartScreen warns on first Windows install.
