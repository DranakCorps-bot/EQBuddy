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
    polls every 500 ms. The status dot is green only if the file grew in the last 30 s.
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
  avoidance %, fizzles/resists, current stance.
- **Damage by attack** — Details!-style breakdown: each source shows
  `total · ×hits · avg · dps (· crit%)`. The dps follows parser convention:
  **that ability's damage ÷ total time in combat** — its contribution rate, which
  falls the longer you go without using it. The tooltip adds share-of-total and the
  **burst rate** (damage ÷ the ability's own active time: consecutive hits within
  10 s accumulate real spacing, an isolated hit counts ~2.5 s). Sort bar:
  total/dps/hits/avg — the bar behind each row is proportional to whichever column
  is sorted.
  Pet damage appears as "Pet (Name)" — provisional charm pets as "Pet? (Name)" until a
  "Master" tell confirms them. Pets show no crit % (the log doesn't annotate pet crits).
- **Damage taken from** per attacker (total · hits · avg).
- **Recent fights** — last 8 encounters: creature, duration, per-fight DPS, with a
  bar comparing each fight's DPS to the hottest recent fight. A fight opens on
  damage, closes on the kill line or a 20 s timeout ("· ?" marks timeouts).
  Back-to-back same-name kills are distinct fights.
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

### Kills card
Header: your kills (+ group kills). Details: per-creature counts, kills/hour +
recent-window kills, **Farming (per creature)**: avg fight length · coin · xp% per
creature, then each creature's observed drops indented with `×count · drop%`
(drop % can exceed 100 for multi-drops). Group kills by member below.
Coin/XP attribution uses a 3 s window around the kill line **in both directions**
(rewards are logged *before* the kill line in live play). Loot→creature attribution is
by corpse name, which the log always includes (even with the advanced loot window).

**Verify:** fixture with kills + loot + coin. Farming coin/xp must be non-zero for
coin/xp-giving kills; animals and gray cons legitimately show 0.

### Loot card
Every looted item with counts (both the `--You have looted…--` form and the auto-sell
form), plus "Created by merging". Auto-sold loot counts as loot AND merchant income.
Selling from the advanced loot window ("You successfully destroyed N X." followed by
"You received … from that item.") is paired into a named merchant sale.

### Tracked card (watch rules)
Rules are defined in Options: **Kind** (Loot / Kill / SkillUp / Death / Milestone /
SpellFade) + name + match text (case-insensitive substring; the name doubles as match
text if the match box is empty; Death/Milestone match everything when empty).
SpellFade matches "Your X spell has worn off (of Y)." by spell name — the mez/charm
break alarm; entries show as "Spell (Target)". Each rule shows
total, per-item breakdown, per-hour rates (wall-clock + active-time), last-match age.
Rules are evaluated over the whole session journal, so editing a rule mid-session
recalculates history, and alerts never fire during startup ingest or character switch.

**Alerts:** 🔔 banner + 🔊 sound per rule, 5 s per-rule cooldown. The banner is a
**floating tile**, independent of the widget: always on top, permanently
click-through, never takes focus, auto-dismisses ~6 s. Position it by opening
Options — the tile appears in placement mode ("drag me") and saves its spot on
close; in play, clicks pass straight through it to the game. Sound is global in Options: seven named Windows Media
sounds or a custom .wav/.mp3, with a ▶ preview. (Linux: sound backend TBD.)

**Verify:** create a Loot rule matching an item in your fixture, append the loot line
live → counter increments, banner pops (also while minimized), sound plays once even
if two matches land within 5 s.

### Money card
Corpse coin vs merchant income, drops count, biggest drop, per-hour rates (wall +
active), everything sold with per-item totals.

### Progress card
XP gains + %/hr (session and recent window), AA points + AA/hr, estimated time to
next level (exact after a level-up this session, else an upper bound), level-ups with
**time-in-level**, skill-ups per skill.

### Faction / Travels & Deaths cards
Net faction standing per faction. Deaths (killer + time), zones visited with times,
camp markers.

## Mini mode

Minimize (or `Ctrl+Shift+M`) collapses to a pill: status dot + starred stats (star
toggles live on each section header) + 📌-pinned watch-rule chips. Alert banners
render above the pill. Double-click or ⤢ restores.

## Global hotkeys & click-through

Defaults (editable as text in settings.json; conflicts/invalid bindings are reported
in error.log): `Ctrl+Shift+H` show/hide · `Ctrl+Shift+T` click-through (border turns
amber; clicks pass through to the game) · `Ctrl+Shift+M` mini · `Ctrl+Shift+K` camp
marker. Windows: RegisterHotKey + WS_EX_TRANSPARENT. Linux: X11 implementation;
Wayland and Wine-fullscreen topmost are known-limited (issue #2 discussion).

**Camp marker:** stamps a timestamped marker into the journal; shows under Travels &
Deaths. Intended use: "since I set up camp here" bookkeeping.

## Options window

Sliders: widget size (80–160 %, scales fonts), background see-through (panel only —
text stays opaque), whole-widget opacity. Auto-empty toggle (see Log hygiene).
Recent-rate window (5/15/30 min). Watch-rule editor (kind dropdown, name, match text,
📌/🔔/🔊 toggles, delete, add). Alert sound picker + ▶ test. Overlay cards: per-card
up/down reorder and hide/show — hidden cards keep collecting; layout persists.

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

## Updates

Checks a OneDrive-synced folder (auto-discovered `EQBuddyDownload`, or `UpdateFolder`
in settings) at startup + every 6 h + on demand. Newer version → green banner → click
installs silently and restarts. The staged installer's SHA-256 is verified against
the GitHub release; mismatch refuses the install, offline fails open with a log
entry. GitHub-only installs get a banner linking the release page.

## Log hygiene

`Log=1` is forced in `eqclient.ini` ([Defaults] section only, byte-preserving
elsewhere) whenever the game isn't running. With auto-empty ON, logs quiet for
60+ min are truncated at startup/every 10 min (never while the game runs). OFF =
logs are never touched (uploader-friendly), which the history dedup makes safe.

## Known limitations

- Invocations produce no known log lines — unparsed until evidence exists.
- Hymn/regen ticks have no amounts in the log — counts only.
- Multi-mob fight attribution is heuristic; timeouts marked "?".
- Per-ability DPS = contribution over combat time (no cast timing in the log).
- Self-signed binaries: SmartScreen warns on first Windows install.
