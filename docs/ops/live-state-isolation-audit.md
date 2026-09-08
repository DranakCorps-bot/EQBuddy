# Live-state isolation audit (E′)

**Date:** 2026-09-08.
**Scope:** remaining ways automated / agent-driven execution can touch owner or player AppData, logs, or settings.
**Out of scope:** re-litigating merged #428 `TestProfileIsolation`. David's live profiles are not restored or written.

The unit-suite host is already fail-closed (#428): it redirects to temp unless the exact string `EQBUDDY_ALLOW_LIVE_APPDATA=1` is set, and a run through that door fails `TestProfileIsolationTests` on purpose.

This audit is the **child-process and launcher** half. A host that is isolated can still launch `EQBuddy.exe` against a live profile.

---

## Path table

| Path | Touches live AppData / logs / settings? | Fail-closed how? | Gap after this change |
|---|---|---|---|
| **Unit suite** (`tests/EQBuddy.Tests`, `check.ps1`) | No, unless exact `EQBUDDY_ALLOW_LIVE_APPDATA=1` | #428 `TestProfileIsolation` — unconditional temp redirect; environment assertions | Residual: the named opt-out. A run through it is red, not silent. |
| **Soft / agent launch template** | Must not. Claimed to clear `EQBUDDY_APPDATA` and `EQBUDDY_ALLOW_LIVE_APPDATA` before `claude.exe` | Out of this repo (control-plane / seat launcher). #428 still holds if a seat inherits the Evolved export | This tree cannot prove the template. Residual: launcher lives elsewhere. |
| **E2E / `AppHarness`** | Child used to be overridable: caller env applied *after* `EQBUDDY_APPDATA = ProfileDir`. Host references Core and has no module initializer — `AppSettings.Save()` would write Evolved | `IsolatedLaunchPolicy.PinChildProfile` after the caller dictionary; refuses live `EQBUDDY_V1_APPDATA`. Source scan: E2E never calls `AppSettings.Save(` / `ProfileJson.Write(` | Residual: host `AppPaths.Dir` is still the product default if a future test writes some other Core API. Scan covers the two writers that destroyed #385/#428. |
| **`scripts/shoot.ps1`** | Fixture children already used a temp profile. Relaunch of the stood-down player app inherited *this* process's env — an Evolved export would redirect the restored widget | Temp profile + `Assert-EqIsolatedProfile`. Relaunch uses `UseShellExecute=false` and `Clear-EqHarnessProfileOverrides` | Residual: `-Force` still stands down a *player* app (intentional) and puts it back. |
| **`drag-verify` / `drag-check` / `mode-swap-verify`** | Children already set temp `EQBUDDY_APPDATA`. `drag-verify -Root` could be pointed at a live tree | `Assert-EqIsolatedProfile` before launch | Residual: hardcoded `$repo = C:\Users\david\source\EQBuddy` on two of them (not an AppData write). |
| **Local product launches** (`install-local.ps1 -Evolved`, `Launch-Evolved-Shell.cmd`) | **Yes, on purpose** — `%AppData%\EQBuddy Evolved`. v1 `%AppData%\EQBuddy` is untouched | Not an agent path. `-Evolved` refuses install / OneDrive / v1 profile. `setlocal` on the `.cmd` keeps the export inside that process | Residual: a shell that *dot-sources* the `.cmd` without `setlocal` (it has it) or copies the `set` line by hand still inherits Evolved AppData into later `dotnet test`. #428 is the backstop. |
| **`scripts/release.ps1`** | Evolved local-only: builds/signs, does not install, does not launch, does not copy to OneDrive | `evolved-channel-guard.ps1` + script refusals. No player profile write on the EvolvedLocal path | Residual: a future channel-open that runs the installer *will* start the product against `AppPaths` — that is a release, not an agent. Play Console stays OFF. |
| **CI** (`.github/workflows/ci.yml`) | Unit job: #428. E2E job: isolated child profile. No `EQBUDDY_APPDATA` export | Hosted runner has no owner profile | None on the runner. |
| **Knowledge refresh** | Writes harvest caches in-repo, not AppData | Curated catalogs are never auto-written | None. |
| **Wine / legacy** | Wine overlay writes *bottle registry knobs*, not `%AppData%\EQBuddy*`. Avalonia lane deleted (E-2c). `legacy-v1` is a different branch | Wine path is player-opt-in and inert on Windows | Residual: a CrossOver bottle is the player's, not this harness. No agent Wine launch remains in-tree. |
| **`EQBUDDY_ALLOW_LIVE_APPDATA=1`** | Yes — unit suite only | Exact string `"1"`; `"0"` / `"true"` / `" 1"` still redirect. Isolation tests fail on purpose | Residual: a person can still type it. Hard to invoke accidentally. |

---

## What was actually open

1. **E2E child env could win.** `AppHarness.Launch` set the isolated profile, then applied the caller dictionary. One key (`EQBUDDY_APPDATA` or `EQBUDDY_V1_APPDATA`) pointed the real exe at a player tree. Host isolation tests could not see it.
2. **`shoot.ps1` relaunch inherited the seat.** `Start-Process $path` with default `UseShellExecute=true` cannot strip env. An Evolved export on the seat redirected the restored widget.
3. **No shared refuse** for the four isolated-launch scripts. Each set a temp path by convention; nothing refused a live path if that convention slipped (`drag-verify -Root`).

#428 is unchanged. Product launches still write Evolved AppData — that is the owner's daily driver, not a gap.

---

## Guards added

- `UI.Shared/IsolatedLaunchPolicy` — pin child profile last; refuse live isolated path; refuse live v1 import source; clear harness overrides on relaunch. **No opt-in.**
- `Core/AppPaths.IsLivePlayerDirectory` — Evolved **and** v1, same comparison `IsProductOwned` already used.
- `scripts/isolated-profile.ps1` — PowerShell twin. `shoot.ps1` / drag / mode-swap dot-source it.
- `IsolatedLaunchPolicyTests` / `IsolatedLaunchScriptTests` — prove-fail the rule and the rendezvous.

---

## Residual risks (named)

1. **Soft/agent launcher is out of tree.** This repo cannot assert that `claude.exe` starts with the two variables cleared. #428 is the backstop if a seat inherits them anyway.
2. **E2E host `AppPaths.Dir` is not redirected.** A new Core write API used from E2E would miss the `Save`/`Write` scan. Do not add a second `TestProfileIsolation` here — that would be a redesign. Add the new writer to the scan instead.
3. **`EQBUDDY_ALLOW_LIVE_APPDATA=1` still exists** for the unit suite. Exact `"1"` only; the isolation test fails. Do not add a matching door to E2E or shoot.
4. **Product launches write Evolved AppData.** Intended. Force-killing a running portable copy mid-save is trap 65 (torn JSON) — `install-local.ps1` already closes gracefully first.
5. **No agent may restore David's live profiles.** Consequence-list #8. Unchanged.

Prove-fail (run 2026-09-08): delete the live-isolated-path check in `PinChildProfile` → `PinChildProfileRefusesALiveIsolatedPath` fails `Assert.Throws() Failure: No exception was thrown`. Mutation reverted. Restore `Start-Process $path` in `shoot.ps1` and `ShootRelaunchClearsHarnessOverridesRatherThanInheritingThem` goes red.
