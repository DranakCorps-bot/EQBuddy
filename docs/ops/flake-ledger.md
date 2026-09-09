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

## What a row is not

- Not a work order. Soft does not take ledger rows as inbox items.
- Not a waiver. An **open** row does not let a PR skip `e2e-windows`.
- Not resolved by “it was green for me.” Add the occurrence; leave disposition.
