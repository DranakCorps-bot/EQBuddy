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

Every class below is run by a **seat on a Paperclip card**, dispatched the
ordinary way (assignment + heartbeat). **Install and both restart classes go to
Sr Executor only**: Cursor Executor refused this card on DRA-601 because an
install touches two of its banned surfaces (signing, live AppData) and a
restart touches live control-plane state. Script runs and merges may go to
either seat, unless the script touches a banned surface. There is no second
orchestrator and no kick script. The card must be attached to the
project whose repo it changes. A card with no project has no worktree to start
in, and the run dies at setup with `fatal: not a git repository` (DRA-601's own
first three runs, 2026-09-30).

## The four classes

| Class | Path | Proves it worked |
|---|---|---|
| **Install** | From a checkout of `main` (plus any PR still awaiting David's smoke, merged on a LOCAL branch that is never pushed): `pwsh -NoProfile -File scripts/install-local.ps1 -Evolved -Install` | The script prints `is INSTALLED and running`; the installed `EQBuddy.exe` ProductVersion carries the commit SHA; `Get-AuthenticodeSignature` is `Valid` with a timestamper; `EQBuddy.previous.exe` holds the build it replaced |
| **Restart — recovery** | The scheduled task `\Dranak - Ensure Paperclip` runs every 5 minutes and starts Paperclip only when it is down. On demand: `schtasks /run /tn "\Dranak - Ensure Paperclip"` (from `pwsh`, trap 27) | Task `Last Result` 0; `http://127.0.0.1:3100/api/health` and `http://100.118.30.124:3101/api/health` both 200; its log is `ensure-paperclip.log` under the Paperclip instance's `logs` folder |
| **Restart — deliberate** (to land a patch or a config change) | `paperclip-restart-request.ps1 -Mode Request -Card DRA-n` from `ops/paperclip-restart/` in the `dranakcorps-ops` repo (run under `powershell -ExecutionPolicy Bypass`, as its README shows), then the card goes `in_review` with an issue monitor in the same PATCH, so a **different** run verifies after the restart (`-Mode Status`). The README there is the procedure; the DRA-408 checklist (`dra408-restart-window-checklist.md` in agent-tools) stays the manual path | State `done` with `stopMethod: ctrl-c`; both health URLs 200; the new server's start time is later than the old one's; `paperclip-patches.ps1 -Verify` prints `missing=0`; the verifying run's `PAPERCLIP_API_URL` is `http://127.0.0.1:3100` |
| **Script runs** | Any script in this repo or in agent-tools, run by the seat on the card, in the form its own docs name. Drilled: `pwsh -NoProfile -File scripts/status.ps1` (repo); `powershell -NoProfile -ExecutionPolicy Bypass -File …\paperclip-patches.ps1 -Verify` (agent-tools, the form `dra408-restart-window-checklist.md` documents) | Exit code plus the script's own output, pasted on the card |
| **Merges** | A PR from the seat's branch; Reviewer sign-off is the merge review; `build-and-test` and `e2e-windows` are required checks with `enforce_admins` on; the seat merges (`gh pr merge`, or `--auto` once reviewed) | `gh pr view <n> --json state,mergeCommit,mergedAt` |

## Why a deliberate restart takes two runs

A seat that Paperclip dispatched **cannot restart Paperclip and survive it**:
stopping the server ends the run doing the stopping, so the "after" half of the
checklist (health, `-Verify`, the next run's env) has nobody to run it. The
request script therefore starts its executor DETACHED through WMI, outside the
Paperclip process tree, and the check after the restart is done by a later run
that the card's issue monitor wakes. The monitor is stored on the issue, so it
survives the restart. The executor waits for a window with no run under the
server and never stops a run to get one; if no window opens it ends in
`no-window` and restarts nothing.

This path was drilled once, on **DRA-619** (2026-09-30, request
`20260930T194010Z-DRA-619`, and the server was down for about 25 s). It was run
in a quiet window, so the guard against a run starting just before the stop has
been reviewed but not exercised.
