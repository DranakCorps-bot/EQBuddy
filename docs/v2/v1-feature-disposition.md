# The v1 feature disposition — one row per feature, and where it goes in Evolved

> **E-2e of the Evolved plan, written BEFORE anything is folded.** This is the sibling of
> [`avalonia-test-disposition.md`](avalonia-test-disposition.md), one phase later and one
> level up: that file said, per test file, what the shipping product loses when a suite is
> deleted; this one says, per **feature**, what a player loses — and where it comes back —
> when v1's cards, breakouts and windows are replaced by seven shell rooms and a HUD.
>
> **It is docs-only. Nothing here is a licence to cut anything.** Every row's execution
> still needs its own Bevel pre-design where it is user-facing, its own Helm last-look, and
> its own PR. The table exists so that when a cut is proposed, the argument about where the
> feature went has already been had, in writing, against the signed IA.

**Why it is written now rather than at the end.** Search chrome landed in E-3 with nothing
to search: the disposition-backed index (old v1 name → the room that has it now) is exactly
this table read at runtime, and Helm's #305 last-look parked that index on this file by
name. Second reason, and the one this repo has paid for repeatedly: a fold is the event that
produces traps 20, 26 and 43 — `SkyQuestCompleted`, `EpicQuestCompleted`, `SkyQuestClass`,
the three `MiniStats` writers the Progress theme nearly took with it, and
`LastAchievementsImport`, which shipped documented as read by a surface that never read it.
Five capability losses, one mechanism: **the data survived the move and the write path did
not.** So every `Remove` and `Merge` row below carries a *what writes it* column, and that
column is the whole point of the exercise.

---

## The spec, verbatim

Cited from `FABLE.md` → **EQBuddy Evolved — LOCAL-ONLY development start**, the signed E-2e
section, so that a reader can check this file against what was actually asked for rather
than against a paraphrase of it:

> **E-2e — the v1 feature disposition table, with Bevel's IA as the destination authority.**
> Output to `docs/v2/v1-feature-disposition.md` (charter §21.1), one row per feature:
> name · today's door(s) · v2 room · class (Keep / Merge / Replace / Advanced / Remove) · why ·
> **what writes it**. Spine: `docs/FeatureGuide.md`'s 13 `##` sections, `docs/Themes.md`'s seven
> themes, ~200 `AppSettings` properties, `BreakoutKind`'s six members, and the **43** `*Window*`
> files under `src/EQBuddy/`.
>
> - The destination column is **not** invented here. `docs/BEVEL-v2-staging-critique.md` §2 is a
>   signed Keep/Merge/Replace table with the old name on the left, and its two live Helm-locked
>   doors bind: Home is identity + readiness (recommendations wait Phase 5), and **Raids hosts on
>   Live** while Progress is personal progression with Faction as Advanced. (Door 2, the LEGACY
>   notice voice pass, is retired — the shipped copy stays.) Do not re-litigate them and do not
>   drag #250, #251 or the 320-cap into this table.
> - **`Options → Cards & windows` is a routing exercise, not a cleanup.** Bevel pass #2 §4 read the
>   tab and found five jobs on it: overlay cards (→ nothing; the shell nav replaces them), the gear
>   checklist import (→ **Gear**; an import workflow is a domain action), the 12 mini-dashboard
>   checkboxes (→ **HUD, edited on the HUD**), the eight breakout toggles (→ **Live** boards and
>   **HUD** chips), and two genuine settings stranded among them. Four of the five are deletions
>   with a destination. **The rows get routed, not carried** — and the tab currently prints
>   *"Double-click a mini pill chip to open/close its breakout"*, one sentence containing three
>   pieces of our own architecture, all three on the signed terminology ban list.
> - **Every `Remove` and `Merge` row names what stops writing.** This pass is a fold of the entire
>   product, and a fold is exactly the event that produces traps 20, 26 and 43.
> - **Trap 30 applies to the tooling**: `shoot.ps1` enumerates `BreakoutKind` by hand. Touching
>   that enum means grepping `scripts/` in the same change.
> - The table is what makes the Phase 2 gate a test rather than an opinion: *every `Keep`/`Merge`
>   row names a v2 door, and no row's only door is the context menu.*

---

## Four counts in that spec have moved, and one of them was never right

Trap 52's rule — *before you act on a premise, re-derive it from a second source* — applied
to the spec's own spine, because a disposition table built on a stale count is a table that
silently omits rows. All four were one `grep` each.

| Spec says | Tree says (verified this pass) | Consequence |
|---|---|---|
| the **43** `*Window*` files | **45** filenames, **25** window classes | E-3 added `ShellWindow.xaml` + `.xaml.cs`. §4 enumerates all 25 by class, not by file, so the count cannot rot again. |
| **ten** overlay cards | **nine** | `quests` left `OverlaySections.Catalog` on 2026-09-05 (HUD subtraction cut 1). §1 carries the retired row anyway — a disposition table that forgets what has already gone is how a fold loses its own record. |
| the **12** mini-dashboard checkboxes | **ten** | `MiniBarPresentation.Order` is `kills, dps, hps, pet, procs, loot, motes, money, xp, deaths`. `OptionsCardsView.BuildMiniStats` walks exactly that list. |
| the **eight** breakout toggles | **six** | `OptionsCardsView.BuildBreakouts` walks `Enum.GetValues<BreakoutKind>()`, and the enum is `{ Damage, Healing, Pet, Watch, Loot, Buffs }`. There is no eighth toggle to route. |

The last two came from Bevel pass #2 §4, which read them off a committed screenshot rather
than off the builder. The **finding** is right and unaffected — five jobs on one tab, four of
them deletions with a destination — and it is what §5 below routes. Only the arithmetic
moved.

`~200 AppSettings properties` is fair: **194** `public … { get; set; }` declarations in
`AppSettings.cs`, of which about 150 belong to the settings object itself and the rest to the
checklist records at the bottom of the file.

---

## How a row was decided

1. **Destination is read, never invented.** `docs/BEVEL-v2-staging-critique.md` §2 is the
   signed Keep/Merge/Replace table and it is the authority for every row it names. Where it
   names a surface, this file copies the verdict and adds the two columns Bevel's table does
   not carry — *today's door(s)* and *what writes it*.
2. **The two live Helm-locked doors bind and are not re-opened here.** Home is identity +
   readiness; recommendations wait Phase 5. Raids hosts on **Live**; Progress is personal
   progression with Faction as **Advanced**. Door 2 (the LEGACY notice voice pass) is retired
   — the shipped copy stays.
3. **Bevel's verbs are mapped onto the spec's five classes**, so the table has one
   vocabulary: `Keep → unify` → **Keep**; `Move` and `Reshape` → **Merge** (the feature
   survives under another room's roof); `Replace (split by job)` → **Replace**; *Advanced
   under Progress* → **Advanced**. **Remove** is reserved for a feature that has no v2 door
   at all, and there are only five of them in this whole file (§2's double-click gesture, §5's overlay-card and breakout blocks, and §7's two geometry groups) — which is the honest headline of this pass: **almost nothing about v1 is being deleted; it is being re-addressed.**
4. **A row that Bevel's table does not name is decided here against the charter**, and says
   so in its *why*. That is most of §4, §6 and §7 — plumbing, diagnostics and chrome that an
   IA pass has no reason to mention.
5. **`what writes it` names the setting's writer, not its reader** — the column exists to
   catch the fold that keeps the data and drops the write path.

**What this file deliberately does NOT decide** — listed once, so nobody reads an omission as
a verdict: the player door (needs-david, parked); the release/tag of anything; #250, #251 and
the 320 px cap (out of scope by the spec); the *order* the cuts happen in (that is `FABLE.md`'s
kick sequence); and whether any individual cut is authorized today (per-item Helm gate, still
standing).

---

## §1 — The nine overlay cards (`OverlaySections.Catalog`)

The widget's cards are the surface Evolved replaces outright: the shell nav is the card list.
So every row here is a subtraction with a destination, and the per-item gate on each is the
one Helm signed for cut 1 — *room on the rail + chip shipped + screenshot parity*.

| Feature | Today's door(s) | v2 room | Class | Why | What writes it |
|---|---|---|---|---|---|
| **Combat card** | Card on the widget; ★ `dps`/`hps` in the mini pill | **Live** board + **HUD** DPS chip | Merge | Bevel §2: one fight — the number on the HUD, the board in the shell. | `HiddenSections`/`SectionOrder` ← `OptionsViewModel.ToggleHidden`/`Move`; `MiniStats` ← the card header ★ (`MainWindow.SetMiniStat`) and `OptionsCardsView.BuildMiniStats`. `ShowCombatFight`/`ShowCombatSession` ← `OptionsViewModel`. |
| **Healing card** | Card on the widget; ★ `hps` | **Live** board + **HUD** HPS chip when healing dominates ~30 s | Merge | Same rule, other role. The HUD swaps the third number; it does not grow a second meter. | As Combat, plus `ShowHealFight`/`ShowHealSession`. |
| **Kills & Drops card** | Card on the widget; `CreatureWindow` (the card's ⧉); ★ `kills` | **Live** (session kills) · **World** (is this camp worth it) · **Search** (lookup) · **Gear** (what dropped for you) | Replace | Bevel §2's four-way split: creature research is not a meter and not a camp map. **This is the largest single row in the file and it is an own-ask** — it is four destinations, not a move. | `MiniStats["kills"]` ← **`CreatureWindow.xaml.cs:126/128` only** (the fold rehomed it there; trap 26). `HiddenSections`/`SectionOrder` as above. |
| **Gear & Loot card** | Card on the widget; `GearLootWindow` (the card's ⧉); ★ `loot` | **Gear** | Keep | Bags, wishlist, item lookup, what you picked up — the v1 fold already made this one window of exactly the tabs a room needs, so hosting it is a move. Already landed as a shell room (E-3 PR 2). | `MiniStats["loot"]` ← **`GearLootWindow.xaml.cs:132/134` only**. `ShowTargetDrops` ← `OptionsViewModel`. **`LootSort`/`LootView` are written by `LootCardView` and `LootBreakoutView` and by nothing else** — both of which this fold removes, so the loot slice and sort strips are a trap 20 candidate the moment either goes. |
| **Watch card** | Card on the widget; Options → Watch rules | **HUD** chips (the fire) + **Settings → Alerts** (the rules) | Replace | Bevel §2: config is not a live card. The deadline earns the overlay; the rule list is a setting. | `TrackedRules` ← Options → Watch rules and the card's own row editor; `WatchSortMode` ← `WatchCardView.cs:151`, the card's own sort strip and **its only writer** — a Replace must rehome or retire it; ~~`PinWatchChips` ← Options → Watch rules~~ **RETIRED in SA-R** (2026-09-05, Helm #341) — it and the per-rule `TrackedRule.Pinned` both answered "does this chip show", so the master left and the pin is the one switch, on both hosts. |
| **Buffs card** | Card on the widget; ★ `buffs`-kind breakout | **HUD** chips (expiring) + **Settings → Alerts** | Replace | Same as Watch: expiring is the deadline, the list is settings. | `BuffTimersExpiringOnly`, `BuffWarnSeconds` ← Options → Alerts & chips. |
| **Progress card** | Card on the widget; `ProgressWindow` (⧉ and the mini bar's xp chip); ★ `xp` | **Progress** (Experience · Wealth) — **Faction becomes Advanced, Raids leave for Live** | Merge (Reshape) | Helm-locked door 3. Personal progression only. | `MiniStats["xp"]`, `["money"]`, `["motes"]` ← **`ProgressWindow.BuildMiniStars` only**, which calls `MainWindow.SetMiniStat` (trap 26's fix; do not fold this window without re-checking it). `ShowAllAAs`, `ShowNextUnlocks`, `ShowSkillUps`, `ShowLevelUps` ← Options. |
| **Motes card** | Card on the widget (hidden by default); also a Wealth-tab block; ★ `motes` | **Progress → Wealth** | Merge | Bevel §2: already folded once; Phase 2 does not reopen #250. **The card exists because David restored it in 1.99, and trap 55 is what it cost to forget that** — any Evolved fold naming `motes` as absorbed re-creates #252. | `HiddenSections` ← Options; `MotesCardOffered`/`MotesCardRestored` ← `AppSettings.MigrateMotesCard` (one-time, and it must stay one-time). |
| **World card** (`misc`, was *Travels & Deaths*) *(RETIRED 2026-09-05, HUD subtraction cut 2)* | ~~Card ⧉~~ → context menu **World…** (which already existed), `toggleMap`/`toggleSpawns` hotkeys, and the **World** shell room; ★ `deaths` | **World** | Keep | Same shape as Gear & Loot: the v1 World fold already made one window of the four tabs a room needs. Already landed as a shell room (E-3 PR 2). **Cut on Bevel's I-5 checks:** the card's only inline body was `TravelsView`, which the window builds its own instance of, and the `World…` row needed no work — the difference from cut 1. Its Options row and the four absorbed names go with it; that is the gap, recorded not papered over. | `MiniStats["deaths"]` ← **`WorldWindow.xaml.cs:146/148` only — never the card**, which is what made the cut safe ahead of SA-R. `TrackSpawns`, `SpawnFollowZone`, `MapFolder` ← Options → Alerts & chips / Behavior. |
| **Quests card** *(RETIRED 2026-09-05, HUD subtraction cut 1)* | ~~Card ⧉~~ → context menu **Quests…**, `toggleQuests` hotkey, and the **Quests** shell room | **Quests** | Keep | Kept here as the worked example of the obligation: the card's ⧉ was one of the three ways back, so the context-menu row went in **with** the cut — nothing is bound by default, so a hotkey is not a door (trap 59). The row it lost in Options → Cards & windows is the gap, recorded rather than papered over. | Nothing. `quests` was never a `MiniStats` key, which is why it went first. |

**One standing hazard for every row above.** A card removed from `OverlaySections.Catalog`
must also leave `MainWindow.SectionMap`, or `ApplySectionLayout` throws on **startup, for
everybody** — the crash the Gear & Loot fold found and E2E caught. And a fold may only name
keys that are **no longer cards** (trap 55): `ProgressSurface.AbsorbedCardKeys`,
`LootSurface.AbsorbedCardKeys` and `OptionsViewModel.AbsorbedTitles` are now checked against
the catalog by `SectionFoldIdempotenceTests`, and Evolved is that same event at ten times the
scale.

---

## §2 — The six breakout windows (`BreakoutKind`)

`{ Damage, Healing, Pet, Watch, Loot, Buffs }`. Bevel §2 is unambiguous — **breakouts do not
survive**: the boards go to Live, the deadlines go to HUD chips. What makes this expensive is
not the windows, it is the ~30 geometry settings behind them (§7).

| Feature | Today's door(s) | v2 room | Class | Why | What writes it |
|---|---|---|---|---|---|
| **Damage breakout** | ★ `dps` while minimized; Options → Cards & windows tick; double-click the mini pill chip | **Live** | Merge | A per-ability board is analysis. David uses this one — it is the reason "change defaults rather than delete" is the standing rule for breakouts. | `BreakoutDamageLeft/Top/Width/Height/Scope/Sort` ← the window itself (drag, resize, its scope and sort strips); `DisabledBreakouts` ← `OptionsCardsView.BuildBreakouts` and `MainWindow`. |
| **Healing breakout** | ★ `hps`, as above | **Live** | Merge | Same board, other role. | `BreakoutHealing*` ← the window. |
| **Pet breakout** | ★ `pet`, as above | **Live** | Merge | Pet DPS is a Live board. Pet *idle* is a chip only if it is a deadline — the binary "is my pet idle" passes the overlay test and the number does not. | `BreakoutPet*` ← the window. |
| **Watch breakout** | A pinned watch rule while minimized | **HUD** chips | Replace | Already a deadline. It is the one kind with no ★ (`BreakoutPresentation.StarKey` returns null), which is why its Options tick reads differently. | `BreakoutWatchLeft/Top/Width/Height` ← the window; no scope, no sort. |
| **Loot breakout** | ★ `loot`, as above | **Live** (session) / **Gear** (what dropped for you) | Merge | The rows are `LootPresentation` either way; the surface is not. | `BreakoutLoot*` ← the window. |
| **Buffs breakout** | ★ `buffs`, as above | **HUD** chips (expiring) | Replace | Expiring is the deadline; the list is Settings → Alerts. | `BreakoutBuffs*` ← the window. |
| **The double-click gesture** | Options → Cards & windows: *"Double-click a mini pill chip to open/close its breakout"* | Nothing | Remove | One sentence containing three pieces of our own architecture — mini pill, chip, breakout — all three on the signed terminology ban list, teaching a gesture that exists only because v1 has no shell. | `DoubleClickChipsToggleBreakouts` ← `OptionsWindow.xaml.cs:228`. Its **only** writer; removing the tick box makes the setting reader-only (trap 20) and it needs a `DeadSettingTests.Known` row or deletion in the same commit. |

→ **Trap 30, restated because it is live on this row set:** `scripts/shoot.ps1` enumerates
`BreakoutKind` **by hand** (the `mini-bar` shot disables every kind so ten stars do not open
ten windows over the capture). `Progress` joining that enum and not that list is how a shot
came to photograph a different feature under the right filename. Touching the enum means
grepping `scripts/` in the same change, and running the **batch**, not one `-Shot` (trap 53).

---

## §3 — The seven themes (`docs/Themes.md`)

Four are BUILT and are already shell pages in all but name; two were never built and this
table is where they stop being "planned windows"; one is built and reshapes.

| Feature | Today's door(s) | v2 room | Class | Why | What writes it |
|---|---|---|---|---|---|
| **1. Quests** (BUILT, the template) | Context menu **Quests…**; `toggleQuests`; **Quests** shell room | **Quests** | Keep | One definition, one room. `QuestsView` is the lift; `QuestsWindow` is a thin host and is a later retirement, not this row. | `QuestsLeft/Top`, `QuestEraFilter`, `SkyQuest*`, `EpicQuest*` ← the surface itself. |
| **2. Live Meters** (PLANNED, never built) | — | **Live** tab + **HUD** metrics | Replace | Bevel §2: *finish as* the Live room. Damage / Healing / Pet / Encounters live in the shell; the HUD carries the glance. **No new theme window.** | Nothing yet — and that is the point: this row is why "build the Live Meters window" is not on any list. |
| **3. Progress** (BUILT 2026-08-19) | `ProgressWindow` (card ⧉, mini bar xp chip); **Progress** shell room | **Progress** — Experience · Wealth; Faction **Advanced**; Raids **leave** | Merge (Reshape) | Helm-locked door 3. The reshape waits on Live existing to move Raids into. | See §1 Progress card. |
| **4. Alerts** (PLANNED, never built) | — | **Settings → Alerts** + **HUD** chips | Replace | Configuration consolidates into Settings; chips stay on the overlay. **No Alerts window as a theme launcher.** | Nothing yet. Options → Alerts & chips and → Watch rules are what it becomes. |
| **5. Gear & Loot** (BUILT 2026-08-20) | `GearLootWindow`; **Gear** shell room | **Gear** | Keep | Already landed. | See §1. |
| **6. World** (BUILT 2026-08-27) | `WorldWindow`; context menu **World…**; **World** shell room | **World** | Keep | Already landed. | See §1. |
| **7. Kills & Drops** (BUILT 2026-08-21) | `CreatureWindow` | Four-way split — **Live** · **World** · **Search** · **Gear** | Replace | The one built theme whose IA verdict is *not* satisfied by its fold. It is an own-ask by signed entry, and it is the reason `SurfaceOwnershipTests` still carries an exemption row. | See §1 Kills & Drops. |

---

## §4 — The 25 window classes (45 `*Window*` files under `src/EQBuddy/`)

Enumerated by **class**, because file count is what rotted. `WindowZoom.cs` is in the glob and
is not a window (it is the Ctrl+wheel zoom helper) — the one false positive in the 45.

| Feature | Today's door(s) | v2 room | Class | Why | What writes it |
|---|---|---|---|---|---|
| `MainWindow` | Launch | **HUD** (Surface A) | Replace | The widget becomes the HUD: collapsed/expanded, Edit on the HUD, chips, toasts. The long pole. | `WindowLeft/Top`, `Opacity`, `UiScale`, `BackgroundOpacity`, `ContentHeight`, `Minimized`, `SectionOrder`, `HiddenSections`, `MiniStats`. |
| `ShellWindow` | `EQBUDDY_SHELL=1` (local-only; the player door is parked) | **is** the shell | Keep | Already the destination. | Shell geometry, per `ShellLayoutPolicy`. |
| `ProgressWindow` | Card ⧉; mini bar xp chip | **Progress** | Merge → retire | Retirement is gated on star rehoming (it owns three `MiniStats` writers) and is later than the room. | `ProgressLeft/Top`, `WindowZooms`/`WindowBaseWidths`/`WindowHeights` keyed for it. |
| `QuestsWindow` | Context menu **Quests…**; `toggleQuests` | **Quests** | Merge → retire | Already a thin host over `QuestsView`. | `QuestsLeft/Top`. |
| `GearLootWindow` | Card ⧉ | **Gear** | Merge → retire | Owns the `loot` star writer; carries a `SurfaceOwnershipTests` exemption. | `GearLootLeft/Top`. |
| `WorldWindow` | Card ⧉; context menu **World…**; `toggleMap`, `toggleSpawns` | **World** | Merge → retire | Owns the `deaths` star writer. | `WorldLeft/Top`, `SpawnZone`, `SpawnFollowZone`. |
| `CreatureWindow` | Card ⧉ | Four-way split (§1) | Replace | Owns the `kills` star writer; carries the second `SurfaceOwnershipTests` exemption. | `CreatureLeft/Top`. |
| `HistoryWindow` | Context menu **Session history…** — *and nothing else* | **Progress** (career) + **Live** (this session) | Merge | Bevel §2. History studio depth stays desktop-only. **Its only door today is the context menu**, which is precisely what UX-002 refuses; the split is an own-ask (S3). | Session records in `history.db`, not settings. |
| `SessionPickerWindow` | Opened by History / archive review | **Home** (recent session) / **Live** | Merge | Bevel §2: identity and "what did I just do", not a hidden window. | Nothing persisted. |
| `FightTimelineWindow` | Combat card / encounter row | **Live** | Merge | Analysis, not an overlay. | `TimelineLeft/Top/Width/Height`. |
| `BreakoutWindow` (×6 kinds) | ★ while minimized; Options tick; double-click gesture | **Live** boards + **HUD** chips | Merge/Replace | §2. | ~30 `Breakout*` settings. |
| `OptionsWindow` | Context menu **Options…** | **Settings** | Replace | Bevel §2: the ⚙ is Options, it is not a window index. §5 routes its five jobs. | `OptionsTab`, `OptionsWidth`, and most of the settings file. |
| `CompanionWindow` | Title-bar button + Options → Behavior | **Settings** (pairing) — Mobile itself is unchanged | Keep | MOBILE-001: the Windows-hosted LAN-only model remains. The pairing UI is a setting; the phone is a surface. **The title-bar button is the control trap 29 was written about** — do not let it lose its door twice. | `CompanionEnabled`, `CompanionPort`, `CompanionToken`, `CompanionHiddenSurfaces`, `CompanionSounds`. |
| `WikiPackWindow` | Context menu → Data & imports → **Wiki contribution pack…** | **World** or **Search** (destination is an own-ask) | Keep | The generative half of the eqlwiki rule — EQBuddy hands the player a paste-ready edit and nothing publishes itself. **Context-menu-only today**; it needs a room door in v2, and this table is where that obligation is recorded. | Nothing persisted; reads the loot log and the wiki cache. |
| `ItemInfoWindow` | A loot row click | **Gear** / **Search** result detail | Merge | UX-001: one canonical door per domain; an item panel is a result, not a window. | Position is session-only, deliberately. |
| `ZoneShareWindow` | World window chrome | **World** (Advanced) | Advanced | Import/export of a zone's spawn archive is a power action opened on purpose. Every import previews first — that stays. | `SpawnPointLedger` files, not settings. |
| `SpawnChipsWindow` | Automatic while timers run | **HUD** chips | **DONE 2026-09-05 (SA-2)** | Folded into `HudChipRowWindow` — one row, slaved to the widget's position, no geometry of its own. Edit verbs arrive in SA-4. | ~~`SpawnChipsLeft/Top/Bottom`, `SpawnChipsGrowUp`~~ — all four removed with the window. |
| `MezChipsWindow` | Automatic while a mez is believed active | **HUD** chips | **DONE 2026-09-05 (SA-2)** | Same fold; it is the row's `Mez` family, slow chips included. | ~~`MezChipsLeft/Top/Bottom`, `MezChipsGrowUp`~~ removed; `MezChipsEnabled` stays as the Options switch. |
| `AlertWindow` | Fires on a watch rule | **HUD** toast | Keep | Bevel §3: toasts, not modals. It is already click-through and never takes focus. | `AlertLeft/Top`, `AlertSound`, `AlertVolume`, `Speech*`. |
| `TutorialWindow` | First launch; context menu → Help → **Quick tutorial…** | **Home** (readiness) + first-run | Replace | UX-011: contextual readiness instead of setup friction. **And its page 1 is the log-truncation consent gate** — trap 47 is that a second code path destroyed logs about a second after the tour appeared. Any replacement inherits `LogJanitorPolicy` as the single answer, or it inherits the bug. | `ShowTutorial` ← the tour; `TruncateLogs`/`ArchiveLogs` ← its page 1 and Options → Behavior. |
| `WhatsNewWindow` | Launch after an update | **Home** or **Settings → About** | Keep | NOTES-001 and the What's-new rule are not negotiable; the surface can move, the promise cannot. | `LastSeenVersion`. |
| `FeedbackWindow` | Context menu → Help → **Send feedback…** | **Settings → About** | Keep | No backend, nothing uploaded — it composes a prefilled GitHub draft. That property is why it survives a privacy-sensitive fold unchanged. | Nothing. |
| `GridOverlayWindow` | Options → Look | **Settings** (Advanced) | Advanced | UX-008: keep advanced controls advanced. A desktop alignment grid is a power tool with no room. | `ShowGridOverlay`, `GridSpacing`. |
| `CursorRingWindow` | Options → Look | **Settings** (Advanced) | Advanced | Accessibility-shaped (#81, "I often lose my tiny cursor"). Advanced, not removed. | `ShowCursorRing`, `CursorRingSize`. |
| `TextProbeWindow` | `--textprobe` on the command line | **not a room** — diagnostic | Keep | **Explicitly kept by the signed E-2d text**, and it runs on Windows: it is the instrument that separated three states which had looked identical for two builds (trap 42). See §8. | Nothing — and it must stay that way: it passes `persistMigrations: false` because `AppSettings.Load` writes at the bottom (trap 13's narrow exception). |

---

## §5 — Options: five jobs on one tab, and the four tabs beside it

Bevel pass #2 §4's finding, routed. **The rows do not get carried over; four of the five
blocks are deletions with a destination.**

| Block on `Options → Cards & windows` | v2 room | Class | Why | What writes it |
|---|---|---|---|---|
| **Overlay cards** — nine rows, eye + reorder, with "…are tabs in here now" notes | Nothing | Remove | The cards are gone; the shell nav replaces them. | `SectionOrder`/`HiddenSections` ← `OptionsViewModel.Move`/`ToggleHidden`. **`HiddenSections` translates to HUD content and to nothing else** (Bevel's migration position 1): "I hid Combat" meant "keep it off my overlay while I play", never "I don't want combat analysis". Translating it into room visibility would delete features from people's products on upgrade — #219 industrialised. |
| **Gear checklist** — *Open EQ Legends Tools* / *Import gear list…* / *Clear* | **Gear** | Merge | An import workflow is a domain action, not a setting. | `GearChecklist`, `GearChecklistName`, `GearInventoryAppliedStamp`. **Carries a `GameCommandsTests.SurfacesNeedingACommand` obligation** — the Gear tab is the surface that told a player to import a file and shipped no way to do it (trap 34). The ⧉ moves with the block. |
| **Mini dashboard** — ten checkboxes | **HUD, edited on the HUD** | Merge | Signed critique §3. And `MiniStats` is the one v1 setting that *is* a HUD statement — the best evidence we will ever have about what each player watches while playing. **It should seed the Evolved HUD.** | `MiniStats` ← `OptionsCardsView.BuildMiniStats` **and** the card ★s **and** four theme windows. Six writers of one list: whatever replaces them must be one. |
| **Breakout windows** — six checkboxes + the double-click blurb | **Live** boards and **HUD** chips | Remove | §2. Breakouts do not survive. | `DisabledBreakouts`, `DoubleClickChipsToggleBreakouts`. Note the asymmetry that exists today and must not be lost: ticking a breakout also **stars** its stat, unticking leaves the star — because quietly removing a pill cell when someone closes a window is a silent surprise in the other direction. |
| **Show target above the Loot card** · **Recent-rate window (15 min)** | **Settings** | Keep | The two genuine settings, stranded among four things that are not. | `ShowTargetDrops`, `RecentWindowMinutes` ← `OptionsViewModel`. |

The other four Options tabs:

| Tab | v2 room | Class | Why | What writes it |
|---|---|---|---|---|
| **Look** — theme, size, opacity, grid, cursor ring, whole-pixel text | **Settings** (+ Advanced for grid/ring) | Keep | Genuine settings. | `Theme`, `CustomTheme*`, `UiScale`, `ChipScale`, `Opacity`, `BackgroundOpacity`. |
| **Alerts & chips** — sounds, speech, buff warnings, chip growth, spawn tracking | **Settings → Alerts** | Keep | This *is* what the never-built Alerts theme becomes. | `AlertSound`, `AlertVolume`, `Speech*`, `Buff*`, `*GrowUp`, `TrackSpawns`. |
| **Watch rules** — the rule editor | **Settings → Alerts** | Keep | Config is not a live card (§1 Watch). The editor is the surviving half. | `TrackedRules`, `DefaultRulesVersion`. |
| **Behavior** — hide-when-unfocused, alt-tab, log hygiene, hotkeys, Mobile | **Settings** | Keep | Genuine settings. **Log hygiene is destructive and stays behind `LogJanitorPolicy`** (traps 47/48): one answer, one path, and enumeration is not permission. | `HideWhenGameUnfocused`, `HideWhenGameNotRunning`, `HideFromAltTab`, `TruncateLogs`, `ArchiveLogs`, `Hotkeys`, `Companion*`. |

---

## §6 — The `FeatureGuide.md` spine (13 `##` sections)

The player-facing guide is the only spine written in a player's words, so it is the one that
catches a *capability* with no card, no window and no setting.

| Guide section | Today's door(s) | v2 room | Class | Why | What writes it |
|---|---|---|---|---|---|
| Testing without playing: fixture logs & isolated profiles | `EQBUDDY_APPDATA`, fixture logs | not a room — dev/support | Keep | Support and every screenshot depend on it. `shoot.ps1` seeds its own throwaway profile for this reason and anything new that captures must too. | env only. |
| Quick tutorial | First launch; Help menu | **Home** + first-run | Replace | See `TutorialWindow` in §4 — and its consent gate goes with it, not after it. | `ShowTutorial`. |
| The widget (main window) | Launch | **HUD** | Replace | §1 + §4. | §4. |
| Camps tab of the World window (Track Spawns) | World window | **World** | Keep | Already in the room. Curated timers are never auto-written; the weekly refresh only flags them. | `SpawnPointLedger`, `TrackSpawns`. |
| EQBuddy Mobile | Title-bar button; Options → Behavior | **Settings** (pairing); the phone is its own surface | Keep | MOBILE-001. Both surfaces stay first-class **in both directions** — a decision shared by desktop and phone goes in `Core`/`UI.Shared` and all three call it, or it drifts (#210). | `Companion*`. |
| Mini mode | Double-click the widget | **HUD** collapsed | Merge | Collapsed HUD is exactly this feature with a name a player can say. | `Minimized`, `MiniStats`. |
| Click-through | Context menu (checkable) | **HUD** | Keep | Stays on the primary menu today *because flipping it mid-pull is what it is for* — that reasoning transfers to the HUD, not to Settings. | `toggleClickThrough` hotkey; no persisted setting. |
| Options window | Context menu | **Settings** | Replace | §5. | §5. |
| Session history | Context menu **only** | **Progress** (career) + **Live** (this session) | Merge | §4 `HistoryWindow`. **The `Take(10)`/`Take(8)` truncation lesson comes with it** (trap 50, #234): a surviving cap must say "…and 5 more", because the memorable mob is the rare one. | `history.db`. |
| What's new popup | Launch after an update | **Home** / **Settings → About** | Keep | §4. | `LastSeenVersion`. |
| Updates | Context menu → Help | **Settings → About** | Keep | Signing is non-negotiable and the update channel is where a player meets it. | `UpdateFolder`. |
| Log hygiene | Options → Behavior; tour page 1 | **Settings** | Keep | Traps 47 and 48 in one row. `LogJanitorPolicy` is the one answer; `GameWrittenLog` is the discriminator, and it keys on the **character set** the game writes, not the segment count. | `TruncateLogs`, `ArchiveLogs`, `ArchiveDefaultMigrated`. |
| Known limitations | The guide | **Settings → About** / docs | Keep | ACCURACY-003/004: honesty about what the log cannot say is a product feature here, not a disclaimer. | — |
| *(§Linux and macOS: this build is the final one)* | `LEGACY-V1.md`, the in-app notice | — | Keep, frozen | LEGACY-002 shipped in `v1.99.18`. The shipped copy stays exactly as written; **there is no outstanding voice pass and none should be scheduled.** | `LegacyFinalNoticeAcknowledged`. |

---

## §7 — The settings ledger: what stops writing

The spec's *what writes it* column, gathered where it is easiest to act on — grouped by the
event that would strand each group. **A `Remove` row here is a `DeadSettingTests.Known` row or
a deletion, in the same commit.**

| Group | Settings | Class | What stops writing, and what must happen |
|---|---|---|---|
| **Card furniture** | `SectionOrder`, `HiddenSections`, `WindowZooms`/`WindowBaseWidths`/`WindowHeights` (card keys) | Merge → HUD content | `OptionsViewModel.Move`/`ToggleHidden` and the card drag handles. Charter DATA-002 already permits translating these into sensible v2 defaults rather than recreating the surface. |
| **Mini dashboard** | `MiniStats` (ten keys) | Keep → seeds the HUD | Six writers today (Options, the card ★s, `CreatureWindow`, `GearLootWindow`, `WorldWindow`, `ProgressWindow`). The HUD must become **one**. |
| **Breakout geometry** | ~30 `Breakout*Left/Top/Width/Height/Scope/Sort`, `DisabledBreakouts`, `DoubleClickChipsToggleBreakouts` | Remove | The six windows and the Options tab are the only writers. This is the single largest `DeadSettingTests` obligation in the fold. |
| **Theme-window geometry** | `QuestsLeft/Top`, `ProgressLeft/Top`, `GearLootLeft/Top`, `CreatureLeft/Top`, `WorldLeft/Top`, `TimelineLeft/Top/Width/Height`, `OptionsWidth` | Remove on retirement | Each window is its own writer. They go with §4's retirements, **not before** — a retired window whose geometry setting survives is harmless; a live window whose geometry setting was deleted resets someone's layout. |
| **Overlay chips** | ~~`SpawnChips*`, `MezChips*`~~ (geometry gone in SA-2; `MezChipsEnabled` stays), `AlertLeft/Top`, ~~`PinWatchChips`~~ (retired in SA-R; the per-rule 📌 is the switch) | Keep the CHIPS, not the geometry | The chips stay and are one row now. **The anchoring did not survive and did not need to**: nothing persists chip geometry, so `UI.Shared/ChipStackAnchor` retired with the two windows and CLAUDE.md's trap 2 carries a tombstone. `ActualHeight` being 0 in a `Closed` handler is still true of every surface that DOES persist geometry. |
| **Domain data** | `SkyQuest*`, `EpicQuest*`, `GearChecklist*`, `TrackedRules`, `SpawnZone`, quest/gear checklists | Keep | The chain (loot → quest → item → mob → camp → route) is the product. **This is the group traps 20/26/43 actually bit**: `SkyQuestCompleted`, `EpicQuestCompleted` and `SkyQuestClass` each lost a writer to a fold and each cost a release. Re-check every writer per move. |
| **Migration flags** | `ArchiveDefaultMigrated`, `WatchPinsMigrated`, `WindowHeightsReset`, `MotesCardOffered`, `MotesCardRestored` and the rest of the eleven-step chain (`ApplyDefaultRules` → `MigrateWindowHeights`, counted this pass) | Keep, and keep **one-time** | `AppSettings.ApplyMigrations` runs eleven steps on every launch and `SectionFoldIdempotenceTests` runs the whole chain twice. #252 and #253 were both "a one-time step that was not marked one-time, undoing the player each launch". Evolved adds steps to this chain; each new one gets a row in that test in its own commit. |
| **Wine/CrossOver** | `WineWholePixelText`, `WineFloatOverFullscreen`, `WineKeepGameFullscreen` | see §8 | E-2d. |

---

## §8 — E-2d: Wine/CrossOver, and a premise that has moved

E-2d was ruled at **#277** — *"drop three Options knobs; keep `TextRenderingPolicy` +
`WineText`; overlay/crossover scripts go with the platform cut"* — and parked. It belongs in
this file because it is a disposition question in the same shape as every row above. **The
ruling is not re-opened here and nothing in this PR changes any of it**; what follows is the
premise, re-derived from the tree, because acting on a stale one is what trap 52 is about.

| Feature | Today's door(s) | v2 room | Class | Why | What writes it |
|---|---|---|---|---|---|
| **Whole-pixel text** | ~~Options → Look, and only under Wine~~ — **removed 2026-09-05** | **Settings** (or nothing) | **Remove — DONE** | `WineText.IsOfferedHere()` was `IsRunningUnderWine() && no EQBUDDY_TEXTMODE override`, so on the supported v2 platform this control **was already invisible**. Removing it was a source cleanup, not a player-facing change. | Nothing now. `WineWholePixelText` is a hand-edited `settings.json` knob with a `DeadSettingTests.Known` row, the same shape as the two rows below it. |
| **CrossOver overlay opt-in** | **No UI, by design** | — | Keep as-is | `DeadSettingTests.Known` already carries both rows, verbatim: *"no UI by design — Wine/Proton escape hatch"*. | `WineFloatOverFullscreen`, `WineKeepGameFullscreen` — hand-edited JSON only. |
| `TextRenderingPolicy` + `WineText` | — | — | **Keep** | Named by #277. Serves people running the **supported Windows artifact** under CrossOver, and costs one `OverrideMetadata` call. | — |
| `WineFonts.cs`, `TextProbeWindow.cs` | `--textprobe` | — | **Keep** | Not named by #277; kept deliberately rather than deleted by adjacency. `TextProbeWindow` is the instrument that ended trap 42, and it runs on Windows. | — |
| `WineOverlay.cs`, `scripts/crossover/`, `docs/CrossOver-macOS-overlay.md` | — | — | **open** | See the correction below. | — |

**Two facts that the E-2d ask should carry, both one `grep` each:**

1. **Only one of the three settings is an Options knob.** `WineWholePixelText` has
   `WholePixelTextPanel`/`WholePixelTextCheck` in `OptionsWindow.xaml`;
   `WineFloatOverFullscreen` and `WineKeepGameFullscreen` have **no UI at all**, by design,
   and `DeadSettingTests.Known` has said so since before the ruling. So "drop three Options
   knobs" can only ever drop one, and the other two are already in the state the ruling wants
   them in.
2. **`WineOverlay.cs` did not go with the platform cut, and it is still wired.**
   `FABLE-FEEDBACK.md` (2026-09-04) reported *"`WineOverlay.cs` and `MacOverlayLevel` went
   automatically, being inside the deleted project"*. `MacOverlayLevel` did — no hits in the
   tree. `WineOverlay.cs` did not: it lives in `src/EQBuddy/`, the **WPF** project, and it is
   called from `App.xaml.cs:117` (`WineOverlay.Configure`) and `MainWindow.xaml.cs:4219`
   (`MakeNonActivating`). `scripts/crossover/` (three files) and
   `docs/CrossOver-macOS-overlay.md` are also still present, and `README.md:178` still points
   players at that doc.

That second fact is a decision rather than a cleanup, which is why this file records it and
does not act on it: the ruling's *"overlay/crossover scripts go with the platform"* clause is
**unexecuted**, and executing it now would delete a documented, README-linked setup for
people running the supported Windows artifact on a Mac — the same population #277 kept
`TextRenderingPolicy` for. Both readings are defensible from the signed text and they lead to
different diffs, so the ask goes to Helm with the evidence rather than to a guess. It is filed
in `HELM-FEEDBACK.md` as a **formality ask with a corrected premise, not a re-ruling**.

**Helm ruled on 2026-09-05 (~12:40 PM CT), and clause (a) has landed.** The ask offered three
readings; Helm signed **(a) — the knob only** and rejected the other two by name. **(b)** would
have executed the literal *"overlay/crossover scripts go with the platform"* clause and deleted
a README-linked CrossOver setup for people running the **supported Windows artifact** on a Mac
— the same population #277 kept `TextRenderingPolicy` for, so the premise had moved.
**(c)** (park it) was refused on the ground that Surface A does not excuse leaving a dead
Wine-only Options panel forever.

So the first row above is now **done** and the rest of this section is a record rather than a
plan. What went: `WholePixelTextPanel`/`WholePixelTextCheck` in `OptionsWindow.xaml`, their
three wiring lines and the `OnWholePixelTextToggled` handler, and the two `WineText` members
that had no other caller (`Reapply`, `IsOfferedHere`). What stayed, by name: `TextRenderingPolicy`,
`WineText.ApplyIfNeeded`/`Resolve`, `WineFonts`, `TextProbeWindow`, `WineOverlay.cs`,
`scripts/crossover/` and `docs/CrossOver-macOS-overlay.md`. **The capability is not lost**, which
is the question `DeadSettingTests` exists to ask: a player who already unticked the box keeps
the setting, `Resolve()` still reads it, and `EQBUDDY_TEXTMODE` overrides on either platform.

---

## §9 — The Phase 2 gate, run against this table

> *Every `Keep`/`Merge` row names a v2 door, and no row's only door is the context menu.*

**Half one passes.** Every `Keep` and `Merge` row above names a room, a HUD element, or
Settings. There is no row whose destination is blank.

**Half two does not pass yet, and these are the rows that fail it.** Each one's *only* door
today is the widget's right-click menu, which is exactly what UX-002 refuses:

| Row | Today | Owed a v2 door by |
|---|---|---|
| **Session history** | context menu **Session history…** | the Progress/Live split (own-ask S3) |
| **Wiki contribution pack** | context menu → Data & imports | a World or Search door — **unassigned; this is the one row in the file with no owner** |
| **Import achievements** / **Copy `/outputfile` achievements** | context menu → Data & imports | **Gear** or **Quests**, wherever the import report lands — and it must land somewhere visible: `LastAchievementsImport` shipped documented as read by a surface that never read it (trap 43), so the report goes **above** the rows, not after them (trap 44) |
| **Review an archived log** / **Choose log folder** / **Auto-detect log folder** | context menu → Data & imports | **Settings** |
| **Quick tutorial** / **Check for updates** / **Send feedback** | context menu → Help | **Home** / **Settings → About** |

Two more that are *not* failures but are worth naming, because they look like ones: **World…**
and **Quests…** are context-menu rows *and* shell rooms *and* (World) a card ⧉. The Quests row
was added **with** cut 1 precisely so the answer to this gate would not change when the card
left — nothing is bound by default, so a hotkey is not a door (trap 59). **That is the pattern
every remaining subtraction copies.**

## §10 — The E-2 gate, run against this table

> *No v2 requirement is blocked on non-Windows desktop parity — provable from the removed CI
> jobs plus a disposition table with no row whose blocker is a non-Windows desktop.*

**Passes.** No row above is blocked on Linux or macOS. The only rows that mention a non-Windows
platform at all are §8's (Wine/CrossOver, which is the *Windows* artifact running under a
compatibility layer, and is a Keep on that basis) and §6's frozen LEGACY row, which is a
preserved artifact and not a v2 requirement.

---

## What is owed after this file

- **The Search index** reads this table. It is the reason E-2e restarted now, and it is the one
  consumer that turns a doc into a test: an old v1 name a player types must resolve to the row
  that has it.
- **Six rows have no owner yet** — the wiki pack's door, the achievements import's destination,
  the Kills & Drops four-way split, the Live Meters and Alerts themes' "finish as", and §8's
  overlay/crossover clause. Each is an own-ask; none is authorized by this file.
- **The per-item gate stands.** A row here is an argument, not a permission.
