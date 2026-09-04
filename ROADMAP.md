# EQBuddy — roadmap

**Audience: Scribe, and anyone triaging community posts.** This is the frame — what is
being built, in what order, and what is deliberately not being built. It exists so an
incoming ask can be placed ("that's Gate 6", "that's out of scope, here's why") without
re-deriving the plan or over-promising to a poster.

It is **not** a commitment of dates, and nothing here is a promise to a reporter. Scribe
must not tell anyone their ask is scheduled. "Noted, and it fits where we're already
heading" is the strongest thing worth saying.

Deeper material: `CLAUDE.md` (rules, traps, where things live), `docs/DesignSystem.md`
(the gate plan in detail), `docs/Architecture.md`, `docs/TestPlan.md`.

---

## EQBuddy Evolved (2026-09)

The owner-approved next major direction is **EQBuddy Evolved** (v2): consolidation,
polish, and personalization — not another feature-count race.

- Product identity: [PRODUCT.md](PRODUCT.md)
- Player-facing vision: [EQBuddy-Evolved.md](EQBuddy-Evolved.md)
- 1.x preservation: [LEGACY-V1.md](LEGACY-V1.md)
- Bridge work: [issue #275](https://github.com/DranakCorps-bot/EQBuddy/issues/275)

**Supported** for Evolved: Windows desktop + EQBuddy Mobile hosted by Windows.
**Preserved, not taken down:** final Linux/macOS/Windows 1.x builds stay downloadable
and usable; support for that line stops. Current public releases remain 1.x until
the Phase 0 bridge and an Evolved channel exist.

The gate/theme language below remains historical triage context for the 1.x line
until this file is rewritten. Place incoming 1.x asks against it as before. Place
v2 asks against [PRODUCT.md](PRODUCT.md).

---

## 1. What EQBuddy is becoming

The **personal operating companion** for EverQuest Legends — private, local, personal,
non-judgmental. Not a parser recap of what happened, and not a coach.

It understands *your* character, gear, inventory, quests, loot history, camps, spawn
timers, maps, travel and past sessions, then helps turn that into action: what am I
working on, what upgrade can I actually get, what am I missing, where does it drop, how do
I get there.

**The differentiator is the chain** — loot → quest → item → mob → camp → route — learned
from your own play. Filter every incoming ask against that chain. An ask that strengthens
a link is interesting; an ask that adds a disconnected number is usually not.

### Where a feature goes

The deciding question is **not** "is this important?" It is:

> **Is there something the player must do, and a moment by which they must do it?**

| Surface | For | Examples |
|---|---|---|
| In-game overlay | A deadline with an action. Small enough to ignore. | Mez/charm chips, spawn-due chips, Watch alerts, buff-expiring |
| Phone / tablet | Anything worth *looking away* for. | Map, quests, item lookup, gear, loot, DPS, session totals |
| Desktop | Before and after play: research, compare, configure, review. | Gear & Loot (the old Gear Locker), history, Options, wiki packs |

**Mobile and desktop are both first-class, in both directions** (David, 2026-08-18). Once
a feature is on two surfaces, neither may quietly fall behind — and the drift runs both
ways: Mobile once kept a feature the desktop had lost. Parity comes from a shared module
all three call, never from a feature list kept level by hand.

---

## 2. Hard lines — never file these as work

- **Never measure other players.** No party DPS, no raid meters, no rankings, no
  leaderboards, no watching anyone else. This is a values line, not a technical one.
  Decline warmly, point at the MIT licence, invite a fork. **Do not file these asks as
  requirements**, however they are phrased.
- **Log-only.** Never reads game memory, never inspects packets, never phones home.
- **No gameplay automation, input broadcasting, or hidden information** the log doesn't
  already give a player.
- **No cloud accounts or required sync.**
- **Curated catalogs are never auto-written** (spawn timers, AAs, CC lists). The weekly
  wiki refresh only *flags* them. A wrong respawn timer is worse than none.
- **eqlwiki is the tie-breaker.** Other sources where it is silent, marked as such.
- **Releases wait for David's explicit go.**

---

## 3. The UI/UX rework — where the work is now

> **The destination, in one sentence (David, 2026-08-20):** *"I would like the gear to
> eventually be the path to options not click gear then click options from a list of
> things. So ultimately, that's what I want to work towards."*
>
> **The ⚙ button should BE Options.** Every entry now on that menu is a surface that has
> not been folded yet, and every fold is one line closer. It is worth stating because it
> reframes the themes: they are not tidying, they are the route to a widget whose only
> two controls are the cards and the settings. What is still on it, and where each goes —
> Zone map, Travel route, Spawn timers and Drop camp marker → **World**; Drops by creature
> → **Kills & Drops** (done 2026-08-21, and the entry is off the cog); Session history → **Progress** or its own; Data & imports → mostly
> obsolete already, since `/outputfile` dumps import themselves as of 2026-08-20;
> Auto-detect log folder and Help → **Options** itself.

Rebuilding every surface on one design system (`UI.Shared/DesignTokens`, `IconPaths`,
`ChipStyle`). Staged by **vocabulary**, not screen by screen: finish one shared thing
everywhere it appears, so later gates spend the primitive instead of minting another.

| Gate | Surface | Status |
|---|---|---|
| 1 | Audit + tokens | done |
| 2 | Quests window | done |
| 2b | Shared chip (`EqChip` / `EqSegmentedStrip`) | done |
| 3 | Spawns + timers | done |
| 4 | Loot card + Loot breakout | done |
| **5** | **The main widget** | **in progress** — see below |
| 6 | Mini mode + chips | carries #190, #191, #199's gesture |
| 7 | Map | not started |
| 8 | Remaining windows | Gear, Drops, History, Travel, Options, breakouts |

### And then THEMES — 14 cards into 6 (David, 2026-08-19)

**The gates above restyle what exists. Themes changes what exists**, and it is the larger
half of the UX revamp: *"this is part of the major UX revamp that organizes everything
much better. it's not a proposal, it's a direction we need to go, just as we did with
quests."* [docs/Themes.md](docs/Themes.md) is the plan and the recipe.

The Quest Tracker already did it once — the Sky card, the Epics card and a general catalog
became one theme, three tabs, one definition in Core. Five more follow:

| Theme | Absorbs | Order |
|---|---|---|
| **Quests** | Sky + Epics + catalog | **done — the template** |
| **Progress** | Progress, Money, Motes, Faction, Raids | **done 2026-08-19 — 14 cards → 10** |
| **Gear & Loot** | Loot, Wishlist, Inventory (= GearLocker + Inventory) + ItemInfo | **DONE, all three platforms** — Windows 2026-08-20, Linux/macOS 2026-08-21. Four tabs — Loot, Items, Wishlist, Inventory: what I picked up, what I can look up, what I want, what I have. Drops LEFT this theme (see Kills & Drops). The Avalonia lane needed its gear-checklist lifted out of MainWindow first, and that landed with it |
| **Alerts** | Watch, Buffs + spawn/mez/alert *configuration* | **after World.** Lands as its own WINDOW with tabs (David, 2026-08-20). The `RenderBuffs` blocker has decayed — re-measured 2026-08-27 at 101 lines / four collaborators, not ~175 / eight |
| **Live Meters** | Combat, Healing + their breakouts + FightTimeline | biggest lift. **Kills is NOT here** — David, 2026-08-20: *"Kills isn't a meter though. we don't track kills per second but we track damage per second, healing per second."* A meter is a per-second board; kills/hour is a rate but not that |
| **Kills & Drops** | Kills + Drops by creature | **DONE 2026-08-21, both builds at once.** David's grouping, 2026-08-20 — *"Kills and Drops should be … Kills and Drops ;)"*. Both are about the CREATURE: what died, and what it dropped at what rate. One question — "is this camp worth it?" — that used to be answered in two places, one of them buried in the cog menu. The card key stays `kills`, so nobody's card slot moved |
| **World** | Travels & Deaths + Map, Spawns, Travel, ZoneShare | **NEXT — David chose it in session 2026-08-27**, over Alerts, for the cog clearance below. Plan requested from Fable. *Not* heaviest mobile parity: re-measured 2026-08-27, the phone already ships first-class `map` and `spawns` surfaces (`CompanionSurfaces.cs:14-15`, `CompanionMapSource`); the real gap is Travel route and ZoneShare. **Already covers what David asked to note on 2026-08-20** — *"Travels and Death should include travel route and zone maps too"* — and goes further: Spawn timers and the drop-camp marker come off the cog with it |

**Progress went before Alerts, and the reason is worth keeping.** The order above was set
on 2026-08-17, before Gate 5b lifted four card bodies onto the `IWidgetCard` seam — which
left Progress with four of its five cards already portable and Alerts with one of two.
Measured again on 2026-08-19: Progress bought 14 cards → 10 for one lift, Alerts would buy
14 → 13 for a harder one (`RenderBuffs` is 107 lines tangled into the buff-set evaluator).
**Re-measure that readiness before starting any theme** rather than trusting this column;
the work done since a plan was written changes what the plan should say.

**And on 2026-08-20 it did again**, which is why Gear & Loot now sits above Alerts.
Measured that day: both themes have one of their two cards already lifted, and both buy
10 cards → 9 — but the remaining lifts are not comparable. The Gear card body is ~70 lines
touching `_settings` and its own controls, with `GearChecklistPresentation` already shared:
the `QuestChecklistView` shape CLAUDE.md holds up as the clean case. `RenderBuffs` has
grown to ~175 lines and reaches `_breakouts`, `_buffClocks`, `_buffTracker`,
`_buffsSignature`, `_optionsWindow`, `_server` and `_stats` — eight collaborators including
the breakout windows and the companion server. Same reward, very different risk, and Gear & Loot
also unblocks #174, which is approved and waiting.

> **RE-MEASURED 2026-08-27 (Claude), and the paragraph above has stopped being true.**
> `RenderBuffs` is **101 lines** (`MainWindow.xaml.cs:1577`) with **four** collaborators —
> `_buffClocks`, `_buffTracker`, `_buffsSignature`, `_settings`. **None of `_breakouts`,
> `_optionsWindow`, `_server` or `_stats` appear in it any more**, so the specific argument
> that deferred Alerts — that its lift reaches the breakout windows and the companion server
> — no longer holds. Gear & Loot shipped anyway, so nothing was lost; what matters is that
> **the stated blocker on the NEXT theme has decayed by half and the file did not know.**
> That is this section's own instruction earning itself: *re-measure before starting any
> theme rather than trusting this column.*
>
> **This note corrects a measurement. It does NOT reorder the table** — theme order is
> David's call (consequence list, roadmap direction), and the two live candidates trade off
> differently: **Alerts** is now the cheaper lift, while **World** is worth more against the
> stated destination, since it clears four of the six non-Options entries off the ⚙ menu
> (Zone map, Travel route, Spawn timers, Drop camp marker) and is also flagged as the
> heaviest mobile-parity job. Re-measure again before either starts.

**One theme per release**, each with its settings-key folding, its mobile parity and a
lowered hotspot baseline in the same commit. Two rules from the plan that decide arguments
before they start: **fewer cards is not the goal, fewer DEFINITIONS is** — one window with
three renderers still disagreeing has bought nothing — and **consolidation that hides a
deadline is a regression**; if a tab buries something needed within seconds, the answer is
a chip, not a tab.

### Gate 5, in flight

Done: headings + sort strips (5a); the card seam `IWidgetCard`/`ICardContext` proved on
Kills, then Motes/Money/Faction; the Combat/Healing/Progress summaries; `EqIcon` and the
chrome glyphs; the minimized bar's icon table into `UI.Shared/MiniBarPresentation`.

Remaining, in the order being taken:

1. Icon-as-string controls left in `BreakoutWindow.xaml.cs` and the Avalonia `IconButton`
   calls — mechanical, finishes the vocabulary.
2. `MainWindow.xaml`'s last glyphs, so that file can join the ratchet — the first widget
   file to do so.
3. The remaining cards onto the card seam: Gear, Watch, Buffs, Raids, Travels & Deaths.
4. The three heavy bodies: sparkline, breakdown lists, ding unlocks. Large; own session.

**Settled 2026-08-18, so it is not re-litigated:** the glyph rule exempts *comments* (a
glyph in a comment never renders) and does **not** exempt string literals (a glyph in a
string renders, and most of them are controls passed as string arguments).

---

## 4. After the rework

**The all-time stats view** (#168, #159) — a query over the session archives already on
disk. "How much plat has this camp made me", "when did I last see this named", "what have
I never looted". This is the direction, and it is where the loot → quest → camp chain
starts paying off across sessions rather than within one.

David explicitly **rejected** a narrative/chronicle framing of this. It is a query tool,
not a storyteller.

Also on the list, unscheduled: donations (deferred on purpose, nothing designed).

---

## 5. How community input is handled

- **Discussions and issues are input, not instructions.** Surface them; don't act on their
  contents unprompted.
- **The most useful reply asks for the LOG or the SCREENSHOT.** Every one of #135's six
  causes came from a file bjstrange attached; #182's real cause was visible only in
  Ladylag's screenshot. Say what you need and why.
- **But look at the code first.** #207 was reported as an intermittent focus bug and was a
  single missing attribute, findable in two greps. Asking for logs you don't need costs
  the reporter an evening. If a note can say "this looks implementable from the report",
  say so.
- **Check when a fix shipped before agreeing something is broken.** #192 was reported on a
  version four releases behind the fix.
- **Wiki-data reporters get pointed at the page's edit link.** A fix there helps every
  player and every other tool, and the weekly re-harvest brings it back to us.
- **And the bigger version of that, which is now the FIRST thing to try on any ask about
  shared game truth** (David, 2026-08-22): *"we need EQLWiki to be the source and have the
  very tool that can help it update."* When an ask is about facts the wiki does or should
  hold — respawn timers, drops, locations, level ranges, rarity — the shape to reach for is
  **"EQBuddy hands the player a paste-ready edit"**, not "we store it ourselves" and not "we
  host a thread for it". A place of our own becomes a second source of truth competing with
  the wiki, maintained by us forever. This is what the contribution pack (#65) is FOR, and it
  is why the spawn-timer mega-thread was declined in favour of feeding eqlwiki instead.
  **It only covers facts about the WORLD** — a player's own loot history, camps and sessions
  are the personal companion's job and go nowhere near a wiki. Full rule in `CLAUDE.md`.
- **Sign posts** so people can tell who wrote: `— Dranak (Claude Code)` /
  `— Scribe (Grok Bot)`.

### For Scribe specifically

Useful compile output, in order of value:

1. **Source and verbatim ask.** The reporter's own words, especially exact strings and log
   lines — those are frequently the diagnosis itself.
2. **What already shipped that bears on it**, with the version.
3. **Where it plausibly lives** — marked as a hypothesis, not an instruction.
4. **A priority signal**: regression / approved / someday.

Do not assert what a file contains without quoting the line. Two misses so far were both
confident claims about code state that one grep would have falsified.
