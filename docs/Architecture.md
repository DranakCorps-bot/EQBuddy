# EQBuddy architecture

Orientation for anyone — human or agent — changing this codebase. Companion to
[../CLAUDE.md](../CLAUDE.md) (loaded every session, deliberately short) and
[TestPlan.md](TestPlan.md) (what the behaviour is supposed to be).

Measured 2026-08-14 at v1.82.0.

---

## 1. The shape of it

```
                    eqlog_<char>_<server>.txt   (the game writes it; we only read)
                                 |
                    LogWatcher   |  150 ms polls, byte-offset, truncation-safe
                                 v
                    LogParser    |  one regex per line type -> GameEvent records
                                 v
                    SessionStats |  aggregation, encounters, DPS, journal
                                 v
                    StatsSnapshot|  ONE immutable snapshot per UI tick
                                 |
            +--------------------+--------------------+
            v                                         v
      WPF MainWindow                            CompanionHost
      (the widget)                              (EQBuddy Mobile, LAN)
```

**There used to be a third arm on that fork.** `EQBuddy.Avalonia` — the cross-platform
Linux/macOS widget, 68 files and 23,201 lines, the largest project in the repo — was deleted
on 2026-09-04 (E-2c). EQBuddy Evolved is Windows-only, by the owner-approved charter; the
final v1 build for Linux and macOS is preserved, downloadable and usable at `v1.99.18` and on
the `legacy-v1` branch, which is where that code still lives. Nothing about it is lost, and
nothing about it is maintained here. See [LEGACY-V1.md](../LEGACY-V1.md), and
[docs/v2/avalonia-test-disposition.md](v2/avalonia-test-disposition.md) for what its 24 test
files proved and where each assertion went.

Sizes re-measured 2026-09-04 (E-2c, `.cs` under each project, excluding `obj/` and `bin/`).
An earlier set had drifted far enough to mislead — UI.Shared had doubled and the Avalonia
build tripled since they were written — and then drifted 10-15% again in FOUR DAYS, which is
why `DocumentationSizeTests` checks this table against the repo: a measurement nobody
re-measures rots without anyone touching it.

| Project | Files | Lines | Role |
|---|---:|---:|---|
| `EQBuddy.Core` | 98 | 22,626 | Parsing, aggregation, settings, catalogs, wiki. No UI. |
| `EQBuddy.UI.Shared` | 102 | 11,526 | View-model/formatting shared by the widget and the mobile projection. **Framework-free — enforced by `ArchitectureTests`.** |
| `EQBuddy.Companion` | 16 | 4,357 | LAN HTTP+WebSocket server and the mobile page. **UI-toolkit-free on purpose** — which is what let the Avalonia build host it unchanged while that lane existed, and what keeps it honest now that only one does. |
| `EQBuddy` | 91 | 27,863 | The WPF widget and its windows. Now the largest project in the repo. |

## 2. Load-bearing invariants

Break one of these and something quietly goes wrong rather than failing loudly.

1. **One snapshot per tick.** `SessionStats.Snapshot()` is taken once per second and
   handed to every consumer. Windows must not build their own — the perf pass exists
   because they used to.
2. **The log is the only input.** No memory reads, no packet sniffing, no telemetry.
   Every network destination is documented in `SECURITY.md` and verified from code.
3. **`UI.Shared` and `Core` reference no UI framework.** Pinned by `ArchitectureTests`.
4. **`EQBUDDY_APPDATA` redirects the whole profile.** Tests set it via a module
   initializer; the isolated-profile flow depends on nothing else leaking out.
5. **Companion surfaces are gated twice.** The desktop decides what may be *sent*
   (`CompanionHiddenSurfaces`); the device decides what it *shows* and in what order
   (its own localStorage). An ungated surface is never even projected.
6. **Per-section fingerprints** decide who gets woken by a push. They must exclude
   anything that drifts every tick.
7. **Curated catalogs are human-written.** Automation may flag, never write.
8. **The ledger's `Revision` counter is how maps notice edits.** Both map windows watch
   it; that is the entire mechanism by which curating on a tablet updates the PC.

## 3. Where the risk is concentrated

**`src/EQBuddy` — 14,432 lines, and no test project references it.** Two routes now
reach into it anyway, and neither is a unit test:

- **Pure arithmetic extracted to `UI.Shared`** (`WidgetMetrics`, `HudChipRow`) —
  ordinary unit tests, because sums do not need a window.
- **The `EQBUDDY_EXPAND` dump read by E2E** — the real app, launched, reporting facts
  about itself. This is the only thing that sees whether the arithmetic is *wired* to
  the controls. To cover a piece of window behaviour: dump the fact, assert it from E2E.

What remains genuinely uncovered is rendering and input: how it *looks*, and what a
mouse does to it.

This is not academic. Both bugs players reported on 2026-08-14 — the clipped card (#144)
and the drifting chips (#152) — live in this layer, and on the morning they were reported
nothing here could have caught either. #144 is held by `WidgetMetricsTests`, with its
wiring on two E2E scenarios; #152's whole subject — a chip stack's saved position — was
deleted rather than guarded in Surface A / SA-2, which is the better outcome and the reason
ChipStackAnchorTests is no longer here.
That is the shape of progress to aim for — each escape converts a manual row into an
automated one. See [TestPlan.md](TestPlan.md) §5.

### Hotspot ratchet

`ArchitectureTests` fails the build if these grow more than 10% past their baseline.
A path may be a glob, and then its matches are **summed** — so splitting a hotspot into
another partial cannot buy headroom. Current state:

**`MainWindow` was re-baselined on 2026-08-19, and the order of operations is the point.**
It had drifted to 4,622 against a 4,274 baseline — legal, but 79 lines from failing and
within ~100 for days. The Watch card came out to `WatchCardView.cs` first (231 lines, on
the `IWidgetCard` seam, its rendered shape pinned in E2E beforehand); the baseline was
then set to the new true count. Re-baselining WITHOUT the lift would have been raising
the ceiling, which is the one move this table exists to make someone argue for out loud.
Worth knowing before the next squeeze: 177 private/internal methods were measured in that
file and not one was unreferenced. There is no free room in it.

**`LogParser.cs` is the tight one now** — 14 lines. It is the next to need this treatment.

**TOMBSTONE — the largest row in this table was the Avalonia widget, and it left with its
lane on 2026-09-04 (E-2c). The row is gone; the lesson it bought is not, and it is the
reason this paragraph stays.** That file had no ratchet at all until 2026-08-19, by which
time it was 5,127 lines — some 700 MORE than the WPF widget the table was written for. It
was missed because the hotspots were picked while the WPF decomposition was the work in
front of us, and nothing since had re-read the list. **A hotspot list is a hand-written list
(trap 30): it stops covering the repo the day the repo grows, and the file it stops covering
is the one nobody is looking at.** Re-read this table when a project is added, not when
something fails.

It came down twice under that ratchet before it was deleted (5,637 → 5,422 when the gear
checklist lifted out; 369 more lines when the Progress fold made the two lanes the same
shape), which is the ratchet doing exactly its job right up to the end.

**Nothing here inherited its headroom.** The WPF row stood at 4,273 with one line of room,
the number it had before the deletion — a deletion that quietly raises somebody else's
ceiling is the re-anchor this table exists to make someone argue for out loud, and E-3's
decomposition budget was exactly that number. **E-3 PR 1 spent it the way the table asks:
the lift came first, and the baseline came down in the same commit.**

| File | Baseline | Now | Fails at | Headroom |
|---|---:|---:|---:|---:|
| `EQBuddy/MainWindow*.xaml.cs` | 3,895 | 4,284 | 4,284 | 0 |
| `EQBuddy.Core/SessionStats*.cs` | 2,375 | 2,444 | 2,612 | 168 |
| `EQBuddy/OptionsWindow.xaml.cs` | 393 | 393 | 432 | 39 |
| `EQBuddy.Core/LogParser.cs` | 853 | 933 | 938 | 5 |

**Lowered 1,547 → 689 on 2026-09-05 (E-3 lane D, SR-4), in the same commit as the lift.**
`OptionsWindow` had been the tight one on this table for weeks. The four alert blocks — the
watch-rules editor, the buff-set builder, the mez and spawn boxes, and the shared
sound/voice/volume/rate header above them — left for `EQBuddy/SettingsAlertsView.cs`, which
is host-neutral: it builds its own controls, takes `(MainWindow, OptionsViewModel, ready-gate)`
and knows nothing about the window it hangs in. That is what lets the Evolved shell's Settings
room compose the SAME blocks under the four `AlertSurface` tabs instead of growing a second
copy of forty control wirings to drift against this one — #210's mechanism with a bigger
surface. The baseline came down to the post-lift count rather than to something with slack in
it, per the rule two paragraphs up: **room that is freed and not claimed quietly refills.**

**Lowered again, 689 → 393 on 2026-09-05 (E-3 lane D, SR-1), in the same commit as the lift.**
Two more blocks left by the same contract: `EQBuddy/SettingsLookView.cs` (the colour theme
picker and its Custom rows, the four size/opacity sliders, the alignment grid and its spacing,
the cursor ring) and `EQBuddy/SettingsBehaviorView.cs` (EQBuddy Mobile pairing and its sounds
switch, the three hide-when rules and the Alt+Tab note, keep-above, the global hotkey rows, the
regen override, auto-empty and its archive, the tutorial toggle, the perf readout). **What
stayed is the part that is about the WINDOW rather than about the settings** — width
persistence, the monitor clamp, the tab links, the resize grips and the one dim sentence that
describes them, plus the routing of a key press to an armed hotkey recorder, which no block can
receive because rebuilding its own rows destroys the control that had focus. The `cards` tab is
the remaining tenant and SR-3 takes it.

**Raised 4,214 → 4,273 on 2026-09-04 (P0-2 / LEGACY-002, #275), and the argument is that
the ratchet was already full.** `main` stood at 4,635 lines against a 4,635 limit, so any
WPF change at all would have failed here; this one adds 64 lines of window plumbing —
a policy call, a browser-open branch, a guarded settings write. The decision itself did
leave, into `UI.Shared/LegacyPlatformUpdatePolicy`, where it is unit tested and was shared
with the Avalonia lane while that lane existed; what stayed cannot leave without moving the update banner, and
Phase 0 was told not to touch it. The bump is the MINIMUM that fits (4,273 × 1.1 = 4,700
against 4,699), so it grants one line and keeps-if-it-fits intact. **The next WPF change
lifts a surface** — there is no room left to argue with.

**Lowered 4,273 → 4,158 on 2026-09-04 (E-3 PR 1, the Evolved shell host), and the previous
paragraph is why it had to be.** The shell needed one field on `MainWindow` and there was
one line to give it, so the lift came first: the sixteen `EQBUDDY_*` window hooks — 135
contiguous lines of `if (env) Loaded += … call a method`, one job between them, owing
nothing to the widget's own state — moved to `EQBuddy/DebugHooks.cs`. That is the standing
move (lift a surface, never a split), and the same shape as the `WidgetDump.cs` lift above.
**Registration order was preserved exactly**: these are `Loaded` handlers that open windows
which stack, so a re-order would be invisible in a diff and would surface as a screenshot
of the wrong window on top. The new baseline is again the MINIMUM that fits (4,158 × 1.1 =
4,573 against 4,572), so the next E-3 move lifts again — which is the pressure, not a
side effect.

**E-3 PR 2 (two more rooms: World and Gear) changed this file by ZERO lines, and that is
worth saying rather than leaving to be inferred from an unchanged number.** The rooms are
new files; the only thing they needed from the widget was for one existing notification to
reach two hosts instead of one, and that was done by REPLACING a line rather than adding
one (`_gearLootWindow?.InventoryChanged()` → `FollowingSurfaces.InventoryChanged(this)`,
which is where the list of satellite surfaces already lives). So the baseline is untouched
and the pressure above still stands, unspent: **the next E-3 move that needs a line here
lifts a surface first.** A PR that needs no room does not get to bank any.

**Lowered 4,158 → 4,123 on 2026-09-05 (E-3 PR 5, the Live room), and the sentence above is
what made it happen.** The file sat at 4,535 against a 4,573 cap, and the Live room needed
about 35 lines of factory and accessors on the widget — so the lift came first, exactly as
the pressure was designed to force. `FillList` (the plain name/value row builder) and
`FillStatList` beside it moved into `BreakdownRows.FillPairRows` / `FillStatRows`.

**It is a lift and not a relocation, and the difference is the second consumer.** The Live
room's Damage tab draws the same procs, stances, area-spell and damage-taken lists the
widget's Combat card draws; leaving the builder in the window would have meant a second
one in the room, which is trap 33's shape (two producers of one row layout, each current,
drifting the day one of them gains a column). The ~40 call sites in `MainWindow` read
unchanged through a one-line forwarder, so the diff is the extraction rather than every
caller. New baseline is again the MINIMUM that fits (4,123 × 1.1 = 4,535.3 against 4,535),
which leaves zero headroom — the next WPF change lifts again.

**Lowered 4,123 → 4,106 on 2026-09-05 (HUD subtraction cut 1, the Quests card), and this
is the first row in this history that got its room by DELETING a surface rather than
moving one.** `quests` left `OverlaySections.Catalog` and `MainWindow.SectionMap` together
— the pairing whose absence is a startup crash for everybody — and with it went the card
build block, the `EQBUDDY_EXPAND` branch, the render call, the launcher header line and
two card-sync calls. QuestsThemeCard.cs (97 lines) and Core's QuestInline.cs (63) left the
repo outright and are not in this sum at all — unlinked here on purpose, since a doc that
points at a deleted path is the rot this file's own tests refuse.

**The honest number is 19 lines, and it is much smaller than the deletion.** About half of
what came out came back as comments recording where the card went and why `_questsHost`
outlived it. That trade is deliberate: a cut that cannot be found afterwards is the shape
CLAUDE.md's "three ways back" and trap 55 both exist to refuse, and a ten-line tombstone is
cheaper than the next session re-deriving the answer. The new baseline is once again the
MINIMUM that fits (4,106 × 1.1 = 4,516.6 against 4,516). **Deleting a surface beats
extracting one** — but only the lines that actually leave count, and here fewer left than
it looks.

**Lowered 4,106 → 4,100 on 2026-09-05 (HUD subtraction cut 2, the World card), and six
lines is the whole story — the first draft of this change made the file BIGGER.** The code
that left is 19 lines: the card build block, the widget's own `TravelsView` field and its
construction, the `EQBUDDY_EXPAND` member, the `SectionMap` row, the `_worldCard` field,
the render call, the `MiscHeader` launcher line and three `_worldCard?.Sync()` calls. The
tombstones written to record all of that came to 32, so `MainWindow` grew by 13 and **the
ratchet failed the commit** — which is the only reason anybody counted.

**That is the entry worth reading, not the number.** The note above already observed that
half of cut 1's deletion came back as commentary; cut 2 went past break-even doing the same
thing, and nothing but this test would have said so. The tombstones stayed — a cut nobody
can find afterwards is what CLAUDE.md's "three ways back" and trap 55 exist to refuse — but
they were compressed to a line or two each, with the reasoning kept in this file and in the
surface files instead of repeated at every call site. `WorldThemeCard.cs` (60 lines) left
the repo outright and is not in this sum, and neither are `WorldSurface.LauncherSummary` /
`InlineModeFor` or `WorldTheme`'s four glance methods, all of which had the cut card as
their only caller. Minimum that fits: 4,100 × 1.1 = 4,510 against 4,509.

**Lowered 4,100 → 3,964 on 2026-09-05 (Surface A / SA-1, the collapsed HUD bar), and the
row had ZERO headroom when the pass started.** 4,516 against 4,516.6 is not a squeeze, it
is a stop, and it is exactly why the plan specified a lift rather than an edit in
place: the bar's chip builder, its divider trim and its per-tick rebuild moved into
`EQBuddy/HudBarView.cs`, and what stayed is WHEN the bar is on screen — the host's job,
per trap 15. It is a **view class, not a `MainWindow.Hud.xaml.cs` partial**, because this
glob sums its matches on purpose and a partial leaves exactly as much untestable window
logic as before. Its cell count was pinned from `tests/EQBuddy.E2E` (`hudCells`) and
proved green on the pre-move tree first, since the WPF layer has no unit tests and that
assertion is the only thing between a move and a silent regression. New baseline is once
more the MINIMUM that fits (3,964 × 1.1 = 4,360 against 4,360), measured on the merged
tree: cut 2 (above) landed while this branch was in flight, and the two changes touch
different parts of the file, so the number here is the one the MERGE produces rather than
either branch's own.

Re-measured 2026-08-26, when the `EQBUDDY_EXPAND` dump block lifted into
`EQBuddy/WidgetDump.cs` (Inline themes PR 2's first commit, exactly the ratchet amendment
Fable's plan prescribed — ~140 lines of pure string-building the hotspot glob was paying
for). The baseline was first re-set to the post-lift count and then RESTORED to 4,214 by
the v1.99.12 Fable review, which ruled the convention **keep-if-it-fits**: a lift banks
into the old baseline unless the post-lift sum still exceeds the old cap. A re-anchor
that raises the ceiling erases the pressure that drives the next lift — the 22 lines of
headroom this leaves are the point, not a problem, and the next squeeze here means the
next surface comes out (the standing move, never a split).

Re-measured 2026-08-27, after World PR 2–4 had spent the WPF file down to ONE line of
headroom and its execution report flagged the squeeze. The relief was
`UI.Shared/ChipStackPlan`: the two chip stacks' existence rules (including the
Bevel-signed World-on-Camps hide-rule, until then an untested inline expression on each
lane) and the placement-preview chip's wording, all previously duplicated across both
MainWindows. Keep-if-it-fits — the baseline stays 4,214.
Earlier: the WPF widget came down 110 lines when the Gear card body was
lifted into `GearCardView.cs` for the Gear &amp; Loot theme, and its baseline came down
with it in the same commit — an unlowered baseline is refilled headroom, which is the one
way this table stops meaning anything. `OptionsWindow` is the tight one now: it took the
mez-duration editor and the breakout rewrite this week, and the editor was lifted straight
back out into `MezDurationsView.cs` when it crossed the line rather than the baseline being
raised to fit it.

**A theme fold buys much less headroom than a lift does, and the 2026-08-19 Progress
theme is the measurement.** The WPF widget came down 31 lines — `RenderRaids` left for
`RaidsCardView.cs` and four more card views stopped being fields there — because the fold
also ADDED ~75 lines of window plumbing (`ShowProgressWindow`, `NewProgressSurfaces`,
`SetMiniStat`), which is where that file already keeps every satellite's launcher. The
surfaces move; the doors to them stay.

The Avalonia twin went the other way at the time — 5,127 → 5,291, deliberately not
re-baselined — because that fold only re-parented five card BODIES where the WPF one moved
five card VIEWS. **The lesson outlived the lane (deleted 2026-09-04): re-parenting is not
decomposition, and a size table must not read as though it were.** A fold that leaves the
building and rendering where they were has moved a reference, not a responsibility.

**`SessionStats` is a GLOB entry as of 2026-08-18, and that is the interesting half.** It
was a literal path, and `SessionStats` is a partial class — so `SessionStats.Tracked.cs`
(207 lines) was never counted, the entry read 2,559 for a class that is 2,766, and the
"just add another partial" escape this ratchet exists to refuse was open on the very file
that had needed a bump. `MainWindow*.xaml.cs` has been a glob for exactly this reason;
this one simply never was. Globbing it costs no headroom today and closes the hole.

Baseline history: **2,324 → 2,559** on 2026-08-17 for #135's sixth confirmed cause (a charm
cast from an ITEM, which prints no cast line) — the file had 22 lines of headroom and the
fix needed 25, which is the ratchet saying the file is full rather than that the fix was
too big. Then **→ 2,766** when the glob landed, which is not a further grant: it is the
same code finally being counted in full.

**The charm state machine came out on 2026-08-18** into `EQBuddy.Core/CharmTracker.cs`,
and the baseline dropped **2,766 → 2,375** with it — 391 lines, `Apply` down from 787 to
about 570. It was the only large coherent seam in the file: the same audit found every
other subsystem (procs, money, fights, journal, faction) to be 7–31 scattered lines inside
`Apply`'s switch and the snapshot builder, where extraction buys indirection and removes
nothing. Trimming prose is not a lever either — the file is 31% doc and comment, which is
why six charm causes were diagnosable at all.

**How the move was verified**, because a behaviour-preserving refactor has to prove it:
all seven logs bjstrange attached to #135 were replayed before and after, tracing every
charm-state transition and every resulting watch-rule label, and the two traces are
identical byte for byte. `MezTracker.cs` is the older instance of the same move.

`MainWindow` sat at 97% of its allowance until 2026-08-15, which is not a place to work
from. The 992-line Epic/Sky checklist surface came out into `QuestChecklistView` — it
only ever touched settings, its own state and eleven named controls, so it was a
component that had never been separated rather than logic that was truly entangled. The
baseline came down with it, banking the room instead of leaving it to refill.

A year's worth of that room came back for free on 2026-08-16, by a different route: the
widget's "Sky Quest" and "Epics" cards became a single **Quests** card that opens the
Quest Tracker window, which had grown the same three tabs. Roughly 780 of
`QuestChecklistView`'s lines were rendering for cards that no longer exist, and deleting
a surface beats extracting one. What stayed is the part that was never about the cards —
the loot auto-checkers and the achievements import — and it now runs with nothing on
screen at all, because it only ever read and wrote settings. The lesson worth keeping is
that "which surface does this belong on?" is a decomposition tool as much as a product
one: the cheapest code to maintain is the code a surface decision deletes.

That is the pattern to repeat, and there is more of it: the render family (`RefreshUi`
at 551 lines, `RenderTracked` at 181) is the next candidate. Two rules make it safe.
**Pin the behaviour in E2E first** — add the facts to the `EQBUDDY_EXPAND` dump and
assert them, as `TheQuestChecklistRendersATabPerClassAndTheSelectedClassesRows` does;
with no unit tests in the WPF layer that assertion is the only thing standing between a
move and a silent regression. And **prefer a class over a partial**, because a class is
a component with a boundary you can read, while a partial is the same window in two
files — which is why the ratchet now sums them.

## 4. Concepts worth knowing before you change them

**Encounter vs pull.** A fight opens on damage and closes on the kill line or 20 s of
silence (`EncounterTimeout`). Fights then group into *pulls* when there is no 10 s lull
between them (`EncounterGrouping.PullGap`). The card and the History review share this
grouping so live and archived agree. In a zone that never goes quiet — Plane of Sky —
a pull runs long and its DPS stops meaning much; this is understood and deliberate
(discussion #151). Per-mob figures live on the Kills card and are unaffected.

**Combat window.** Separate from encounters: it is the DPS denominator. Damage taken
opens and extends it; self-inflicted damage deliberately does not ("a swim across a
lake is not a fight").

**Zone naming.** Three names for one place: the log's (`The Lair of the Splitpaw`), the
catalog's (`Splitpaw Lair`), and the map file's (`paw.txt`). `ZoneMapFiles` and
`SpawnCatalog.logZoneName` bridge them. A curation edit must quote the *catalog* zone.

**Mobile projection.** `CompanionMapSource` caches zone geometry and hands it out by
reference; `CompanionSnapshot.ForClient` withholds it from a device already holding the
stamp. A device parked in one zone receives the picture exactly once.

**Mobile cadence — two paths, on purpose.** The *latency* path is `PumpCompanion`, a
50 ms `DispatcherTimer` gated by `UI.Shared/CompanionPumpGate`: it pushes as soon as
`SessionStats.CurrentVersion` moves. The *correctness* path is the `CompanionHost.Tick`
inside `RefreshUi`, still once a second, which is what keeps `ForcedPushInterval`
reconciliation running through a camp quiet enough that the version never moves.

Mobile rode the 1 Hz redraw until 1.85.0, which added up to a second to every update for
no reason but shared plumbing — the tailer polls at 150 ms. Pumping at 20 Hz is
affordable because it is nearly always a no-op: unpaired costs a bool read, unchanged
costs a long compare, and a rebuild is 0.081 ms (`IngestBenchmark.SnapshotRebuildCost`),
so continuous change costs ~1.6 ms/s of one core. The gate is unit-tested; that the real
timer is wired to it, and pushes nothing when unpaired, is asserted from `EQBuddy.E2E`.

Countdowns are unaffected by any of this — devices compute them locally from
authoritative timestamps, and they are excluded from the section fingerprints. A ticking
clock is not news, and including one would wake every device on every pump.

## 5. Known limits, stated honestly

- Position updates only when a `/loc` reaches the log — there is no live feed. The
  breadcrumb trail is the last minute of movement and needs two crumbs 25+ units apart.
- Browsers refuse a wake lock over plain HTTP, so the mobile page cannot hold a screen
  awake; it says so rather than pretending.
- A firewall prompts on first listen and a dismissed prompt fails silently from the
  device's side. Windows asks; macOS asks once and remembers; most Linux desktops need
  the port opened by hand, and the pairing window says so per platform.
- **One host, since E-2c (2026-09-04): the WPF widget.** The Avalonia build hosted EQBuddy
  Mobile too from 1.96.2 (#208) — same `CompanionHost`, same `CompanionSources` record, same
  50 ms pump gated by `CompanionPumpGate` — and the guard that a declared source is never
  left unwired moved to the lane that ships it. `CompanionSourcesAreWiredTests` scans
  `src/EQBuddy`'s `new CompanionSources { … }` from source and fails the build on a member
  the record declares and the widget forgets. **Nothing checked the Windows lane's wiring
  before that port**, which is the point of it — the deleted test guarded the build that was
  about to go, not the one that ships the feature.
