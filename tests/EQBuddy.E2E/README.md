# EQBuddy.E2E — the end-to-end suite

The tests that launch the **real** `EQBuddy.exe`, grow a **real** log file under it, and
assert on the **rendered** result. Everything else in `tests/` exercises code in-process;
this suite exercises the app.

**It runs on every push and pull request as of 2026-09-04** (`e2e-windows` in `ci.yml`),
and that is load-bearing: `src/EQBuddy` has no unit tests (`docs/TestPlan.md` §5), and
E-2 removes the Avalonia lane that had been the repo's only rendering coverage running on
a push. A flake here is a bug to fix, not a reason to gate the job again.

*One exception to "launches the app": `IconGeometryTests` parses every `UI.Shared` icon
path with WPF's own geometry parser and starts nothing. It lives here for the target
framework — this is the repo's one `net10.0-windows` test project — and for no other
reason.*

## Running locally

```powershell
dotnet build EQBuddy.slnx -c Release          # prerequisite: the exe under test
dotnet test tests/EQBuddy.E2E -c Release
```

The suite launches the built exe from `src/EQBuddy/bin/Release/net10.0-windows/` — it
never builds the app itself, so a test run can't mutate build outputs mid-flight. No
Release build → every test fails fast with a message pointing here.

**Expect a widget AND the Evolved shell to appear briefly, on the display beside your
primary one.** Each test starts its own always-on-top EQBuddy against an isolated
profile; tests run sequentially (one app at a time) and kill + clean up on teardown. A
Windows session is required — a `windows-latest` runner has one, which is what the
un-gating rests on.

`AppHarness.Launch` sets **`EQBUDDY_SHELL=1` by default** — the owner's standing order
while E-3 is being built: a suite run must not pop a bare v1 widget on the monitor the
game is on. A test that wants a room passes an address (`ShellHostTests.OpenOn`); a test
that wants the widget alone passes an empty string, because the hook reads
`is { Length: > 0 }`. `ShellHostTests.TheHarnessOpensTheEvolvedShellWithNoScenarioAskingForIt`
asserts both halves — a default nobody asserts is a default that comes back off, and
every other test in the suite would go on passing without it.

Both windows land on the second display when the desk has one, and beside each other
rather than stacked: the shell asks `WindowPlacement.SecondaryOrigin` and the harness
asks the *same* function, then offsets the widget by the shell's open width. On a
single-screen desk — and on a 1024×768 hosted runner — that function answers null and
both fall back to where they always were.

**Nothing may assert the SCREEN.** A hosted runner is 1024×768, so a test that needs a
tall monitor is asserting the desk it was written on:
`DraggingTheWidgetTallerGrowsTheOpenThemesBody` did exactly that, and the fix was to put
the arithmetic's two inputs in the dump and assert the relationship instead of a number.
**And nothing may assert the CLOCK either.** `WaitForDump` is an equality, so a counter
still climbing when a baseline is sampled sails past the expected value between two polls
and the wait can never be satisfied. `Launch` settles that before any test samples
anything — and it does so by ASKING the app, never by watching for stillness, which was
wrong three times running (trap 56). Two keys, together:

| key | means |
|---|---|
| `ingestDone=1` | the watcher has finished the startup replay — the totals have stopped |
| `logPending=0` | the tail has consumed every byte the log holds |

**There used to be a third, `surfacesBehind=0`, and waiting for it was a wait on a
coincidence.** The six theme windows follow the widget's tick on their own throttles
(1–3 s), so a row count and the total beside it in one dump line came from two different
moments, and "the two agree" was something the throttles were never obliged to deliver —
it timed out at 90 s beside a dump reading `ingestDone=1 logPending=0 killKinds=14
kills=13`: a complete log, complete data, one row short on screen. The dump now paints
every open surface from the snapshot it is about to report (`WidgetDump.PaintOneMoment`),
so one dump is ONE MOMENT and `kills == killKinds` always. `surfacesBehind` stays in the
dump as the assertion that this holds, not as something to wait for.

`AppendLogLines` waits for `logPending` to return to 0 as a post-condition, so a stalled
tail fails at the append that caused it instead of 90 s later as a wrong row count. And
every wait aborts early — naming the app, not the assertion — when the process has exited
or the dump's `tick` (RefreshUi's own count) has stood still for 30 s. "This counter will
never move" and "this app is no longer moving" look identical from out here.

## How the harness works

`AppHarness` builds, per test:

- a temp **profile** dir passed as `EQBUDDY_APPDATA` — `settings.json` pre-seeded
  (LogFolder, on-screen window position, tutorial/spawn-window/update-check noise off),
  so no UI interaction is ever needed for setup;
- a temp **game install** whose `Logs\` holds `tests/fixtures/eqlog_Testchar_fixture.txt`
  with timestamps shifted to end one minute ago (`FixtureLog` is a C# port of
  `scripts/make-test-session.ps1`), so the replay produces a *live* session;
- the app launched with `EQBUDDY_EXPAND=1`: every card expands, and MainWindow writes a
  `debug.txt` state dump each UI tick — key=value counts and totals
  (`killsTotal`, `lootTotal`, `tracked`, per-list row counts). That dump is the primary
  assertion channel; `history.db` (via `SessionRepository`) is the persistence channel.

Tests append fresh log lines (exact shapes copied from the fixture) and poll the dump
with `Wait.Until` — every assertion is an observable condition with a timeout and a
reason; there are no bare sleeps. Timeouts fold in the dump content and the profile's
`error.log` tail.

## What v1 covers

1. Live session + fresh melee kill updates the kill surface.
2. Kill → loot line lands on the loot surface.
3. A pre-seeded watch rule counts its matching loot.
4. Graceful close persists the session; relaunch adopts (not duplicates) it in history.

## Deliberately NOT covered yet (the ledger against scope creep)

- **UI Automation** — no clicks, no visual-tree reads. `debug.txt` proved sufficient
  for v1, so no FlaUI/UIA dependency was taken. v2 candidate if a scenario needs
  interaction (Options, breakouts, satellite windows).
- **Avalonia app** — it has its own headless render tests; a Linux E2E lane is separate work.
- **Installer / updater** — `UpdateFolder` is pointed at an empty dir on purpose.
- **Spawn timers, mez/slow chips, buff timers, alerts firing** (sound/speech/banners),
  breakout windows, zone map, quest/gear/epic checklists, multi-character follow,
  log truncation janitor, crash recovery (`RecoveredAfterCrash`).
