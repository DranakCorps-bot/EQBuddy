# Soft seat claims — Experiment A′ (EQBuddy lab)

Verification implementation of operating-model experiment A′ (2026-09-08) **on
EQBuddy**. Enhancements are proposed in the Corps control-plane repo; this
tree is the lab that implements and measures them.

**Not a Corps standard.** A separate PR will land the formal proposal under
the control-plane `proposals/` tree. Nothing here graduates until we have
evidence from this machine.

One-seat mutex for Soft / Claude executors on this clone. **Not** a
scheduler, **not** a control plane, **not** a mailbox.

`scheduled_tasks.lock` and `%TEMP%\eqbuddy-screen.lock` are different mutexes
(the latter is the SCREEN — CLAUDE.md trap 61). They do not claim a work item.

## Evidence we will gather

Keep a note (anywhere local; not this file, not a mailbox) when a kick hits
the store:

- **Duplicate starts prevented** — second default claim on the same work item
  refused, and named the holder.
- **Stale claims** — a holder that was gone, and whether age or a dead pid
  was what `-ForceStale` used.
- **False blocks** — a seat that should have started (disjoint / challenger /
  replacement, or a dead holder we could not recover) and was refused.
- **Recovery** — own-seat `release-seat.ps1` vs `-ForceStale` steal; did the
  next default claim then succeed.

That list is the graduation bar, not a promise that the mechanism is done.

## Why the live file is gitignored

Claims are machine-local. Tracking `claims.json` would turn every kick into a
git conflict — trap 60's mailbox rebase war, wearing a JSON hat. The template
below is what the file looks like; the live copy is created on first claim.

Live files (gitignored):

- `claims.json` — the store
- `claims.lock` / `claims.json.tmp` — write interlock

Every worktree of this clone shares the **main** tree's `.claude/soft-seats/`
(resolved via `git rev-parse --git-common-dir`). Do not copy the store into a
seat worktree.

## How Soft / Dranak calls it

Before kicking a default seat:

```bat
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem 428 -SeatId opus-isolation -Branch claude/opus-isolation-20260908 -Worktree .claude\worktrees\opus-isolation
```

A second default claim on `#428` exits `1` and names the holder. That is the
whole feature.

Explicit second seats (must be chosen, never the default):

```bat
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem 428 -SeatId docs-ssc -Mode disjoint
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem 428 -SeatId challenger -Mode challenger
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem 428 -SeatId takeover -Mode replacement
```

When the seat is done, or to recover a dead holder (age ≥ 8 h, or a recorded
pid that is no longer running):

```bat
pwsh -NoProfile -File scripts\release-seat.ps1 -WorkItem 428 -SeatId opus-isolation
pwsh -NoProfile -File scripts\release-seat.ps1 -WorkItem 428 -ForceStale
```

`-ExecutorPid` is the long-lived executor (`claude.exe`), not the claim script —
the script exits immediately, so defaulting to its pid would make every claim
look dead. Omit it if you do not have one. The JSON field is still `pid`.

`claim-seat.ps1 -List` prints live claims. `scripts/soft-seat-selftest.ps1`
proves the refuse (and is a `check.ps1` / CI stage).

The PROMPT-only launcher (`.claude/launch-templates/run-seat-PROMPT-only.cmd`)
runs the claim before anything else and never writes `HELM-FEEDBACK.md`.
