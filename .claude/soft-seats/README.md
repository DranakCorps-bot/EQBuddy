# Soft seat claims — Experiment A′ (EQBuddy lab)

Verification implementation of operating-model experiment A′ (2026-09-08) **on
EQBuddy**. Enhancements are proposed in the Corps control-plane repo; this
tree is the lab that implements and measures them.

**Not a Corps standard.** A separate PR will land the formal proposal under
the control-plane `proposals/` tree. Nothing here graduates until we have
evidence from this machine.

One-seat mutex for Soft / Claude executors on this clone. **Not** a
scheduler, **not** a control plane, **not** a mailbox.

**Since DRA-76 (2026-09-14) it is a refusing per-work-item mutex.** A default
claim is refused by **any** live seat on that card — `active`, `challenger`,
`disjoint` or `replacement`. Only an `abandoned` claim releases the item. Until
then it refused only against an *exclusive* holder (`active`/`replacement`),
which meant a default executor could start beside a live challenger or disjoint
seat and neither was told: two executors, one card. That is what PRs #566/#568
cost — one full run and two rulings.

`scheduled_tasks.lock` and `%TEMP%\eqbuddy-screen.lock` are different mutexes
(the latter is the SCREEN — CLAUDE.md trap 61). They do not claim a work item.

## Evidence we will gather

Keep a note (anywhere local; not this file, not a mailbox) when a kick hits
the store:

- **Duplicate starts prevented** — default claim on a held work item refused,
  and named the holders. Say which MODE held it: a refusal against a
  `challenger` or `disjoint` seat is one the pre-DRA-76 mutex would have let
  through, and those are the rows that measure the graduation.
- **Stale claims** — a holder that was gone, and whether age or a dead pid
  was what `-ForceStale` used.
- **False blocks** — a seat that should have started and was refused. **DRA-76
  widened this surface on purpose**, so it is the row to watch: nothing expires
  a claim, so a card whose seat simply ended without releasing stays held until
  someone runs `-ForceStale`. If false blocks outnumber prevented duplicates,
  the answer is an expiry or a release-on-exit, not a narrower refusal.
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

**`claims.json` is ABSENT in a fresh worktree, and that is correct** (DRA-90,
2026-09-15). `README.md` and `claims.template.json` are committed, so they
appear in every working copy; the live store is gitignored and lives in the
main tree only. Listing a worktree's `.claude/soft-seats/` therefore shows a
directory with no store in it — which reads exactly like "the store is
per-working-copy and the mutex is blind", and was reported as that. It is not:
a linked worktree resolves to the main tree's file, and a default claim in one
is refused by a holder in the other (proved both directions, with a real
worktree, in `scripts/soft-seat-selftest.ps1`).

Do not infer the store from a directory listing. **Ask:**

```powershell
pwsh -NoProfile -File scripts\claim-seat.ps1 -Where
```

It prints the resolved path and how it was resolved. `shared by every worktree
of <root>` is the healthy answer. If it warns that the store is **private**,
git resolved no common dir and this copy genuinely cannot see another seat —
that, and only that, is the DRA-90 failure. A claim granted in that state says
so on the same screen.

Two independent **clones** still do not share a store. Nothing here closes
that; the remote is the only thing both can see (`gh pr list` / `git ls-remote`
for a branch naming the card) and checking it before you push is a habit, not
a guard.

## The claim key is the Paperclip card, and only that

`-WorkItem` must be `DRA-<n>` (EXO-HARDEN-A2 / DRA-50, 2026-09-10). Anything
else is refused with the reason.

A mutex only refuses a second seat that spells the key the **same way**, and
one scope here carries two names — GitHub `#445` IS Paperclip `DRA-28`. Two
seats, one claiming `445` and one claiming `DRA-28`, held one scope and
collided with neither. That is trap 70 wearing a naming hat: the store was
doing exactly what it was asked, over a key that was never canonical.

- A bare issue number (`445`, `#445`) → **refused**, naming the DRA form.
- A free-text id (`options-ia-impl`) → **refused**.
- `dra-28` is the same key as `DRA-28`; the store canonicalises the case.
- **No auto-map.** Guessing which tracker the caller meant produces a claim
  key that silently misses the holder — the same bug, being helpful.
- `-PaperclipIssue` may only restate `-WorkItem`. It is an opt-in to writing
  the card to `in_progress`, not a second name for the work.

`release-seat.ps1` stays permissive on purpose: claims recorded before this
change still carry bare numbers and have to remain releasable.

## How Soft / Dranak calls it

Before kicking a default seat:

```bat
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem DRA-28 -SeatId opus-isolation -Branch claude/opus-isolation-20260908 -Worktree .claude\worktrees\opus-isolation
```

A default claim on a `DRA-28` that any live seat holds exits `1`, names **every**
holder with its mode, and says which of them look stale. That is the whole
feature.

Explicit second seats (must be chosen, never the default):

```bat
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem DRA-28 -SeatId docs-ssc -Mode disjoint
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem DRA-28 -SeatId challenger -Mode challenger
pwsh -NoProfile -File scripts\claim-seat.ps1 -WorkItem DRA-28 -SeatId takeover -Mode replacement
```

When the seat is done, or to recover a dead holder (age ≥ 8 h, or a recorded
pid that is no longer running):

```bat
pwsh -NoProfile -File scripts\release-seat.ps1 -WorkItem DRA-28 -SeatId opus-isolation
pwsh -NoProfile -File scripts\release-seat.ps1 -WorkItem DRA-28 -ForceStale
```

`-ExecutorPid` is the long-lived executor (`claude.exe`), not the claim script —
the script exits immediately, so defaulting to its pid would make every claim
look dead. Omit it if you do not have one. The JSON field is still `pid`.

`claim-seat.ps1 -List` prints live claims. `scripts/soft-seat-selftest.ps1`
proves the refuse (and is a `check.ps1` / CI stage).

The PROMPT-only launcher (`.claude/launch-templates/run-seat-PROMPT-only.cmd`)
runs the claim before anything else and never writes `HELM-FEEDBACK.md`.
