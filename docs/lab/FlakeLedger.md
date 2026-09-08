# Flake ledger (lab)

**Lab experiment, 2026-09-08. Not a Corps-wide standard yet.**
Companion to [VerificationLadder.md](VerificationLadder.md).

A flake is a failure that is not explained by the change under test. This file
is the EQBuddy record. It exists so "it passed on rerun" stops being treated as
a close.

## Rules

1. **Signature first.** Name the *shape* (what disagreed, which wait, which
   collection), not only the test method. The same method can fail two ways.
2. **One row per signature.** Add an occurrence; do not open a second row
   because the run id is new.
3. **`Passed on rerun` is an observation, never a disposition.** Put it in
   Occurrences (`N failed, M passed on rerun`). Disposition stays `open` or
   `watched` until a cause is named.
4. **Environment is part of the identity.** `CI build-and-test`,
   `CI e2e-windows`, `local check.ps1`, `local E2E`, `shoot.ps1` are different
   machines. A local green does not retire a CI signature.
5. **Do not "fix" product code to silence a known flake** unless the cause is
   the product. Helm has signed that line on the wiki in-flight and
   SettingsClobber signatures more than once.
6. **Retired means the surface is gone**, not that we got bored of the row.
   Avalonia render-cleanup flakes retired with E-2c.

## Allowed dispositions

| Disposition | Means |
|---|---|
| `open` | Seen, cause not named, no guard. |
| `watched` | Cause or owner-signed handling is known (`re-run, do not fix product`). Still a flake. |
| `fixed` | Cause named and a guard holds it. The next occurrence of the same signature reopens the row. |
| `retired` | The suite, lane, or assertion that produced it no longer exists. |
| `accepted` | Named residual: we know why it can still blink and have chosen not to chase it further. Rare; needs a reason in Notes. |

`passed-on-rerun`, `resolved`, `flake`, and `ignore` are not dispositions.
`LabDocsTests` fails the ledger if a row uses them.

## Columns

| Column | What it holds |
|---|---|
| Signature | Short stable name for the shape. |
| Test | Class.method, or the shot / gate row. |
| Environment | Where it was observed. |
| Occurrences | Count + dates or run ids. Include "passed on rerun" here. |
| Disposition | One of the five words above. |
| Notes | Cause if known, Helm/Fable cite, the trap it became. |

## Ledger

Newest signatures stay at the top when a *new* shape arrives. Updates to an
existing row edit that row in place.

| Signature | Test | Environment | Occurrences | Disposition | Notes |
|---|---|---|---|---|---|
| wiki in-flight concurrency | `EqlWikiMobsTests.NoMoreThanTwoFetchesAreEverInFlight` | CI `build-and-test` (and the retired Avalonia lane) | Many, 2026-09-04 onward. Repeated "1/2955, passed on rerun." | watched | Helm: re-run, do not fix product. Network/concurrency bound; not the PR under test when it blinks on a docs head. |
| settings clobber collection | `SettingsClobberTests.AForeignWriteBetweenLoadAndSaveIsReported` | CI `build-and-test` | Several, 2026-09-04. Passed on rerun each time it blocked a signed docs PR. | watched | Same Helm handling. Adjacent work (`SettingsFileCollectionTests`, WatchPin migration) stayed in the PR that owned the collection, trap 30 — that is not this signature being "fixed". |
| shell vs World room dump | `TheShellAndTheWorldWindowAgreeAboutTheSameRoom` | CI `e2e-windows` | #328 and later docs-only heads; expected -1 vs 232/233. Passed on rerun. | watched | Two hosts write one dump namespace if unprefixed (trap 58). Product flake on a docs diff: re-run, do not expand the PR. |
| WorldWindow shot / E2E title | `scripts/shoot.ps1` World rows; occasional E2E WorldWindow | local `shoot.ps1`; CI `e2e-windows` | #328 one WorldWindow flake on a docs-only head. | watched | Trap 53 (stale shot title stops a batch) and trap 61 (wrong window / screen mutex) are the two shapes this has already been. Name which one before filing a new row. |
| E2E WaitForDump equality sail-past | `EndToEndTests.SessionGoesLive_AndFreshKillUpdatesLiveStats`; `KillThenLoot` | CI `e2e-windows` | Campaign 2026-09-04: runs `33924866579`, `33925423795` — "kills to reach 12; last seen 13". Mixed with the wiki in-flight signature. | fixed | Trap 56. One dump was two moments (widget totals vs satellite rows). `PaintOneMoment` + tick-after-snapshot. A new sail-past of a *different* key is a new signature. |
| GearCard / app-stopped-ticking | E2E tick-freeze class; player shape "EQBuddy stopped updating but I can still use it" | CI `e2e-windows`; live | #296, #343 pause-before-4th; four OE-tip re-run loops. Minidump `freeze-tick3` run `34075983046`. | fixed | Trap 63. WPF `ToolTipService.ShowDuration` default `int.MaxValue` overflowed the shared dispatcher timer. `ToolTipPolicy` / `ToolTipDefaults.ApplyOnce` / `ToolTipTimerTests`. Helm: do not expand #351/#352/#353 with a second freeze theory. |
| Avalonia headless thread affinity | `ZoneWindowsRenderTests.MapCircleMenuConfirmsThenRemovesTheSpawnPoint`; `ThemeBodyCapRenderTests.TheCapFollowsTheGripBothWaysRatherThanBeingSampledOnce` | CI `build-avalonia-linux` (deleted) | 2026-09-04 docs PRs; cleanup `InvalidOperationException`. Passed on rerun. | retired | Lane deleted in E-2c. Trap 57 was the parallel-collection half of the same family. Do not re-open; a new headless session on a new test project gets its own row and an assembly-level `DisableTestParallelization`. |
| screen mutex / random row | `shoot.ps1` batch; `tests/EQBuddy.E2E` | local desktop (two seats) | #306 batch died at a different shell shot each run; each row passed alone. | fixed | Trap 61. Both parties take `%TEMP%\eqbuddy-screen.lock`. A new failure that names a random shot while another harness is up is this signature reopening, not a defect in that shot. |

## How to add an occurrence

1. Grep this file for the test name **and** the assertion text.
2. If the signature matches, add the run id / date to Occurrences. Leave
   Disposition alone unless you are naming a cause.
3. If it is a new shape, add a row at the top with `open`.
4. Do not delete a `fixed` or `retired` row. The next agent will otherwise
   rediscover it and call it new.
