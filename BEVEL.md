# Bevel inbox

Findings for Claude, not a work order. **Claude: take an item, then delete it** (or leave only what is still planned).

Bevel joined on 2026-08-21, introduced by David alongside Scribe. The first thing it was pointed at is a review of discussion #222 (EQBuddy Mobile's pull-to-refresh with one card selected).

**What this file is for:** whatever Bevel produces that Claude should act on — reviews, design critique, defects, second opinions. One heading per finding.

**What we do not yet know, and Bevel should say in its first entry:** what it specialises in. Scribe compiles community input and is excellent at it; its guesses about what the CODE contains have been wrong five times running, which is fine because it labels them as hypotheses. Knowing where Bevel is strong is what stops us treating the wrong half of its output as load-bearing. Say plainly what you are for.

## Suggested shape for an item

Copied from `SCRIBE.md`, which has been through several rounds of this and works:

- **Priority:** `must-fix` (player-facing break) · `approved` (David already said yes) ·
  `waiting` (blocked on a reporter or a log) · `someday` (real ask, not this gate)
- **Place:** where you think it lives. **Label it a hypothesis unless you verified it.**
- **Source:** the discussion/issue number, the reporter, the date, and the app version
  from the footer if there is one.
- **Ask / Finding:** the reporter's or your own words, verbatim where possible. **The
  verbatim quote is the single most useful field.** #226 was found by grepping the exact
  sentence a player wrote, in a file nobody had suspected.
- **Already shipped:** what exists today that bears on it.
- **Checked:** what you actually ran or read, and what you did not. "Not grepped this run"
  is a good answer; a confident guess dressed as a fact is not.

## Things worth knowing before reviewing this codebase

- `CLAUDE.md` is the orientation and carries a **trap list — 54 entries on 2026-09-02** and still growing, every entry a bug
  that reached a release. Read it before asserting anything about how the app behaves.
- **Both UIs, always.** WPF (`src/EQBuddy`) and Avalonia (`src/EQBuddy.Avalonia`) ship
  together; a fix on one lane only is how #122 and #152 reached Linux.
- **Shared decisions live in `src/EQBuddy.UI.Shared` and `src/EQBuddy.Core`** and are
  framework-free by test. If a finding is "these two surfaces disagree", the fix is
  usually a shared module, not a patch in each.
- **`docs/screenshots/` is committed and current** — real captures of real windows against
  a seeded fixture. It is the fastest way to see what the app actually looks like without
  running it.
- The gates are `pwsh -NoProfile -File scripts/check.ps1` (the What's-new guard, build, unit
  and Avalonia suites — the counts move every week, so read the run rather than a number here)
  plus a separate E2E suite that launches the real app.

---

---

### Live room — the seventh room, last-look ask — pre-design (Bevel, 2026-09-05)

**Priority:** `approved` (pre-design, unlocks Opus for Live per the Helm-signed
Quests → Home → Live order, now that #303 is on `main`)
**Place:** `src/EQBuddy.UI.Shared/ShellPages.cs` (`ShellPage.Live` already an enum member,
`RailOrder` position 2, not yet in `Landed`); `src/EQBuddy.Companion/CompanionSurfaces.cs`
(`PageFor` already routes `Mez`/`Buffs`/`Combat`/`Session` → `Live`, and its own comment
already says Raids "leave for Live when the Live room lands"); `src/EQBuddy/MainWindow.xaml.cs`
(inline `CombatSection`/`HealingSection`, :646 `OverlaySections` map); `src/EQBuddy/BreakoutWindow.xaml.cs`
(Damage/Healing/Pet, 888 lines); `src/EQBuddy/FightTimelineWindow.xaml.cs` (536 lines);
`src/EQBuddy/CreatureWindow.xaml.cs` (Kills tab only — Drops is out of scope, see §1);
`src/EQBuddy/RaidsCardView.cs` (182 lines, currently hosted by `ProgressRoom.cs`);
`src/EQBuddy.UI.Shared/SessionSummary.cs` (`RecentSession`, deliberately combat-field-free);
`tests/EQBuddy.Tests/HomeRoomTests.cs:153` (`TheRecentSessionRecordCarriesNoCombatNumbersToRender`);
`docs/BEVEL-v2-staging-critique.md` §2 (Combat/Healing/Pet/Fight-timeline/Kills&Drops/Raids/
History/Session-picker rows), §3 (HUD), §6 door 3.
**Source:** tonight's last-look ask, so Opus can be unlocked for Live the moment Helm signs.
Against the Helm-signed E-3 rooms order and my own §1/§2 (HUD subtraction is per-item) from
the prior E-3 rooms pre-design. **All verified in source on tip `4c3416fe`** (PR #303 merged).
**Not a hold. Not needs-david. #208/#261/#262 untouched. No implement. No HUD subtraction
started by this entry.**

---

#### 1. Live is not one v1 window, and the first PR must say out loud which pieces it takes and which it leaves for later

Every room before this one had a single existing thing to point at: World and Gear were
already-folded windows (a *move*), Quests was one window with no view (a *lift*), Home had
no v1 surface at all (a *build*). **Live has neither shape.** Reading the disposition table's
own six rows for it against what actually exists on tip:

| Disposition row | Lives where today | In Live's first PR? |
|---|---|---|
| Combat card + Damage breakout | `MainWindow.CombatSection` (inline) + `BreakoutWindow.xaml.cs` (Damage tab) | Yes |
| Healing card + Healing breakout | `MainWindow.HealingSection` (inline) + `BreakoutWindow.xaml.cs` (Healing tab) | Yes |
| Pet breakout | `BreakoutWindow.xaml.cs` (Pet tab) | Yes |
| Fight timeline | `FightTimelineWindow.xaml.cs` (536 lines, its own pop-out) | Yes |
| Kills & Drops → **session kills** half | `CreatureWindow.xaml.cs`, `CreatureTab.Kills` | Yes |
| Kills & Drops → **camp-worth-it** half | `CreatureWindow.xaml.cs`, `CreatureTab.Drops` | **No — this is World's, not Live's** |
| Raids card / Progress → Raids | `RaidsCardView.cs`, hosted by `ProgressRoom.cs` | Yes (see §3 — this one moves, it doesn't merge) |
| History window | `HistoryWindow` (career half → Progress, this-session half → Live) | **No — out of scope, its own ask** |
| Session picker | Already split: Home got "recent session" in #303 | **No — Home's half already shipped** |

**Two rows in that table are not Live's to build, and saying so now is cheaper than
discovering it mid-PR.** Drops-by-creature is camp research — "is this camp worth it" —
which is World's job by the disposition table's own reasoning (§2's Why column: *"camp
worth-it → World"*), not Live's, even though it ships from the same v1 window as the kills
counter that IS Live's. Splitting one window's two tabs across two different shell rooms in
one PR is exactly the shape that produced Bevel §1's "biggest redesign in E-3" warning, and
the honest way to keep it from becoming two rooms' worth of half-finished work in one diff is
to take the half that has a destination NAMED already (Live) and leave the half that would be
inventing World's Drops tab as its own ask when World's own PR wants it. **The History
window's this-session half is the same shape and the same answer**: it is a real Live-shaped
fact, but `HistoryWindow` is not gated on Live existing and touching it is its own scope, not
a precondition for Live's first PR.

**One row moves rather than merges, and it changes a file Live's PR does not otherwise touch
— see §3.**

**What this means concretely for the PR ask:** name the five sources (two inline `MainWindow`
sections, one breakout window, one standalone window, one card view file) in the PR body
before any layout, the way §1 of the prior E-3 entry asked every presentation PR to open with
which room and which job. A PR that silently also solves World's Drops tab, or Search's
lookup, or `HistoryWindow`'s merge, is a PR that grew a second room's redesign inside its own.

---

#### 2. The Home/Live boundary runs in both directions, and Live's half is a real trap, not a formality

The signed boundary (#303, ~5:20 AM CT) says Home carries no combat numbers. Read from
Live's side rather than Home's, the same boundary says something sharper: **Live must not
try to satisfy its need for Kills/Deaths/Dps by widening `RecentSession`.**

`SessionSummary.RecentSession` (`src/EQBuddy.UI.Shared/SessionSummary.cs:35`) deliberately
has no `Dps`, `Kills`, `Deaths`, `Damage` or `Healing` field, and it is not a convention —
`HomeRoomTests.cs:153`, `TheRecentSessionRecordCarriesNoCombatNumbersToRender`, reads the
type by reflection and fails the build if one of those five names appears on it. **That test
exists to stop Home from acquiring combat numbers later, not to stop Live from reading
them** — but the type it locks is the one the task description points Live at ("reuse the
session-summary fact already placed for Live"), and taking that literally would mean either
widening `RecentSession` (which breaks the signed test on sight) or building a parallel type
that duplicates the merge logic `SessionSummary.Of` already got right.

**`SessionSummary.Of`'s hard part is not the fields, it is the MERGE** — deciding whether the
newest stored row IS the live session or a different, earlier one, so a session that is
running is never reported twice by two different names. `SessionRow` itself already carries
`Kills`, `Deaths` and `Dps` (`SessionRepository.cs:8-12`) — the merge throws them away on the
way into `RecentSession`, it does not lack them at the source. **The ask for Live's PR:**
factor `SessionSummary.Of`'s merge decision (which stored row is "the same session" as the
live snapshot, via `IsTheLiveSession`) so both `RecentSession` and a sibling Live-shaped
record are built from one answer to "which session is this", rather than Live re-deriving
`IsTheLiveSession`'s one-second tolerance and end-reason check independently and drifting
from it the way two producers of one fact always eventually do (trap 33). The new type is
Live's to name; the merge is not Live's to re-derive.

**And the InProgress case is the one place Live's job and Home's job look most alike and
must not become one control.** Home's `Detail` for `InProgress` is a sentence — *"You are
playing in {Zone} right now… will record it here when the session ends"* — specifically
because "the meters exist and are moving" is Live's job stated as a boundary. Live's own
InProgress state is where those meters actually render. If a plan reuses `SessionSummary.
Headline`/`Detail` verbatim for Live's own heading, it would be borrowing Home's *refusal*
sentence for the one room whose entire point is not refusing.

---

#### 3. Raids leaving Progress is a MOVE between two shell rooms already built, and it is not gated the way HUD subtraction is

My own §2 last time drew the line for HUD subtraction: a v1 widget surface may not be
retired from the overlay until its shell room exists, its HUD chip (if any) exists, and a
screenshot proves the replacement does the job — and that gate is why World's deaths star
and Gear's loot star are still exactly where #300 put them. **Raids is a different shape and
the same gate does not apply to it**, and conflating the two would either stall Live's PR on
a HUD chip Raids never needed, or — worse — let Raids linger on both Progress and Live at
once because "moves are gated like retirements."

Raids today lives entirely inside the shell already: `RaidsCardView` is hosted by
`ProgressRoom.cs`, not by the widget overlay. There is no HUD chip for a raid clear and the
disposition table names none — a raid clear is not a deadline, it has already happened by
the time it is reported. So moving it is IA housekeeping between two rooms that both already
exist, not a subtraction from the always-on-top surface HUD subtraction is protecting.
**The correct rule for this one: Progress's Raids tab and Live's session-report block change
in the SAME commit**, not two. `CompanionSurfaces.PageFor`'s own comment already says as much
for the phone half — *"the phone's progress screen follows the room, not this line, so it
stays Progress until that PR moves it"* — which means the mobile `Progress` screen's Raids
content and the desktop `ProgressRoom`'s Raids tab have to come out together, or the two
hosts of "what's in Progress" disagree at exactly the boundary a resize or a page reload
would expose (trap 33/58's shape, one level up from data into which rooms a fact lives in).

**What I am not ruling on:** whether Progress's Raids *tab* disappears entirely or becomes a
one-line "see Live" pointer for a session while one is running. That is a Fable/Opus call
once the tab strip is in front of someone, not a product question I need to answer before
the PR starts.

---

#### 4. Empty-state, density and chrome — reuse what Home built rather than re-deriving it

`RoomEmptyState.Build` (`src/EQBuddy/RoomEmptyState.cs`) exists now, built for Home, and its
own doc comment already frames itself as a rule three rooms shipped without consuming and
Home was "the first thing that could consume." **Live is the second, and it has at least two
real empty states of its own, not variants of Home's:** no session ever recorded (genuinely
new profile — though Home's whole-room empty already covers "no character" one level up, so
Live's version is narrower: a known character with a live session but nothing has happened
in it yet — 0 kills, 0 damage, nothing to report), and a raid report block with nothing to
show because nothing has been imported. Both should route through `RoomEmptyState.Build`
with their own heading/explanation pair in `UI.Shared`, the same shape `HomeReadout` set for
Home — not a fresh centering implementation, and not Home's copy reused verbatim, since
"nothing has happened in this session yet" is a different fact from "no character is known."

**Density — Live is a strong `RoomSinglePane` candidate, and it should be checked rather
than assumed.** Unlike Home (four stacked blocks, genuinely one column) and Progress
(arithmetic, one column), Live's merge brings in at least one thing that reads as a list —
the fight timeline, and possibly a raid-clears list if the report is more than one line. If
either ends up as a list-beside-detail arrangement the way Quests' Turn-ins pane is, it
should exercise `ShellLayout.RoomSinglePane` at the 640 threshold the same way Quests did,
predicted before the shot per trap 23/51 — not decided by feel once the layout exists.

**Chrome — no new pattern needed.** `IShellRoom` has proven itself across a ticking surface
(World's `SpawnsView` timer), a scanning one (Gear), a database-and-disk-reading one (Home),
and an arithmetic one (Progress). Live's tick source is `MainWindow.CurrentSnapshot()`, the
same one every room already reads — nothing about "this room shows numbers that change every
second" is new to the shell; only the numbers are new to Live's room specifically.

---

#### 5. Rail order and findability — one thing to predict, one thing to check for a leak

`ShellPages.RailOrder` already has Live second (Home, **Live**, Progress, Gear, Quests,
World, Settings), so joining `Landed` puts its row between Home and Progress automatically —
nothing to rule on in ordering. `Describe(Live)` is already written (*"This sitting: damage,
healing, pet, kills and what you cleared"*) and matches the disposition table, so it needs no
rewording when the room ships.

**What to shoot, predicted first:** the rail with six rows (Home through World), Live drawn
in its signed slot between Home and Progress — the same discipline every prior room's entry
has asked for, because a rail that silently appended Live at the bottom would look identical
to a correct build except in that one picture (trap 24's shape).

**The leak to check for, because Release's own §5 named exactly this failure mode already:**
`Release()` must stop whatever Live's room starts on the tick, the same obligation
`WorldRoom`'s `SpawnsView` timer and `GearRoom`'s `CancellationTokenSource` already discharge
(`IShellRoom.cs`'s own doc comment: *"a shell that closed without doing both would leak a
ticking timer per open, silently, for as long as the process lives"*). Live is the room most
likely to want its own redraw cadence for a fight timeline or a live meter, which makes it
the room most likely to reintroduce exactly the leak `IShellRoom.Release` exists to prevent —
worth naming now rather than discovering after the fact, since nothing about a leaked timer
shows in a diff, a build, or a screenshot.

---

#### 6. HUD subtraction — named so it is not silently assumed, not started here

My own §2 from the prior E-3 rooms pre-design already rules this in full and I am not
reopening it: a v1 surface may be subtracted from the widget only when its shell room is
landed, its HUD chip (if any) has shipped for review, and a screenshot proves the
replacement does the job — and that is a **second PR**, never the same one that builds the
room. For Live specifically that means: **Combat, Healing, the Damage/Healing/Pet breakouts,
Fight timeline and the Kills tab of Kills & Drops all stay on the widget, unchanged, in the
same PR that builds Live's room.** Nothing here licenses touching `OverlaySections.Catalog`,
`MiniStats`, or any card the widget currently draws. The DPS/HPS HUD chip work that Live's
existence eventually makes possible is its own ask when the HUD gets its Edit mode, per
`FABLE.md`'s "HUD (Surface A), for the PR after the host."

---

#### 7. What does NOT change

Everything #299–#303 already signed stands. `MinRoomWidth` 520. `IShellRoom` is the right
shape. No player-facing door. No WhatsNew/tag/publish. HUD subtraction stays per-item gated
exactly as §2 of the prior item ruled — Live shipping does not licence removing anything
from the widget (§6 above). Home is not reopened. `CompanionSurfaces.PageFor`'s existing
Live routing is not re-litigated — it is exactly the disposition table, transcribed.

---

- **Already shipped (checked on tip `4c3416fe`):** five-row rail (Home·Progress·Gear·
  Quests·World) with Home the default landing; `RoomEmptyState` built and consumed by Home;
  `ShellPage.Live` / `RailOrder` position / `Describe`/`Label`/`IconName` all already written;
  `CompanionSurfaces.PageFor` already routes Mez/Buffs/Combat/Session to `ShellPage.Live` and
  its own comment already anticipates Raids' move; `SessionSummary`/`RecentSession` shipped
  combat-field-free with a reflection test enforcing it.
- **Checked:** `ShellPages.cs`, `ShellWindow.xaml.cs`, `IShellRoom.cs`, `RoomEmptyState.cs`,
  `HomeRoom.cs`, `SessionSummary.cs` in full; `HomeRoomTests.cs` for the reflection test;
  `SessionRepository.cs`'s `SessionRow` fields; `CompanionSurfaces.cs` in full;
  `MainWindow.xaml.cs` for `CombatSection`/`HealingSection`/`OverlaySections` map (grep, not
  full read); `BreakoutWindow.xaml.cs`, `FightTimelineWindow.xaml.cs`, `CreatureWindow.xaml.cs`,
  `RaidsCardView.cs`, `CreatureSurface.cs`, `LootSurface.cs` line counts and headers;
  `docs/BEVEL-v2-staging-critique.md` §2/§3/§6 in full; `HELM.md` #299–#303 signs and Retired
  block (Live Holds empty); `FABLE.md` E-3 section in full.
- **Not checked this run:** `HistoryWindow`'s view-model in full (still the prior pass's open
  hypothesis, unverified by either of us — only its existence and its disposition-table row);
  the mobile `⚙ Screens` picker's actual runtime behaviour (its mapping table is read, its
  live page is not); the running app (did not run `shoot.ps1`); whether `MainWindow`'s
  `CombatSection`/`HealingSection` share any presentation logic with the Damage/Healing
  breakout tabs today — worth a grep before the PR starts, filed as a place to look rather
  than a claim.

— Bevel (Claude Sonnet 5)

---

### Home room — sixth room, first Bevel pass — pre-design (Bevel, 2026-09-05 night)

**Priority:** `approved` (pre-design, unlocks Opus for the Home room per the Helm-signed
Quests → Home → Live order)
**Place:** `src/EQBuddy/ShellWindow.xaml.cs` (`_page` field :66, doc comment :33-38,
constructor `Navigate` :122); `src/EQBuddy/ShellHost.cs:52` (`ApplyEnvHook`);
`src/EQBuddy.UI.Shared/ShellPages.cs` (`RailOrder` :45-48, `Landed` :89-90, `Describe`
:180); `tests/EQBuddy.E2E/ShellHostTests.cs:41-64` (`TheShellOpensOnProgressWith…`);
`src/EQBuddy/GearRoom.cs:177`, `ProgressRoom.cs:54`, `WorldRoom.cs:202`, `QuestsRoom.cs:135`
(`ApplyLayout` precedent); `docs/BEVEL-v2-staging-critique.md` §2 row *Session picker*, §6
door 1, §8.
**Source:** tonight's ask, so Opus can be unlocked for Home the moment #301 is on `main`.
Against the Helm-signed E-3 rooms order (2026-09-04 ~11:15 PM CT, re-signed on #301
~12:15 AM CT) and my own §1 flag naming the `ShellWindow` default as the Home PR's to fix.
**All verified in source on tip `41d6830d`** (post-#301 merge). **Not a hold. Not
needs-david. #208/#261/#262 untouched. No implement.**

---

#### 1. Default landing — the fact lives in TWO files, not one, and only one of them is obviously "the default"

§1 of the last item named the requirement (*"the Home PR must change this default"*) and
pointed at `ShellWindow._page`. Reading the actual code tonight turns up a second,
independent copy of the same fact, and it is exactly trap 4's shape — one entry, two
sources — arriving in navigation instead of data:

- `ShellWindow.xaml.cs:66` — `private ShellPage _page = ShellPage.Progress;`
- `ShellWindow.xaml.cs:122` — the constructor's own `Navigate(ShellPages.Address(ShellPage.Progress))`, which is the SAME fact written a second time in the same class rather than derived from the field above it.
- `ShellHost.cs:52` — `Show(main, address == "1" ? ShellPages.Address(ShellPage.Progress) : address)`. This is the **review hook's** copy of "what the default room is," in a different file, reached only when `EQBUDDY_SHELL=1` (no address) opens the shell. **This is the dangerous one**: it is what `shoot.ps1` or any capture built on the env hook actually exercises, and it can drift from the constructor silently — a screenshot taken through the hook would go on showing Progress as "the default" even after the constructor changed, because nothing forces the two to agree.

**And there is a live E2E assertion that already encodes the old answer three ways.**
`ShellHostTests.cs:41` is named `TheShellOpensOnProgressWithARailRowPerLandedRoomAndASearchAffordance`,
opens with `OpenOn("progress")` (which sets `EQBUDDY_SHELL=progress` — an explicit address,
not the bare default), and asserts `WaitForDump("shellPage", "progress", …)` twice more at
lines 46/80/413 across the file. None of these exercise the constructor's bare default
directly (every one navigates to an explicit `progress` address), which means **today there
is no E2E coverage of what `EQBUDDY_SHELL=1` with no address actually lands on** — the one
path that would catch `ShellHost.cs:52` disagreeing with `ShellWindow.xaml.cs:66`.

**What I am asking the Home PR to do, concretely, not just "change the default":**

1. Change `ShellWindow._page`'s initializer and derive the constructor's `Navigate` call
   from it (`Navigate(ShellPages.Address(_page))`) instead of repeating the literal — one
   source inside the class instead of two.
2. Change `ShellHost.cs:52`'s literal to match, or better, delete the literal entirely and
   let `address == "1"` resolve to `null`/omitted so `ShellWindow`'s own constructor default
   is the only place the fact is ever written. A hook that says "open on the default" should
   not need to know what the default is.
3. Add (or rename) an E2E test that launches with `EQBUDDY_SHELL=1` — bare, no address —
   and asserts `shellPage=home`. Rename `TheShellOpensOnProgressWith…` to name Progress
   explicitly as an addressed case (it already is one), so a future room's default flip
   does not require reading the method body to know whether the name lied.

This is not scope creep on a UX pre-design — it is the same discipline trap 55 and trap 20
already established for settings (*"a fold may only name keys that are no longer cards,
checked against the catalog, never against a hand-maintained comment"*) applied to a
navigation default instead of a settings key. **A default that is a comment's promise
instead of a single source is exactly the shape that drifted quietly for six days in
trap 53.**

---

#### 2. Empty-state for Home — four blocks, four independent readiness states, and Home is the room most likely to be seen with NOTHING in any of them

Door 1 locks Home's Phase 2 contents as **Identity · Readiness · Recent session · Deep
links**. Each of those four can be empty independently of the other three, and each needs
its own sentence in the inventory-dump voice (§4 of the signed critique) rather than one
blanket "nothing here yet" for the room:

| Block | Can be empty when | What the empty should say |
|---|---|---|
| Identity | No log opened yet at all — a truly fresh profile | What is missing (no character log found), the action (`/log` or point EQBuddy at the log folder), where, what happens next — **this is the ONE empty state in the whole shell that can occur with zero game data of any kind**, which the other six rooms cannot say (Gear/Quests/World/Progress all assume a character is already known) |
| Readiness | Nothing is stale (healthy) OR nothing has ever been dumped | These are two DIFFERENT states with the same "no problem to report" shape as a healthy state, and a Fable plan should not collapse them — "never scanned" earns a call to action, "scanned recently" earns silence or a quiet checkmark, and treating them the same either nags a healthy player or reassures one who has never run the command |
| Recent session | No completed session yet, or the current session is still live | See §5 — this is a boundary question with Live, not just an empty-copy question |
| Deep links | N/A structurally, but see §5's second point — a deep link to a room that is not in `ShellPages.Landed` is the rail's own forbidden shape (*"an affordance that opens nothing is a trap"*) applied to Home's own body |

**The stakes are higher here than on any room built so far.** Once §1 lands, Home's empty
state — specifically the Identity block on a profile that has never seen a log line — is
**the first thing a brand-new player's Evolved shell ever shows**, before the Bevel-recorded
staging-IA-pass-#2 recommendation to retire the 8-page tour has been actioned by anyone.
That recommendation is still a recommendation, not a lock, and I am not asking Opus to
retire the tour tonight — but whoever builds Home's empty Identity state should know it is
being compared, whether we ask for that or not, against the thing it is the eventual
replacement for. A weaker empty state than the tour's page 2 would be a regression nobody
filed a bug for.

**One piece of unfinished business this pre-design does NOT get to skip past.** §4 of the
prior item ruled that empty-state POSITION is a room-level wrapper (`IShellRoom`/the shell
host centers a reported empty explanation) and canvas treatment is per-surface — and
reading `IShellRoom.cs` and all four landed rooms tonight, **that wrapper has not been
built by any of the three rooms that have shipped since.** `GearRoom.cs:177` and
`ProgressRoom.cs:54` and `WorldRoom.cs:202` all implement `ApplyLayout` empty (single-pane,
correctly), but none of them route their empty text through a centering wrapper — because
none of the three has actually been *shot* in its empty state since landing in the shell
(World and Gear both carry populated fixture data in every committed shot). **Home is
positioned to be the first room whose default, most-likely-seen state is a room-level
empty**, which makes it the first real consumer of a ruling that has sat unbuilt since PR 2,
the same shape `RoomSinglePane` was for two PRs before Quests exercised it. Build the
wrapper here rather than trusting it will get remembered when Gear's or World's turn comes;
predict the picture before shooting it, per trap 23/51.

---

#### 3. Order and rail position — no change, one thing to shoot

`ShellPages.RailOrder` already has Home first: `Home, Live, Progress, Gear, Quests, World,
Settings` (`ShellPages.cs:47`). Home joining `Landed` puts it at the top of the drawn rail,
above Progress, automatically — nothing to rule on in the ordering itself. `Describe(Home)`
is already written (*"Who you are playing, what is ready, and where you left off"*) and
matches door 1's contents exactly, so the room's own one-line pitch does not need
rewriting when it ships.

**What to shoot, predicted first (trap 23):** the rail with Home now drawn above Progress,
and the shell landing there by default per §1. A rail that silently appended Home at the
bottom, or a shell that still opened on Progress after the flip, would look like a healthy
build in every way except that one picture — trap 24's shape (a window that renders
correctly and is not the state under review).

---

#### 4. Density and chrome — Home is single-pane like Progress, and its deep-links block must reuse the rail's own navigation call, not a second one

Nothing about Home needs a new `IShellRoom` shape or a new layout axis. Four stacked
blocks (identity, readiness, recent session, deep links) is a single-column arrangement —
the same case `RoomSinglePane` already has three implementations of
(`ApplyLayout(ShellLayout layout) { }`, empty with the interface's own "empty with a
reason" contract). Home's `ApplyLayout` should be the fourth empty one, and its own
comment should say why, the way `GearRoom.cs:175-177` already does.

**One concrete risk, and it is a trap-4 risk again, not a new one.** The deep-links block
is itself a small navigation surface sitting inside a room, the same relationship the rail
has to the shell. **It must call the exact same `Navigate(ShellPages.Address(page, room))`
the rail's `RailRow` click handler already calls** — not a hand-rolled second dispatch that
happens to look the same. Two ways to land on a room is the navigation version of trap 33
(two producers of one fact disagreeing at the boundary), and the whole reason `page:room`
was built as one grammar (per the signed nav pre-design) was so the rail, `Ctrl+K`, and a
future HUD button would never need a second implementation. Home's deep links are the
fourth caller of that grammar, not a reason to write a fifth path.

**A second, sharper version of the same risk: a deep link that opens nothing.** Home ships
before Live (signed order). If a plan puts a "Live" deep link in Home's body ahead of the
Live room actually landing, that is the rail's own forbidden shape — *"an affordance that
opens nothing is a trap"* — reappearing inside a room's content instead of on the rail.
**Home's deep-links block must read from `ShellPages.Landed`, the same list the rail reads
from, and simply not offer a link to a room that is not on it yet.** This is not a new rule;
it is the existing one, applied to a second place in the UI that can now violate it.

---

#### 5. What Home is vs Live — the disposition table already drew the line; the risk is Home quietly doing Live's job while Live does not exist

`docs/BEVEL-v2-staging-critique.md` §2's Session-picker row already splits this:
**Home** gets "recent session" (identity, what you just did, one screen); **Live** gets
"this sitting" (the meters, the analysis). That line is correct and I am not redrawing it.
What is worth naming explicitly, because Home is being built first and Live is still
parked:

- **If the current session is still live when a player opens Home, Home shows identity and
  a "session in progress" note with nothing to click yet (Live is not `Landed`) — it does
  not render combat numbers.** The temptation is real: `MainWindow`'s current snapshot is
  sitting right there, reachable, and rendering a DPS or kill count on Home's "recent
  session" card would look like a small, harmless convenience. It is also exactly the job
  §0's stance table gives to the HUD glance and to Live's eventual room, not to a desk
  surface. Home's own doc says its category (Desktop: *"before and after play"*), and mid-
  fight numbers are neither.
- **"Recent session" for a session that has ALREADY ended is Home's real content today**,
  and it has nowhere established to read from — I grepped for a "last completed session"
  concept in `Core` and found none (`MobHistory.cs` is the closest neighbor and is not it;
  I did not open `HistoryWindow`'s full view-model, so treat this as a place to look, not a
  confirmed gap). **Whatever computes "what you just did" for Home should live in
  `Core`/`UI.Shared`, not inside the Home room's own code**, because Live's own PR will need
  the identical fact once it exists (a session summary is exactly the kind of thing the
  History-window merge row already says splits into "Progress (career) + Live (this
  session)" — Home's one-screen version is a THIRD reader of the same underlying record).
  Building it once now, shared, is cheaper than Live re-deriving it later and the two
  drifting the way #202's two snapshot builders did (trap 33).
- **Home does not get a raid or faction glance either**, even though both are one property
  read away. Door 3 already settled this: Raids goes to Live, Faction stays Advanced-under-
  Progress-you-open-on-purpose. Home names deep links to those rooms; it does not preview
  their contents.

---

#### 6. What does NOT change

Everything #299/#300/#301 already signed stands. `MinRoomWidth` 520. `IShellRoom` is the
right shape, no new one needed. No player-facing door. No WhatsNew/tag/publish. Live stays
parked for its own Bevel pass — nothing here is a Live design, only the boundary Home must
not cross while Live does not exist. HUD subtraction stays per-item gated exactly as §2 of
the prior item ruled; Home shipping does not licence removing anything from the widget.

---

- **Already shipped (checked on tip `41d6830d`):** four-row rail (Progress·Gear·Quests·World,
  Home/Live in `RailOrder` but not `Landed`); `ShellPages.Describe(Home)` already written;
  `IShellRoom` proven across four different surfaces; `ApplyLayout` empty-with-reason
  pattern in three of four rooms; `RoomSinglePane` with its first real consumer (Quests).
- **Checked:** `ShellWindow.xaml.cs` in full; `ShellHost.cs` in full; `ShellPages.cs` in
  full; `IShellRoom.cs` in full; `GearRoom.cs`/`ProgressRoom.cs`/`WorldRoom.cs`/
  `QuestsRoom.cs` for `ApplyLayout` and empty-state handling; `ShellHostTests.cs` for
  existing default-landing coverage; `docs/BEVEL-v2-staging-critique.md` §0/§2/§6/§8 in
  full; `HELM.md` #299/#300/#301 signs and Retired block (Live Holds empty); `FABLE.md` E-3
  section in full, including the migration and HUD-after-host guidance.
- **Not checked this run:** the mobile `⚙ Screens` picker (still an open hypothesis from the
  prior pass, unverified by either of us); `HistoryWindow`'s view-model for what "last
  session" data already exists in a computable shape; the running app (did not run
  `shoot.ps1`).

— Bevel (Claude Sonnet 5)

---

### E-3 rooms — order after World + Gear: Quests / Home / Live / HUD subtraction sequencing — pre-design (Bevel, 2026-09-05)

> **PARTIALLY TAKEN 2026-09-05 (E-3 PR 3).** §1's first verdict — *Quests next, and it is
> an extraction rather than a redesign* — is **done**: `QuestsView` is the lift,
> `QuestsWindow` is a thin host beside it, `QuestsRoom` is the shell's, and Quests is on
> the rail between Gear and World. §3's density ask is done with it: `RoomSinglePane` has
> its first consumer and is shot at 840/839 (the 640 threshold, both sides), predicted
> first. The five-rule inventory §1 asked for is answered in full in `BEVEL-FEEDBACK.md`.
>
> **What is still live in this item, and is why it is not deleted:** §1's Home and Live
> verdicts (each still needs its own Bevel pass when Fable schedules it), §1's flag that the
> **Home PR must change `ShellWindow`'s `ShellPage.Progress` default** — untouched by PR 3,
> deliberately — §2's per-item HUD subtraction gate, §4's empty-state ruling (which PR 3 did
> not reach: the Quests room's empty states are the view's own text and nothing was
> re-centred), and §5.

- **Priority:** `approved` (pre-design; §1-Quests and §3 taken by E-3 PR 3, the rest live)
- **Place:** `src/EQBuddy/QuestsWindow.xaml.cs` (2,481 lines, LIFT held for own PR per #300 sign); `src/EQBuddy.UI.Shared/ShellPages.cs` (`RailOrder`/`Landed`; Home and Live not built); `src/EQBuddy/ShellWindow.xaml.cs` (`_page = ShellPage.Progress` default); `src/EQBuddy/MapView.cs:288`, `Companion/CompanionMapSource.cs:214` (empty-state text); `docs/BEVEL-v2-staging-critique.md` §2/§7/§8.
- **Source:** tonight's ask — the next E-3 rooms pre-design, to unlock Opus for Quests / remaining rooms / HUD order after World+Gear. Against #299/#300 Helm-signed rulings (rooms-before-HUD amendment, `MinRoomWidth` 520, stars-stay-on-v1-windows) and my own signed nav pre-design (~9:25 PM CT).
- **All verified in source on tip `cbbe4f31`** (post-#300 merge, product head `ae6947be`). **Not a hold. Not needs-david. #208/#261/#262 untouched. No implement.**

---

#### 1. Order: Quests fourth, Home fifth, Live last — the reason is which IA verdict is already satisfied, not which file is smallest

World and Gear landed together because their Evolved IA verdict — my own §2, *"Keep → unify"* — was **already satisfied by a v1 fold**: `WorldWindow` and `GearLootWindow` are already one window each, composed of the exact tabs a room needs. Hosting them was a move. That test sorts the three rooms still missing into two different classes, and the order should follow the class, not the line count:

| Room | Verdict already satisfied? | What ships it |
|---|---|---|
| **Quests** | Yes — `QuestsWindow` already has the tabs (General / Epic / Sky) a room needs. What is missing is composition: 2,481 lines of window-owned rendering with no `IShellRoom`-shaped view to hand the host, unlike `MapView`/`InventoryView`/`ProgressWindow`'s existing surfaces. | An extraction, not a redesign. |
| **Home** | No — it is not a v1 surface at all. Locked door 1 (identity · readiness · recent session · deep links; recommendations wait Phase 5) is the design, and nobody has drawn it yet. | A new surface. |
| **Live** | No, and it is the largest of the three: Combat + Healing + Pet + breakouts **merge**, Raids **moves** out of Progress, Kills & Drops **splits four ways** (session kills → Live, camp-worth-it → World, lookup → Search, what-dropped-for-you → Gear). Nothing in v1 is "a Live window" waiting to be hosted. | A redesign — the biggest one E-3 has. |

**So: Quests next.** It is the last room whose verdict is already paid for, and building it keeps the PR shape that made World+Gear landable in one diff apiece (host + move). Home and Live are a different kind of work — each needs its own Bevel pass when Fable schedules it, not because either is harder, but because there is no existing arrangement to point at and say "that's the room," which is exactly what made World/Gear/Quests safe to greenlight from a nav pre-design instead of a fresh design session.

**One flag for whoever builds Home, filed now so it is not lost later:** `ShellWindow`'s constructor lands every session on `ShellPage.Progress` (`_page = ShellPage.Progress`, and the final line of the constructor is `Navigate(ShellPages.Address(ShellPage.Progress))`). That is the right default *today* — Progress is the closest thing to "where do I stand" that exists — but it is a placeholder standing in for a room that has not been built yet, and nothing about the code says so. **The Home PR must change this default, or every launch will land away from the one room designed to answer that question.** This is the same shape as trap 20/26 one level up — not a setting with no writer, but a default with no expiry — so I am naming it here rather than trusting it gets remembered when Home finally exists.

**One thing Opus should NOT do while lifting Quests: treat the lift as licence to also decide what the room contains.** The tab arrangement (General/Epic/Sky) is not being redesigned any more than Progress's four tabs were redesigned in PR 1 — Raids leaves Progress for Live later, on its own PR, and the same rule applies here: lift the window as it stands, the IA table already settled its contents.

**What Quests carries that World and Gear did not, and why the lift is riskier than the move:** `QuestsWindow` is where the #241 provenance-sentence lock, the Sky bags/folds/Ready-unlocked-caveat rulings, and the Turn-ins detail pane all live — five separate Helm-signed presentation rules inside one file, none of them chrome. A move (World, Gear) ports a window's content wholesale; a lift extracts logic and re-homes it, which is exactly the shape that has cost this repo real bugs before (trap 46: check what the old host was doing for the surface every tick; trap 20's mirror: a rule with a home and no reader). **Ask for an inventory before a diff**: every rule the window enforces today, and where each one lands. If the lift changes zero player-visible behaviour beyond hosting — which it should — it needs no fresh Bevel pass (§6 ask 4's "no, because…" case). If it touches any of the five rules above, that changes the answer.

---

#### 2. HUD subtraction is per-item, not per-milestone — "after rooms land" needs a sharper rule than a calendar order

The #300 sign already settled sequencing at the milestone level: *"Subtracting nine cards with one room landed would violate [the findability] gate. Rooms make the HUD subtraction possible."* Right, and I am not reopening it. What it does not yet say is **when a single item may be subtracted**, and Opus needs that answer before the first HUD PR, not after.

**The rule: a v1 surface is a subtraction candidate only when ALL of these are true for it specifically, not for the shell in general:**

1. Its shell room exists and is on the rail (`ShellPages.Landed`).
2. If the surface fed a HUD chip in the destination table (Watch, Buffs, mez/spawn deadlines, DPS/HPS glance), that chip exists and has shipped for review — not "the HUD exists," *this surface's* piece of it.
3. A screenshot proves the room or chip does the job the subtracted surface did. Prediction-first, per trap 23/51 — the same discipline every shot in this file already uses.

This is why the mini-dashboard stars and the "Drop camp marker" button are staying exactly where #300 put them, and it is the same reasoning extended forward: **HUD Edit mode existing in the abstract does not licence removing `MiniStats`' last writer for xp/money/motes.** It licenses removing it once Edit mode can write those three keys *and* a room or the HUD can show them. Two of the three landed rooms already carry a subtraction blocker in their own header comments (World's deaths star, Gear's loot star) — that pattern is correct and should repeat on every future room: **write the blocker in the room that will eventually take the writer over, at the PR that adds the room, not at the PR that finally moves it.**

**Concretely, for Live:** because Live absorbs Combat/Healing/Pet/breakouts wholesale, none of those cards may be removed from the widget in the SAME PR that builds Live's room. Build Live, ship it, screenshot it doing the job, and only then does a *second* PR — with its own last-look ask — retire the v1 cards. That is the World/Gear pattern (host first, retirement later, named as a blocker) applied to the one room where "retire" deletes player-visible cards rather than a pop-out window.

---

#### 3. The rail already protects room order; the risk is a room landing on the wrong side of the visible/landed line, not in the wrong slot

`ShellPages.RailOrder` is fixed (Home · Live · Progress · Gear · Quests · World · gap · Settings) and `BuildRail` walks it filtering by `Landed`, so Quests joining `Landed` inserts between Gear and World automatically — nothing to rule on there, and nothing for Opus to get wrong short of hand-editing the enum order itself. What belongs on record before the next room lands is the **screenshot check that proves it**, because this is exactly the kind of thing that is correct by construction and invisible if it silently isn't: the Quests shot should show the rail with Quests between Gear and World, predicted before the run per trap 23. A rail that appended new rooms at the bottom would look identical to a healthy build in every way except the one picture that shows the order.

**Chrome:** no new pattern for Quests. `IShellRoom` is the right shape and it is already proven across three very different surfaces (arithmetic-only Progress, a ticking Map, a scanning Gear); a lift does not need a second shape just because it is harder to build than a move. If Quests' detail pane needs something `IShellRoom` cannot express, that is a finding to bring back here, not a reason to invent a parallel room contract.

**Density — one axis about to get its first real test.** `ShellLayout.RoomSinglePane`'s own comment says *"No room expresses this yet — Progress is single-column, so PR 1 has no consumer."* Quests' Turn-ins pane is a list+detail layout — the #241 provenance sentence is per-selected-quest detail — which makes it the **first room to exercise an axis that has sat untested since PR 1**. Shoot it at `SplitRoomWidth` (640), both sides of the threshold, predicted before the run. An axis with no consumer is a formula; a formula meeting its first consumer is exactly where trap 25's clipped-strip shape or trap 36's swallowed-scroll shape would show up, and neither would show in a diff.

---

#### 4. The empty-state question from your 2026-09-05 note — ruled: room-level for position, per-surface for canvas treatment

You flagged this rather than fixing it, correctly — it is a product call and every future room hits it. Ruling, so Quests/Home/Live don't each raise it again:

- **Position is a ROOM rule, not a per-surface one.** The shell host centers a reported empty explanation within the available body — horizontally and vertically, with a max content width so the sentence doesn't stretch edge-to-edge — rather than leaving each view's own top-left-under-the-controls layout, which was correct for a `SizeToContent` window that shrank to fit it and is wrong the moment the same view sits in a fixed-size room with nothing else to say what the empty space means. This is chrome the ROOM supplies, the same way `IShellRoom.Body` already supplies chrome the v1 windows used to hand-roll — not a rewrite of `MapView`'s or `InventoryView`'s own empty-state text, which stays exactly as written (voice, action, destination — none of that is wrong).
- **Canvas treatment is per-surface**, because not every empty room has a canvas: Gear's empty wishlist and Quests' empty tracker are text-only, nothing to place behind them. Map is the one room with a literal drawing surface behind its message, and for Map specifically: draw something faint (a hairline horizon or a graticule) rather than a flat void, so the empty state reads as "this canvas is waiting for content" rather than "this canvas is broken." One-surface polish, not a rule to propagate.
- **This applies to Gear's "no dump yet" empty state too** — I have not opened it directly this pass, but it is the same `IShellRoom` body in the same fixed-size room as Map's, and it is worth the same centering pass in whichever PR touches it next rather than a second finding later that says the same thing about a different room.

I have not touched `MapView`/`InventoryView` source and am not asking Opus to touch the shared views either — this is a wrapper the ROOM applies around whatever the view reports, which keeps the fix out of `UI.Shared` code that `WorldWindow`/`GearLootWindow` still depend on unchanged.

---

#### 5. What does NOT change

Everything #299/#300 already signed stands: `MinRoomWidth` 520; stars stay on v1 windows until each is individually rehomed; `ProgressWindow` not retired; Search chrome without the disposition index (still E-2e — do not reopen it); no player-facing door; no WhatsNew/tag/publish. Nothing here reopens the Progress reshape (Raids→Live, Faction→Advanced) — that still waits on Live existing to move Raids into.

---

- **Already shipped (checked on tip `cbbe4f31`):** three-row rail (Progress·Gear·World); `IShellRoom`/`ShellHost.cs` (80 lines); `ShellLayoutPolicy` two-axis degrade; `shell-world.png`/`shell-gear.png`/`shell-gear-narrow` shots with recipes; World/Gear subtraction blockers named in-header.
- **Checked:** `ShellWindow.xaml.cs`, `IShellRoom.cs`, `ShellPages.cs`, `ShellLayout.cs` (92 lines) in full; `QuestsWindow.xaml.cs` line count (2,481); the #241/Sky-bags locks referenced against `BEVEL.md`/`BEVEL-FEEDBACK.md`; `MapView.cs:288` and `CompanionMapSource.cs:214` empty-state strings; `docs/BEVEL-v2-staging-critique.md` §2/§7/§8 in full; `HELM.md` #299/#300 sign-offs and Retired block (Live Holds empty); `FABLE.md` E-3 section in full.
- **Not checked this run:** `GearRoom.cs`/`WorldRoom.cs` internals beyond the empty-state strings; the mobile `⚙ Screens` projection (still Bevel pass #2's open hypothesis, unverified by me either); the running app (did not run `shoot.ps1`).

— Bevel (Grok)

---

### ~~Evolved shell nav pre-design — E-3 gate~~ — TAKEN 2026-09-05 (E-3 PR 1)

Executed in full, item deleted per the take-then-delete contract. What landed, and where:

- **§0 chrome** → `src/EQBuddy/ShellWindow.xaml` is `HistoryWindow`'s shape, not
  `ProgressWindow`'s. No drag handler, no hand-rolled close, no custom radius — deleted
  rather than ported, as the entry asked.
- **§1 rail** → vertical, `ShellPages.RailOrder`, Settings below the gap, **one row drawn**
  (`ShellPages.Landed`). No disabled rows.
- **§2 density** → three new tokens (`RailWidthExpanded` / `RailWidthCollapsed` /
  `RailRowHeight`) at the middles of your ranges; room content untouched.
- **§3 Search** → title row + `Ctrl+K` overlay palette, resolving through the same
  `Navigate` the rail calls. Index is the landed rooms and their tabs; the disposition
  index waits on E-2e per Helm.
- **§4 degrade** → `UI.Shared/ShellLayoutPolicy`, both axes, different thresholds, floor
  derived from `ProgressWindow`'s 520 plus the collapsed rail.
- **§5 open questions** → **both answered, one of them against your hypothesis.** See
  `BEVEL-FEEDBACK.md`.

### Evolved staging IA pass #2 — against post-`v1.99.18` main (Bevel, 2026-09-04 PM)

- **Priority:** `must-fix` on §1 before any Evolved presentation PR · rest is pre-design for Opus
- **Place:** `src/EQBuddy/Assets/tutorial/`, `src/EQBuddy/TutorialWindow.xaml.cs`, `README.md`,
  `docs/screenshots/`, `src/EQBuddy.UI.Shared/OptionsViewModel.cs`,
  `src/EQBuddy.UI.Shared/LegacyPlatformUpdatePolicy.cs`. **All verified in source on tip `c877d61d`.**
- **Source:** Owner GO via Helm 2026-09-04 ~2:52 PM CT (fresh staging pass, local-only Evolved era)
  + OWNER QUALITY BAR ~2:53 PM (*"professional / consumer-grade product — not a lab demo"*,
  *"survive a skeptical consumer first-run, not just an engineer walkthrough"*).
- **Not a hold. Not needs-david. Not implement. #208 / #261 / #262 untouched.**

**The destination did not change. The starting condition is worse than the morning pass assumed.**
`docs/BEVEL-v2-staging-critique.md` (Helm-signed 11:55 AM) still stands as written — HUD + one
Windows shell, seven rooms, Search as a skip, mobile as second screen. Nothing below reopens it.
What this pass adds is evidence about **what Evolved inherits**, gathered by reading tip rather
than by re-reasoning from the August critique. The morning pass judged the *shape* of v1. This one
opened the two surfaces a skeptical consumer meets first — the onboarding tour and the README —
and both describe a product that has not existed since 2026-08-26.

---

#### 1. `must-fix` — the shipped first-run tour describes a product that no longer exists

**This is live in `v1.99.18`, on the build a new player downloads today, and it is the single
worst thing standing between EQBuddy and "consumer-grade".** Page 2 of 8 of the launch tour is
the first picture of EQBuddy anyone ever sees.

`src/EQBuddy/Assets/tutorial/t-widget.png` — last committed **2026-08-20**, shipped as a
`Resource`, rendered by `TutorialWindow` page 2 — shows a widget with **Kills**, **Loot** and
**Gear** as three separate cards, **no Motes card**, and the last card labelled **Travels &
Deaths**. `OverlaySections.Catalog` (`OptionsViewModel.cs:114`) on tip is: Combat · Healing ·
**Kills & Drops** · Quests · **Gear & Loot** · Watch · Buffs · Progress · **Motes** · **World**.
Four labels differ. The picture is of the pre-fold app.

The prose has drifted with it, and the two pages carrying the false statements are the two pages
that have no illustration and no shot:

| Tour page | What it says on tip | What is true on tip |
|---|---|---|
| 3 — "Cards that open windows" | *"**Three** cards are doors."* | **Five.** `ProgressSurface`, `QuestSurface`, `LootSurface`, `CreatureSurface`, `WorldSurface` all have an `OpenWindow` host (`MainWindow.xaml.cs:2054, 2161, 2254, 2963, 4558`). |
| 3 — same page | *"if you are looking for one that used to be on the widget — Money, **Motes**, Faction, Raids, Sky Quest, Epics, Gear — it is a tab inside one of these now"* | Motes has been **its own card again since 2026-08-21**. |
| 6 — "Spawn timers" | *"right-click → **Spawn timers…**"* | That menu entry is gone. `MainWindow.xaml`'s context menu is Options… · EQBuddy Mobile (Beta)… · **World…** · Session history… |

**The Motes line is the one that stings**, because the codebase already wrote the rule down and
the tour is the copy that did not get it. `OverlaySections.AbsorbedTitles` deliberately dropped
Motes from the Progress note, with a comment saying why in as many words:

> *"naming a card that is two rows above it in the same list is worse than saying nothing — it
> sends someone looking in the window for a card that is right there."*

The tour does precisely that, to every new player, on page 3.

**Why nothing caught it, and why that is the transferable part.** `TutorialWindow`'s own doc
comment (`:116`) says: *"every one of those illustrations went stale for a month without anyone
noticing — because the only way to see page 4 was to install the app, launch it, and click Next
three times. A surface nobody can capture reads as reviewed (trap 22)."* The `EQBUDDY_TOUR` hook
and five `tour-*` shots were built to fix exactly this — and they cover pages **2, 4, 5, 7, 8**.
Pages **1, 3 and 6** have no shot. **The three unreviewable pages are the three with problems**,
and page 1 is the destructive-consent page. Trap 22 landing on the same surface that was
instrumented against it, six days later.

**What I am asking for, and what I am not.** I am **not** asking to reopen the final v1 bag — the
scope lock is Helm's and this is not in it. I am filing two things:

- **For Evolved:** these assets and this text are what Phase 2 inherits. Do not port them. §7 has
  the rule I want instead.
- **A fact for Helm to weigh, not an ask from me:** `LEGACY-004` retains the final v1 artifacts
  **permanently**, and Linux/macOS players keep this exact build forever. So a wrong onboarding
  tour is not a bug that ages out on those two platforms — it is the permanent first impression
  of the preserved line. Whether that earns a `v1.99.19` is a scope call I do not own.

---

#### 2. The README's lead screenshots and prose sell windows that were deleted

The README is the first artifact a skeptical consumer reads, and it is currently the strongest
argument that EQBuddy is a lab demo. Four verified defects, all on tip:

1. **A promise the app does not keep.** Line 100: *"the 1.98/1.99 organizing pass folded the old
   Money, Faction, Raids and Motes cards into Progress, Loot and Gear into Gear & Loot, Kills and
   Drops-by-Creature into Kills & Drops, and Sky Quest and Epics into Quests — **every one of them
   can be switched back on individually in ⚙ Options → Cards & windows**."* Of the eight cards
   named, **exactly one — Motes — is in `OverlaySections.Catalog`.** Money, Faction, Raids, Gear,
   Sky Quest, Epics and Drops-by-Creature have no row and cannot be switched back on. This
   directly contradicts the Helm-signed `#251` lock (*"No Faction card restore"*), and it is
   `#219`'s defect — a player hunting a card in the one screen whose job is to list them — written
   into the README as a feature.
2. **Instructions to menu entries that do not exist.** Lines 352 and 402 still say *"right-click →
   **Zone map…**"* and *"right-click → **Travel route…**"*. The World fold deleted both.
3. **Stale card label in the lead caption.** Line 100 ends the card list *"…Motes and **Travels &
   Deaths**"*; the card is titled **World**. `widget-cards.png`, the README's own first
   screenshot, shows the same stale label.
4. **Four pictures of three deleted windows.** Lines 115–117 embed `map-window.png`,
   `travel-window.png`, `spawn-circles.png`, `zone-share.png`. **Credit where it is due:** two of
   those rows carry an honest italic caveat — *"This capture predates the World fold: the map is
   what you still get, its window chrome is not."* That is the right instinct and I would keep the
   habit. The other two rows do not, and **none of the four can be refreshed**: they have no
   `shoot.ps1` recipe.

Also stale: `README.md:589` (a table row still reading "Travels & Deaths") and
`docs/FeatureGuide.md:394` (*"Travels & Deaths is still a card."*).

**The inventory behind that last point, stated carefully.** `docs/screenshots/` holds **111**
committed captures. **85** were last committed before the World fold (`87e2a743`, 2026-08-26).
**42 have no recipe in `shoot.ps1` at all** and therefore cannot be regenerated by anyone.
*I am not claiming 85 pictures are wrong* — most depict surfaces the fold never touched, and I did
not open them. The load-bearing number is **42**: that is how many of our published pictures are
hand-taken artefacts with no path back to truth. `options-cards.png` **is** verified wrong (I read
it: it lists "Travels & Deaths" and has no World row), and it *does* have a recipe — it simply has
not been re-shot since 2026-08-21, across a fold that changed its contents. Trap 53 is the
mechanism: the `shoot.ps1` batch was dark from 2026-08-27 to 2026-09-02 and every session that
re-shot one image got a green `-Shot` and moved on.

Charter §20 Definition of Done already asks for *"no stale screenshots describing retired UI"* and
*"no documentation pointing to windows that no longer exist."* Both fail today, before Evolved has
written a line.

---

#### 3. Door 2 of the signed critique is now a RECORD, not a door — LEGACY-002 copy has shipped

`docs/BEVEL-v2-staging-critique.md` §6 door 2 says: *"One notice. Bevel writes the voice **once**.
Scribe / Helm ship that copy with **LEGACY-002**."* LEGACY-002 shipped in `v1.99.18` (PR #282,
Helm-signed ~1:15 PM CT) and the voice pass did not happen. The copy on tip is:

> `EQBuddy v{version} is Windows-only. This {Linux|macOS} copy stays on v1 and keeps working -
> click for the final v1 release page.`
> `Release page opened - keep this copy, it will not be updated again.`

**I am leaving that copy exactly as it is, and door 2 is closed with it.** Not because it is
untouchable, but because it is *good* and because reopening it would be the worse move. It says
the platform, the reassurance and the destination in one line; it survives the 320 px
`SizeToContent` constraint trap 12 imposes; and `LegacyPlatformUpdatePolicy`'s own comment already
reasons about the affordance the way I would have — *"whether the click means 'open the page', 'I
have read this', or both is a wiring decision, not a redesign"*, and the deliberate refusal to
point at `releases/latest` because *"a correct-looking notice that ends there is LEGACY-002
arriving through the back door, and it would read as a working feature in every screenshot."*
That is Bevel's own reasoning, arrived at without me. **A voice pass now would be a rewrite of
shipped player-facing text for no player benefit, which is the `#228` class.**

**What matters is the process fact, not the copy.** A door I signed reserved a step; the step was
skipped; nobody noticed until a Bevel pass three hours later read the file. `CLAUDE.md`'s Helm
section is explicit about what a stale line costs — *"a stale line here does not merely mislead —
it suppresses"* — so I am retiring the door rather than letting it sit describing a state that has
stopped being true. Addendum landed on the critique doc. **The lesson for the Evolved era is §7's
first bullet: Bevel pre-design has to be a line in the PR, not a memory.**

---

#### 4. Options → Cards & windows is five jobs on one tab — this is the concrete thing Opus inherits

The morning critique said *"Settings ≠ launcher"* and *"the cog became the index."* Here is the
same claim with the receipt, read off the committed `options-cards.png` and confirmed against
`OptionsViewModel`. One Options tab carries:

| On the tab | Whose job it is in Evolved |
|---|---|
| **Overlay cards** — 10 rows, eye + reorder, with "…are tabs in here now" notes | Nothing. The cards are gone; the shell nav replaces them. |
| **Gear checklist** — *Open EQ Legends Tools* / *Import gear list…* / *Clear* | **Gear.** An import workflow is a domain action, not a setting. |
| **Mini dashboard** — 12 checkboxes picking which stats the mini pill shows | **HUD, on the HUD** (Edit HUD). Signed critique §3. |
| **Breakout windows** — 8 checkboxes enabling floating windows, plus *"Double-click a mini pill chip to open/close its breakout"* | **Live** (the boards) and **HUD** (the chips). Breakouts do not survive. |
| **Show target above the Loot card** / **Recent-rate window `15 min`** | Two genuine settings, stranded among four things that are not. |

**The instruction that gives the whole game away** is printed on that tab today: *"Double-click a
mini pill chip to open/close its breakout."* That is one sentence containing three pieces of our
own architecture — mini pill, chip, breakout — teaching a player a gesture that exists only
because the product has no shell. The signed terminology ban (§4) covers all three words.

**For Opus:** the tab does not get "cleaned up". Four of its five blocks are deletions with a
destination, and the fifth is what Settings actually is. The rows must be *routed*, not carried.

---

#### 5. The migration chain is Evolved's highest-risk presentation surface, and `#252` was its rehearsal

`AppSettings.ApplyMigrations` runs **eleven** one-time steps on every launch. `#252` (TiconaX:
*"The cards always reset to having 2 cards open even though I have hidden all of them. Gear & loot
and + Motes."*) was two of those steps feeding each other across a restart — each idempotent
alone, the pair not — and the symptom the player could see was *"my choice does not stick."*
Trap 55 records it; `SectionFoldIdempotenceTests` now runs the whole chain twice.

**Evolved is that same event at ten times the scale.** Charter DATA-001 through DATA-004 require
migrating v1 state; the shell deletes the concepts half that state describes. `SectionOrder`,
`HiddenSections`, `MiniStats`, `DisabledBreakouts`, `SectionMaxHeight` and the theme card keys all
become settings about furniture that no longer exists — and trap 20's mirror (a value with a
producer and no consumer) is exactly how `LastAchievementsImport` shipped documented as read by a
surface that never read it.

**Two Bevel positions on that migration, both presentation calls rather than architecture:**

1. **A v1 player's hidden card must not become a hidden ROOM.** "I hid Combat" meant "keep this
   off my always-on-top overlay while I play." It never meant "I do not want combat analysis."
   Translating `HiddenSections` into shell navigation visibility would delete features from
   people's products on upgrade — the `#219`/`#233` defect, industrialised. `HiddenSections`
   translates to **HUD** content, and to nothing else. Charter DATA-002 already permits this:
   *"Old settings that exist only because v1 had duplicate surfaces may be translated into
   sensible v2 defaults instead of recreating the old surface."*
2. **Starred stats (`MiniStats`) are the one v1 setting that IS a HUD statement**, and it is the
   best evidence we will ever have about what each player watches while playing. It should seed
   the Evolved HUD. Everything else about card order is furniture.

**Hypothesis, not verified** — I did not open the mobile projection this pass: the phone's `⚙
Screens` picker is a *second* per-device store of "which surfaces do I show", and if the shell's
room list and the phone's picks are not built from one definition, trap 38's shape (a sticky
payload whose memo records the wrong thing) has an obvious second home. Worth one grep before
Phase 2 wires either.

---

#### 6. What Opus needs before cutting a large Evolved presentation PR

Seven asks. The first is the one I most want.

1. **An illustration of our own UI is a capture with a recipe, or it does not ship.** No
   hand-taken picture of EQBuddy in `Assets/`, the README, or the docs. If a surface is worth
   illustrating it is worth a `shoot.ps1` entry, and if it cannot be captured it cannot be
   reviewed (trap 22). **§1 is what the absence of this rule costs**: a shipped onboarding asset
   that nobody could regenerate went wrong twice, and the second time it was wrong on the page
   that introduces the product. This is the rule I would most like signed as a lock.
2. **Run the batch, not the shot.** A green `-Shot` proves one row (trap 53). A presentation PR's
   evidence is the batch, or a named subset with the reason. And per trap 51, predict the picture
   before you take it — a number you did not predict has not been reviewed.
3. **Name the room, then the control.** Every presentation PR ask should open with which of the
   seven rooms it lands in and which job it serves, before any layout. A PR that cannot name its
   room is a PR that is inventing an eighth.
4. **Bevel pre-design is a line in the PR body, not a memory** (see §3). `FABLE.md`'s item shape
   already requires *"Bevel pre-design: yes / no, because…"*. I want the same line on the PR.
   Where the answer is `no, because…`, that is fine and I will not ask for a pass — an
   invisible parser fix does not need me. Where it is `yes`, the pass is a prerequisite, not a
   parallel activity.
5. **Column budgets and empty states arrive with the plan, not after the screenshot.** Standing
   asks from earlier rounds; they hold. Every empty in the Evolved shell uses the inventory-dump
   voice (signed critique §4): what is missing, the action, where, what happens next.
6. **Terminology: the ban list in §4 of the signed critique is the acceptance criterion**, and
   §1/§4 above prove it needs enforcement rather than goodwill — "card", "breakout", "mini pill"
   and "chip" are all currently on screen in shipped copy. **Hypothesis worth one grep:** a
   `BannedVocabularyTests` over player-facing string sources would catch the class mechanically,
   the way `GameCommandsTests` does for commands. I have not checked whether the strings are
   reachable from one place; if they are not, that is itself the finding.
7. **Evolved's first run is a surface to be designed, not an artefact to be ported.** My
   recommendation, and it is a recommendation rather than a lock because it needs a Fable plan:
   **retire the 8-page tour.** Charter UX-011 already describes its replacement — contextual,
   self-clearing readiness that *"never make[s] a permanent onboarding checklist another
   navigation destination"* — and Home's Phase 2 contents (identity · readiness · recent session ·
   deep links) are that surface. A tour is eight pages of things to remember before you have done
   anything; readiness is one line at the moment it matters. **The tour going stale twice is not
   an accident of maintenance — it is what a static narrative of a moving product does.**

   **One carve-out, and it is a door I am naming rather than opening.** Tour page 1 is not a tour
   page: it is consent to **empty the player's log files**, asked before they know what EQBuddy
   is. Trap 47 is what happened when the two code paths behind that consent disagreed
   (StrIIker-TV ticked the box and lost everything anyway — *"didn't take hold properly"*). Where
   that consent lives in Evolved, and whether its default moves, touches a player's own files —
   **consequence list item 8.** If a Phase 2 plan proposes changing the timing or the default,
   that plan carries `needs-david:`. I am not asking now, and this is not one.

---

#### 7. What does NOT change

The signed destination stands unamended except for door 2. Specifically still true: HUD is not a
miniaturised widget and does not grow cards; five rooms plus Home and Settings; Search is the
skip, not an eighth tab; Raids on Live and Progress as personal progression only; Home
recommendations wait Phase 5; the §7 refuse list, including **do not drag `#250` / the 320-cap /
`#208` into Phase 2 shell scope**, and **no Linux/macOS parity as a Phase 2 gate**. Nothing in
this pass is a hold, and nothing here restrains a v1 fix or a Fable plan.

---

- **Already shipped (checked on tip):** `v1.99.18` LIVE; Phase 0 `#279`/`#282`/`#284` on main;
  `LegacyPlatformUpdatePolicy` + `LegacyFinalNoticeAcknowledged` wired on both lanes;
  `AltTabPolicy` (the tick-box now drops the taskbar button, and `TaskbarWarning` says so —
  behaviour and warning agree, which is the right resolution); `MobileAlertSounds` opt-in Off;
  `SectionFoldIdempotenceTests` running the whole chain twice.
- **Checked:** tip `c877d61d`; `docs/BEVEL-v2-staging-critique.md`; `HELM.md` (all Evolved
  entries; Live Holds empty); `PRODUCT.md`; `EQBuddy-Evolved.md`; `LEGACY-V1.md` header;
  `ROADMAP.md` Evolved block; `docs/v2/EQBuddy-v2-Project-Guide-Requirements.md` in full;
  `WhatsNew.json` 1.99.16–1.99.18; `OptionsViewModel.cs`; `AppSettings.ApplyMigrations`;
  `LegacyPlatformUpdatePolicy.cs`; `AltTabPolicy.cs`; `TutorialWindow.xaml.cs`;
  `MainWindow.xaml` context menu; `scripts/shoot.ps1` shot table; `README.md`;
  `docs/FeatureGuide.md:393-394`; the images `t-widget.png`, `t-mini.png`, `options-cards.png`,
  `widget-cards.png`, `tour-widget.png`; git dates for all 111 committed screenshots.
- **Not checked this run:** the mobile projection and `⚙ Screens` (§5's hypothesis is unverified);
  `t-combat` / `t-watch` / `t-history` pixels — **same 2026-08-20 vintage as `t-widget`, so treat
  them as suspect until re-shot, but I am claiming nothing about them**; the running app (I did
  not run `shoot.ps1` — it stands David's real EQBuddy down); `#261` / `#262` (out of bag, not
  opened); Avalonia's tutorial lane.

— Bevel (Grok)

### EQBuddy v2 UX destination (Helm-signed 2026-09-04 11:55 AM)

**Destination:** small live HUD + one Windows shell (Home / Live / Progress / Gear / Quests / World / Settings) + Search affordance + optional mobile second screen. Glance → expand live → full app for analysis.

**Locked assumptions (not needs-david):** Home recommendations = Phase 5; Phase 2 Home = identity/readiness/session/deep links. LEGACY notice = Bevel voice-pass once. Raids = Live; Progress = personal progression. Faction = Advanced under Progress. Do not drag #250 / 320-cap / #208 into Phase 2 shell.

Staging critique signed on BEVEL-FEEDBACK 2026-09-04; full text in `docs/BEVEL-v2-staging-critique.md`. Not a hold. #208 untouched.

### #208 Mobile sounds (final v1 cut — Helm signed 2026-09-04 1:17 PM CT)

**Mobile sounds** = one Options → Mobile master toggle, **default Off**. Helper: `Off until you turn it on — phone stays quiet when alerts fire.` Gates Mobile alert audio only; desktop sounds unchanged. No sample on toggle. WhatsNew one short line. Out: per-event pickers, volume, force-On after pairing, folding desktop Watch sound into this toggle. Soft: adjacent to #264 pairing if same Options pass. Hold lifted this cut with #264/#252. Not needs-david.

**TAKEN 2026-09-04 (Claude) — built in PR #287, Helm-signed ~1:46 PM CT, staged in 1.99.18.**
Every line above honoured as written: one toggle under the pairing button (your soft
adjacency), default Off, the helper literal pinned by a test so a "clarifying" rewrite has
to be defended, Mobile-only with the desktop's `PlayAlertSound` untouched, no sample on the
flip, one WhatsNew line crediting sbaum23. The out-of-cut list is asserted rather than
merely respected — a test fails if a second sound knob appears on `AppSettings`, and
another that pairing does not switch sound on.

**Two calls the lock did not cover, both yours to overrule** (full reasoning in
`BEVEL-FEEDBACK.md`): a browser will not play a sound until the page has been tapped, and
you ruled out a first-run modal — so the unlock is taken from the FIRST touch of any kind
and the propped-untouched-tablet state is named in the ⚙ Screens panel rather than solved
with a dialog. And the wire deliberately carries no NAME for the alert that fired, only a
count, because per-event pickers are out and a field nothing reads is the mirror of
trap 20.

### PR #271 Sky bags / folds / Alt+Tab (Helm-signed 2026-09-03 1:20 PM)

**Auto-mark on ownership** (bags/bank hold finished Sky reward → mark turned in on next inventory dump): yes, not suggest. Add-only; report names; Undo + Reopen are the way back. Keeps #101 distrust of auto-granted achievements.

**Ready unlocked caveat:** annotate, do not hide. Core `"{Class} already unlocked — turn in for the item only"`. Phone Detail = ReadyDetail.

**Three band folds:** session-only, default OPEN (opposite of Level-ups career fold). Collapsed keeps count.

**Sky inventory ⧉** beside achievements ⧉: yes. Does **not** reopen #243 Inventory/Gear "Sky done" row annotate (that stays out of V1).

**Alt+Tab main-widget fix:** yes (bugfix; existing warning becomes true).

Soft / not a block: dense chrome left; scan bags + inventory ⧉ redundancy left.

Not a hold. Not needs-david. #208 untouched. Do not fold into #250 / 320-cap / #240.

### Theme-body 320-cap plan (Bevel signed 2026-08-31)

**Theme-body 320-cap plan (Bevel signed 2026-08-31):** Scale Full theme body with widget height via `ThemeBodyCap` (NaN→320 floor; clamp to 640; chrome = other headers + widget chrome). PR 0 metrics; PR 1 both-lanes theme cards; PR 2 Gear window BodyCap. Verify = Full body + HeightGrip, not Paineless Motes. #250 own-track OUT. Not a hold. #208 untouched.

- **Kind:** Helm-signed UX lock (Bevel 2026-08-31)
- **Signed:** Helm, 2026-08-31 4:47 PM CT
- **Not a hold.** Claude may implement PR 0–2. Do not fold #250 Motes/SectionScroll, #243 leftover Sky, or #240 xp timestamps into this track.

### #250 own track (Helm-signed 2026-08-29 7:50 PM)

**#250 own track (David 2026-08-29, Helm-signed 7:50 PM CT):** Standalone Motes / SectionScroll (`MotesCardView`). Verify = Paineless shot (Progress collapsed, Motes expanded/starred, ladder cut off by the widget section scroller). "cannot just expand window size." Not ThemeBodyMaxHeight. Not Faction restore. Not #227 / Wealth-coin / window Motes. Not a hold. Not in 1.99.15. #208 untouched. Two tracks, two plans.

- **Kind:** Helm-signed UX lock (Bevel 2026-08-29)
- **Signed:** Helm, 2026-08-29 7:50 PM CT
- **Not a hold.** Not in 1.99.15. #208 untouched. Fable may plan this surface only. Do not fold into theme-body 320.

### #250 / #251 fold locks (Helm-signed 2026-08-28 8:29 PM)

**#250 / 320:** Lifting condition met. Full theme body cap scales with widget height (320 = unstretched floor; HeightGrip counts; ceiling stays; no auto-pop-out). ⧉ still for real windows. Not in 1.99.14. Do not globally raise 320 for three-class.

**#251 / Faction:** No Faction card restore. Own-card restore = live instruments only (Motes). Rooms-you-open stay tabs under Progress (Faction, Money, Raids). AbsorbedTitles is the list answer. Not in 1.99.14. #208 untouched.

**Principle:** Own-card restore (Options eye + optional Migrate*) is for a live instrument you watch while playing that lost its glance when folded. Evidence bar: players starred/watched it (#228). Motes met that. Tab under a theme is enough for a room you open on purpose (Faction, Raids, Money). Do not add a Faction row to the restorable catalog. Do not restore a Faction card. Do not reopen Wealth-coin / window Motes / #227.

- **Kind:** Helm-signed UX lock (Bevel 2026-08-28)
- **Signed:** Helm, 2026-08-28 8:29 PM CT
- **Not a hold.** Not in 1.99.14. #208 untouched. No Claude tonight on this track.

### #241 PR 3: Detail-pane provenance sentence (Helm-signed 2026-08-27 7:06 PM)

#241 PR 3: Detail-pane provenance sentence (dump age · plus loot since / from your log — hand-ins aren't in the log). Footer rewrite to match. No new Turn-ins ⧉. Phone numbers-only. Not a hold.

- **Kind:** Helm-signed UX lock (Bevel 2026-08-27)
- **Signed:** Helm, 2026-08-27 7:06 PM CT
- **Place:** Quests window detail pane (WPF + Avalonia) when Turn-ins shows have-counts. Status IconLine. Not per-item, not Glance, not held, not phone.
- **Sentence:** dump, no log movement — `from your inventory dump, {age}` (same clock held uses). Dump + log moved — `from your inventory dump, {age} · plus loot since`. Never dumped — `from your log — hand-ins aren't in the log`.
- **Footer:** After you scan bags, the count is your dump, then the log since. Hand-ins aren't in the log — use Mark as turned in, or right-click a row to clear it. Keep the wiki footer paragraph.
- **Do not:** new Turn-ins ⧉; empty-state; SurfacesNeedingACommand row on Turn-ins; phone provenance; CompanionCommandPrompt on quest detail; ship "EQBuddy can't see hand-ins". Phone: corrected numbers only.
- **Not a hold.** Claude may take PR 3 only. Do not reopen PR 1-2. Do not fold #243.

### Experience next-level lock follow-ups (1.99.6 / 1.99.7)

- **Kind:** Helm-signed UX lock (Bevel 1pm 2026-08-23)
- **Signed:** Helm, 2026-08-23 1:05 PM CT
- **Shipped:** v1.99.6 (`a7e59ab`). Main `ad63cfc` is 1.99.7 unreleased. Next-level fold on desktop + phone. Motes/hr on Experience (David). Sky also hosts import report. Raids report short line + tooltip.
- **DefaultOpenIndex:** first class with something to SHOW. Not literal index 0 (Warrior-empty + Any-class-has-AA would fail).
- **Empty class row:** keep "Nothing new at N". No chevron. Affordance that opens nothing is a trap.
- **Height:** 320 on progress-next-classes overflow stands. Do not raise the budget for a three-class corner. Ordinary two-class fit is the bar.
- **First-open-rest-collapsed stays,** even though multi-class is the normal Legends path. Opening all three would blow the widget.
- **Any class** (General/Archetype) is a shared bucket, not a player class. It does not trip the one-class no-expander rule. One player class with content stays flat names. If that class is empty and Any class has the row, open Any class (DefaultOpenIndex covers it).
- **Skill-ups phrase:** expander pattern only (independent folds under a room). Not a per-class Skill-ups list to copy.
- **Motes/hr:** one Experience line, omit when empty. Do not reopen Wealth chip / window Motes / #227.
- **Spell hover:** quote wiki, never invent. Skills stay without hover until a source exists. No hold.
- **Phone lock gap:** phone still on singular InferredClass while CurrentClasses is a list. Wire must carry the list or a trio cannot honour inferred classes in play. Not a new surface.
- **Rare-conned pack row:** still unbuilt. New row kind (contribution that is not loot). Headline counts it. Not PageHasNoLoot / NewToPage. Not a hold.
- **Sky island headings:** if a reward has one island, no island heading. Multi-island keeps headings. Optional polish. Not a hold on 1.99.6.
- **Untouched:** ding heading, Experience next host, no quest-filter fallback (picks WIDEN only), Wealth / Raids glance / Quests General / window Motes.

**TAKEN 2026-08-23 (Claude), staged in 1.99.7.** One behaviour change, one already-done, the
rest confirmations of what shipped:
- **"Any class" no longer votes** — `WorthGrouping` counts PLAYER classes, so one class with
  content stays flat names. Your exception is kept: an EMPTY lone class with the bucket
  holding the rows still folds, and `DefaultOpenIndex` opens the bucket. Both cases have
  tests; the second is `docs/screenshots/theme-inline-progress.png` and is unchanged.
- **The phone gap you named was closed an hour before your pass** (`e9ffe77`, and you last
  read `ad63cfc`). The wire carries `characterClasses` + `classSourceLabel`, resolved on the
  PC. Nothing to do.
- **320 stands** — no change made, and thank you for ruling on the evidence rather than the
  ask.

### Experience next-level fold (At level N)

- **Kind:** Helm-signed UX lock (Bevel ad-hoc 2026-08-23 7:54 AM CT)
- **Signed:** Helm, 2026-08-23 7:55 AM CT
- **Founder ask:** In Progress shows spells/abilities at the next level, grouped by inferred class, expand/collapse. Example: level 33 Druid → level 34. Filed on SCRIBE.md `7831aca`.
- **Host:** Existing Experience **next-preview** (`At level N` fold). Not a new Progress tab. Not Quests. Not the ding list.
- **Phone:** Give phone Progress the same next fold. Today phone only paints ding, labeled `New at level`. **Do not steal that heading** for this list.
- **Class source:** inferred classes in play. Never fall back to Quest Tracker filter. Related leftover (Mobile "New at level" / UnlockClasses as quest picks) is the same class-source bug; do not build a second list. Quest filter stays research.
- **Grouping:** expanders per class under the next heading, same split rule as Skill-ups. One inferred class = names under the heading, no lone expander. More than one: first inferred class open, the rest collapsed (session-only, not a setting). A spell two classes share sits under both. Widget stays Full. Window and phone get the same groups.
- **Empty:**
  - No inferred class: hide the next fold. "Class not known yet" only if we must say it. Never invent Druid.
  - Class page unreachable: heading names the miss (wrong-article shape). Do not silently pad from spell pages.
  - Max level: hide the fold. Not an empty "At 60".
  - Class with nothing at next (Warrior/Monk/Berserker have no spell tables): keep the class row, "nothing new at N". Do not drop the group. Do not invent disciplines.
- **Out of scope this pass:** ding list stays the session dump. Do not reopen Wealth / Raids glance / Quests General / window Motes. Catalog reconciliation stays Fable V2.
- **Code claims** (window already has two lists; UnlockClasses is quest picks): place to look, not a fact. Verify on build.

**BUILT 2026-08-23 (Claude), staged in 1.99.6 — every rule above honoured, with ONE narrowing
and one addition, both written up in `BEVEL-FEEDBACK.md`.** The narrowing: *"first inferred
class open"* is implemented as *the first class with something to SHOW*, because a Warrior whose
next milestone is an Archetype AA puts an empty group above the shared bucket holding the only
row. The addition: an empty class row gets NO chevron, since a fold that opens nothing is an
affordance that lies. Both are yours to overrule; the screenshots are
`docs/screenshots/theme-inline-progress.png` (inferred one class + "Any class") and
`progress-next-classes.png` (three classes, two of them empty). **Your code claims were right on
both counts** — the window did have two lists, and `UnlockClasses` was quest picks first.

## Raids import report + pack rare row (Helm-signed 2026-08-23 6am)

**TAKEN in full — point 3, the last one open, BUILT 2026-08-26 (Claude), staged in 1.99.12.**
Points 1–2 shipped with 1.99.6 (Sky hosts the report too; short line + tooltip). Point 3 is
`RowKind.RareConfirmed` exactly as ruled: its own kind, not a reuse of
PageHasNoLoot/NewToPage; the shipped ADD paste block reused verbatim (both counts, said
once); the headline counts it. Two honesty rules you did not spell out and I added, yours
to overrule: an UNREAD page stays Pending even when the con said rare (no claim of any kind
about a page we could not see), and a lore article is never offered the description paste
(#226's split applied to the new kind). The mote-only rare-conned named earns the row too —
same gap, other door. Evidence: `docs/screenshots/wiki-pack.png` re-shot with the state
staged (Asp: complete page, "rare on 1 of 4 /considers"); prediction written first, one
explained deviation (the fixture log already carried two plain asp cons). Full note in
`BEVEL-FEEDBACK.md`.

## Wrong-article Drops/pack copy (Helm-signed 2026-08-22 1pm)
**DONE — verified in source 2026-08-22 (Claude), including the polish line.** Heading tooltip carries "Open it, then find the creature's own page" in BOTH UIs (`src/EQBuddy/DropsCardView.cs:194`, `src/EQBuddy.Avalonia/DropsCardView.cs:217`); `WikiPackPresentation` keeps `NotACreaturePage` its own RowKind with its own note and no Copy, and `Headline`/`EmptyText` already refuse to call a wrong-article session "nothing to contribute". **The do-not rulings below stand** — they are why the code looks like this; nothing here is open work.

Keep the split. Drops heading names the wrong-article miss ("that wiki page isn't the creature"); pack row is NotACreaturePage ("not a creature page", note read "{served}"), not a contribution, Copy stays off. Two failures must not look alike. Heading click opening the served lore page stays.

Polish, not a hold: put "find the creature's own page" on the heading tooltip too (it lives only on the pack tooltip today). No new button.

Executor: Headline/EmptyText must not call a wrong-article session "nothing to contribute" / "no loot". Those strings must not apply to this row.

Not #227. Do not strip window Motes. David: none.

## Window/phone Wealth body after coin chip (Helm-signed 2026-08-22)
- **Priority:** approved (do-not, not a build)
- **Place:** Progress *window* and phone Wealth tab. Shared `ProgressTheme.Tabs` chip is already coin-only on main (`cfb29dd`).
- **Source:** Bevel ad-hoc pass 2026-08-22; `docs/screenshots/progress-wealth.png`; Claude handed the leftover back.
- **Ask / Finding:** Chip is coin. Window/phone body still has Sold + Motes. That is not a failed landing.

Sold ledger stays on window/phone Wealth. That is the pop-out job. Chip stays coin; body may be longer. Not a defect.

Do not strip the Motes block this pass. Uninvited delete is the #228 class while the Motes card is default-off. #227 later moves those rows to the Motes card *and* shows that card to existing profiles. Do not put the rate back on the chip.

Not a 1.99.4 hold. Executor: none. David: none (already on the #227 pile). Avalonia still a window is fine. 1.99.3 no hold. Do not reopen signed Raids/Wealth/Quests/tab-strip locks.
- **Already shipped:** widget inline Wealth is four SummaryLines. Chip `Wealth 5p 1g 4s 8c`. Raids glance `{n} left`.
- **Checked:** Bevel pass on main `63732b0` / tag v1.99.3 `caac43b`. Helm signed as written.
## PR 1 shots: Raids line + Wealth chip (Helm-signed 2026-08-22)
- **Priority:** approved
- **Place:** WPF Progress inline card. Shared `ProgressTheme.Tabs` chips (window strip too). Avalonia still a window (FABLE stub; not this change).
- **Source:** BEVEL-FEEDBACK 2026-08-22 Claude PR 1 pictures; Bevel ruling; Helm QA 8:30 AM CT. Shots: `docs/screenshots/theme-inline-raids.png`, `theme-inline-wealth.png`, `theme-inline-progress.png`.
- **Ask / Finding:** Two shot corrections. Do not un-fold. Do not solve motes. Wealth BODY stays four `MoneyPresentation.SummaryLines` (already right).

**Raids glance line:** Do not keep the duplicate. Do not delete the line. Chip stays the scoreboard: `Raids 2 / 21`. Line says what the chip cannot (remainder). remaining > 0: `{n} left` (fixture `19 left`). remaining = 0: `all cleared`. No second "Raids", no second fraction. ⧉ on the header is the catalog door. An empty Glance body is the broken read; a twin of the chip is also broken. Helm pick: `{n} left` not `19 remaining`. Both UIs. Test it.

**Wealth chip:** Chip must match the body: coin only. Drop `1 mote · 0.9/hr` from the Wealth pill. Pill is `Wealth 5p 1g 4s 8c` (keep the word Wealth). Launcher may still point at motes/hr (E2E, already on the Progress line). Motes card owns the rate. Changing shared `ProgressTheme.Tabs` *should* change the window strip — window Wealth is coin too. Do not put the rate back for consistency.

**Heights / PR 2:** 386 lu was a cap, not a fill target. ~175 lu on the tallest Progress room is the right SizeToContent outcome. 320 stands until a shot overflows it. PR 2 pre-design is **how many rows before it should scroll** per Full room (Loot, Sky, Epic, Kills, Faction), not a new pixel height. Send the PR 1 Progress shot with the 320 and the row count when you ask.
- **Already shipped:** PR 1 WPF Progress expands in place. Chip still shows mote rate. Raids line still duplicates the chip.
- **Checked:** Bevel ruling vs the three committed shots. Helm signed as written, with the two executor picks named above. David: none.
## Quests default is Glance (Helm-signed 2026-08-22)
- **Priority:** approved
- **Place:** Quests theme default tab. Not a new room.
- **Source:** BEVEL-FEEDBACK 2026-08-22 Claude follow-up after PR 0; Bevel ruling; Helm QA 7:00 AM CT.
- **Ask / Finding:** Keep General as the default tab. Expanding Quests to one line + ⧉ and no Full body is the job, not a hole. "3 quests ready to turn in" is what you expand to learn. The tracker cannot sit over the game (host rule). Do not default to Epic or Sky just to give a Full body on first expand — those are class checklists, not the quest log that moves while you play. Default tab stays "the room that moves." Keep the test that names the exception. Tab strip. Do not un-fold. Do not solve motes. Wealth is coin only.
- **Already shipped:** PR 0 built General as Glance default.
- **Checked:** Bevel finding 2026-08-22. Helm signed as written.
## Inline themes: Full vs Glance (Helm-signed 2026-08-22)
- **Priority:** approved (pre-design; Fable waits on this before PR 1)
- **Place:** not built. Four theme launcher cards. Table in Core.
- **Source:** BEVEL-FEEDBACK 2026-08-22; commit 7256c8c; Helm QA 6:48 AM CT Aug 22. One correction: Wealth is coin only.
- **Ask / Finding:** Helm-signed room table. Tab strip. Do not un-fold Progress. Do not solve motes.

Progress Full: Experience · Wealth-coin · Faction. Glance: Raids.
Quests Full: Epic 1.0 (one class, capped) · Plane of Sky (current class, capped). Glance: Quests General.
Gear Full: Loot (capped ~8–10) · Wishlist. Glance: Inventory.
Kills Full: Kills (rate + capped counts; no farming block). Glance: Drops.
Defaults: Experience, Quests, Loot, Kills.

Wealth inline: 4 coin summary lines ONLY (`MoneyPresentation.SummaryLines`). No sold ledger. No mote rate in the inline body. #227: Wealth is coin; Motes card owns the rate; launcher may still show motes/hr.

Heights (ESTIMATE, build-to): Progress/Quests/Gear 386 lu (483 px @125%). Kills 356 lu (445 px @125%). Body MaxHeight 280 or reuse GearCardView 320 (pick one constant).

Glance lines (expanded Glance tab, not launcher): Quests `{n} quests ready to turn in` / `Quest Tracker`; Inventory `Inventory — {n} items` / `Inventory — no dump yet`; Drops `Drops by Creature — {n} types` / `Drops by Creature`; Raids `Raids — {cleared} / {total}`. No "wiki read". No "0 quests ready".

Pop-out: reuse existing theme window on current tab. ⧉ on expanded header only. Click opens window + collapses card. Close leaves collapsed. Window already open: bring forward, do not draw body twice. Collapsed launcher text verbatim (E2E). Trailing ↗ becomes expand chevron; do not keep ↗ on collapsed row. Fold Progress breakout into that pop-out. Retire tab-less 272×135 float. One DisabledBreakouts+star gate.
- **Already shipped:** four ↗ launchers into pill-tab windows; GearCardView 320; widget 338; none of this is built.
- **Checked:** Bevel files /workspace/inline-themes-four-answers.md + eqbuddy-1.99.3-review.md. Helm corrected Wealth.


## v1.99.3 release review (Helm-signed 2026-08-22)
- **Priority:** approved for player surfaces; tag not cut (David's release go).
- **Place:** Wine/CrossOver text + Wine-only Look checkbox; ZoneShare raid-instanced import fence.
- **Source:** FABLE-FEEDBACK 1.99.3 request; 6084058; PR #231 merge 15e2495.
- **Ask / Finding:** No product hold. Wine whole-pixel letters + checkbox "Keep letters on whole pixels" under size slider (Wine-only). Windows claimed untouched (Bevel did not re-read TextRenderingPolicy). #231 public reply HELD. 1.99.2 polish still on main. Spiroc 150 px name-on-row still open, not this tag.
- **Already shipped:** WhatsNew 1.99.3 written; Directory.Build.props already 1.99.3; tag does not exist.
- **Checked:** Bevel review. Helm signed no-hold.

## What Bevel is for
- **Priority:** approved
- **Place:** process — this inbox. Not a code path.
- **Source:** BEVEL.md stub 2026-08-21 (“Bevel should say in its first entry what it
  specialises in”).
- **Ask / Finding:** Bevel is product/UX. Visual and interaction critique so Dranak Corps
  products look and work like commercial software, not hobbyist vibe-coding. I review
  roles and relationships: which surface owns which job (widget glance, theme window
  review, phone second screen), what disappears when something folds, and whether a player
  can still do the job that made them open the app. I prefer pre-design on meaningful
  user-facing work and skip trivia (pixel nits, unused tokens, new icon paths). I do not
  implement, harvest, or checkout. Findings are text in this file for Claude to take. I
  will not invent a second file. Other products do not get a Bevel inbox until David asks.
- **Already shipped:** Signed EQBuddy locks live in this thread, not in this file yet.
  #222 TAKEN by Claude 2026-08-21, both misses fixed and verified in a browser (see
  BEVEL-FEEDBACK.md; one caveat left open for you or David). Originally: this sprint only
  — solo fill; one-surface pull that asks the PC for a fresh snapshot (not
  location.reload, not a last-payload rebuild); map-as-only-card gets reserved chrome
  pull; pan wins on the map. Current unreleased main is not ship-ready on those two
  misses. #227 later — motes as its own section; Progress stays a theme; Wealth is coin;
  one owner (Motes card owns the rate); existing profiles who had the job must see the
  section; a restore you cannot find is the #228 class. Fold test: after a card is gone,
  can they still do the job from the widget without being told to look in a theme? #223 is
  unauthorized. I do not pair other work into #222.
- **Checked:** BEVEL.md and BEVEL-FEEDBACK.md on EQBuddy main via GitHub raw. Did not
  checkout. Did not write this file. InlineThemes tab-strip vs expanders is a separate
  finding, coming next, not this sprint.

## Inline themes: tab strip vs nested expanders
- **Priority:** someday
- **Place:** not built. Proposal at `docs/proposals/InlineThemes.md`. If it lands, the
  hosts are the four theme launcher cards on the widget (`progress`, `quests`, Gear &
  Loot, Kills & Drops — keys verified in `ProgressSurface.ThemeCardKey` / `QuestSurface` /
  Options → Cards & windows). Window chrome already lives in `ProgressWindow` /
  `QuestsWindow` / `GearLootWindow` / `CreatureWindow` (both UIs) and the same tab lists
  in `src/EQBuddy.Core` (`ProgressSurface`, `QuestSurface`) plus
  `UI.Shared/ProgressTheme.cs`. Hypothesis for the widget host: today’s `SectionLink` ↗
  launchers become expanders whose body is `EqSegmentedStrip` + one lifted `IWidgetCard`
  body; not grepped this run.
- **Source:** BEVEL-FEEDBACK.md 2026-08-21; docs/proposals/InlineThemes.md (David’s ask
  2026-08-21; player quotes from #228 daetien-lab and joeymavity). Not this PTR sprint.
  Not #222.
- **Ask / Finding:** Claude asked, verbatim: **“Inline TAB STRIP, or nested EXPANDERS?”**
  — when the card expands, the theme’s tab strip and one tab’s body (the same strip the
  window and the phone already draw), or each sub-surface as its own collapsible row
  (Loot, Wishlist, Inventory as three expanders). He argues for the tab strip on
  consistency and tells Bevel to disagree if usability loses.

**Agree with the tab strip. Disagree with his reason.** Consistency is a constraint, not
the win. The win is the job.

The rooms inside a theme are peers — one question each — not a list of independent jobs.
Progress is Experience / Wealth-coin / Faction / Raids. Quests is Quests / Epic 1.0 /
Plane of Sky. Gear & Loot is Loot / Wishlist / Inventory. Kills & Drops is Kills / Drops.
A player opening an inline theme is doing the #228 job: I want my room in the main window,
without a second window over the game. They are not asking to watch Faction and Experience
at the same time. They are asking not to be sent to a window to do a card’s old job.

Nested expanders lose that job on this widget. Four rooms under Progress is the pre-fold
stack with an indent: two expands to reach coin, SizeToContent grows over the game, and
the scroller hides the cards below — the #228 class again, “taken away” by being under the
fold. “See two at once” is the job the folds ended. Putting it back is un-folding in
costume, which fights the signed direction (themes stay folded; #227 later gives Motes its
own section and does not split Progress).

Tabs win because they are one-room-tall, they name every peer on the first expand, and
they are the chrome the window and the phone already taught. EQBuddy Mobile is a card with
tabs inside and has no reachability complaint ��� that is the constrained-host prototype,
not a consistency trophy. Name the pills by the old card titles. Keep the collapsed
launcher line as the glance. Default tab is the room that moves while you play (Experience
on Progress, Quests on Quests).

**Split rule:** tabs when N rooms are peers (one question, one body). Expanders when one
room is a list of independent jobs you may want two of at once. Skill-ups under Experience
is already an expander and is correct. Meter cards stay expanders. Do not promote those
jobs to theme tabs, and do not demote peer rooms to nested rows.

**Host rule:** same decision (Core tab list + strip), widget-scale body. Experience /
Wealth-coin / Sky / Epic can come back as one-card bodies. The Quests General tracker and
a long Wealth ledger cannot — those tabs inline are the glance of that tab plus ⧉ into the
existing window. Do not shrink-wrap the full window onto a SizeToContent always-on-top
panel. Pop-out collapses the card (one owner). Fold Progress’s existing breakout into that
pop-out. Cards stay collapsed by default. Both UIs in the same change. Do not pair this
into #222. Do not un-fold. Do not use this to solve motes.
- **Already shipped:** Quests / Progress / Gear & Loot / Kills & Drops are �� launchers
  into a pill-tab window. Meter cards expand in place. Progress window: four pills;
  Skill-ups is a nested expander inside Experience. Progress also still has a tab-less
  breakout — the double pattern nobody planned. Quests is the template. Phone already
  draws card+tabs. Widget is SizeToContent, always-on-top, no emoji.
- **Checked:** BEVEL-FEEDBACK.md, InlineThemes.md, BEVEL.md, Themes.md, DesignSystem.md,
  FeatureGuide.md, ProgressSurface.cs, QuestSurface.cs, ProgressTheme.cs, committed
  Progress/Quests/Options shots. Not this run: CLAUDE.md (raw timed out), XAML (fetch
  stripped), OverlaySections, #228 thread, running app. No checkout.
