# Soft seat claims — Experiment A′ (EQBuddy lab)

Verification implementation of operating-model experiment A′ (2026-09-08) **on
EQBuddy**. Enhancements are proposed in the Corps control-plane repo; this
tree is the lab that implements and measures them.

**Not a Corps standard.** A separate PR will land the formal proposal under
the control-plane `proposals/` tree. Nothing here graduates until we have
evidence from this machine.

One-seat mutex for Soft / Claude executors on this **machine** — every worktree
of this clone by store, every other registered clone by path registry since
DRA-102 (below). **Not** a scheduler, **not** a control plane, **not** a mailbox.

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
pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1" -Where
```

It prints the resolved path and how it was resolved. `shared by every worktree
of <root>` is the healthy answer. If it warns that the store is **private**,
git resolved no common dir and this copy genuinely cannot see another seat —
that, and only that, is the DRA-90 failure. A claim granted in that state says
so on the same screen.

## The store is shared. The SCRIPT is not. (DRA-107)

**Invoke both scripts resolved, through the clone's MAIN checkout** — never by a
bare relative path (Helm-signed 2026-09-16):

```powershell
pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1" -WorkItem DRA-28 -SeatId my-seat
pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/release-seat.ps1" -WorkItem DRA-28 -SeatId my-seat
```

A linked worktree resolves to the main tree's **store** (the section above, and
it is correct), but it carries its **own committed checkout of
`scripts/claim-seat.ps1`** — pinned at whatever commit that worktree sits on. So
a relative invocation from a worktree created before DRA-102 runs a copy with **no
registry code in it**: it reads one store, finds nothing, and grants the very
cross-clone duplicate DRA-102 closed.

Measured on one card in one second, from the same worktree:

| invocation | result |
|---|---|
| `scripts/claim-seat.ps1` (worktree's own copy) | `OK: DRA-98 is claimable` — **grants** |
| `"$(git rev-parse … --git-common-dir)/../scripts/claim-seat.ps1"` | `REFUSED … [in another CLONE: …]` |

**72 of 205** worktrees in the Bosun clone and **6 of 7** in the harness clone
carry that stale copy; one of the six is an agent workspace the dispatcher can
start a run in. `--git-common-dir` answers the clone's `.git` from a linked
worktree **and** from the main checkout alike, so **one invocation is right
everywhere** and nothing branches on where you are standing — it resolves the
SCRIPT the way DRA-90 already resolves the STORE, and it turns "keep 205 files
current" into "keep 2 files current", one per clone.

**The bare relative form still works and is DEMOTED, not removed** — correct from
a main checkout, wrong from a stale worktree, and unable to tell you which one you
are in. Needs **git ≥ 2.31** for `--path-format=absolute` (both clones: 2.54.0).

Code on `main` cannot repair a copy that will never receive it — which is why
this is a call-site rule rather than a guard inside the script. An in-script
forward guard is **deferred** to its own card, and it would be a guard against
the *next* mechanism change, not a fix for this exposure.

## Two independent CLONES: union-READ, local-WRITE (DRA-102)

A linked worktree shares the main tree's store. **Two independent clones share
nothing**, and on this machine the two clones ARE the two dispatch lanes — one
holds the `opus-*` / `fable-*` seats, the other the harness's `*-executor` ones.
DRA-87's two executors **both claimed**, 2m29s apart, and neither was refused
*or warned*, because neither store could see the other.

**Since DRA-102 (DRA-95 shape A′, Helm-signed 2026-09-16) the refusal crosses
clones.** The shape is a machine-level registry of store **paths**:

```
%LOCALAPPDATA%\DranakCorps\soft-seats\stores.json
```

- Each clone registers **its own resolved store path**, once, the first time it
  runs `claim-seat.ps1` or `release-seat.ps1` (`-Where` and `-List` count as
  first use — the diagnostic you are told to run is what puts you on the board).
- A claim reads **every registered store** and writes **only its own**. The
  refusal names the holder **and the clone it is in**, and the recovery command
  it prints runs `release-seat.ps1` **in that clone**, because this script has
  no business writing another clone's file.
- **Paths only.** No seat id, no card, no claim ever enters the registry. That
  is why this is A′ and not A (moving the claims themselves out of the tree),
  and it is what Helm's ruling rests on — `soft-seat-selftest.ps1` asserts the
  registry file contains none of those strings.
- An explicit `-StoreDir` **never registers and never union-reads**. It is a
  per-call store; putting a throwaway temp dir in front of every future refusal
  is the opposite of the point.

**It degrades, deliberately, to the pre-DRA-102 behaviour** when the registry is
absent, unreadable, or switched off with `EQBUDDY_SOFT_SEAT_REGISTRY=off`
(exactly that string). No new failure mode — **and a grant decided that way
SAYS so on the same screen**, because "consulted every clone and found nothing"
and "never looked" are the same sentence otherwise (trap 68). A registered store
that could not be read is named for the same reason (trap 81).

```powershell
pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1" -Where
```

now prints the registry, **every store consulted**, which one this call writes,
and any registered store whose directory has since vanished. (Run it resolved —
a stale worktree's own copy has no `-Where` at all and hard-errors with
`A parameter cannot be found that matches parameter name 'Where'`, which reads
like a broken diagnostic rather than a stale script. DRA-107.)

### A claim-rate measurement is only valid swept over EVERY registered store

`claim-seat.ps1 -List` is **this clone's board**, not the machine's. The
graduation numbers above — duplicates prevented, false blocks, stale claims —
are per-machine quantities, so **a count taken in one clone is not that number
and must not be reported as it**. Sweep every store `-Where` names (the run that
found the DRA-87 duplicate reported the other lane's seats as never claiming,
which is the same error one layer down). `-List` prints how many other stores it
did not show.

What is still **not** closed: a seat that never runs `claim-seat.ps1` at all is
refused by nobody, in any clone — the registry is a mutex for seats that take
it, not an obligation to take one. Two seats duplicated an authorized one-line
`CLAUDE.md` correction six minutes apart on 2026-09-16 with **neither having
claimed**, and no store design would have refused that. `gh pr list` /
`git ls-remote` for a branch naming the card before you push stays the habit
that crosses everything, including machines.

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

Every example below resolves the script through the clone's main checkout
(DRA-107). `$SEAT` is that path, bound once:

```powershell
$SEAT = "$(git rev-parse --path-format=absolute --git-common-dir)/.."
```

Before kicking a default seat:

```powershell
pwsh -NoProfile -File "$SEAT/scripts/claim-seat.ps1" -WorkItem DRA-28 -SeatId opus-isolation -Branch claude/opus-isolation-20260908 -Worktree .claude\worktrees\opus-isolation
```

A default claim on a `DRA-28` that any live seat holds exits `1`, names **every**
holder with its mode, and says which of them look stale. That is the whole
feature.

Explicit second seats (must be chosen, never the default):

```powershell
pwsh -NoProfile -File "$SEAT/scripts/claim-seat.ps1" -WorkItem DRA-28 -SeatId docs-ssc -Mode disjoint
pwsh -NoProfile -File "$SEAT/scripts/claim-seat.ps1" -WorkItem DRA-28 -SeatId challenger -Mode challenger
pwsh -NoProfile -File "$SEAT/scripts/claim-seat.ps1" -WorkItem DRA-28 -SeatId takeover -Mode replacement
```

When the seat is done, or to recover a dead holder (age ≥ 8 h, or a recorded
pid that is no longer running):

```powershell
pwsh -NoProfile -File "$SEAT/scripts/release-seat.ps1" -WorkItem DRA-28 -SeatId opus-isolation
pwsh -NoProfile -File "$SEAT/scripts/release-seat.ps1" -WorkItem DRA-28 -ForceStale
```

`-ExecutorPid` is the long-lived executor (`claude.exe`), not the claim script —
the script exits immediately, so defaulting to its pid would make every claim
look dead. Omit it if you do not have one. The JSON field is still `pid`.

`claim-seat.ps1 -List` prints live claims. `scripts/soft-seat-selftest.ps1`
proves the refuse (and is a `check.ps1` / CI stage).

The PROMPT-only launcher (`.claude/launch-templates/run-seat-PROMPT-only.cmd`)
runs the claim before anything else and never writes `HELM-FEEDBACK.md`.
