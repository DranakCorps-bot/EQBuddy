# Flake ledger

Intermittent failures we have already paid for. Soft / CI / last-look all write
here. **“Passed on rerun” is an observation, not a resolution.**

A row stays **open** until a disposition other than rerun exists: a guard, a
harness fix, a named environment exclusion, a lane that no longer exists, or
an explicit “will not fix — here is why.” Closing on a green retry is how a
real race becomes tribal knowledge and then a merge-blocker again.

## How to add a row

Append. Do not rewrite the table. One failure mode per row. Signature is the
thing a later seat can grep — test name, assert fragment, or log line — not a
novel.

| Column | Meaning |
|---|---|
| Signature | Stable grep key (test method, assert text, or log token) |
| Occurrences | Dates / run IDs / PR numbers. Add, do not replace. |
| Affected test | Class and method if known |
| Environment | `ci/build-and-test`, `ci/e2e-windows`, local Windows, local `check.ps1`, … |
| Disposition | `open` · `observed-rerun` · `guarded` · `harness` · `lane-gone` · `wont-fix` — plus one clause |

Required header (pinned by `DocumentationTests`):

`Signature | Occurrences | Affected test | Environment | Disposition`

## Ledger

| Signature | Occurrences | Affected test | Environment | Disposition |
|---|---|---|---|---|
| `NoMoreThanTwoFetchesAreEverInFlight` | Helm last-looks 2026-09-04 (PR #292 / #293 era); “re-run, do not fix product” | `EqlWikiMobsTests.NoMoreThanTwoFetchesAreEverInFlight` | `ci/build-and-test` | **open** — observed-rerun only. Wiki concurrency; do not invent a product fix from one red. |
| `AForeignWriteBetweenLoadAndSaveIsReported` | Helm last-looks 2026-09-04 (PR #292); Mobile sounds #264 sign-off | `SettingsClobberTests.AForeignWriteBetweenLoadAndSaveIsReported` | `ci/build-and-test` | **open** — observed-rerun only. Timing around a file that another writer touched. |
| `SessionGoesLive` / `kills to reach N; last seen N+1` | E2E four rounds ~2026-09-04; runs `33924866579`, `33925423795`; trap 56 | `EndToEndTests` session ingest (`SessionGoesLive`, `KillThenLoot`) | `ci/e2e-windows` | **guarded** — two-moment dump. `PaintOneMoment` / `surfacesBehind` as assert / `tick` abort. A new equality sail-past is a **new** row, not this one closed-by-rerun. |
| `no visible window matching '…'` mid-batch, passes alone | #306 three runs (`shell-gear-narrow`, `options-window`, `drops-window`); trap 61 | `scripts/shoot.ps1` row varies | local Windows / second seat | **guarded** — screen lock in `shoot.ps1` and `AppHarness.Launch`; `ScreenLockTests`. A new random-row fail with the lock held is a **new** row. |
| `MapCircleMenuConfirmsThenRemovesTheSpawnPoint` thread-affinity cleanup | Helm 2026-09-04 (PR #277 era) | Avalonia ZoneWindowsRenderTests.MapCircleMenuConfirmsThenRemovesTheSpawnPoint (suite deleted with the lane; un-backticked so `DocumentationTests` can name it) | `ci` Avalonia Linux (deleted) | **lane-gone** — E-2c removed the Avalonia suite. Shape (shared session + parallel collections) is trap 57; do not re-learn it on a new shared-state project. |
| ThemeBodyCap / `ThemeBodyCap` flake on docs-only head | Helm 2026-09-04 (PR #276) | Avalonia theme-body cap render | `ci` Avalonia | **lane-gone** — same deletion. Do not treat a docs-only red as a docs defect. |
| `WorldWindow` one-off during docs-only last-look | PR #328 (trap 60 filing); Helm: “one WorldWindow flake; docs-only — do not expand” | E2E / shoot World host | `ci/e2e-windows` | **open** — observed-rerun. Docs-only tip; do not expand product. |
| Tooltip clock death (`EQBuddy stopped updating but I can still use it`) | #354 / #359 / #360; minidump `freeze-tick3` run `34075983046`; trap 63 | `ToolTipTimerTests` (E2E), `ToolTipPolicyTests` | player Windows; `ci/e2e-windows` | **guarded** — `ToolTipPolicy` 30 s + overflow arithmetic; prove-failed at zero ticks. Intermittent because the overflowed due time only wins when another timer is already late. |
| `TheShellAndTheWorldWindowAgreeAboutTheSameRoom` — `spawnsRows` vs `shellWorldSpawnsRows`, `Expected: 4 / Actual: 13` | 2026-09-09 PR #472, run `34371064898` (tip `9dff4997`). Same branch one commit earlier (`10555c87`, run `34370862597`) passed. | `ShellHostTests.TheShellAndTheWorldWindowAgreeAboutTheSameRoom` (line 353) | `ci/e2e-windows` | **open** — observed-rerun. **The tip that failed was channel-markdown-only** (`DECISIONS.md` / `FABLE-FEEDBACK.md` / `HELM-FEEDBACK.md`); it cannot change a spawn-row count, so this is the harness, not the product. Two hosts read the Camps room at different moments and the seeded spawns are still arriving — the trap 56 shape, one surface further out. Possibly the same mode as the docs-only `WorldWindow` row above; filed separately because that one names no test and this one does. **Do not expand product from this red.** |
| `EveryLandedRoomIsReachableByItsOwnAddress` — `shellRail`, `Expected: 7 / Actual: -1` | 2026-09-09 `main` at `511d221c` (the PR #472 merge), run `34373888681`. The NEXT commit on main, `4ec7c26c`, CONTAINS `511d221c` and its `e2e-windows` was green. | `ShellHostTests.EveryLandedRoomIsReachableByItsOwnAddress(address: "world:drops", key: "shellWorldTab", room: "drops")` (line 317) | `ci/e2e-windows` | **open** — observed-rerun. **-1 is `AppHarness.DumpValue`'s missing-key sentinel, so the rail count was not WRONG, it was ABSENT from that read** — and a wrong rail count would have failed all 21 addresses, not the one. The test waits on `shellWorldTab` and then reads `shellRail` in a SECOND `DumpValue`: two reads, two moments, the trap 56 shape the harness already owns tools for (`DumpValues` for one read, `WaitForDump` for a value that may not have landed). Candidate fix NAMED, not applied — the E2E lane needs an interactive Windows session and the screen lock (trap 61), so this seat filed rather than guessed at a harness edit it could not run. **Do not expand product from this red**; P1b touched no shell room. |

| `TheShellAndTheCreatureWindowAgreeAboutTheDropsTheyBothShow` — mid-suite only; **assert text NOT captured** (this seat's grep filtered it before it was read, which is the mistake to avoid next time) | 2026-09-09, local full `e2e-windows` run on `claude/opus-runes-and-next-card-20260909` (DRA-44 + DRA-36), 331 tests, this the only red at 6 m 39 s. Re-run ALONE immediately after: passed in 1 s. | `ShellHostTests.TheShellAndTheCreatureWindowAgreeAboutTheDropsTheyBothShow` | local Windows full suite | **open** — observed-rerun, and the rerun is an observation. Same family as the `no visible window matching` and shared-dump rows above: two hosts reading one dump at different moments (trap 56), passing alone and failing in company. **Named as a flake in `HELM-FEEDBACK.md` 2026-09-07 (#410 diagnose, `Expected -1 / Actual 13`) but never given a row until now** — which is why a second seat had to re-derive it. The diff under it touches the Quests tab, not Drops or the creature window; it does add a per-refresh read of the guide ledger to `QuestsView.ChecklistTickSignature`, so a timing nudge cannot be ruled out from one red and is **not** claimed to be ruled out. **Do not expand product from this red.** |

| `The active test run was aborted. Reason: Test host process crashed` — **no test named**, 83 of 336 passed then the host died at 2 m 45 s | 2026-09-11, local full `e2e-windows` on `claude/opus-dra45-n1` (DRA-45). The invocation that crashed was the one that also BUILT (`dotnet test` without `--no-build`); two later `--no-build` runs on the same tree passed 336/336 at 6 m 56 s and 6 m 52 s. | unknown — the abort names no test, and the 83 that had passed are not a prefix anything can grep | local Windows full suite | **open** — observed-rerun. **The one thing that distinguishes the bad run is that MSBuild was copying `EQBuddy.exe` / `EQBuddy.UI.Shared.dll` into the same `bin` the harness launches from**, which is trap 64's neighbour: the E2E project does not reference the app it launches, so a build and a launch can race over one file. Reproduced the file-lock half accidentally later the same session (`MSB3027 … locked by: EQBuddy (38512)`) by building while a run was live, which is evidence for the mechanism and not proof of this red. **Candidate disposition, not applied: `dotnet test` for this project should require `--no-build`, or `AppHarness` should assert the exe's write time is older than the run.** Not applied because it is a harness change this seat could not prove-fail without a second crash. **Do not expand product from this red** — DRA-45 adds no code the app runs before the Quests surface is opened. |
| One unnamed red, 340/341 — **test name NOT captured: this seat piped `dotnet test` through `tail -3`, the row-44 mistake repeated verbatim, four rows below the warning** | 2026-09-12, local full `e2e-windows` on `claude/fable-dra66-character-setup-20260911` (DRA-66), 7 m 1 s. Immediate full-suite rerun on the same binaries (`--no-build`, complete capture this time): 341/341 at 7 m 5 s. | unknown — nothing in the truncated output names it | local Windows full suite | **open** — observed-rerun, and the rerun is an observation. Context that narrows it without claiming it: the red run started minutes after `shoot.ps1`'s `finally` relaunched the REAL EQBuddy (installed copy, not `bin\Release`, so the screen-lock refusal does not apply to it) — a second live EQBuddy during the suite is a difference between the red run and the green one, named as a candidate and not a cause. The truncation lost even the ability to say whether the red was one of DRA-66's own touched tests — that cannot be ruled in or out, which is half of what the lost name cost; the four DRA-66-touched tests were also run as a targeted set before the full suite (4/4) and in the green rerun (341/341). **Process fix that needs no second red to prove: capture full `dotnet test` output to a file FIRST and filter the file** — this row exists because the ledger already said so once and a seat still lost the name. **Do not expand product from this red.** |

## What a row is not

- Not a work order. Soft does not take ledger rows as inbox items.
- Not a waiver. An **open** row does not let a PR skip `e2e-windows`.
- Not resolved by “it was green for me.” Add the occurrence; leave disposition.
