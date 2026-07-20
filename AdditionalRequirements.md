# EQ Buddy Product Requirements

**Document version:** 3.0  
**Date:** July 20, 2026  
**Product owner:** David Edwards  
**Repository:** https://github.com/DranakCorps-bot/EQBuddy  
**Status:** Approved direction for implementation planning with Claude Code  
**Supersedes:** `EQ_Buddy_Enhancement_Requirements_v2.md`

---

## 1. Executive Summary

EQ Buddy should evolve into the definitive **single-monitor live companion and automatic historical session library for EverQuest Legends**.

The product must retain and strengthen the advantage that makes it useful during actual play: it can remain visible over the game in a compact, native, always-on-top interface. A browser-based parser may process a live log, but on a single-monitor setup the user generally must switch windows, obscure part of the game with a browser, or maintain a second display. EQ Buddy should be designed specifically to avoid that friction.

EQ Buddy should therefore be developed as two connected experiences:

1. **Live Companion Mode** — a compact, customizable overlay that surfaces the most useful information while the player is actively playing, without requiring Alt-Tab.
2. **History & Analysis Mode** — a larger offline window that automatically preserves sessions by character and server, then allows the user to browse, search, compare, and analyze those sessions later.

The highest-priority enhancements are those that strengthen the live experience:

- User-defined tracked loot, such as `mote`, with counts, matching item names, and per-hour rates.
- Configurable visual and audio alerts for important drops and log events.
- Customizable overlay cards and layouts.
- Recent-window and active-play rates in addition to whole-session averages.
- Hotkeys, click-through support, and one-click camp/session markers.
- Current stance, invocation, encounter, and recent-fight information where it can be shown compactly.

EQ Buddy should then automatically save structured session data locally so the user can later review:

- Sessions by character and server.
- Encounters and creatures killed.
- XP, coin, loot, damage, healing, deaths, skills, faction, crafting, and travel.
- Mob-specific farming performance and personal drop rates.
- Level, stance, and invocation performance.
- Tracked-item results across sessions.
- Comparisons between sessions, camps, builds, or time periods.

The product should selectively incorporate the strongest analytical capabilities currently associated with tools such as EQLTools, but it should not become a dense browser-style parser inside the live overlay. Detailed tables, timelines, and comparisons belong in History & Analysis Mode.

---

## 2. Product Vision

> EQ Buddy automatically tells an EverQuest Legends player what matters while they play, remembers the entire session for them, and makes that history easy to learn from later.

EQ Buddy should answer three classes of questions.

### 2.1 Questions during active play

- How much damage am I doing right now?
- Is my current XP rate improving or declining?
- How long until the next level?
- How many kills have we completed?
- How many items matching `mote` have dropped?
- What were the names of those matching items?
- How many matching items am I receiving per hour?
- Did the rare item I care about just drop?
- Is this camp outperforming the previous camp?
- Which stance and invocation are active?
- How long has the current fight lasted?
- How much of the session has been active play versus downtime?

### 2.2 Questions after a session

- Which part of the session produced the best XP/hour?
- Which creatures produced the most coin or tracked loot?
- Which encounter had the highest DPS or damage taken?
- How did one stance/invocation combination compare with another?
- How much time was spent fighting versus idle?
- What dropped during the session, and from which creatures?
- How did this camp compare with the last time I played here?

### 2.3 Questions across historical sessions

- Which character, zone, and camp produced the best XP/hour?
- How many motes have I collected over the last month?
- Which mob has historically produced a particular item for me?
- What is my observed personal drop rate for that item?
- How long did each level take?
- Is my build improving over time?
- Which stance, invocation, weapon, song setup, or group composition performed best?
- Which saved session should I reopen to inspect its encounters or loot?

---

## 3. Product Positioning

### 3.1 Primary identity

EQ Buddy is a **live companion first** and an **offline historical analyzer second**.

The default experience must remain:

- Automatic.
- Native to Windows.
- Compact.
- Always available without leaving the game.
- Understandable at a glance.
- Useful with minimal or no setup.
- Local-first and private.

### 3.2 Core differentiation

EQ Buddy must differentiate through the complete workflow:

```text
Automatically detect active character
              ↓
Show useful information over the game
              ↓
Alert the player to important events
              ↓
Record the session without manual action
              ↓
Save it under the correct character/server
              ↓
Allow offline browsing and comparison later
```

The defining product statement is:

> **A native, single-monitor EverQuest Legends companion that surfaces important information during play and automatically builds a complete, navigable history without log uploads or manual session management.**

### 3.3 Relationship to deeper log parsers

EQ Buddy should incorporate analytical capabilities that make sense for its users, including:

- Encounter-by-encounter analysis.
- Mob-level XP, coin, loot, and drop-rate summaries.
- Level-scoped analysis.
- Stance and invocation analysis.
- Damage-source composition.
- Existing-log import.

However:

- The live overlay must not become a large report or dense spreadsheet.
- Advanced analysis must not be required to use the product.
- Detailed data should be available in a separate window opened intentionally.
- EQ Buddy should prioritize automatic capture and live usefulness over duplicating every table in another parser.

---

## 4. Product Modes

### 4.1 Live Companion Mode

The live experience consists of:

1. **Compact Overlay** — the minimum information the player wants visible continuously.
2. **Expanded Live View** — additional current-session detail without entering the full historical interface.
3. **Transient Alerts** — temporary banners, sounds, highlights, or notifications for important events.

The compact overlay must be suitable for a single 1920×1080 monitor while EverQuest Legends is running in windowed or borderless-windowed mode.

### 4.2 History & Analysis Mode

The history experience is a normal resizable application window containing:

- Character and server navigation.
- Saved session list.
- Session overview.
- Timeline and segments.
- Encounter list.
- Mob farming summaries.
- Loot and tracked-item analysis.
- Stance/invocation analysis.
- Comparisons and trends.
- Import and export functions.

History & Analysis Mode must work when EverQuest Legends is not running and without reopening or reparsing the original log file for normal saved-session navigation.

---

## 5. Product Principles

### 5.1 Single-monitor usefulness is mandatory

A live feature is not complete merely because its data updates in real time. It must be presented in a form the user can consume while actively playing on one monitor.

### 5.2 Lightweight by default

The overlay must minimize visual obstruction, input interference, CPU use, disk activity, and configuration burden.

### 5.3 Local-first

Parsing, session storage, settings, and analysis must remain on the user’s computer unless the user explicitly exports data.

### 5.4 Accurate before elaborate

Incorrect attribution, rates, or encounter correlation are more harmful than missing charts. Parser correctness and transparency take priority over visualization breadth.

### 5.5 Automatic capture, intentional analysis

The player should not need to start, upload, or save a normal session manually. Detailed analysis should be available when the player chooses to open it.

### 5.6 Preserve provenance and uncertainty

Estimated, provisional, inferred, imported, or incomplete values must be distinguishable from directly parsed facts.

### 5.7 Historical data must be durable

A reboot, application crash, character switch, log rollover, or application upgrade must not silently destroy valid saved history.

### 5.8 Avoid premature platform expansion

The core should be separable from WPF where practical, but cross-platform UI work must not delay the Windows live companion and historical-session roadmap.

---

## 6. Goals

### 6.1 Immediate goals

- Add parser regression tests and continuous integration.
- Fix known reliability, configuration, and updater risks.
- Add multiple saved tracked-loot rules.
- Add configurable live alerts.
- Add customizable overlay cards.
- Add recent-window and active-play metrics.
- Introduce a timestamped session event journal.
- Introduce automatic local session persistence by character and server.

### 6.2 Medium-term goals

- Add a full offline History & Analysis Mode.
- Reconstruct and save individual encounters.
- Correlate kills, XP, coin, and loot to encounters and creature types.
- Add mob farming summaries and personal drop-rate calculations.
- Add session segments and camp markers.
- Add stance, invocation, and level analysis.
- Add saved-session comparison and historical tracked-item analysis.
- Add existing-log import.

### 6.3 Long-term goals

- Generalize tracked loot into user-defined log watch rules.
- Add richer historical trends and optional charts.
- Extract a reusable, UI-independent core library.
- Consider cross-platform clients only after the core Windows roadmap is stable.

---

## 7. Non-Goals

The following are outside the scope of this roadmap:

- Gameplay automation.
- Input broadcasting.
- Game-memory reading.
- Packet inspection.
- Hidden map, spawn, or entity information unavailable through player-visible logs.
- Cloud accounts or required synchronization.
- Server-wide or guild-wide data collection.
- A full ACT plugin ecosystem.
- Retaining every raw combat line forever.
- Replacing the WPF interface before the session engine and persistence model stabilize.
- Making advanced charts or detailed encounter tables part of the default compact overlay.
- Regular-expression syntax as a requirement for normal tracked-item use.

---

## 8. Release Strategy

### Release A — Live Companion Advantage

**Theme:** Make EQ Buddy more valuable while the player is actively playing on one monitor.

Required scope:

- Parser regression test foundation.
- Reliability and project-hygiene fixes.
- Multiple tracked-loot rules.
- Tracked-loot overlay cards.
- Sound and visual alerts.
- Overlay card selection and ordering.
- Recent-window metrics.
- Active-time versus wall-clock rates.
- Overlay hotkeys and click-through behavior.
- Manual camp/segment marker.
- Current-session event journal.

### Release B — Automatic Session Library

**Theme:** Record and preserve every meaningful session without manual work.

Required scope:

- SQLite persistence.
- Automatic session lifecycle.
- Periodic active-session checkpointing.
- Crash recovery.
- Per-server and per-character organization.
- History & Analysis Mode MVP.
- Session search, filters, notes, tags, deletion, and export.
- Historical tracked-loot results.

### Release C — Encounters and Farming Intelligence

**Theme:** Explain what happened at the fight, creature, camp, and session levels.

Required scope:

- Encounter reconstruction.
- Encounter list and detail view.
- Kill/XP/coin/loot correlation.
- Mob farming summaries.
- Personal observed drop rates.
- Session segments and segment comparison.
- Session comparison.
- Current/recent encounter summary in Live Mode.

### Release D — EQL-Specific Build Analysis

**Theme:** Add the deeper analysis that meaningfully supports EverQuest Legends builds.

Required scope:

- Level windows.
- Stance tracking.
- Invocation tracking.
- Stance/invocation combination analysis.
- Damage-source composition.
- Existing-log import and reconstruction.
- Scope selector for session, segment, level, recent window, mob, and encounter.

### Release E — Generalized Watch Rules and Extensibility

**Theme:** Let users define what matters to them without turning the product into a scripting environment.

Potential scope:

- General text-event watch rules.
- Faction, skill-up, death, spell, and named-mob alerts.
- Rule templates.
- Diagnostic bundle export.
- Core library extraction.
- Optional cross-platform work only if it has an independent maintainer.

---

# 9. Functional Requirements — Live Companion Mode

## 9.1 Automatic log and character monitoring

### Requirements

- **LIVE-001:** EQ Buddy must locate the configured EverQuest Legends log directory automatically when possible.
- **LIVE-002:** The user must be able to select a custom log directory.
- **LIVE-003:** The selected directory must persist across application restarts.
- **LIVE-004:** EQ Buddy must detect the active character from log activity without requiring routine manual file selection.
- **LIVE-005:** Switching active characters must safely finalize, pause, or checkpoint the previous session according to the session-lifecycle rules.
- **LIVE-006:** Log truncation, rollover, recreation, temporary lock, and delayed writes must not crash the application.
- **LIVE-007:** The overlay must clearly identify the active character and server whenever that information is known.
- **LIVE-008:** The application must show a non-blocking status when no active log is available.

### Acceptance criteria

- Starting EQ Buddy while the game is already logging attaches to the appropriate active log.
- Starting the game after EQ Buddy causes monitoring to begin without restarting EQ Buddy.
- A character switch does not merge statistics from two characters into one session.
- A custom log path remains selected after reboot.

---

## 9.2 Customizable overlay cards

### Requirements

- **OVERLAY-001:** Users must be able to choose which information cards appear in the compact overlay.
- **OVERLAY-002:** Users must be able to reorder visible cards.
- **OVERLAY-003:** Users must be able to collapse or hide individual cards.
- **OVERLAY-004:** The overlay must support at least compact and expanded density presets.
- **OVERLAY-005:** The application must remember card visibility, order, density, position, size, and transparency.
- **OVERLAY-006:** Overlay configuration may be global or character-specific; if both are supported, character-specific settings override global defaults.
- **OVERLAY-007:** A reset-to-default-layout action must be available.
- **OVERLAY-008:** The compact overlay must avoid horizontal scrolling at common sizes.
- **OVERLAY-009:** Cards with no current data should collapse gracefully or display a concise empty state.

### Initial card types

- Current fight.
- Recent fights.
- Session DPS.
- Damage taken.
- Healing and HPS.
- XP/hour and time to level.
- AA XP/hour.
- Kills/hour.
- Money/hour.
- Tracked loot.
- Deaths.
- Current stance/invocation.
- Active versus idle time.
- Session duration.

### Acceptance criteria

- A user can configure a small layout containing only XP, current DPS, and a `mote` tracker.
- The configured layout is restored on the next launch.
- Hidden cards continue to collect data unless the user explicitly disables collection.

---

## 9.3 Multiple tracked-loot rules

### User objective

A player can enter a simple string such as `mote` and see all matching items, their quantities, and their rates during the current session.

### Requirements

- **TRACK-001:** Users must be able to create multiple tracked-loot rules.
- **TRACK-002:** The default match type must be case-insensitive substring matching.
- **TRACK-003:** Each rule must have a user-editable display name.
- **TRACK-004:** Each rule must store its search text.
- **TRACK-005:** Each rule may be enabled or disabled without deletion.
- **TRACK-006:** Each rule may be pinned to or removed from the overlay.
- **TRACK-007:** Each rule must show total matching quantity for the current session.
- **TRACK-008:** Each rule must list distinct matching item names and quantities.
- **TRACK-009:** Each rule must show matching quantity per wall-clock hour.
- **TRACK-010:** Each rule must show matching quantity per active-play hour when active-time data is available.
- **TRACK-011:** Each rule must show the timestamp or relative age of the most recent match.
- **TRACK-012:** Each rule should show time since the first match.
- **TRACK-013:** Each rule should show drops per kill after encounter/kill correlation is available.
- **TRACK-014:** Each rule should show matching creatures after loot-source correlation is available.
- **TRACK-015:** Matching must use normalized item names while preserving the original parsed display name.
- **TRACK-016:** Quantity-bearing loot lines must count the parsed quantity, not merely one event.
- **TRACK-017:** Duplicate log reads must not double-count tracked loot.
- **TRACK-018:** Rules must persist across application restarts.
- **TRACK-019:** Rules may be global or character-specific.
- **TRACK-020:** Editing a rule during a session must recalculate from the in-memory current-session loot journal when possible.
- **TRACK-021:** The first implementation must not require regular expressions.
- **TRACK-022:** Future exact-match and wildcard modes may be added without changing stored rule identity.

### Example

```text
MOTES
14 total · 6.8/hour

Crystallized Fire Mote       7
Crystallized Water Mote      4
Faint Mote of Shadow         3

Last drop: 2m ago
```

### Acceptance criteria

Given session loot containing:

- `Crystallized Fire Mote` × 2
- `Faint Mote of Shadow` × 1
- `Spider Silk` × 3

A rule with search text `mote` reports total quantity 3, lists the two matching names, and excludes Spider Silk.

---

## 9.4 Configurable live alerts

### Requirements

- **ALERT-001:** A tracked-loot rule may trigger an alert when a matching item is received.
- **ALERT-002:** Alert channels must support at least:
  - Temporary overlay banner.
  - Sound.
  - Card highlight or flash.
  - Windows notification where supported.
- **ALERT-003:** Each rule must allow alert channels to be enabled independently.
- **ALERT-004:** The user must be able to test an alert from settings.
- **ALERT-005:** Sound volume must be configurable.
- **ALERT-006:** Alerts must not capture keyboard or mouse focus.
- **ALERT-007:** Duplicate parsed events must not trigger duplicate alerts.
- **ALERT-008:** High-frequency matching items must support an alert cooldown or aggregation window.
- **ALERT-009:** The alert must include the matched item name and quantity.
- **ALERT-010:** When source correlation is available, the alert may include the source creature.
- **ALERT-011:** Alerts must be suppressible while the user is marked idle or while the overlay is hidden, as a user preference.
- **ALERT-012:** Alert failures must be logged without interrupting parsing.

### Example

```text
RARE DROP
Crystalline Spider Fang
1 received from a crystal spider
```

### Acceptance criteria

- Enabling banner and sound for a tracked item produces one banner and one sound per qualifying event.
- The banner disappears automatically and does not steal focus from the game.

---

## 9.5 Recent-window metrics

### Requirements

- **RATE-001:** Where meaningful, the overlay must distinguish whole-session rates from recent-window rates.
- **RATE-002:** Supported recent windows must include at least 5, 15, and 30 minutes.
- **RATE-003:** The user must be able to select a default recent window.
- **RATE-004:** Initial recent-window metrics should include XP/hour, kills/hour, money/hour, tracked loot/hour, DPS, HPS, and damage taken/minute where sufficient data exists.
- **RATE-005:** A metric must clearly indicate when the window contains insufficient data.
- **RATE-006:** Rolling-window calculations must be based on timestamped events, not a proportional estimate of session totals.
- **RATE-007:** Recent rates must remain correct across UI refreshes and window hiding.
- **RATE-008:** The live card should make session and recent rates visually distinguishable without becoming dense.

### Example

```text
XP/hour
Session: 8.4%
Last 15m: 11.7%
Active play: 12.3%
```

---

## 9.6 Active-play and idle-time handling

### Requirements

- **ACTIVE-001:** EQ Buddy must track wall-clock session duration.
- **ACTIVE-002:** EQ Buddy should calculate active-play duration using documented activity rules.
- **ACTIVE-003:** Activity signals may include combat, XP, loot, movement-related zone events, skill activity, healing, casting, and user-defined markers.
- **ACTIVE-004:** Idle detection thresholds must be configurable.
- **ACTIVE-005:** Both wall-clock and active-play rates must remain available; the application must not silently replace one with the other.
- **ACTIVE-006:** Idle periods must be visible in History Mode.
- **ACTIVE-007:** The user should be able to correct an incorrectly classified idle interval in History Mode.
- **ACTIVE-008:** Active-time calculations must be deterministic and testable.

---

## 9.7 Overlay interaction, click-through, and hotkeys

### Requirements

- **INPUT-001:** The overlay must support a click-through mode.
- **INPUT-002:** A global hotkey must toggle click-through mode.
- **INPUT-003:** A global hotkey must show or hide the overlay.
- **INPUT-004:** A global hotkey should switch compact and expanded Live Mode.
- **INPUT-005:** A global hotkey should create a new segment/camp marker.
- **INPUT-006:** A global hotkey should add a quick note or timestamp marker.
- **INPUT-007:** A global hotkey may manually end the current session.
- **INPUT-008:** Hotkeys must be user-configurable.
- **INPUT-009:** Conflicting or invalid hotkeys must produce a clear validation message.
- **INPUT-010:** The overlay must never intercept game input while click-through is active.
- **INPUT-011:** A temporary modifier may allow interaction with a click-through overlay if technically reliable.
- **INPUT-012:** The current click-through state must be visually discoverable without being distracting.

---

## 9.8 Current and recent encounter summaries

### Requirements

- **LIVEFIGHT-001:** Live Mode must continue to show current-fight duration and current-fight metrics.
- **LIVEFIGHT-002:** Once encounter reconstruction exists, Live Mode should show a compact list of recent completed encounters.
- **LIVEFIGHT-003:** Recent encounter rows should include creature name, duration, and group or player DPS as configured.
- **LIVEFIGHT-004:** Selecting a recent encounter may open its detail in the expanded view or History Mode.
- **LIVEFIGHT-005:** The compact overlay must limit the number of visible recent encounters.
- **LIVEFIGHT-006:** Encounter detail must not be forced into the compact overlay.

Example:

```text
Current fight               412 DPS
────────────────────────────────────
Last 3 fights
Ghoul knight       42s      381 DPS
Ghoul knight       39s      407 DPS
Greater skeleton   21s      465 DPS
```

---

## 9.9 Current stance and invocation display

### Requirements

- **LIVESTATE-001:** When reliably parsable, the overlay may show the current stance.
- **LIVESTATE-002:** When reliably parsable, the overlay may show active invocation state.
- **LIVESTATE-003:** Unknown or ambiguous state must be shown as unknown rather than inferred without evidence.
- **LIVESTATE-004:** A state change may optionally create a timeline marker.
- **LIVESTATE-005:** Full comparative analysis belongs in History & Analysis Mode.

---

# 10. Functional Requirements — Session Journal and Persistence

## 10.1 Timestamped current-session event journal

### Purpose

The existing incremental totals are insufficient for rolling windows, tracked-item recalculation, encounter reconstruction, source correlation, historical navigation, and comparisons. EQ Buddy needs a structured event journal.

### Requirements

- **JOURNAL-001:** Every meaningful parsed event must carry a timestamp.
- **JOURNAL-002:** The journal must retain all current-session loot, coin, XP, AA, kill, level, death, faction, skill-up, zone, crafting, sale, session marker, stance, and invocation events.
- **JOURNAL-003:** The journal must retain sufficient combat detail to reconstruct current and recent encounters.
- **JOURNAL-004:** Long sessions must not cause unbounded memory growth.
- **JOURNAL-005:** Raw combat swings may be compacted or aggregated after their encounter is finalized, provided required analysis remains possible.
- **JOURNAL-006:** The journal must preserve source line position or a stable event identity sufficient to prevent duplicate processing.
- **JOURNAL-007:** Events must record parser confidence/provenance where attribution is provisional or inferred.
- **JOURNAL-008:** Event creation and aggregation must be independent of whether a UI card is visible.
- **JOURNAL-009:** The journal must support replay into a fresh session aggregator for testing and recovery.

### Suggested base contract

```csharp
public abstract record GameEvent(
    DateTimeOffset Timestamp,
    EventProvenance Provenance,
    string EventId);
```

Representative event types:

```csharp
LootReceivedEvent
CoinReceivedEvent
ExperienceGainedEvent
AaExperienceGainedEvent
DamageEvent
HealingEvent
KillEvent
DeathEvent
ZoneChangedEvent
SkillIncreasedEvent
FactionChangedEvent
CraftingEvent
VendorSaleEvent
PetOwnershipEvent
StanceChangedEvent
InvocationChangedEvent
SessionMarkerEvent
```

---

## 10.2 Persistent session store

### Storage decision

Use **SQLite** as the primary session-history store.

### Requirements

- **STORE-001:** Completed sessions must persist locally in SQLite.
- **STORE-002:** The database must be stored under the user’s application-data directory, not beside the executable.
- **STORE-003:** Schema migrations must be versioned and automatic.
- **STORE-004:** Migrations must be transactional where supported.
- **STORE-005:** Existing history must survive application upgrades.
- **STORE-006:** The database must support servers, characters, sessions, segments, encounters, rewards, tracked-rule results, state windows, notes, tags, and aggregate summaries.
- **STORE-007:** Database writes must not block log parsing or the overlay UI.
- **STORE-008:** Batched writes and prepared commands should be used where practical.
- **STORE-009:** The repository layer must expose asynchronous APIs.
- **STORE-010:** A storage failure must be logged and surfaced non-destructively.
- **STORE-011:** EQ Buddy must provide a backup/export option for its history database or a portable data package.
- **STORE-012:** EQ Buddy must provide a documented recovery path for a corrupted database.
- **STORE-013:** Deleting a session must require confirmation and support an optional undo/trash period if practical.
- **STORE-014:** Stored timestamps must use an unambiguous format such as UTC plus local offset metadata.
- **STORE-015:** The data model must preserve imported versus automatically recorded sessions.

---

## 10.3 Automatic session lifecycle

### Session identity

A session is associated with:

- Server.
- Character.
- Start timestamp.
- End timestamp.
- End reason.
- Source log identity.
- Application version.
- Parser/schema version.

### Requirements

- **SESSION-001:** EQ Buddy must start or resume an active session automatically when meaningful activity is detected.
- **SESSION-002:** Merely discovering a log file must not create a permanent empty session.
- **SESSION-003:** A configurable meaningful-session threshold must prevent storage of trivial noise-only sessions.
- **SESSION-004:** Character switches must not merge sessions.
- **SESSION-005:** Manual session end must be supported.
- **SESSION-006:** Configurable inactivity may end a session.
- **SESSION-007:** Application shutdown must finalize or checkpoint the session safely.
- **SESSION-008:** Log rollover or truncation must not automatically lose the current logical session.
- **SESSION-009:** A new session may begin after a configurable inactivity threshold even if the same log file remains active.
- **SESSION-010:** End reasons must be stored.

Suggested end reasons:

```text
Manual
ApplicationExit
CharacterChanged
IdleTimeout
LogSourceChanged
ImportedBoundary
RecoveredAfterCrash
Unknown
```

---

## 10.4 Active-session checkpointing and crash recovery

### Requirements

- **RECOVERY-001:** Active sessions must be checkpointed periodically.
- **RECOVERY-002:** Checkpoints must include the last processed log position or stable event identity.
- **RECOVERY-003:** Checkpoints must include sufficient aggregate and journal state to resume without double-counting.
- **RECOVERY-004:** On startup, EQ Buddy must detect an interrupted active session.
- **RECOVERY-005:** The application must either resume the interrupted session or finalize it with a recovery reason according to deterministic rules.
- **RECOVERY-006:** Recovery must not re-alert old tracked-loot events.
- **RECOVERY-007:** Recovery behavior must be covered by integration tests.

---

# 11. Functional Requirements — History & Analysis Mode

## 11.1 Character and session hierarchy

### Requirements

- **HISTORY-001:** History must be organized first by server and then by character.
- **HISTORY-002:** Each character must have a summary page.
- **HISTORY-003:** Sessions must be listed in reverse chronological order by default.
- **HISTORY-004:** The user must be able to navigate to previous and next sessions.
- **HISTORY-005:** The selected server, character, filters, and session should be restored when practical.
- **HISTORY-006:** History Mode must work with the game closed.

Suggested hierarchy:

```text
Server
└── Character
    ├── July 20 — Lower Guk — 2h 14m
    ├── July 19 — Befallen — 1h 31m
    └── July 18 — Unrest — 3h 02m
```

---

## 11.2 Session list, search, and filters

### Requirements

- **HISTORY-010:** The session list must show date, start time, duration, primary zone, level range, and key summary metrics.
- **HISTORY-011:** The user must be able to search by item, creature, zone, note, tag, or tracked-rule name.
- **HISTORY-012:** Filters must include date range, server, character, zone, level, and imported/recorded source.
- **HISTORY-013:** Optional filters should include minimum duration, deaths, tracked-item matches, and session tags.
- **HISTORY-014:** Search and filter operations must not require reparsing original logs.
- **HISTORY-015:** Empty-result states must explain which filters are active.

---

## 11.3 Session overview

### Requirements

- **DETAIL-001:** The session overview must show start, end, duration, active duration, character, server, level range, zones, deaths, kills, XP, AA, money, loot, DPS, healing, and tracked-rule results where available.
- **DETAIL-002:** The overview must distinguish wall-clock and active-play rates.
- **DETAIL-003:** The overview must indicate incomplete or recovered sessions.
- **DETAIL-004:** The overview must identify imported sessions.
- **DETAIL-005:** The user must be able to add or edit a session title, note, and tags.
- **DETAIL-006:** The user must be able to delete or export the session.
- **DETAIL-007:** The user must be able to navigate from summary values to relevant detail views.

---

## 11.4 Timeline and segments

### Requirements

- **TIMELINE-001:** History Mode must display a chronological session timeline.
- **TIMELINE-002:** The timeline must include zones, levels, deaths, manually added markers, idle intervals, stance changes, invocation changes, and segment boundaries where available.
- **TIMELINE-003:** The user must be able to create, rename, adjust, merge, or split segments after the session.
- **TIMELINE-004:** Segment edits must cause dependent aggregates to recalculate.
- **TIMELINE-005:** Segment comparison must include XP/hour, kills/hour, money/hour, tracked loot/hour, DPS, damage taken, healing, and active time where available.

---

## 11.5 Historical tracked-loot analysis

### Requirements

- **HTRACK-001:** History Mode must show tracked-rule results for each session.
- **HTRACK-002:** The user must be able to select a tracked rule and view results across sessions.
- **HTRACK-003:** Historical results must include total quantity, distinct item names, quantity/hour, quantity/active hour, and matches per kill where available.
- **HTRACK-004:** Historical results should show source creatures after correlation is available.
- **HTRACK-005:** The user should be able to compare the same tracked rule across selected sessions or segments.
- **HTRACK-006:** Editing a rule must not silently rewrite historical results; the system must distinguish stored rule versions or explicitly offer historical recomputation.
- **HTRACK-007:** Historical search must also support ad hoc item substring queries even when no rule existed during the original session, provided the stored loot events are sufficient.

---

## 11.6 Notes, tags, and manual metadata

### Requirements

- **META-001:** Users must be able to add free-text notes to sessions and segments.
- **META-002:** Users must be able to assign multiple tags.
- **META-003:** Tags should support use cases such as `solo`, `full group`, `Befallen basement`, `new weapon`, or `Matthew absent`.
- **META-004:** Notes and tags must be searchable.
- **META-005:** Manual metadata must be stored separately from parser-derived facts.

---

## 11.7 Session comparison

### Requirements

- **COMPARE-001:** Users must be able to select two or more sessions for comparison.
- **COMPARE-002:** Comparison must normalize rate metrics appropriately.
- **COMPARE-003:** Comparison must include duration, active duration, XP/hour, kills/hour, money/hour, tracked loot/hour, DPS, damage taken, healing, deaths, and selected mob summaries where available.
- **COMPARE-004:** The comparison must identify different levels, zones, or group contexts that may make direct comparison imperfect.
- **COMPARE-005:** Users should be able to compare segments as well as complete sessions.
- **COMPARE-006:** The interface must avoid presenting correlation as causation.

---

## 11.8 Export

### Requirements

- **EXPORT-001:** Users must be able to copy a compact session summary to the clipboard.
- **EXPORT-002:** Users must be able to export session data as CSV.
- **EXPORT-003:** Users must be able to export structured session data as JSON.
- **EXPORT-004:** A human-readable Markdown or text summary should be available for Discord, Reddit, or GitHub.
- **EXPORT-005:** Exports must identify units, time basis, character, server, and session date.
- **EXPORT-006:** Exported data must clearly identify estimated or incomplete fields.
- **EXPORT-007:** Users should be able to exclude character names or other identifying metadata from a diagnostic/share export.

---

# 12. Functional Requirements — Encounters and Farming Intelligence

## 12.1 Encounter reconstruction

### Requirements

- **ENCOUNTER-001:** EQ Buddy must represent completed combats as encounters.
- **ENCOUNTER-002:** Each encounter must have a stable identifier.
- **ENCOUNTER-003:** Each encounter should record start, end, duration, target creature name, outcome, and confidence.
- **ENCOUNTER-004:** Encounter completion may be detected by a kill line, explicit combat transition, or inactivity timeout.
- **ENCOUNTER-005:** Encounter timeout must be configurable or centrally defined and testable.
- **ENCOUNTER-006:** The model must allow overlapping or ambiguous combat when multiple creatures are engaged.
- **ENCOUNTER-007:** The parser must not force unrelated combat events into a single encounter merely because they are temporally close.
- **ENCOUNTER-008:** Encounter attribution must preserve uncertainty where the log cannot identify the exact target.
- **ENCOUNTER-009:** Encounters must contain or reference damage, healing, damage taken, XP, coin, loot, kill, pet, stance, invocation, and segment context where available.
- **ENCOUNTER-010:** Current encounter state and finalized encounter state must be distinct.
- **ENCOUNTER-011:** Finalized encounter aggregates must be persistable without retaining all raw swing events indefinitely.

### Acceptance criteria

- Three sequential kills of the same creature name appear as three distinct encounters.
- A fight ending through an inactivity timeout is marked as timeout-ended rather than killed when no kill line exists.
- Pet and charm ownership changes do not retroactively corrupt unrelated encounters.

---

## 12.2 Encounter list and detail

### Requirements

- **ENCOUNTER-020:** A session must expose an ordered encounter list.
- **ENCOUNTER-021:** Encounter rows should show creature, start time, duration, outcome, DPS, XP, coin, and loot indicators where available.
- **ENCOUNTER-022:** Encounter detail must show player, pet, charm, spell, skill, ranged, DoT, damage-shield, healing, and damage-taken composition where available.
- **ENCOUNTER-023:** Encounter detail must show active stance/invocation windows where available.
- **ENCOUNTER-024:** The user must be able to navigate previous and next encounters.
- **ENCOUNTER-025:** The user must be able to filter encounters by creature, outcome, segment, or time range.

---

## 12.3 Reward correlation

### Requirements

- **CORRELATE-001:** EQ Buddy must attempt to associate XP, coin, and loot events with the correct encounter or killed creature.
- **CORRELATE-002:** Correlation rules must be deterministic and covered by fixtures.
- **CORRELATE-003:** Correlated rewards must store confidence and method.
- **CORRELATE-004:** Ambiguous rewards must remain unassigned or marked ambiguous rather than silently assigned.
- **CORRELATE-005:** Multiple loot lines following one kill must associate with the same encounter when supported by timing and log order.
- **CORRELATE-006:** Vendor sales, autosell income, crafting, and unrelated loot must not be attributed to a creature encounter without evidence.
- **CORRELATE-007:** The UI must expose unassigned rewards when they materially affect totals.

---

## 12.4 Mob farming summaries

### Requirements

- **MOB-001:** History Mode must aggregate encounters by normalized creature name.
- **MOB-002:** Each mob summary should include kill count, encounter count, average fight duration, kills/hour, XP/kill, XP/hour, coin/kill, coin/hour, and deaths involving the mob where available.
- **MOB-003:** Mob summaries must list associated loot items and quantities.
- **MOB-004:** Mob summaries must calculate observed personal drop rates using eligible correlated kills as the denominator.
- **MOB-005:** The interface must label these as observed personal rates, not authoritative game drop rates.
- **MOB-006:** The denominator and sample size must always be visible with a drop-rate percentage.
- **MOB-007:** The user must be able to scope a mob summary to session, segment, character, level, date range, or all history.
- **MOB-008:** Tracked-item matches must be visible in mob summaries.

Example:

```text
Ghoul knight
17 kills · 26.2 kills/hour
Average fight: 44 sec
Average XP: 0.18%
Average coin: 8g 3s

Loot:
Fine Steel Long Sword       3   17.6% (3 / 17)
Research Page               5   29.4% (5 / 17)
```

---

# 13. Functional Requirements — EQL-Specific Analysis

## 13.1 Level windows

### Requirements

- **LEVEL-001:** Level-up events must create level windows within a session.
- **LEVEL-002:** A session spanning multiple levels must be analyzable by individual level.
- **LEVEL-003:** Level summaries should include duration, active duration, XP/hour, kills/hour, DPS, deaths, coin, and tracked loot.
- **LEVEL-004:** Historical views should support comparison of performance at different levels while clearly identifying gear, group, and zone differences where known.

---

## 13.2 Stance tracking

### Requirements

- **STANCE-001:** The parser must recognize known stance-change log lines through tested fixtures.
- **STANCE-002:** Stance state must be represented as time windows.
- **STANCE-003:** Unknown initial stance must remain unknown until a stance event establishes it.
- **STANCE-004:** Each encounter may contain one or more stance windows.
- **STANCE-005:** Stance summaries should include combat time, DPS, damage taken/minute, healing received/minute, kills/hour, and selected tracked-loot context where meaningful.
- **STANCE-006:** The UI must show sample size and combat duration for every comparison.

---

## 13.3 Invocation tracking

### Requirements

- **INVOCATION-001:** The parser must recognize invocation activation, replacement, expiration, or deactivation lines when available.
- **INVOCATION-002:** Invocation state must be represented as time windows.
- **INVOCATION-003:** Multiple simultaneous invocation states must be supported if the game permits them.
- **INVOCATION-004:** Invocation summaries should include combat time, DPS, damage taken, healing, encounter outcomes, and sample size.
- **INVOCATION-005:** Unknown or incomplete state must be disclosed.

---

## 13.4 Stance/invocation combination analysis

### Requirements

- **COMBO-001:** The system must calculate overlapping stance and invocation windows.
- **COMBO-002:** Combination summaries must include total combat time and encounter count.
- **COMBO-003:** Comparisons should include DPS, damage taken/minute, healing received/minute, average fight duration, and kills/hour.
- **COMBO-004:** Very small samples must be visibly flagged.
- **COMBO-005:** Combination analysis must be available by session, segment, level, and historical filter where sufficient data exists.

---

## 13.5 Damage-source composition

### Requirements

- **SOURCE-001:** Damage must be attributable to the most specific reliable source category available.
- **SOURCE-002:** Initial source categories should include melee skill, ranged, spell direct damage, DoT, song, pet, charm, damage shield, proc, and unknown.
- **SOURCE-003:** Pet and charm rows should be expandable into attack/spell composition where data allows.
- **SOURCE-004:** Crits, hit count, average hit, maximum hit, accuracy, resist, and avoidance data should be included where the log supports them reliably.
- **SOURCE-005:** Unknown or unmatched damage must remain visible rather than being discarded.
- **SOURCE-006:** Attribution changes must be regression-tested against real sanitized logs.

---

## 13.6 Analysis scope selector

### Requirements

- **SCOPE-001:** Detailed views must support a consistent analysis scope model.
- **SCOPE-002:** Initial scopes should include:
  - Current session.
  - Selected historical session.
  - Selected segment.
  - Selected level.
  - Recent time window.
  - Selected creature.
  - Selected encounter.
- **SCOPE-003:** Every table or metric must clearly display its active scope.
- **SCOPE-004:** Changing scope must not silently change the meaning of a rate without updating its label and denominator.

---

## 13.7 Existing-log import

### Requirements

- **IMPORT-001:** Users must be able to select an existing EverQuest Legends log for offline import.
- **IMPORT-002:** Import must not modify the source log.
- **IMPORT-003:** The importer must process large logs incrementally rather than loading the entire file into memory.
- **IMPORT-004:** Import must report progress and support cancellation.
- **IMPORT-005:** The importer must attempt to reconstruct session boundaries using character, timestamps, zones, inactivity, levels, and other available evidence.
- **IMPORT-006:** The user must be able to review proposed sessions before committing them to history when reconstruction is ambiguous.
- **IMPORT-007:** Imported sessions must be marked as imported.
- **IMPORT-008:** Re-importing the same log range must not create duplicates without explicit confirmation.
- **IMPORT-009:** Import must use the same parser/event model as live monitoring.
- **IMPORT-010:** Import warnings and unparsed-line diagnostics must be available after completion.

---

# 14. Generalized Watch Rules

## 14.1 Purpose

Tracked loot should become the first implementation of a broader, user-friendly watch system, but the generalized system should follow only after loot tracking is stable.

### Potential event categories

- Loot text.
- Exact item.
- Named creature kill.
- Faction change.
- Skill increase.
- Death.
- Level or AA milestone.
- Spell, song, stance, or invocation message.
- Arbitrary log substring.

### Requirements

- **WATCH-001:** Rules must be based on structured events whenever possible rather than raw text alone.
- **WATCH-002:** Raw substring rules may be supported as an advanced fallback.
- **WATCH-003:** Rule creation must use plain-language fields and templates.
- **WATCH-004:** Rules must support counters, alerts, overlay cards, and historical reporting where applicable.
- **WATCH-005:** The system must protect against alert storms through cooldowns and aggregation.
- **WATCH-006:** Rule evaluation must not materially delay log parsing.
- **WATCH-007:** Advanced regular expressions, if ever added, must be clearly identified as advanced and validated safely.

---

# 15. Parser Reliability, Diagnostics, and Security

## 15.1 Parser regression suite

### Requirements

- **TEST-001:** A dedicated test project must be added if one does not already exist.
- **TEST-002:** Real sanitized log fixtures must be the primary parser regression mechanism.
- **TEST-003:** Every parser bug fix must add a failing fixture before the fix.
- **TEST-004:** Coverage must include at least:
  - Melee damage.
  - Ranged damage.
  - Direct-damage spells.
  - DoTs.
  - Songs.
  - Damage shields.
  - Procs.
  - Pet claims.
  - Charm claim and break.
  - Healing and regeneration.
  - Resists, misses, avoidance, and fizzles.
  - XP and AA XP.
  - Kills and deaths.
  - Loot quantities.
  - Coin and vendor income.
  - Crafting/merging.
  - Skills and faction.
  - Zones and levels.
  - Stance and invocation lines when implemented.
  - Log truncation and rollover.
- **TEST-005:** Tests must validate event type and all parsed fields, not only that a line matched.
- **TEST-006:** Event replay tests must validate session aggregates.

---

## 15.2 Parser coverage diagnostics

### Requirements

- **DIAG-001:** EQ Buddy should count parsed lines by event category.
- **DIAG-002:** It should identify lines that appear relevant but match no known pattern.
- **DIAG-003:** Users should be able to export a sanitized diagnostic bundle.
- **DIAG-004:** Diagnostic export must allow removal or hashing of character names, server names, paths, and other identifying values.
- **DIAG-005:** Diagnostics must include application version, parser version, schema version, active configuration, and representative unmatched lines.
- **DIAG-006:** Diagnostics must never include credentials or unrelated personal files.

---

## 15.3 Continuous integration

### Requirements

- **CI-001:** Every pull request must build the solution.
- **CI-002:** Every pull request must run unit and integration tests.
- **CI-003:** Release builds should publish deterministic or reproducible metadata where practical.
- **CI-004:** Static analysis and nullable warnings should be enabled at a practical severity.
- **CI-005:** A release must not be published when parser regression tests fail.

---

## 15.4 Reliability and project hygiene

### Required work

- **REL-001:** Invoke session-rollover callbacks outside locks to avoid re-entrancy or deadlock risk.
- **REL-002:** Make `eqclient.ini` editing section-aware and preserve unrelated settings/comments where practical.
- **REL-003:** Replace silent exception swallowing with structured logging and user-facing nonfatal errors where appropriate.
- **REL-004:** Remove personal-machine or OneDrive paths from runtime update discovery.
- **REL-005:** Add a clear open-source license.
- **REL-006:** Validate configuration values and recover from malformed settings.
- **REL-007:** Use atomic or replace-safe writes for settings and important local metadata.
- **REL-008:** Ensure file watchers and streams are disposed safely on shutdown or source change.
- **REL-009:** Prevent duplicate event processing after reconnecting to a log.
- **REL-010:** Add application-level crash logging with privacy-conscious content.

---

## 15.5 Update security

### Requirements

- **UPDATE-001:** Update checks must use a trusted release source.
- **UPDATE-002:** Downloaded installers must not be executed solely because a file with the expected name exists locally.
- **UPDATE-003:** Release integrity should be verified using a cryptographic hash, code signing, or both.
- **UPDATE-004:** The user must approve installation of an update.
- **UPDATE-005:** Update failure must not prevent normal application use.
- **UPDATE-006:** Update channels and prerelease behavior must be explicit.

---

# 16. Recommended Data Model

The exact schema may evolve, but the implementation should support the following logical entities.

## 16.1 Identity and configuration

```text
Server
Character
CharacterProfile
OverlayLayout
TrackedRule
TrackedRuleVersion
AlertConfiguration
ApplicationSetting
```

## 16.2 Session structure

```text
Session
SessionCheckpoint
SessionSegment
SessionMarker
IdleInterval
LevelWindow
ZoneVisit
StateWindow
```

## 16.3 Activity and rewards

```text
Encounter
EncounterParticipant
DamageSummary
HealingSummary
LootOccurrence
CoinOccurrence
ExperienceOccurrence
DeathOccurrence
SkillOccurrence
FactionOccurrence
CraftingOccurrence
VendorSaleOccurrence
```

## 16.4 Historical analysis

```text
TrackedRuleSessionResult
TrackedRuleSegmentResult
MobSummaryCache
SessionSummary
SegmentSummary
EncounterSummary
SessionTag
SessionNote
ImportRecord
DiagnosticRecord
```

## 16.5 Minimum session fields

```text
SessionId
ServerId
CharacterId
StartedAtUtc
EndedAtUtc
LocalUtcOffset
ActiveDuration
EndReason
PrimaryZone
StartLevel
EndLevel
SourceType
SourceLogIdentity
ApplicationVersion
ParserVersion
SchemaVersion
IsIncomplete
IsRecovered
CreatedAtUtc
UpdatedAtUtc
```

## 16.6 Provenance fields

Correlated or inferred entities should support:

```text
ProvenanceType
Confidence
CorrelationMethod
SourceEventIds
IsAmbiguous
DiagnosticNote
```

---

# 17. Recommended Architecture

## 17.1 Data flow

```text
Live LogWatcher ───────────────┐
                               │
ImportedLogReader ─────────────┤
                               ↓
                           LogParser
                               ↓
                         Typed GameEvent
                               ↓
                       SessionCoordinator
                ┌──────────────┼──────────────┐
                ↓              ↓              ↓
          SessionStats   SessionJournal  EncounterTracker
                │              │              │
                └──────────────┼──────────────┘
                               ↓
                        RewardCorrelator
                               ↓
                       SessionRepository
                ┌──────────────┴──────────────┐
                ↓                             ↓
          Live ViewModels              History ViewModels
```

## 17.2 Separation of responsibilities

### `LogWatcher`

- Watches the active file.
- Handles file replacement, truncation, delayed writes, and encoding.
- Emits complete raw lines with source position metadata.
- Contains no game-specific parsing logic.

### `ImportedLogReader`

- Streams existing logs incrementally.
- Supports progress and cancellation.
- Emits the same raw-line contract as `LogWatcher`.

### `LogParser`

- Converts raw lines into typed events.
- Contains line-pattern recognition only.
- Does not update UI or database directly.

### `SessionCoordinator`

- Owns active server, character, session, and lifecycle state.
- Routes events to journal, aggregators, encounter tracker, and repository.
- Handles checkpoint and finalization policy.

### `SessionStats`

- Maintains fast current totals required by the live overlay.
- Is replayable from typed events.
- Does not own persistence.

### `SessionJournal`

- Retains timestamped current-session events.
- Supports rolling windows, tracked-rule recalculation, recovery, and replay.
- Applies retention/compaction policy.

### `EncounterTracker`

- Builds current and completed encounter state.
- Applies tested boundary rules.
- Preserves ambiguity.

### `RewardCorrelator`

- Associates XP, coin, and loot with encounters and creatures.
- Stores correlation confidence and method.

### `StateWindowTracker`

- Builds level, zone, stance, invocation, active, and idle windows.

### `SessionRepository`

- Owns SQLite access and migrations.
- Provides asynchronous operations.
- Is UI-independent.

### Presentation layer

- Live view models consume current snapshots and notifications.
- History view models query persisted projections.
- Views do not parse logs or perform SQL directly.

---

## 17.3 Main window refactoring

The main WPF window should not remain responsible for all of the following simultaneously:

- File discovery.
- Parsing.
- Session state.
- Update checking.
- Persistence.
- Alert execution.
- Window-layout storage.
- Historical queries.

Refactor toward injected services and testable view models before adding major History Mode complexity.

---

# 18. Nonfunctional Requirements

## 18.1 Performance

- **NFR-PERF-001:** Normal log processing must keep pace with sustained combat logging on a typical Windows gaming PC.
- **NFR-PERF-002:** The overlay must remain responsive during heavy log activity.
- **NFR-PERF-003:** Database operations must not run synchronously on the UI thread.
- **NFR-PERF-004:** UI refresh frequency should be decoupled from raw event frequency.
- **NFR-PERF-005:** Long sessions must have bounded memory use.
- **NFR-PERF-006:** History searches over thousands of sessions should return interactively with appropriate indexing and pagination/virtualization.
- **NFR-PERF-007:** Existing-log import must stream and report progress.

## 18.2 Reliability

- **NFR-REL-001:** A malformed log line must not stop monitoring.
- **NFR-REL-002:** A storage or alert failure must not stop parsing.
- **NFR-REL-003:** Application restart must recover or safely finalize an interrupted session.
- **NFR-REL-004:** Duplicate file events must not produce duplicate game events.
- **NFR-REL-005:** Schema migration failure must not destroy the existing database.

## 18.3 Privacy

- **NFR-PRIV-001:** No session data may be uploaded automatically.
- **NFR-PRIV-002:** Telemetry, if ever added, must be opt-in and separately documented.
- **NFR-PRIV-003:** Diagnostic exports must support sanitization.
- **NFR-PRIV-004:** Local file paths and character names must not appear in public crash reports without user approval.

## 18.4 Accessibility and usability

- **NFR-UX-001:** Text must remain readable at common Windows scaling settings.
- **NFR-UX-002:** Overlay transparency must not reduce all text below usable contrast.
- **NFR-UX-003:** Important status must not rely on color alone.
- **NFR-UX-004:** Keyboard navigation must work in History & Analysis Mode.
- **NFR-UX-005:** Alerts must support a visual-only option.

## 18.5 Compatibility

- **NFR-COMPAT-001:** Windows 11 is the primary supported operating system.
- **NFR-COMPAT-002:** The application should remain compatible with common 1080p and 4K scaling configurations.
- **NFR-COMPAT-003:** Borderless and windowed gameplay must be first-class overlay scenarios.

---

# 19. UX Requirements

## 19.1 First-run experience

The first run should:

1. Locate the likely log directory.
2. Confirm or allow changing it.
3. Explain that EQ Buddy reads local text logs only.
4. Offer a small default overlay layout.
5. Offer creation of an initial tracked-loot rule, without requiring it.
6. Explain the show/hide and click-through hotkeys.
7. Avoid presenting the full History interface before the user has data.

## 19.2 Live overlay

The live overlay must:

- Start in a useful default state.
- Avoid requiring constant interaction.
- Preserve its screen position.
- Remain visible above the game when configured.
- Support click-through.
- Avoid stealing focus.
- Clearly show when data is stale, paused, unavailable, or tied to another character.
- Use concise labels suitable for glance reading.

## 19.3 History window

The History window must:

- Use familiar master-detail navigation.
- Make server and character context continuously visible.
- Preserve filter and selection state.
- Support keyboard and mouse navigation.
- Avoid loading all detailed event data until needed.
- Show confidence/incompleteness warnings near affected data.

---

# 20. Testing Requirements

## 20.1 Unit tests

Required unit-test targets:

- Every parser pattern.
- Tracked-rule matching.
- Alert cooldown logic.
- Rolling-window calculations.
- Active/idle calculations.
- Session lifecycle rules.
- Encounter boundary rules.
- Reward correlation.
- State-window overlap.
- Rate denominators.
- Schema-independent domain aggregators.

## 20.2 Integration tests

Required integration-test scenarios:

- Tail a growing log.
- Log file is replaced.
- Log file is truncated.
- Character changes.
- Application closes and resumes.
- Application crashes after checkpoint.
- Session writes to SQLite.
- Migration from prior schema.
- Import a large existing log.
- Re-import duplicate range.
- Tracked rule changed mid-session.
- Alert generated exactly once.

## 20.3 UI tests

High-value UI tests:

- Overlay layout persists.
- Click-through toggles.
- Overlay does not take focus when alerting.
- History filters return expected sessions.
- Previous/next session navigation works.
- Session deletion requires confirmation.
- Import cancellation leaves the database consistent.

## 20.4 Golden-session fixtures

Maintain a small set of sanitized end-to-end log fixtures with expected final session summaries, encounters, rewards, tracked-item results, and state windows. These fixtures should act as release gates.

---

# 21. Implementation Sequence for Claude Code

## Step 1 — Establish the safety net

- Add test project.
- Add sanitized parser fixtures.
- Add CI build and test workflow.
- Capture current expected behavior before refactoring.

## Step 2 — Fix confirmed reliability risks

- Move rollover callbacks outside locks.
- Make INI edits section-aware.
- Replace silent failures with structured logging.
- Remove personal update paths.
- Add license and update-integrity plan.

## Step 3 — Introduce typed event journal contracts

- Ensure all events have stable timestamps and identities.
- Add current-session journal.
- Make current aggregates replayable.
- Add retention/compaction policy.

## Step 4 — Deliver Live Companion Advantage

- Implement tracked-loot rules.
- Implement tracked-loot cards.
- Implement alert services.
- Implement overlay card customization.
- Implement recent-window rates.
- Implement active-time rates.
- Implement hotkeys, click-through, and segment markers.

## Step 5 — Introduce SQLite persistence

- Add repository abstraction.
- Add migrations.
- Store identity, sessions, journal projections, and tracked results.
- Add checkpointing and recovery.

## Step 6 — Build History & Analysis MVP

- Server/character tree.
- Session list.
- Session overview.
- Search and filters.
- Notes and tags.
- Historical tracked-loot results.
- Export.

## Step 7 — Add encounter reconstruction

- Implement encounter tracker.
- Add recent encounter list in Live Mode.
- Persist encounter summaries.
- Add encounter detail in History Mode.

## Step 8 — Add reward correlation and mob intelligence

- Correlate XP, coin, and loot.
- Build mob summaries.
- Add personal drop rates with sample sizes.
- Add segment analysis and comparison.

## Step 9 — Add EQL-specific state analysis

- Parse level, stance, and invocation windows.
- Add current state card.
- Add historical combination comparisons.
- Add damage-source composition.

## Step 10 — Add session comparison and import

- Compare sessions and segments.
- Stream existing logs.
- Reconstruct and review imported sessions.
- Add duplicate detection.

## Step 11 — Generalize watch rules and extract core

- Extend tracked loot to structured watch templates.
- Export diagnostics.
- Extract UI-independent core where the preceding boundaries prove stable.

---

# 22. Suggested GitHub Epics

## Epic A — Parser Reliability Foundation

- Add parser fixture suite.
- Add CI.
- Fix rollover callback locking.
- Fix section-aware INI editing.
- Add structured logging.
- Harden update flow.
- Add license.

## Epic B — Single-Monitor Live Experience

- Customizable overlay cards.
- Click-through and hotkeys.
- Recent-window metrics.
- Active-time rates.
- Camp markers.
- Current state card.

## Epic C — Tracked Loot and Alerts

- Multiple substring rules.
- Matching-item breakdown.
- Per-hour rates.
- Sound and banner alerts.
- Rule persistence.
- Historical rule results.

## Epic D — Persistent Sessions

- SQLite schema.
- Session lifecycle.
- Checkpointing.
- Crash recovery.
- Server/character identity.
- Migration and backup.

## Epic E — History and Offline Navigation

- Character/session browser.
- Session overview.
- Timeline.
- Search and filters.
- Notes, tags, deletion, and exports.

## Epic F — Encounters and Mob Intelligence

- Encounter reconstruction.
- Encounter detail.
- Reward correlation.
- Mob summaries.
- Personal drop rates.
- Segment comparison.

## Epic G — EQL Build Analytics

- Level windows.
- Stance windows.
- Invocation windows.
- Combination analysis.
- Damage-source composition.

## Epic H — Import and Comparison

- Existing-log import.
- Session-boundary reconstruction.
- Duplicate detection.
- Session and segment comparison.

## Epic I — Generalized Watch Rules

- Structured event templates.
- Named-mob, faction, skill, death, and milestone alerts.
- Rule diagnostics and testing.

---

# 23. Release A Definition of Done

Release A is complete only when all of the following are true:

- Parser regression tests protect current major event categories.
- CI builds and tests every pull request.
- Known lock, INI, silent-failure, update-path, and licensing issues are resolved or explicitly documented as deferred blockers.
- A user can create at least three tracked-loot rules.
- A rule such as `mote` correctly aggregates all matching item names and quantities.
- Each rule can show total quantity, matching names, session rate, and recent match time.
- Rules persist after restart.
- A rule can trigger a focus-safe visual and/or sound alert.
- The user can choose and reorder overlay cards.
- Overlay layout persists.
- The overlay supports show/hide and click-through hotkeys.
- Recent-window metrics are based on timestamped events.
- Wall-clock and active-play rates are distinguishable.
- A user can create a segment/camp marker without leaving the game.
- Long-session memory use remains bounded under the chosen journal policy.
- The application remains usable when alerts, settings writes, or noncritical diagnostics fail.

---

# 24. Release B Definition of Done

Release B is complete only when all of the following are true:

- Meaningful sessions are automatically saved to SQLite.
- Sessions are organized by server and character.
- Active sessions are checkpointed and recoverable.
- Character switches do not merge data.
- History Mode works with the game closed.
- The user can navigate sessions, search/filter them, add notes/tags, and export them.
- Historical tracked-loot results are visible.
- A database migration test protects existing data.
- The user has a documented backup and recovery option.

---

# 25. Product Success Measures

Initial success should be evaluated with product-behavior metrics gathered through user feedback or opt-in diagnostics rather than mandatory telemetry.

Useful measures include:

- Users can configure a useful overlay in under five minutes.
- A normal returning user does not need to select a log manually.
- Tracked-loot counts match the source log in regression fixtures.
- No measurable event duplication after restart or log rollover.
- Session history remains navigable after hundreds or thousands of sessions.
- Users can answer “which camp was better?” without exporting logs to another tool.
- Users can see important live metrics on one monitor without Alt-Tab.
- Parser bug reports increasingly include usable sanitized diagnostics.

---

# 26. Final Product Boundary

EQ Buddy should not attempt to win by being the largest parser or by placing every analytical table over the game.

It should win by combining four things better than alternatives:

1. **Live visibility:** useful information remains visible during play on one monitor.
2. **Automatic capture:** the user does not manage log uploads or session files during normal use.
3. **Personal objectives:** tracked loot and alerts surface what matters to the individual player.
4. **Durable history:** every meaningful session becomes an offline, searchable, comparable record organized by character.

Advanced encounter, mob, level, stance, and invocation analysis should strengthen that workflow without compromising the compact live companion that makes EQ Buddy distinct.
