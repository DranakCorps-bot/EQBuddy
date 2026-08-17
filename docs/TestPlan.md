# EQBuddy test plan

**What EQBuddy is expected to do, and how each expectation is held down.** This is the
contract. When behaviour changes, this file changes in the same commit — a test plan
that has drifted from the product is worse than none, because it teaches confidently
wrong things.

Audited at **v1.82.0 (2026-08-14)**: 1,317 unit + 45 Avalonia + 6 E2E, all green.

**How to read the Held-by column**

| Mark | Meaning |
|---|---|
| **Auto** | A test fails if this breaks. Safe to refactor against. |
| **Partial** | The logic is tested; the wiring to the screen is not. |
| **Manual** | Only a human sitting in front of it will notice. See §6 for the pass. |

---

## 1. Reading the log

| Expectation | Held by |
|---|---|
| Every known line type parses to its `GameEvent`; unknown lines are ignored, never guessed at | **Auto** — `LogParserTests`, `SplitOnceParserTests` |
| A growing file is tailed from its last offset, survives truncation and rollover | **Auto** — `LogWatcherTests`, `LogSessionsTests` |
| 60+ minutes of silence ends a session; the next line starts a fresh one | **Auto** — `SessionStatsTests`, `SessionArchiverTests` |
| The active character is followed automatically (whichever log is growing) | **Partial** — detection tested, the 5 s re-scan is not |
| `Log=1` is forced in `eqclient.ini`; stale logs truncate only while the game is closed | **Auto** — `EqConfigTests` |
| A log folder is found from the registry, with Wine/CrossOver prefixes handled | **Auto** — `LogFolderDetectionTests` |

## 2. What the numbers mean

| Expectation | Held by |
|---|---|
| Session DPS = damage ÷ time in combat; wall-clock DPS reported separately and labelled | **Auto** — `SessionStatsTests` |
| Per-ability DPS is that ability's damage ÷ total combat time (contribution rate); burst rate is damage ÷ its own active time | **Auto** — `BreakdownRangeTests` |
| Self-inflicted damage counts as damage taken but opens **no** combat window and **no** encounter | **Auto** — `EncounterTests` |
| A fight closes on the kill line, or after 20 s of silence marked `Timeout` | **Auto** — `EncounterTests` |
| Fights group into a pull when no 10 s lull separates them; live card and History agree | **Auto** — `EncounterTests`, `LastFightTests` |
| **A pull in a never-quiet zone runs long and its DPS is a long average.** Deliberate (#151); per-mob figures live on the Kills card | **Auto** (the rule) / documented limit |
| Pet damage is attributed to the pet, never to your accuracy or crit counters | **Auto** — `SessionStatsTests`, `SpellTrackingTests` |
| A charm cast confirms a pet; an interrupted or faded charm never claims one | **Auto** — `SessionStatsTests` |
| **A charm landing is ours only within that spell's own cast time + 1.5 s** — 9 s for Cajole Undead, 3.9 s for Charm. An instant or unknown cast time keeps the generic 30 s window, never a tighter one | **Auto** — `SpellTrackingTests` |
| **The charm family is decided from wiki slot effects, not names**: Dictate and Thrall of Bones are charms, Allure of Death is not | **Auto** — `SpellTrackingTests` |
| **`/pet who leader` settles ownership both ways**: naming you claims the creature, naming anyone else releases a pet we claimed. Unknown character name never releases (#177) | **Auto** — `SpellTrackingTests` |
| Crit, miss, resist, fizzle and block counts follow the log's own words | **Auto** — `LogParserTests`, `StackingTests` |
| **Every one of the sixteen classes can produce class evidence**, so an inference can always be argued back down (#120) | **Auto** — `ClassInferenceTests` |
| Class evidence decays with a 10-minute half-life: a swap converges on what is being played now, and silence alone never flips a reading | **Auto** — `ClassInferenceTests` |
| A class is named only from 3+ sightings, with two distinct spells where an item could have cast them, and a 2× lead — otherwise **no** inference | **Auto** — `ClassInferenceTests` |

## 3. Knowledge and catalogs

| Expectation | Held by |
|---|---|
| Every zone EQBuddy knows resolves to a real map file (aliases included) | **Auto** — `ZoneMapCoverageTests` |
| Catalogs stay internally consistent; no duplicate or orphaned entries | **Auto** — `CatalogHygieneTests`, `CatalogSanityTests` |
| Wiki lookups try article, case and backtick variants, then a bounded-fuzzy backstop; a merely-similar page is never accepted | **Auto** — `EqlWikiMobsTests` |
| **A redirected lookup records the title the wiki SERVED, not the one requested** | **Auto** — `EqlWikiMobsTests` (regression, #65 — this broke twice) |
| An epithet (`X, the Y`) falls back to the base name rather than proposing a duplicate page | **Auto** — `EqlWikiMobsTests` (#65) |
| **A contribution pack names the zone the creature died in, everywhere in the entry** | **Auto** — `WikiContributionTests` (#65) |
| Rarity labels only appear from 10+ kills | **Auto** — `WikiContributionTests` |
| Zone-knowledge share strings round-trip; imports preview every change; wild timers arrive flagged | **Auto** — `SpawnPointLedgerTests` |
| Fuzzy kill matching never bridges name-family siblings: a shared prefix with a different last word is another mob unless one truncates the other | **Auto** — `SpawnTimerTests` (Sol A CWG models, 2026-08-16) |
| Spawn timers only learn from the named's own kills and sightings — a placeholder death restarts the clock but a gap or elapsed measured from it teaches nothing | **Auto** — `SpawnTimerTests` (the 93-second EXG, 2026-08-16) |
| A manual ▶ start always replaces a running timer, even backdated | **Auto** — `SpawnTimerTests` |
| Curated catalogs are never written by automation | **Process** — the weekly refresh PR only flags them |
| The charm catalog is generated, so a new charm or a corrected cast time reaches the arm window without anyone remembering to type it | **Auto** — `SpellTrackingTests`; generated by `charms-harvest.py` in the weekly refresh |

## 4. EQBuddy Mobile

| Expectation | Held by |
|---|---|
| Off by default: a fresh install opens no socket | **Auto** — `CompanionEnableTests` |
| Turning it on is the only thing that opens the port; a token is minted and rides the URL fragment | **Auto** — `CompanionEnableTests` |
| A tick with nobody paired builds no projection | **Auto** — `CompanionEnableTests` |
| A device receives only the surfaces the desktop offers **and** it subscribed to | **Auto** — `CompanionSurfaceTests`, `CompanionServerTests` |
| A section only wakes devices watching it; drifting values are excluded from fingerprints | **Auto** — `CompanionProjectionTests` |
| Map geometry is sent once per zone per device | **Auto** — `CompanionMapSourceTests` |
| The breadcrumb trail matches the desktop's fade curve, anchor crumb included | **Auto** — `CompanionMapSourceTests`, `TrailFadeTests` |
| Camp pins and the Named list come from one projected list, so a pin and its row cannot disagree | **Auto** — `CompanionMapSourceTests` |
| Curation applies to the same ledger the desktop edits, and always answers — including when nothing changed | **Auto** — `CompanionCurationTests` |
| An edit made on a device appears on the PC map (via the ledger's `Revision`) | **Auto** — `CompanionCurationTests` |
| The page's fade constants match `TrailFade` | **Auto** — pinned by reading the shipped page |
| Theme reaches the page for every `var(--x)` it uses | **Auto** — `CompanionThemeTests` |
| The widget's Quests card shows, opens the Quest Tracker, and keeps both checklists' counts on screen | **Auto** — `EndToEndTests` (EQBUDDY_EXPAND) |
| The old `sky`/`epic` card keys fold onto `quests`, keeping position; hidden only if BOTH were hidden | **Auto** — `OptionsViewModelTests` |
| The quest surface's tab strip comes from Core's `QuestSurface`; General carries no badge | **Auto** — `CompanionQuestsTests` |
| The quest catalog index ships once per device by stamp, and re-ships when any field changes | **Auto** — `CompanionQuestsTests` |
| The general list's membership and order are Core's `QuestMatcher`'s; dismissed and completed non-repeatables are excluded | **Auto** — `CompanionQuestsTests` |
| Class restrictions are resolved by Core's `QuestClassFilter` at index build — the page checks membership, never parses class text | **Auto** — `CompanionQuestsTests` |
| A device's 📌 and class-picker taps land on the same `QuestLedgerStore` the desktop writes; repeats are not changes | **Auto** — `CompanionQuestsTests` |
| Epic/Sky rows inside the quest surface still tick under their old surface names | **Auto** — `CompanionQuestsTests`, `CompanionSurfaceTests` |
| A same-named attacker while your pet is on HOLD does not break the charm claim | **Auto** — `SpellTrackingTests` |
| Releasing the hold restores the ordinary break rule; another player's pet holding is not our excuse | **Auto** — `SpellTrackingTests` |
| One mez break costs ONE chip, though the game prints both a fade and an awakened line | **Auto** — `MezTrackerTests` |
| Two real breaks in the same second still drop two chips (pairing stays 1:1) | **Auto** — `MezTrackerTests` |
| With no class filter and no tab, a shared drop ticks ONCE and is flagged — never once per class | **Auto** — `SkyLootAutoCheckTests`, `EpicLootAutoCheckTests` |
| A proper-named kill outside the catalog starts tracking; its SECOND kill measures the cycle | **Auto** — `DiscoveredNamedTests` |
| Discovery never fires for articled trash, someone else's kill, or a catalogued family's siblings | **Auto** — `DiscoveredNamedTests`, `NamedMobHeuristicTests` |
| The named/trash verdict is taken from the RAW name at parse time, before `Normalize` strips the article | **Auto** — `NamedMobHeuristicTests` |
| Sky groups by REWARD (not by turn-in NPC), with ready/in-progress/done and a per-reward count | **Auto** — `QuestChecklistLayoutTests` |
| Every checklist row names the turn-in NPC and the drop location; Epic prefers its source | **Auto** — `QuestChecklistLayoutTests` |
| A tick the loot auto-checker placed itself wears `*` on **all three** surfaces, not just mobile | **Auto** — `QuestChecklistLayoutTests`, `CompanionProjectionTests` |
| The class-filter button face never grows with the selection, so the mode strip stays on screen | **Auto** — `ClassFilterLabelTests` |
| A mis-clicked checklist tick can be taken back (↶ undo / Ctrl+Z), repeatedly | **Manual** — open Quests → Plane of Sky, tick a row, undo |
| The Quest Tracker's height cap follows the monitor it is on, re-applied as it is dragged | **Manual** — drag it to a shorter second screen; it must not overflow |
| Ctrl+wheel resizes the Quest Tracker WINDOW, and a saved zoom survives reopening it | **Manual** — zoom out, close, reopen; layout must be identical, only smaller |
| Layout: solo panel fills the viewport; chrome shrinks in fullscreen; nothing scrolls sideways | **Manual** — §6, or the harness |

## 4b. The widget's geometry

| Expectation | Held by |
|---|---|
| The card list covers the same screen height at any UI scale; the last card is always reachable by scrolling | **Auto** — `WidgetMetricsTests` (#144) |
| A dragged height is stored pre-scale and clamped against a cap in the same units | **Auto** — `WidgetMetricsTests` |
| The list never collapses below one card, and no scale can make the cap infinite | **Auto** — `WidgetMetricsTests` |
| A grow-up chip stack holds its bottom edge as chips come and go | **Auto** — `ChipStackAnchorTests` (#122) |
| A closing window (height 0) cannot move the anchor; the stack returns to the same spot across repeated empty/refill cycles | **Auto** — `ChipStackAnchorTests` (#152) |
| A drag moves the anchor; a grow-down stack is never repositioned | **Auto** — `ChipStackAnchorTests` |
| The card list's cap is converted at the point of assignment, in the real app at a real scale | **Auto** — `EndToEndTests` (E2E; #144's other half) |
| The chip anchor is actually wired to the window's events | **Manual** — §6 items 2–3 |
| **Nothing on a timer may change the widget's measured size.** The title-bar CPU/memory readout formats to a fixed shape and its label reserves a fixed width, so a new sample repaints and never asks the windowing system to resize | **Auto** — `PerfReadoutTests`, `WidgetRenderTests` (#173) |
| The readout stays off by default and costs no title-bar width until it is turned on | **Auto** — `WidgetRenderTests` (#112) |
| That an always-on-top widget resizing does not disturb a fullscreen game underneath | **Manual** — §6 item 10; no headless test can see it |

## 4d. Settings, and who is allowed to write them

A save serialises the **whole** `AppSettings` from the snapshot loaded at startup, so any
second writer's changes are reverted wholesale. That is cheap and fine with one writer —
and it was silent with two, which is what made #169 so hard to place.

| Expectation | Held by |
|---|---|
| One EQBuddy per profile on **every** platform; a second launch surfaces the running copy rather than starting a twin | **Auto** — `SingleInstanceTests` (#169) |
| An isolated `EQBUDDY_APPDATA` profile still runs alongside a normal install | **Auto** — `SingleInstanceTests` |
| A stale lock that nobody answers never stops EQBuddy from launching | **Auto** — `SingleInstanceTests` |
| A save that is about to overwrite another writer's changes says so in `error.log` instead of reverting in silence | **Auto** — `SettingsClobberTests` (#169) |
| …and says it once per process, not once per save | **Auto** — `SettingsClobberTests` |
| The two hide-the-widget tick-boxes survive a real click, a reopened Options window, and a restart | **Auto** — `OptionsRenderTests` (#169) |

## 4c. Alert sounds

Which clip an alert plays, and at what volume, is decided in `UI.Shared/AlertSoundPlan.cs`
so it can be pinned without an audio device. Both UIs obey the same plan.

| Expectation | Held by |
|---|---|
| A rule's own sound beats the shared choice; a muted rule resolves to nothing; legacy SystemSounds names map onto the palette | **Auto** — `AlertSoundTests` |
| A built-in resolves to this platform's own file; "Off" plays nothing and reports nothing | **Auto** — `AlertSoundPlanTests` |
| **Every audible outcome carries the Options volume** — built-in, custom, and substitute alike. There is no route left that makes a noise the slider cannot reach | **Auto** — `AlertSoundPlanTests` (#153) |
| A custom file that has gone missing substitutes Ding *at the chosen volume* and names the missing file so the UI can say so — never a silent swap | **Auto** — `AlertSoundPlanTests` (#153) |
| A gap in the platform's own sound theme substitutes without blaming the player | **Auto** — `AlertSoundPlanTests` |
| A stored volume of NaN or out of range is clamped rather than handed to the player | **Auto** — `AlertSoundPlanTests` |
| That the plan is actually audible — the player, the device, the clip | **Manual** — §6 item 6 |

## 5. The gap — read this before trusting the suite

**`src/EQBuddy` (the WPF app, 14,432 lines across 37 files) has no automated coverage.
No test project references it.**

**Partly closed 2026-08-14.** The *arithmetic* behind both reported bugs now lives in
`UI.Shared` and is unit-tested — `WidgetMetricsTests` (screen-to-pre-scale conversions,
#144) and `ChipStackAnchorTests` (grow-up anchoring, #122 and #152). Both suites were
verified by reintroducing the original bugs and watching them go red. The windows keep
only the wiring, which is the part a human still has to look at.

Everything below remains **Manual** and can only be caught by a human or a player:

- Window placement, screen guards, and the wiring of the transform to the controls
- Card expand/collapse, section order, star/pin behaviour, the mini pill
- Chip stacks — that the tested anchoring is actually wired to the window events
- Click-through, see-through, focus-hide, tray icon, hotkeys
- The zone map window: rendering, pan/zoom, context menus, camp pins
- Alert banners and sounds at the moment they fire

This is where the risk lives, and it is not theoretical. **Both player-reported bugs of
2026-08-14 — the clipped Epics card (#144) and the drifting chips (#152) — are in this
layer, and no existing test could have caught either.** Both were also unit-mismatch
bugs of the kind a small pure helper *could* pin.

### Recommended, in order of value per effort

1. ~~**Extract the arithmetic, then test it.**~~ **Done 2026-08-14** — see
   `UI.Shared/WidgetMetrics.cs` and `UI.Shared/ChipStackAnchor.cs`. Apply the same move
   to the next window bug rather than fixing it in place: if it is a sum, it belongs in
   `UI.Shared` where it can be pinned.
2. **A WPF render-test project** mirroring `EQBuddy.Avalonia.Tests`, which already
   proves the approach works on this codebase. Would cover placement, scaling and chip
   layout properly.
3. **Extend E2E** past its four scenarios into what the widget *shows*, not just what it
   ingests. **Started 2026-08-14**: the `EQBUDDY_EXPAND` dump now carries `uiScale100`,
   `sectionCapScreen` and `sectionMaxH`, and two scenarios assert the conversion in the
   launched app at 1.6x and at 1.0x. The pattern generalises — **to cover a piece of
   window behaviour, dump the fact and assert it from E2E.** It is cheaper than it looks
   and it is the only route that sees real WPF layout.

   *Note on the road not taken:* a WPF unit-test project (recommendation 2) is harder
   than the Avalonia precedent suggests, because Avalonia ships a headless platform for
   exactly this and WPF does not. E2E already launches the real app; extending it beats
   building a second harness that would fight the desktop.

## 6. Manual pass

Run before a release that touches the widget, and after any change in §5.

Orient first with `pwsh -NoProfile -File scripts/status.ps1`, which lists anything
open and any thread waiting on a reply.

**Setup** (never against the real profile):
```bash
EQBUDDY_APPDATA=<scratch>   # isolated settings/history
EQBUDDY_EXPAND=1            # all sections expanded + a state dump
```
Fixture logs: see [FeatureGuide.md](FeatureGuide.md) §"Testing without playing".

1. **Scale** — drag the size grip from 80% to 160%. Every card stays reachable by
   scrolling at *every* scale; the bottom card is never clipped. (#144 — the cap's
   arithmetic and its wiring are now both automated; this pass is for what neither can
   see: whether it *looks* right.)
2. **Chips, grow-up** — enable grow-upwards, let a mez stack empty completely, mez
   again. The stack returns to the same spot. Repeat five times; it must not walk. (#152)
3. **Chips, grow-down** — same, unmoved.
4. **Multi-monitor** — drag the widget to a portrait secondary screen; height caps
   follow that monitor.
5. **Focus-hide / click-through / see-through** — each toggles cleanly and the tray icon
   always brings it back.
6. **Alerts** — a watch rule fires with banner, sound and colour, and the cooldown
   behaves per matched label. Then, with a **custom** .wav chosen in Options, compare
   the volume slider at 10% and at 100%: they must differ, and renaming that .wav must
   produce the "Alert sound file is missing" banner rather than a different noise (#153).
7. **Mobile** — pair a device; untick a surface on the PC and it says "Not shared";
   tick an Epic row on the device and the desktop card repaints; curate a spawn point
   and the PC map drops the circle within a tick; "New code" drops paired devices.
8. **Mobile layout** — one surface picked fills the screen on both a phone and a tablet;
   fullscreen shrinks the chrome; no sideways scroll at 375 px.
9. **Off switch** — set `CompanionEnabled=false`, restart, confirm nothing is listening.
10. **The readout over a fullscreen game** (Linux especially) — play fullscreen, tick
    Options → Behavior → "Show EQBuddy's own CPU & memory", and keep playing for a few
    minutes. Keyboard and mouse must both keep reaching the game, and the widget must not
    visibly change width as the numbers move. (#173 — the readout used to resize a real
    always-on-top window every three seconds.)
11. **Second launch** — with EQBuddy already running, launch it again. Exactly one widget
    exists afterwards, the running one comes to the front, and settings changed just
    before the second launch are still set. (#169 — off Windows there was no guard at
    all, and the loser of the race lost every setting it had changed.)

## 6b. The docs check themselves

| Expectation | Held by |
|---|---|
| Every file `CLAUDE.md`, `docs/Architecture.md` and this plan point at actually exists | **Auto** — `DocumentationTests` |
| Every test class cited in the Held-by column above exists | **Auto** — `DocumentationTests` |
| The ratchet table in `docs/Architecture.md` matches `ArchitectureTests`' baselines | **Auto** — `DocumentationTests` |
| `CLAUDE.md` links the other docs, so none is orphaned | **Auto** — `DocumentationTests` |

This is the mechanism behind §7. Documentation rots when keeping it true is a habit;
it stops rotting when the build fails. It caught a wrong path in `CLAUDE.md` on its
first run. What it cannot check is prose being *misleading* while every path resolves —
that still needs a reader.

## 7. Keeping this true

- Behaviour change → update the row in the same commit.
- New regression test → move its row from Manual/Partial to **Auto** and name it.
- A bug that reached a player → add its expectation here, and its cause to `CLAUDE.md`'s
  trap list. Every entry in §5 that becomes Auto is permanent progress.
- Re-audit the counts and the ratchet table in [Architecture.md](Architecture.md) at
  each minor version.
