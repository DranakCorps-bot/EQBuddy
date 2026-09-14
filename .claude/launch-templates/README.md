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
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem DRA-<n> -SeatId <seat> -Worktree .claude\worktrees\<seat>
if errorlevel 1 exit /b 1
```

**The work item is the Paperclip card id, `DRA-<n>`, and only that.** A bare
GitHub issue number is refused: `#445` and `DRA-28` are two names for one
scope, and a claim under each collides with neither. Look the card up and
pass it — the launcher will not map a number onto a card for you.

`run-seat-PROMPT-only.cmd` does that, then stops at `PROMPT.txt`. It does not
scaffold a mailbox.

```bat
.claude\launch-templates\run-seat-PROMPT-only.cmd DRA-28 opus-isolation
.claude\launch-templates\run-seat-PROMPT-only.cmd DRA-28 docs-ssc disjoint
```

Release when the seat is done (`scripts\release-seat.ps1`). See
`.claude/soft-seats/README.md`.
