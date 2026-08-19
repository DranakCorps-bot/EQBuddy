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
            v                    v                    v
      WPF MainWindow      Avalonia MainWindow    CompanionHost
      (the widget)        (Linux/macOS)          (EQBuddy Mobile, LAN)
```

| Project | Files | Lines | Role |
|---|---:|---:|---|
| `EQBuddy.Core` | 65 | 14,507 | Parsing, aggregation, settings, catalogs, wiki. No UI. |
| `EQBuddy.UI.Shared` | 35 | 3,612 | View-model/formatting shared by both UIs. **Framework-free — enforced by `ArchitectureTests`.** |
| `EQBuddy.Companion` | 14 | 2,921 | LAN HTTP+WebSocket server and the mobile page. **UI-toolkit-free on purpose**, so Avalonia can host it too. |
| `EQBuddy` | 37 | 14,432 | The WPF widget and its windows. |
| `EQBuddy.Avalonia` | 22 | 6,423 | Cross-platform build, trails by a few releases. |

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

- **Pure arithmetic extracted to `UI.Shared`** (`WidgetMetrics`, `ChipStackAnchor`) —
  ordinary unit tests, because sums do not need a window.
- **The `EQBUDDY_EXPAND` dump read by E2E** — the real app, launched, reporting facts
  about itself. This is the only thing that sees whether the arithmetic is *wired* to
  the controls. To cover a piece of window behaviour: dump the fact, assert it from E2E.

What remains genuinely uncovered is rendering and input: how it *looks*, and what a
mouse does to it.

This is not academic. Both bugs players reported on 2026-08-14 — the clipped card (#144)
and the drifting chips (#152) — live in this layer, and on the morning they were reported
nothing here could have caught either. Both are now held: their arithmetic by
`WidgetMetricsTests` and `ChipStackAnchorTests`, and #144's wiring by two E2E scenarios.
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

**`LogParser.cs` is the tight one now** — 25 lines. It is the next to need this treatment.

**And the biggest file in the repo had no ratchet at all until 2026-08-19.**
`EQBuddy.Avalonia/MainWindow.cs` is 5,127 lines — some 700 more than the WPF widget this
table was written for — and it was missed because the hotspots were picked while the WPF
decomposition was the work in front of us, and nothing since had re-read the list. The
Avalonia twin grew unwatched for the entire time the Windows one was being pulled apart.
It is entered at its current size, because a ratchet's job is to stop growth today; it
should come down the way `SessionStats` did, by lifting card bodies out (`LootCardView.cs`
is the worked example on that side).

| File | Baseline | Now | Fails at | Headroom |
|---|---:|---:|---:|---:|
| `EQBuddy/MainWindow*.xaml.cs` | 4,422 | 4,422 | 4,864 | 442 |
| `EQBuddy.Core/SessionStats*.cs` | 2,375 | 2,375 | 2,612 | 237 |
| `EQBuddy/OptionsWindow.xaml.cs` | 1,547 | 1,604 | 1,702 | 98 |
| `EQBuddy.Core/LogParser.cs` | 853 | 913 | 938 | 25 |
| `EQBuddy.Avalonia/MainWindow.cs` | 5,127 | 5,173 | 5,639 | 466 |

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
- Windows Firewall prompts on first listen and a dismissed prompt fails silently from
  the device's side.
- Avalonia trails WPF by a few releases and does not host EQBuddy Mobile yet, though
  the seam is deliberately there.
