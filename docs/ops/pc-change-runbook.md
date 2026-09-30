# Changes on David's PC, on demand — the runbook (DRA-601)

David's bar, 2026-09-29 (DRA-529): before any Bosun routine is retired or Bosun
is parked, there must be a **working, TESTED** path to make changes on his PC
when he asks — install, restart, script runs, merges — that does not need Bosun.
This file names that path for each class. The drill evidence (command, output,
timestamp) is on **DRA-601**, not here; this file says what to run, the card
says what happened the last time it was run.

**Nothing is retired by this file.** Retiring a Bosun routine or parking Bosun
needs David's sign-off (DRA-529, DRA-601).

## Who runs it

Every class below is run by a **seat on a Paperclip card**: Sr Executor or
Cursor Executor, dispatched the ordinary way (assignment + heartbeat). There is
no second orchestrator and no kick script. The card must be attached to the
project whose repo it changes. A card with no project has no worktree to start
in, and the run dies at setup with `fatal: not a git repository` (DRA-601's own
first three runs, 2026-09-30).

## The four classes

| Class | Path | Proves it worked |
|---|---|---|
| **Install** | From a checkout of `main` (plus any PR still awaiting David's smoke, merged on a LOCAL branch that is never pushed): `pwsh -NoProfile -File scripts/install-local.ps1 -Evolved -Install` | The script prints `is INSTALLED and running`; the installed `EQBuddy.exe` ProductVersion carries the commit SHA; `Get-AuthenticodeSignature` is `Valid` with a timestamper; `EQBuddy.previous.exe` holds the build it replaced |
| **Restart — recovery** | The scheduled task `\Dranak - Ensure Paperclip` runs every 5 minutes and starts Paperclip only when it is down. On demand: `schtasks /run /tn "\Dranak - Ensure Paperclip"` (from `pwsh`, trap 27) | Task `Last Result` 0; `http://127.0.0.1:3100/api/health` and `http://100.118.30.124:3101/api/health` both 200; its log is `ensure-paperclip.log` under the Paperclip instance's `logs` folder |
| **Restart — deliberate** (to land a patch or a config change) | The DRA-408 restart-window checklist (`dra408-restart-window-checklist.md` in agent-tools) until DRA-529 T7 scripts it | See **the gap** below |
| **Script runs** | Any script in this repo or in agent-tools, run by the seat on the card, from `pwsh -NoProfile -File …`. Example drilled: `paperclip-patches.ps1 -Verify` (agent-tools), `scripts/status.ps1` (repo) | Exit code plus the script's own output, pasted on the card |
| **Merges** | A PR from the seat's branch; Reviewer sign-off is the merge review; `build-and-test` and `e2e-windows` are required checks with `enforce_admins` on; the seat merges (`gh pr merge`, or `--auto` once reviewed) | `gh pr view <n> --json state,mergeCommit,mergedAt` |

## The gap: a deliberate Paperclip restart

A seat that Paperclip dispatched **cannot restart Paperclip and survive it**:
stopping the server ends the run doing the stopping, so the "after" half of the
checklist (health, `-Verify`, the next run's env) has nobody to run it. Two
working pieces exist: `ensure-paperclip.ps1` starts the server DETACHED through
WMI, so it is outside any job a stop would kill, and the 5-minute task brings a
stopped server back on its own. Nobody has yet drilled a stop followed by that
recovery, run from a card. Until that drill passes, **the deliberate-restart
class does not count**, and Bosun routines that own it stay (DRA-601's fix
card).
