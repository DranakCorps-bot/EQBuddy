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

Seven steps, in this order. The quest consolidation did the first six; step 7 was added
on 2026-08-19 after the Progress theme shipped without it and a player lost a stat inside
the hour. The class names below are the precedent to copy.

| # | Step | Precedent |
|---|---|---|
| 1 | **Name the tabs in Core.** An enum + labels + stable wire keys, so no UI invents its own. | `Core/QuestSurface.cs` (`QuestTab`, `LabelFor`, `KeyFor`) |
| 2 | **Put the row shape in Core too.** Grouping, ordering, headings, detail text — pure and unit-tested. | `Core/QuestChecklistLayout.cs` |
| 3 | **One launcher card** on the widget, showing the summary that used to justify the separate cards. | Quests card: `Epic 0/486 · Sky 0/222` |
| 4 | **One window, tabs inside, filters shared across tabs.** | `QuestsWindow` — class lens, era, state, mode strip |
| 5 | **Fold the old settings keys**, preserving position and hidden state. | `sky`/`epic` → `quests`, pinned by `OptionsViewModelTests` |
| 6 | **Mobile in the same change**, reading the Core definitions from step 1–2. | `CompanionProjection.Checklists` |
| 7 | **Say that it moved**, in the app and in the tour. | `OverlaySections.AbsorbedNote`, the "Cards that open windows" tutorial page |

**Step 7 is not politeness, it is the one step a fold cannot do for itself.** A
consolidation is invisible by construction: the thing that would have told you where a
card went is the card that was removed. #219 (typical-usual-chaos) is the worked example —
the Motes card became a tab, he went to **Options → Cards & windows**, which is the one
screen in the app whose entire job is to list every card, found no Motes row and nothing
anywhere saying why, and filed *"now I can't get it back"*. From where he stood the
feature had been deleted, and he was not wrong to say so.

So the surviving card names the cards it absorbed, **by their old titles**, on that
screen — "Money · Motes · Faction · Raids are tabs in here now" — because those are the
words someone is scanning for. `OverlaySections.AbsorbedTitles` is the one table; add a
line to it in the same commit as the fold. And re-read the tour: it told people every card
"drills into details" for three months after the first card became a launcher.

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

**And on 2026-09-05 it became the first theme to leave the widget altogether** (HUD
subtraction cut 1). The launcher card in the table below is gone; the window, the four
rooms and the phone are untouched, and the Evolved shell's Quests room is where the
surface lives now. It went first for the reason this table can be read for: it is the only
one of the ten cards that strands nothing — no `MiniStats` star writer, no HUD chip owed,
and a way into its window that does not depend on the card. Everything below stays a true
record of the fold that made this cut possible.

### 2. Live Meters
**Tabs:** Damage · Healing · Pet · Encounters
**Absorbs:** Combat card, Healing card, the Damage/Healing/Pet breakout
windows, `FightTimelineWindow`
**The Kills card is NOT here, and that was David's correction** (2026-08-20): *"Kills isn't
a meter though. we don't track kills per second but we track damage per second, healing per
second. Kills and Drops should be ... Kills and Drops ;)"* A meter is a per-second board;
kills/hour is a rate, which is not the same thing. See theme 7.
**Surface:** phone + desktop. **Not the overlay** — nothing about seeing 412 rather
than 438 changes the next second, and the comparison that makes competitors put it
on-screen is the thing we refuse to build.
**The exception to keep separate:** the *binary* "am I actually attacking / is my pet
idle" does pass the deadline test. If that gets built it is a chip, not a meter.
**Note:** `BreakoutKind` already gates these per kind via `AppSettings.DisabledBreakouts`,
and David uses the damage breakout — so this is a defaults-and-home change, not a
deletion.

### 3. Progress — **BUILT 2026-08-19, and it went first**
**Tabs:** Experience · Wealth · Faction · Raids
**Absorbs:** Progress card (xp/hr, time to level), Money card, Motes card, Faction
card, Raids card
**Surface:** phone + desktop. Entirely retrospective — this is the theme the
all-time-stats direction (#168/#159) plugs into, and Wealth is where the mote
weighting from #154 belongs.

**Shipped as:** `Core/ProgressSurface.cs` (tabs, labels, keys, absorbed-card list),
`UI.Shared/ProgressTheme.cs` (badges + the launcher line), `EQBuddy/ProgressWindow.xaml`
(the Avalonia twin went with that lane on 2026-09-04), `AppSettings.MigrateProgressSections`, and the
four-tab `CompanionProgressSection` on the phone. 14 cards → 10.

**What the build taught, beyond the recipe:**

- **The launcher line has to FIT, and only a screenshot says whether it does.** Passing the
  tab badges straight through rendered `16.0% xp, +1 aa · 5p 1g 4s 8c · 1 mot…` — clipped
  mid-word, faction lost off the end. The launcher takes each part's headline; the tabs
  carry the detail. A summary that replaces five headers and then truncates has not kept
  the glance, it has hidden the loss.
- **The tab strip must WRAP.** A horizontal `StackPanel` measures with infinite width, so
  the fourth chip was simply clipped off the window with nothing to say so — trap 14 with
  chips instead of text, and the same bug #184 hit at NEC. Found by looking at the first
  screenshot, where Raids was not on screen at all.
- **Check what still WRITES every setting the folded cards owned** (trap 20). Three cards
  here carried the only `MiniStats` writers for `xp`, `money` and `motes`. They moved into
  the window rather than onto the launcher — three identical stars in one card header say
  nothing about which is which, and a `ToggleButton` nested in a `Button` bubbles its
  `Click` to the button's handler, so every star press would also have opened the window.
- **Shot names are not identities.** Four shots now share the window title
  `EQBuddy Progress`, and a previous shot's app that has not finished exiting is a perfect
  match for the next shot's request — which filed a Faction tab as `progress-wealth.png`.
  `shot.ps1` takes `-OwnerPid` now.

### 4. Alerts
**Tabs:** Watch rules · Buffs · Spawns · Mez/Charm

**Where the configuration lands: ONE ALERTS WINDOW, with tabs, like Progress and Quests**
(David, 2026-08-20, asked before the work rather than during it). This is the one theme
whose home was genuinely open, because "settings live in Options" is a standing rule and
alert configuration is settings — so the recipe and the rule pointed different ways. The
window wins: four places to say *"alert me, at this volume, with this sound"* is the
problem being solved, and moving three of them into a fourth tab of Options would leave
the per-named bells in the Spawns window out in the cold anyway. Options keeps what is
genuinely global; the window owns what belongs to a rule, a buff, a named or a mez.
**Absorbs:** Watch card, Buffs card, and the *management* of `SpawnChipsWindow`,
`MezChipsWindow`, `AlertWindow`
**Surface:** the **chips stay on the overlay** — every one of them is a deadline with
an action, which is exactly what earns that space. What consolidates is the
*configuration*: four different places to say "alert me, at this volume, with this
sound" become one. `UI.Shared/AlertSoundPlan.cs` already owns the decision; this gives
it one front door.

### 5. Gear & Loot
**Tabs:** Loot · Wishlist · Inventory (`ItemInfoWindow` is still named-but-unhosted;
Drops LEFT this theme on 2026-08-20 for Kills & Drops — it is about the MOB, not your bags)
**Absorbs:** Loot card, Gear card, `ItemInfoWindow`, `InventoryWindow`, `GearLockerWindow`
**Shipped:** Windows 2026-08-20, Linux/macOS 2026-08-21 — the Inventory tab was the
one-release gap between them. Both windows are deleted on both builds.
**Surface:** phone + desktop. This is where #174's approved features land — mob
lookup, "what is this for?" from a loot row, upgrade preview at +N.

### 6. World — **BUILT 2026-08-27 (PR 0-4, all lanes and the phone)**
**Tabs:** Map · Camps · Path · Travels (Bevel-signed pre-design; "Path" rather than
"Routes" so it stops reading as a near-synonym of "Travels")
**Absorbs:** Travels & Deaths card, `MapWindow`, `SpawnsWindow`, `TravelWindow`,
`ZoneShareWindow` (the last stays a desktop dialog, opened from the Map tab — its door
moves, the window does not)
**Card key stays `misc`** — this theme absorbs exactly one card, so there is no settings
migration at all; the card's title becomes "World" (PR 3).
**Surface:** phone + desktop, with spawn-due chips staying on the overlay. The phone
gains a `travel` surface and camp-marker pins (PR 4); `map`/`spawns` stay separate
first-class phone surfaces on purpose — a tablet showing both at once is the product's
uncontested ground, and folding them to match the desktop window would delete that.

### 7. Kills & Drops — **BUILT 2026-08-21**
**Tabs:** Kills · Drops
**Absorbs:** Kills card, `DropsWindow`
**Surface:** phone + desktop. Both tabs are about the CREATURE — what died, and what it
dropped at what rate — which is one question ("is this camp worth it?") that was being
answered in two places, one of them buried in the cog menu where nobody found it.
**Card key stays `kills`**, so there is nothing for a settings migration to fold: the card
keeps whatever slot the player put it in, and its mini-dashboard star moved into the window
with the header it belonged to (trap 20/26 — that star is `MiniStats`' only writer for
"kills").
**Shipped on both builds in the same change**, because the theme switches on in shared
vocabulary: a fold that landed on Windows alone would take the Kills card off the Linux and
macOS widget with nowhere for it to go.

**Stays as-is:** Options, History, Tutorial, WhatsNew, Feedback, SessionPicker,
Companion, CursorRing, GridOverlay. These are chrome and setup, not themes.

**Outcome:** 14 cards → 6, and each theme owns one window with one Core definition
behind it. **Three are built** (Quests, Progress, Gear & Loot) plus Kills & Drops, which
the numbering above puts last only because it was not in the original plan — it came out of
David reading "Kills" under Live Meters and disagreeing.

---

## Sequencing

Ordered by value per unit of risk, not by size.

**This list was written on 2026-08-17 and Progress went first anyway. Re-measure before
picking the next one.** The order below ranked value against risk correctly for the
codebase as it stood — but Gate 5b then lifted four card bodies onto the `IWidgetCard`
seam, which is most of the work a theme fold does. Measured again on 2026-08-19: Progress
had 4 of its 5 cards already portable and bought 14 cards → 10; Alerts had 1 of 2 and
would have bought 14 → 13, with `RenderBuffs` (107 lines, tangled into the buff-set
evaluator) to lift first. **The right question is not "what does the plan say next" but
"which theme is most nearly built already".**

0. ~~**Gear & Loot**~~ — **done 2026-08-20** (Windows) and **2026-08-21** (Linux/macOS,
   the Inventory tab), and ~~**Kills & Drops**~~ — **done 2026-08-21**, both builds at once.
   Kills & Drops was not on this list at all: it exists because David rejected Kills being
   filed under Live Meters, which is the best argument in this document for showing the
   grouping to a player before building the window.
1. ~~**Progress**~~ — **done 2026-08-19.** Pure aggregation over data already computed;
   the natural home for all-time stats.
2. **Alerts** — mostly a settings surface, the chips it configures already work, and
   `AlertSoundPlan` is already shared and tested. Also closes the "settings live in
   Options except where they don't" seam. Needs `RenderBuffs` lifted first.
3. ~~**Gear & Loot**~~ — done; #174's features can land on its Items tab, which is
   named-but-unhosted.
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
