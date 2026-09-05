# The Avalonia test spine — one row per file, and where its assertion went

> **EXECUTED 2026-09-04 (E-2c).** `src/EQBuddy.Avalonia/` and `tests/EQBuddy.Avalonia.Tests/`
> are deleted; `ci.yml`'s `build-avalonia-linux`, its render-test step, `check.ps1`'s
> `avalonia` stage and `release-assets.yml` went with them. **This file is now the record
> rather than the plan**, and it is the reason the deletion could be reviewed at all: every
> row below was written and signed while the code still existed, so nothing became an
> unnamed line in a delete commit. The three destinations it promised were all honoured —
> two ports landed in E-2a, twenty scanner rows were re-decided one by one in E-2b, and the
> six genuine losses in the ledger at the bottom are named rather than absorbed.
>
> `docs/TestPlan.md` rows that this suite used to hold now say so in their Held-by column,
> and point back here. **The one thing that could still go wrong is silent re-coverage**: a
> future PR that claims one of these six is covered again should have to show which test,
> because the whole cost of this phase was paid to keep that honest.

**E-2a of the Evolved plan (`FABLE.md` → "Phase 1: subtract the platform"), written BEFORE
anything is deleted.** `tests/EQBuddy.Avalonia.Tests` is 24 files and ~170 assertions, and
until this phase it was **the repo's only rendering coverage that runs on a push** — the WPF
app has no unit tests at all (`docs/TestPlan.md` §5). Deleting that without saying, per file,
what happens to what it proved is how a fold loses a capability quietly: traps 20, 26 and 43
are one event told three times, and a fold is exactly the event that produces it.

The plan's instruction was to attempt three destinations in order — un-gate the E2E suite so
"ported to E2E" is a row somebody actually runs; port what is pure into `tests/EQBuddy.Tests`;
and write down, per remaining file, what is accepted as lost and why.

---

## The prerequisite, settled first: the E2E suite DOES run on a hosted runner

`ci.yml`'s `e2e-windows` job was `if: github.event_name == 'workflow_dispatch' && inputs.run-e2e`,
and `tests/EQBuddy.E2E/README.md` gave the reason: *"a desktop session is required — this is
why the suite is not part of push/PR CI"*. **That was an assumption nobody had tested.** A
dispatch of the unmodified job on `main` (run `33921376980`, 2026-09-04) launched the real
`EQBuddy.exe` 44 times on `windows-latest` and came back **41 passed, 3 failed, in four
minutes**. The three failures were ours, not the runner's:

| Failure | Cause | Fix |
|---|---|---|
| `SessionGoesLive_AndFreshKillUpdatesLiveStats` | `AppHarness.Launch` waits for the replay to **start** (`killsTotal > 0`) while its doc comment claimed it waits for it to finish. Tests sample a baseline and wait for baseline + 1, and `WaitForDump` is an **equality** — a counter still climbing through the fixture sails past the expected value between two polls. It failed with *"kills to reach 10; last seen 9"* beside a dump reading `kills=14`. | `Launch` now waits for `killsTotal` and `lootTotal` to hold still for two UI ticks. |
| `KillThenLoot_ShowsUpOnTheLootSurface` | The same race on the other surface. | Same fix. |
| `DraggingTheWidgetTallerGrowsTheOpenThemesBody` | It asserted the **monitor**. The drag is clamped to the work area before it reaches the arithmetic, so on the runner's 1024×768 display the stack was granted 560 units and the cap correctly stayed at the floor. | The `EQBUDDY_EXPAND` dump now carries the two **inputs** beside the answer (`themeBodyRoom`, `themeBodyChrome`), and the test asserts the relationship — the cap in force is the tested formula applied to the room this machine granted. Equally strong on any screen. |

**So the destination is real, and this file's "ported to E2E" rows mean a test that runs on
every push** rather than one nobody would run. The un-gate itself lands in the same PR as
these fixes, with the run evidence in its message.

The cost is honest: ~5 minutes of `windows-latest` per push, and a suite that launches GUI
apps. The bar taken before flipping it on was the plan's own — **eight consecutive green
runs**, recorded in the PR.

---

## How each row was classified

The question is never "does this test still pass" — it is **what does the SHIPPING product
lose**. Three things turned out to be true across the whole spine, and they decide most rows:

1. **The decisions these render tests exercise live in `Core` / `UI.Shared`, and every one of
   them already has its own test file in `tests/EQBuddy.Tests`** — `LootPresentationTests`,
   `QuestPresentationTests`, `HistoryPresentationTests`, `WikiPackPresentationTests`,
   `DropsReportTests`, `SpawnTimerTests`, `TimerViewTests`, `OptionsViewModelTests`,
   `WidgetMetricsTests`, `ChipStackAnchorTests`, `CompanionThemeTests`, `ThemePaletteTests`.
   Those run on every push and are untouched by this phase.
2. **What the Avalonia render tests uniquely proved is that a decision REACHED A CONTROL on
   the Avalonia lane** — and that lane is being removed. The equivalent claim for the shipping
   lane is `EQBUDDY_EXPAND` + `tests/EQBuddy.E2E`, which is why the un-gate above came first.
3. **Several of these files were written FOR the Avalonia copy of a hand-copied twin** (the
   `WindowZoom` zero-width bug, the simplified `BreakdownRows`, the missing Companion wiring).
   Their subject is drift between two lanes. With one lane there is no drift to catch, and the
   WPF original is left exactly as covered as it was before that lane existed — which is to
   say, by `docs/TestPlan.md` §5's manual pass.

Where a row says **accepted loss**, it means: the assertion dies with the code it was about,
or the shipping lane never had it. Where it says **already covered**, the destination test is
named and it runs on push today.

---

## The table

| # | File | Tests | What it proved | Disposition |
|---|---|---|---|---|
| 1 | `AppThemeTests.cs` | 3 | Derived tones follow the palette formulas, retint on a switch, every catalog theme applies | **Already covered.** `CompanionThemeTests.DerivedTonesMatchTheDesktopsOwn` pins all four formulas to exact hexes and `ThemePaletteTests.EveryValueIsParseableArgbHex` parses every key of every catalog theme — both in `EQBuddy.Tests`, both on push. The formulas moved to `UI.Shared/ThemeTones` long ago; what is lost is Avalonia `AppTheme`'s consumption of them. **This settles plan item 5: a WPF twin buys nothing, because the catalog guard was never Avalonia's to hold.** |
| 2 | `BreakdownRowsTests.cs` | 5 | Headline/dim split, miss %, resist share, blocked casts + tooltip, overflow said out loud | **Accepted loss (control level).** WPF's `BreakdownRows.FillRows` renders **pre-built `HistoryPresentation` rows**, so every string and number here is shared and covered by `HistoryPresentationTests`. These tests exist because the Avalonia lane had a simplified inline row builder and none of it. E-3 rebuilds these as Live boards. |
| 3 | `ChipStackTests.cs` | 3 | Mez chips carry the elapsed fraction, the stack takes a clock source, chips draw their gauge track | **Already covered / accepted loss.** The fraction is `UI.Shared/TimerView`'s (`TimerViewTests`); the show/hide rule is `ChipStackPlanTests`; the anchoring is `ChipStackAnchorTests`. The gauge being *drawn* is the Avalonia control, deleted with it. |
| 4 | `ClickThroughTests.cs` | 1 | An unsupported backend declines instead of throwing | **Accepted loss — the code goes too.** `EQBuddy.Avalonia/ClickThrough.cs` is X11/Wayland; the WPF click-through is a different implementation on a platform that always supports it. |
| 5 | `CompanionWiringTests.cs` | 4 | **Every source `CompanionSources` declares is wired**; a fresh profile listens on nothing; the pairing window renders its QR and offers the address picker | **PORTED** (row 5a below) — the reflection guard becomes a source scanner over `src/EQBuddy` in `EQBuddy.Tests`, because the WPF lane is the one that ships Mobile and nothing checks its wiring today. QR rasterisation is `QrEncoderTests`; "off by default" is `CompanionEnableTests`. |
| 6 | `DropsRenderTests.cs` | 3 | Creature groups with rates/badges/stars, a re-check on every heading, the filter reports emptiness | **E2E covers the shape** (`TheDropsSurfaceDrawsACreatureHeadingAboveItsDropRows`, plus `dropsMobs`/`dropsRows`/`dropsRecheck` in the dump — the re-check-per-heading count is asserted there). Grouping and rates are `DropsReportTests`. |
| 7 | `FightTimelineRenderTests.cs` | 2 | The timeline view consumes the shared builder; "no fight yet" says so | **Accepted loss.** `FightTimelineTests` covers `TimelineBuilder`; the WPF `FightTimelineWindow` is uncovered as it always was. |
| 8 | `HistoryRenderTests.cs` | 2 | History draws the shared DPS timeline and expands encounter detail | **Accepted loss.** `HistoryPresentationTests` + `SessionRepository` tests cover the data; WPF's `HistoryWindow` is uncovered as it always was. |
| 9 | `HotkeyManagerTests.cs` | 3 | The gesture grammar, **the modifier-required rule** (the 1.34.0 disaster), stable action keys | **Accepted loss, with the reason.** Both lanes have a `HotkeyManager`; this tested the Avalonia one. Lifting `Parse` to `UI.Shared` would mean re-implementing WPF's `Key` → virtual-key table by hand — a rewrite of a safety gate, tested only by its own transcription. The WPF original is unchanged and stays as covered as it has been since #100. **E-3 owes this one a home**: hotkeys are re-hosted when Options is rebuilt, and that is the moment to lift the grammar. |
| 10 | `IconGeometryTests.cs` | 2 (×every icon) | Every `IconPaths` entry parses through a **real** geometry parser and fills the 24×24 grid | **PORTED to `tests/EQBuddy.E2E`** (row 10a) — `DesignSystemTests.EveryIconPathIsWellFormed` says in as many words that it is only "the cheap structural half" and that the real check "cannot live here" because UI.Shared is toolkit-free. E2E is `net10.0-windows`, so it can use WPF's own parser — the one the shipping app actually hands these strings to — and after the un-gate it runs on push. |
| 11 | `IconSheetTests.cs` | 1 (opt-in) | Draws every icon to one PNG for human review | **Accepted loss.** A capture surface for the lane being removed. The Windows equivalent is `scripts/shoot.ps1`; an icon sheet for the WPF lane is E-3 work if the shell wants one. |
| 12 | `InventoryRenderTests.cs` | 6 | The Inventory tab's pivots, the `/outputfile` recipe when there is no dump, no scroller of its own (trap 36), the badge staying quiet | **Partly covered.** E2E asserts the Gear/Inventory rooms (`TheGearThemeExpandsInPlaceAndInventoryIsAGlance`, `TheGearCardDrawsItsGroupsAndPivotsBetweenSlotAndZone`, `AnEmptyGearCardSaysSoAndOffersNoPivot`) and `GameCommandsTests.SurfacesNeedingACommand` holds the ⧉ command rule for both lanes. Trap 36's "brings no scroller of its own" is **an accepted loss**, and it is named in the ledger below. |
| 13 | `LegacyNoticeRenderTests.cs` | 3 | The legacy notice reaches a control, the second automatic check paints nothing, the menu still answers | **Accepted loss, as recommended by the E-0/E-1 review.** The surface it proves ships frozen at `v1.99.18` on `legacy-v1` and can never change again; `LegacyPlatformUpdatePolicyTests` (12 tests) and the six-call-site scanner survive in `EQBuddy.Tests`. This row exists so it is not an unnamed line in a delete commit. |
| 14 | `OptionsRenderTests.cs` | 32 | The Options window end to end: every tab, persistence round-trips, the width drag, the saved tab, share-string import, hotkey rows, buff-set editing | **The largest single loss, and it is named.** Every *decision* is covered by `OptionsViewModelTests`, `SpellTrackingTests`, `SpokenAlertsTests`, `WatchArrangeTests`, `AlertSoundPlanTests` and `SectionFoldIdempotenceTests`; what goes is the proof that the controls exist and write through. WPF's `OptionsWindow` has never had any. **E-3 replaces this surface outright** (Bevel pass #2 §4 routes four of its five jobs elsewhere), so porting 32 assertions to a window scheduled for demolition is work with a known expiry — the honest move is to write the loss down here and make the shell's Options a must-list row when it is built. |
| 15 | `QuestsRenderTests.cs` | 16 | The quest window's tabs, detail pane, checklist ticking, the two leftover bands and their folds | **Partly covered.** E2E has six quest scenarios (`TheQuestTrackerBuildsAListWithASelectionAndAFilledDetailPane`, the Sky tab's leftover bands with and without a dump, the epic filter, both checklists glanceable); `QuestPresentationTests` + `QuestChecklistLayout` tests own the rules. The band FOLD interactions are an accepted loss. |
| 16 | `ThemeBodyCapRenderTests.cs` | 5 | #250: the cap follows the height grip, both directions, and **reaches the control** | **Already covered, and strengthened in this PR.** Its own header says the WPF twin "says the same thing through the `EQBUDDY_EXPAND` dump and `tests/EQBuddy.E2E`" — four E2E tests, all of which passed on the hosted runner, and the growth direction is now asserted as a relationship rather than a constant. |
| 17 | `UpdateOfferTests.cs` | 11 | Which artifact each platform is offered; nothing off Windows ever auto-installs | **Accepted loss — the code goes with the platform.** `UpdateOffer` is `internal` to the Avalonia lane and is entirely about the Linux/macOS artifacts E-2 stops shipping. The two rules that outlive it are already in `EQBuddy.Tests`: `LegacyPlatformUpdatePolicyTests.NoDesktopOffWindowsIsEverOfferedVersionTwo` and `.CurrentAgreesWithTheRunningOperatingSystem`. |
| 18 | `WidgetRenderTests.cs` | 60 | The whole Avalonia widget: cards, themes, breakouts, pop-outs, the CPU readout's fixed width, per-host surfaces | **Partly covered; the rest accepted.** E2E covers the card stack, the five themes' expand/pop-out/close sequences, the Progress rooms, the level-ups fold, gear, quests, watch rules, alt-tab and the Mobile pump — 44 scenarios against the real exe. Trap 45's "every host gets its own surfaces" is guarded lane-independently by `SurfaceOwnershipTests`; trap 12's fixed-width CPU readout by `PerfReadoutTests`. |
| 19 | `WidgetSheetTests.cs` | 2 (opt-in) | A picture of the Linux/macOS widget | **Accepted loss.** Capture surface for the deleted lane; `scripts/shoot.ps1` is the Windows equivalent and stays. |
| 20 | `WikiPackRenderTests.cs` | 5 | Rows with status and pooled scope, copy dimmed with nothing to paste, the re-check names its targets | **Covered.** E2E's `TheWikiPackWindowDrawsRowsAndCarriesTheRecheck` asserts the same window on the shipping lane; `WikiPackPresentationTests` owns the rules. |
| 21 | `WindowZoomTests.cs` | 2 | A window with no saved zoom keeps its full width; a saved zoom scales it | **Accepted loss — it was about the twin.** The bug it pinned (`saved` passed through as 0.0) was Avalonia's alone; WPF has always had the `: 1.0` fallback, three lines from the one that was wrong. `WindowZoomMath.Step` is covered in `QuestTrackerTests`. |
| 22 | `ZoneWindowsRenderTests.cs` | 5 | Map circles, the camp-removal confirm, travel answers for the zone, the share preview, the session picker | **Partly covered.** E2E's `WorldOpenersTests` opens the map, travel and spawns surfaces on the shipping lane and asserts their state; `ZoneMap`, `SpawnPointLedger` and the share format have their own Core tests. The map's context-menu confirm is an accepted loss. |
| 23 | `GlobalUsings.cs` | — | Project scaffolding | Deleted with the project. |
| 24 | `TestAppBuilder.cs` | — | Headless Avalonia app builder | Deleted with the project. |

### The two ports, in full

**5a — `CompanionSourcesAreWiredTests` (new, `tests/EQBuddy.Tests`).** The Avalonia test
reflected over `CompanionSources` and asked the live widget which properties were still null;
the WPF widget cannot be constructed in a toolkit-free test project, so the port is a source
scanner in the shape `CompanionSnapshotArgumentTests` already uses — every property the record
declares must be assigned in `src/EQBuddy/MainWindow.xaml.cs`'s `new CompanionSources { … }`.
**This protects the lane that actually ships EQBuddy Mobile, and nothing does today.** A
member added to the record and forgotten fails at the moment the gap is created, which is the
whole point of the original.

**10a — `IconGeometryTests` (moved to `tests/EQBuddy.E2E`).** Same assertions, WPF's
`Geometry.Parse` instead of Avalonia's, no app launch. It belongs to the E2E project because
that is the repo's one Windows-targeted test project, not because it needs the harness.

---

# E-2b — the shared-suite scanners, one explicit call per file

**Re-derived at execution rather than copied forward, as the plan instructs: 20 files in
`tests/EQBuddy.Tests` named `EQBuddy.Avalonia`** — the same number the E-0/E-1 review
re-counted on 2026-09-04, and the same list.

Every one of them exists because two lanes could drift. With one lane that reason is gone,
and the plan's warning is the thing to hold on to: *a scanner that scans one file it will
always find is a guard that cannot fail, and it passes forever while reading as coverage.*
So no row was dropped without asking what the guard was FOR once the parity half is
removed — and in every case but one the answer was that the parity half was the SECOND
reason, not the first.

| File | Call | What it is for now |
|---|---|---|
| `ArchitectureTests` | **Row removed, with a tombstone** | The Avalonia widget was the largest file in the repo. The comment that replaces it says the WPF row did **not** inherit its headroom: 4,273 stands, one line from its cap. A deletion that quietly raises someone else's ceiling is exactly the re-anchor this table exists to make somebody argue for out loud. |
| `ChipStackPlanTests` | Row dropped, reason rewritten | Was "two lanes ask the plan"; is "the decision stays LIFTED". A widget that regrows its own placeholder wording passes every unit test in the file — `ChipStackPlan` would still be correct and simply not consulted. |
| `ClassSourceWritersTests` | **Row KEPT — it goes with the FILE, in E-2c** | The discovery that set this PR's boundary. Dropping the Avalonia writer row turned `NoOtherFileParsesAnAchievementsDumpUnnoticed` red immediately: that guard is a catch-all, an un-listed file that still parses a dump is precisely what it reports, and it was right. **A row may only be dropped once the thing it names has stopped existing.** |
| `CompanionQuestsTests` | Row dropped, reason rewritten | The cross-lane half goes; the half that made it a scan stays — an init-only property left unset compiles, runs and serves an empty tab. |
| `CompanionSnapshotArgumentTests` | Row dropped, reason CORRECTED | The #202 defect was never cross-lane: it was **two push sites in one widget** disagreeing about arguments, and both are in the file that remains. |
| `DesignRatchetTests` | Five rows dropped | "The list only ever grows" is a rule about migration, not about deletion. The glyph ban is not about Linux: a dingbat renders as a box under Wine, and a hardcoded size argues with `DesignTokens` on any platform — including E-3's shell. |
| `DesignSystemTests` | Comment re-pointed (in the E-2a commit) | It named `IconGeometryTests` as living in the Avalonia suite; it now says where the test actually is. |
| `DocumentationTests` | **No change here; one comment updates in E-2c** | Its scan reads every test project from source, so it keeps working. The comment naming `EQBuddy.Avalonia.Tests` as a legitimate citation target becomes false only when the project goes. **See the E-2c note below — this test is the one that will fail loudest at deletion time, and that is it doing its job.** |
| `FocusHideTests` | **Replaced, and the old one had never worked** | `TheTwoUisNameTheirWindowsTheSameWay` was doubly unable to fail: with one lane it is `Assert.Subset(x, {})`, and its regex carried two literal **backspace characters** (0x08) where `\b` was meant — a `\b` eaten as an escape by whatever wrote the file, exactly the hazard CLAUDE.md's tooling note describes. It matched nothing in either lane, ever, and passed. Replaced by `EveryDenyListedWindowNameStillExists`, which guards what survives: `FollowsWidgetHide` compares a type NAME, and a name has no compiler behind it (trap 53). |
| `GameCommandsTests` | Seven rows dropped, rule restated | The rule was never about parity: a surface that names an `/outputfile` command and offers no way to run it is the same defect as a silent no-op, worst in the empty state. E-3 adds rows as the shell takes these surfaces over. |
| `ImportReportReachesASurfaceTests` | Three rows dropped | The remaining rows are per-OUTCOME, not per-lane, and the trap-43 shape they guard is untouched. |
| `LegacyPlatformUpdatePolicyTests` | Row dropped, **weight kept deliberately** | The one policy whose subject IS the platforms being cut, so "the question is settled" is the tempting wrong read. The remaining widget is what tells a Linux or macOS player on the final v1 where their build ends. |
| `LogJanitorPolicyTests` | Row dropped | The count of lanes was never the point; the count of DECIDERS was, and three of the original four are in the file that remains. |
| `MobileAlertSoundsTests` | Row dropped | The must-list is per call site, and the sites it counts are all in the remaining file. |
| `OverlayActivationTests` | Seven rows dropped | The first reason is untouched: a window that takes the foreground mid-fight steals the player's keyboard. |
| `QuestLedgerClearCountTests` | Row dropped | The hand-rolled offset it forbids is a within-lane regression. |
| `QuestReconcileWiringTests` | Row dropped | A constructor is where wiring goes missing, nothing in Core can see it, and the widget's constructor is edited on every theme change. |
| `SurfaceOwnershipTests` | **Re-pointed at the WPF lane, and a false claim fixed** | The plan says the exemptions must not be re-justified as "one lane, so ownership does not matter", and this file would have gone vacuous the other way: every check in its first group read `EQBuddy.Avalonia`, and its own header claimed "the same scan runs over both lanes" — the WPF half was a claim, not a scan. Now it scans the five WPF hosts, matching the SHAPE (`UIElement`/`FrameworkElement`/`Control … TabBody(`) rather than four Avalonia signatures, and the silent `if (!File.Exists) return;` is an assertion. Trap 45 was found by Avalonia and is not about Avalonia: a WPF `UIElement` has one parent too, and there the symptom is a surface silently vanishing rather than an exception. |
| `WatchPinMigrationTests` | Row dropped | A migration inlined back into the widget is invisible to every other test in the file; `DoesNotContain` on the setting name is what keeps that honest. |
| `WikiRecheckPathTests` | Row dropped, renamed | The whole original defect was the WINDOW defeating a correct Core. |

**After this pass, five files still name `EQBuddy.Avalonia`**: two as provenance in a doc
comment (`CompanionSourcesAreWiredTests`, `DesignSystemTests`), one as history in a
rewritten header (`SurfaceOwnershipTests`), and **two that E-2c must handle** — the
`ClassSourceWritersTests` writer row, and `DocumentationTests`' comment.

### What E-2c inherits, found here

**`DocumentationTests` will fail on the deletion commit unless the docs move with it.** It
asserts that every suite named in `CLAUDE.md`, `docs/TestPlan.md` and
`docs/Architecture.md` exists, and **15 of the deleted suites are cited across those three
files** — `WidgetRenderTests`, `DropsRenderTests`, `OptionsRenderTests`,
`QuestsRenderTests`, `IconSheetTests`, `WidgetSheetTests`, `ThemeBodyCapRenderTests`,
`AppThemeTests`, `ChipStackTests`, `CompanionWiringTests`, `LegacyNoticeRenderTests`,
`UpdateOfferTests`, `WindowZoomTests`, `WikiPackRenderTests`, `IconGeometryTests`. That is
the guard doing exactly its job — *a trap that names a guard which no longer exists is
worse than a trap with no guard at all* — so the doc edits belong **in** the deletion
commit, beside the `docs/Architecture.md` size numbers `DocumentationSizeTests` pins.

---

## The ledger: what is genuinely gone

Six things, and no row of the table hides one of them:

1. **A rendering assertion that runs without a display server.** E2E needs a Windows session
   (hosted or real); Avalonia's headless platform did not. That is the price of one lane.
2. **The Options window's controls** — 32 assertions, on a surface E-3 replaces.
3. **The hotkey gesture grammar** (#100's safety gate), until E-3 lifts it.
4. **Trap 36's "this view brings no scroller of its own"**, which no dump key can currently
   see. If the shell reintroduces lifted views inside scrolling hosts — and it will — this
   wants an `EQBUDDY_EXPAND` fact rather than a memory.
5. **Two capture sheets** (icons, widget), for the lane being removed.
6. **The Linux/macOS update routing** — deliberately, with the platform.

## What E-3 owes this file

`GameCommandsTests.SurfacesNeedingACommand`, `ImportReportReachesASurfaceTests` and
`DeadSettingTests.Known` are the three curated lists that catch a fold losing a writer. The
shell rebuilds Options, Gear, Live and the HUD; **items 2, 3 and 4 above are the rows those
lists owe back**, per moved surface rather than at the end.
