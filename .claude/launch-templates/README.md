# Seat launch templates

EQBuddy lab wiring for Experiment A′ — not a Corps-wide launcher standard.
Kick text belongs in a **seat-local** `PROMPT.txt`. Launchers must never
whole-file overwrite `HELM-FEEDBACK.md`, `HELM.md`, or any `*-FEEDBACK.md`
(Helm #428 / CLAUDE.md trap 60).

## Claim before kick (this experiment)

Soft max ≤3 is a process count, not a mutex. Two default seats on one work
item both used to start. On this machine, claim first so we can measure
whether the refuse holds:

```bat
for /f "delims=" %%G in ('git rev-parse --path-format=absolute --git-common-dir') do set "EQB_MAIN=%%G\.."
pwsh -NoProfile -File "%EQB_MAIN%\scripts\claim-seat.ps1" -WorkItem DRA-<n> -SeatId <seat> -Worktree .claude\worktrees\<seat>
if errorlevel 1 exit /b 1
```

**Resolve the script through the clone's MAIN checkout — do not invoke it by a
bare relative path** (DRA-107, Helm-signed 2026-09-16). A linked worktree shares
its clone's claim STORE (correct, DRA-90) but carries its **own checkout of
`claim-seat.ps1`**, so a relative invocation from a stale worktree runs a
pre-DRA-102 copy that consults no registry and **grants the cross-clone duplicate
DRA-102 closed**. 72 of 205 worktrees in the Bosun clone and 6 of 7 in the harness
clone are that stale copy — one of them an agent workspace the dispatcher can
start a run in, which is precisely this file's job. Needs git ≥ 2.31 for
`--path-format=absolute`. The bare relative form still works and is **demoted,
not removed**.

**The work item is the Paperclip card id, `DRA-<n>`, and only that.** A bare
GitHub issue number is refused: `#445` and `DRA-28` are two names for one
scope, and a claim under each collides with neither. Look the card up and
pass it — the launcher will not map a number onto a card for you.

`run-seat-PROMPT-only.cmd` does that, then stops at `PROMPT.txt`. It does not
scaffold a mailbox.

Run the **main checkout's** copy of it, for the same reason — the launcher
resolves the repo from its own location (`%~dp0..\..`), so a worktree's copy
points `claim-seat.ps1` back at that worktree:

```bat
for /f "delims=" %%G in ('git rev-parse --path-format=absolute --git-common-dir') do set "EQB_MAIN=%%G\.."
"%EQB_MAIN%\.claude\launch-templates\run-seat-PROMPT-only.cmd" DRA-28 opus-isolation
"%EQB_MAIN%\.claude\launch-templates\run-seat-PROMPT-only.cmd" DRA-28 docs-ssc disjoint
```

Release when the seat is done — resolved the same way:

```bat
pwsh -NoProfile -File "%EQB_MAIN%\scripts\release-seat.ps1" -WorkItem DRA-28 -SeatId <seat>
```

See `.claude/soft-seats/README.md`.
