# EQBuddy Roadmap Assessment & Implementation Plan

**Assessed:** `AdditionalRequirements.md` v3.0 (July 20, 2026) and PR #1 (Avalonia port)
**Decisions (D. Edwards):** MIT license · merge PR #1 with fixes · first push = Foundation + Release A

---

## 1. Assessment of the requirements document

**Overall verdict: adopt the direction.** The two-mode framing (Live Companion first,
History & Analysis second) matches what actually makes EQBuddy distinct — no other EQL
tool is a native, zero-setup, single-monitor overlay with automatic capture. The
architecture it prescribes (typed event journal → aggregators → SQLite) is the correct
prerequisite for everything else and matches how the code already wants to grow.

### Adopt as written

- **Tracked-loot rules + alerts (§9.3–9.4).** Highest value per effort. Substring rules,
  per-hour rates, distinct-name breakdowns, focus-safe alerts. Our logs already show the
  exact use case ("Mote of Infinitesimal Potential").
- **Session event journal (§10.1).** Correct diagnosis: incremental totals cannot do
  rolling windows, rule recalculation, or encounter reconstruction. This is the keystone.
- **Recent-window + active-time rates (§9.5–9.6).** Timestamp-based, never proportional
  estimates — correct requirement. Builds directly on the journal.
- **SQLite persistence + session lifecycle + crash recovery (§10.2–10.4).**
  Microsoft.Data.Sqlite, versioned migrations, app-data location, async repository. Right
  store, right rules.
- **Parser test suite + CI (§15.1, §15.3).** Overdue. Every category listed in TEST-004
  already exists in our parser and has real sanitized fixture lines available from this
  project's development history.
- **Reliability fixes (§15.4).** REL-001 (rollover callback fires inside the stats lock —
  confirmed real in `SessionStats.Apply`), REL-002 (INI editing isn't section-aware —
  confirmed), REL-004 (David's personal OneDrive path is hardcoded in public source —
  confirmed in `UpdateChecker.FindUpdateFolder`), REL-005 (no license — fixed with MIT).
- **Non-goals (§7).** Fully agreed, especially no game-memory reading and no telemetry.

### Adopt with modifications

- **Stance tracking (§13.2):** feasible now — logs confirm `"You begin to change your
  stance."` / `"You assume a ranged stance."` Add the parser fixture immediately; the
  analysis views come with Release D.
- **Invocation tracking (§13.3):** no invocation state lines observed in any log yet
  (only chat mentions). Gate on log evidence, exactly as LIVESTATE-003 requires. Ask
  family to note when they activate invocations so we can capture the lines.
- **Click-through + hotkeys (§9.7):** WS_EX_TRANSPARENT + RegisterHotKey are reliable on
  Windows. INPUT-011 ("temporary modifier to interact while click-through") is flaky in
  practice — implement toggle-hotkey only, revisit later.
- **Encounter reconstruction (§12):** hardest item in the document. Our damage events
  carry target names, which is the needed raw material, but multi-mob group fights are
  genuinely ambiguous. Phase it after persistence, keep the PRD's confidence/ambiguity
  fields — they are what makes this tractable.
- **Update security (§15.5):** publish SHA-256 hashes with each GitHub release and verify
  before silent-install from the OneDrive channel. Full code-signing trust remains
  self-signed (documented limitation).

### Defer (not wrong, just not yet)

- TrackedRuleVersion history, session undo/trash (STORE-013), UI automation tests
  (§20.3), diagnostic bundle hashing (§15.2), generalized watch rules (§14 — explicitly
  sequenced last by the PRD itself), character-specific overlay profiles (OVERLAY-006 —
  global settings only until someone asks), reproducible-build metadata (CI-003).

### Reject / correct

- Nothing structurally wrong. One correction: §16.5's `SourceLogIdentity` should not be
  the file path alone — with truncation-based pruning, identity must be
  (path + first-line timestamp) or a content hash of the session's first line, or
  re-attachment after pruning will misidentify sessions.

---

## 2. PR #1 (Avalonia port by Don Thompson) — verdict: merge with fixes

**Quality:** good. Clean Core extraction (`EQBuddy.Core` compiles existing files via
wildcard; WPF excludes them and references the project — no double compilation). Faithful
UI replica including mini mode, stars, sort bars, options. Official Avalonia 12.1
packages only; no concerning code. Registry lookup correctly guarded with
`OperatingSystem.IsWindows()`; `UpdateChecker` correctly switched to entry-assembly
version.

**Problems to fix at merge time (branch targets v1.5.1, main is v1.5.4):**

1. References removed `AppSettings.AlwaysOnTop` (+ pin button UI) — compile error; bring
   to v1.5.4 behavior (always topmost, no pin).
2. Ignores the v1.5.2 `TruncateLogs` setting in its startup janitor call; its Options
   window lacks the truncation checkbox — add both.
3. Its Options window predates the v1.5.3 positioning fix — apply the same
   manual-placement logic.
4. Hardcoded `Version 1.5.1` in the Avalonia csproj — sync with release version.

**Follow-ups after merge:** physically move `src/EQBuddy/Core` → `src/EQBuddy.Core`
(both the contributor and the PRD want this); add both UIs to CI; invite Don to continue
as the Avalonia/Linux maintainer (PRD §5.8 requires an independent maintainer for
cross-platform work — we may have found him).

---

## 3. Implementation plan

### Phase 0 — Foundation (this push, before features)

1. **MIT LICENSE** at repo root.
2. **Merge PR #1** with the fixes above; verify WPF publish still produces the single
   signed exe and the Avalonia app builds and launches on Windows.
3. **Restructure:** physically move Core files into `src/EQBuddy.Core`; add
   `EQBuddy.sln` (Core, WPF, Avalonia, Tests).
4. **Tests:** `tests/EQBuddy.Tests` (xUnit).
   - Parser fixtures: one test per line category (melee in/out incl. all verbs and
     annotation suffixes, school nukes, DoTs in/out, songs, damage shields, pet claim /
     blink / charm-break, heals in/out/self, regen ticks, loot both variants, auto-sell,
     merges, corpse coin / splits / vendor sales, XP/AA/level, skills, faction, zones,
     deaths/kills, resists both forms, fizzles, stance lines) — validating every parsed
     field, not just match success (TEST-005).
   - Aggregate replay tests: feed fixture event streams through `SessionStats` and assert
     final snapshots (DPS windows, pet crediting, avoidance, session rollover at 60 min).
   - `EqConfig` tests: ini rewrite preserves unrelated content; stale-log truncation.
5. **CI:** GitHub Actions on windows-latest — restore, build all projects, run tests, on
   PRs and main. Release gate = tests green (CI-005).
6. **Reliability fixes:**
   - REL-001: raise `SessionRolledOver` outside the stats lock.
   - REL-002: section-aware `eqclient.ini` editing (only touch `Log=` inside
     `[Defaults]`, preserve everything else byte-for-byte).
   - REL-003: swallowed catches route to `App.LogError` (respect the never-crash-parsing
     rule but stop being silent).
   - REL-004: delete the personal OneDrive path constant from `UpdateChecker`; rely on
     the existing OneDrive env-var discovery (David's own machine resolves identically).
   - Update integrity: release script computes SHA-256, publishes it in the GitHub
     release body; updater verifies the OneDrive installer against the released hash
     when reachable (fails open to current behavior offline, with a log entry).

### Phase 1 — Release A: Live Companion Advantage (same push)

Build order (each step shippable):

1. **SessionJournal** (`Core/SessionJournal.cs`): timestamped typed events appended by
   `SessionStats.Apply` (single lock family, events already flow through there);
   retention: full journal for loot/coin/XP/kill/death/marker events, ring-buffer with
   time-based compaction for combat swings (JOURNAL-004/005). Replayable into a fresh
   `SessionStats` (used by tests).
2. **Recent-window rates** (RATE-*): computed from the journal for XP, kills, money,
   tracked loot, DPS/HPS over 5/15/30-min windows; surfaced as "Session / Last 15m"
   pairs on existing headers. Default window in Options.
3. **Active-time rates** (ACTIVE-*): active = union of 2-minute buckets containing any
   meaningful event (combat, xp, loot, coin, zone, cast). Wall-clock and active rates
   both shown, never replaced (ACTIVE-005).
4. **Tracked-loot rules** (TRACK-*): `TrackedRule` (name, substring, enabled, pinned,
   alert config) persisted in settings; evaluation over the journal so mid-session edits
   recalculate (TRACK-020); rule results = total qty, per-item breakdown, qty/hour
   (wall + active), last/first match age. UI: "Tracked" section listing each rule like
   the requirements example, plus a mini-chip per pinned rule; rule editor in Options.
5. **Alerts** (ALERT-*): banner (existing update-banner pattern, auto-dismissing,
   focus-safe by construction), sound (`SystemSounds` default + optional custom .wav via
   `SoundPlayer`, volume via settings), card flash; per-rule channel toggles; 5-s
   cooldown aggregation per rule (ALERT-008); test button in Options.
6. **Overlay cards** (OVERLAY-*): sections become orderable/hideable via settings
   (list of section keys in order + hidden set); drag-to-reorder deferred — v1 is
   up/down controls in Options; hidden sections keep collecting (they already do —
   aggregation is UI-independent). Reset-to-default action.
7. **Hotkeys + click-through** (INPUT-*): `RegisterHotKey` service (default:
   Ctrl+Shift+H hide/show, Ctrl+Shift+T click-through, Ctrl+Shift+M mini toggle,
   Ctrl+Shift+K camp marker); click-through via WS_EX_TRANSPARENT with a visible dot
   state; hotkeys editable as text in settings (validation), conflicts reported.
8. **Camp markers** (part of INPUT-005/§9.8 groundwork): `SessionMarkerEvent` into the
   journal; markers shown in the Travels section; per-marker "since marker" rates
   deferred to segments work in Release B/C.

### Phase 2+ (subsequent pushes, per PRD Releases B→E)

B: SQLite store, session lifecycle/checkpointing, History MVP.
C: encounters, reward correlation, mob farming, personal drop rates.
D: level/stance windows (invocations when log evidence exists), damage-source scopes.
E: generalized watch rules, diagnostics bundle.

---

## 4. Verification

- Full parser test suite green locally and in CI.
- Replay: yesterday's archived Kaybek/Douglas/Caybin logs produce identical snapshot
  numbers before/after the journal refactor (golden fixtures).
- Manual: live widget against the real Logs folder; create a `mote` rule; kill something
  that drops a mote (Crushbone) → card counts, banner + sound fire once; toggle
  click-through and hotkeys in-game; reorder cards; verify OneDrive + GitHub update flows
  still work end-to-end (family silent install unaffected).
- Avalonia: builds in CI; smoke-launch on Windows; ask Don to validate on Linux.
