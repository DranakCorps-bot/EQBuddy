# Themes: consolidating the surfaces

**Status: ACCEPTED DIRECTION.** David, 2026-08-19: *"I do want to move to themes, this
is part of the major UX revamp that organizes everything much better. it's not a proposal,
it's a direction we need to go, just as we did with quests."*

Written 2026-08-17 as a proposal; ruled on 2026-08-19. **Quests is built and is the
template. The other five are the work**, in the sequencing below — not a menu to pick
from, and not something to re-argue. What is still open is HOW each one lands, never
whether.

The Quest Tracker is the worked example. Three separate things — the widget's "Sky
Quest" card, its "Epics" card, and a general quest catalog — became one theme with
three tabs, one definition in Core, and one launcher on the widget. This file
generalises that into a recipe, then applies it to what's left.

---

## Why bother

The widget carries **14 cards** (Combat, Healing, Kills, Loot, Motes, Quests, Gear,
Watch, Buffs, Raids, Money, Progress, Faction, Travels & Deaths) and the app carries
**20+ windows**. Every one was justified when it was added. Together they are a menu,
not a product, and three specific costs are already being paid:

1. **Surfaces disagree.** Sky rows wore the auto-tick `*` on the phone and a bare tick
   on both desktops for months (#184) because three renderers each owned their own
   idea of a row. One definition in Core is the only thing that stops this.
2. **A rewrite drops things silently.** Folding the Sky card into the tracker lost the
   drop location, lost the `*`, and left `SkyQuestClass` with no writer — which turned
   the loot auto-tick's class filter into a wildcard and ticked items the player never
   looted (#193). A theme with one owner has one place to check.
3. **MainWindow is the hotspot** (4,432 / 4,701 lines). `CLAUDE.md` is explicit that
   the way out is to **lift a surface out**, not to split the file. Each theme lifted
   is real headroom.

---

## The recipe

Six steps, in this order. The quest consolidation did all six; the class names below
are the precedent to copy.

| # | Step | Precedent |
|---|---|---|
| 1 | **Name the tabs in Core.** An enum + labels + stable wire keys, so no UI invents its own. | `Core/QuestSurface.cs` (`QuestTab`, `LabelFor`, `KeyFor`) |
| 2 | **Put the row shape in Core too.** Grouping, ordering, headings, detail text — pure and unit-tested. | `Core/QuestChecklistLayout.cs` |
| 3 | **One launcher card** on the widget, showing the summary that used to justify the separate cards. | Quests card: `Epic 0/486 · Sky 0/222` |
| 4 | **One window, tabs inside, filters shared across tabs.** | `QuestsWindow` — class lens, era, state, mode strip |
| 5 | **Fold the old settings keys**, preserving position and hidden state. | `sky`/`epic` → `quests`, pinned by `OptionsViewModelTests` |
| 6 | **Mobile in the same change**, reading the Core definitions from step 1–2. | `CompanionProjection.Checklists` |

**Pin behaviour in E2E before the move**, not after — facts into the `EQBUDDY_EXPAND`
dump, asserted from `tests/EQBuddy.E2E`. With no unit tests in the WPF layer that
assertion is the only thing between a lift and a silent regression. Then lower the
hotspot baseline in the same commit, or the room refills.

**The surface rule decides where a theme lives**, and it is a filter, not a
formality — *is there something the player must do, and a moment by which they must
do it?* Overlay for deadlines, phone for anything worth looking away for, desktop for
before-and-after. A theme that spans two surfaces is fine; a theme that can't answer
the question isn't a theme.

---

## The proposed themes

Six, covering 13 of the 14 cards and most of the windows.

### 1. Quests — **done, and the template**
General · Epic 1.0 · Plane of Sky. Phone + desktop.

### 2. Live Meters
**Tabs:** Damage · Healing · Pet · Encounters
**Absorbs:** Combat card, Healing card, Kills card, the Damage/Healing/Pet breakout
windows, `FightTimelineWindow`
**Surface:** phone + desktop. **Not the overlay** — nothing about seeing 412 rather
than 438 changes the next second, and the comparison that makes competitors put it
on-screen is the thing we refuse to build.
**The exception to keep separate:** the *binary* "am I actually attacking / is my pet
idle" does pass the deadline test. If that gets built it is a chip, not a meter.
**Note:** `BreakoutKind` already gates these per kind via `AppSettings.DisabledBreakouts`,
and David uses the damage breakout — so this is a defaults-and-home change, not a
deletion.

### 3. Progress
**Tabs:** Experience · Wealth · Faction · Raids
**Absorbs:** Progress card (xp/hr, time to level), Money card, Motes card, Faction
card, Raids card
**Surface:** phone + desktop. Entirely retrospective — this is the theme the
all-time-stats direction (#168/#159) plugs into, and Wealth is where the mote
weighting from #154 belongs.

### 4. Alerts
**Tabs:** Watch rules · Buffs · Spawns · Mez/Charm
**Absorbs:** Watch card, Buffs card, and the *management* of `SpawnChipsWindow`,
`MezChipsWindow`, `AlertWindow`
**Surface:** the **chips stay on the overlay** — every one of them is a deadline with
an action, which is exactly what earns that space. What consolidates is the
*configuration*: four different places to say "alert me, at this volume, with this
sound" become one. `UI.Shared/AlertSoundPlan.cs` already owns the decision; this gives
it one front door.

### 5. Loot & Items
**Tabs:** Session loot · Drops by creature · Item lookup · Gear
**Absorbs:** Loot card, Gear card, `DropsWindow`, `ItemInfoWindow`, `InventoryWindow`,
`GearLockerWindow`
**Surface:** phone + desktop. This is where #174's approved features land — mob
lookup, "what is this for?" from a loot row, upgrade preview at +N.

### 6. World
**Tabs:** Map · Camps & timers · Routes · Travels
**Absorbs:** Travels & Deaths card, `MapWindow`, `SpawnsWindow`, `TravelWindow`,
`ZoneShareWindow`
**Surface:** phone + desktop, with spawn-due chips staying on the overlay.

**Stays as-is:** Options, History, Tutorial, WhatsNew, Feedback, SessionPicker,
Companion, CursorRing, GridOverlay. These are chrome and setup, not themes.

**Outcome:** 14 cards → 6, and each theme owns one window with one Core definition
behind it.

---

## Sequencing

Ordered by value per unit of risk, not by size.

1. **Alerts** — highest value, lowest risk. It is mostly a settings surface, the chips
   it configures already work, and `AlertSoundPlan` is already shared and tested.
   Also closes the "settings live in Options except where they don't" seam.
2. **Loot & Items** — unblocks #174, which is already approved and waiting.
3. **Progress** — pure aggregation over data already computed; the natural home for
   all-time stats.
4. **Live Meters** — biggest MainWindow lift, so the most E2E pinning needed first.
5. **World** — the map is the heaviest surface and the one with the most mobile
   parity work; do it when the recipe is boring.

One theme per release. Each lands with its card-key folding, its mobile parity, and a
lowered hotspot baseline in the same commit.

---

## What would make this a bad idea

Worth writing down so the plan can be argued with:

- **Consolidation that hides a deadline is a regression.** If a tab buries something
  the player needed within seconds, the surface rule was applied wrongly — the answer
  is a chip, not a tab.
- **Fewer cards is not the goal; fewer *definitions* is.** A theme that ships one
  window but still has three renderers disagreeing has bought nothing (#184 in
  miniature).
- **The `wide`/`fills` lesson.** Reusing a presentation class for its layout and
  inheriting its behaviour shipped an unscrollable quest list on mobile. Every theme
  reusing a container must read every rule that selects it.
- **Do not consolidate on the way to a release.** Each of these touches settings
  migration; the fold-the-old-keys step is where silent data loss lives.
