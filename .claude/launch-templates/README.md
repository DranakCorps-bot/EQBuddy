# Seat launch templates

Kick text belongs in a **seat-local** `PROMPT.txt`. Launchers must never
whole-file overwrite `HELM-FEEDBACK.md`, `HELM.md`, or any `*-FEEDBACK.md`
(Helm #428 / CLAUDE.md trap 60).

## Claim before kick

Soft max ≤3 is a process count, not a mutex. Two default seats on one work
item both used to start. Claim first:

```bat
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem <issue-or-id> -SeatId <seat> -Worktree .claude\worktrees\<seat>
if errorlevel 1 exit /b 1
```

`run-seat-PROMPT-only.cmd` does that, then stops at `PROMPT.txt`. It does not
scaffold a mailbox.

```bat
.claude\launch-templates\run-seat-PROMPT-only.cmd 428 opus-isolation
.claude\launch-templates\run-seat-PROMPT-only.cmd 428 docs-ssc disjoint
```

Release when the seat is done (`scripts\release-seat.ps1`). See
`.claude/soft-seats/README.md`.
