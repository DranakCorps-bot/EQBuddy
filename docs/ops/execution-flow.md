# Execution flow — how a slice gets from a signed plan onto `main`

DRA-73 plan §7 **M0**, adopted 2026-09-14. Two process cutovers, docs-only,
**reversible by a HOLD**. The compact live rules are in
[CLAUDE.md](../../CLAUDE.md) under *Helm → How a ruling lands, and what a
SIGN buys*; this file is the detail and the evidence behind them.

CI and the merge bar are **unchanged**. `build-and-test` + `e2e-windows`
remain the gates, and nothing here weakens
[verification-ladder.md](verification-ladder.md), the guard suite, or
David's release go.

---

## 1. The flow that was retired

```
Fable plan PR → LIVE ASK commit → webhook → Helm SIGN (HELM.md ruling)
→ helm/ssc-N branch + PR → Soft merges plan PR → Soft merges SSC PR
→ Helm AUTHORIZE next executor kick ("dra-N-dX after land")
→ claim-seat → executor works → executor PR + LIVE ASK commit → webhook
→ Helm SIGN (again) → second SSC PR → Soft merges both → next slice …
```

Per slice: **2 PRs, ≥2 Helm touches, 1 webhook wake, 1 explicit kick
authorization** — for implementing a slice a signed plan had already
declared.

## 2. The flow now

```
Fable plan PR → webhook → Helm SIGN (PR review and/or HELM.md commit)
  — the SIGN covers every slice the plan declares, in order
→ plan merges → executor claims the seat for D1 → PR → gates green → merge
→ executor takes D2 on that merge … through Dn
   (Helm wakes only for a departure, an outgrown boundary, a guard
    failure, a cross-lane conflict, or a consequence-list door)
```

Per slice: **1 PR, 0 scheduled Helm touches.**

## 3. Cutover 1 — no more `helm/ssc-N` PRs

**The rule.** A Helm ruling lands as a **GitHub PR review** on the thing it
rules on, and/or as a **direct commit to `HELM.md`** on `main`. There is no
branch and no pull request whose only cargo is the signature prose. Do not
open one, ask for one, wait on one, or carry "land the SSC" on a posture
list.

**Why the audit trail survives it.** The thing an SSC PR was buying was a
durable, signed, timestamped record of the ruling. A PR review *is* that
record — immutable, timestamped, attributed, indexed by GitHub, and
attached to the diff it judges rather than to a second branch that has to
be cross-referenced. `HELM.md` keeps prose rulings where prose is what is
wanted. Additions-only discipline on `HELM.md` is unchanged (see
[trap 60](claude-archive/traps.md#trap-60) — a channel file is shared state
another agent is writing while you write it).

**Migration.** `helm/ssc-*` branches and PRs already open on the remote
land or close on their own terms; this rule creates **no new ones**. A
reader hitting an old `helm/ssc-N` reference in `HELM.md`, `DECISIONS.md`
or the archive is reading history, not a live instruction — the same way
"both lanes" in a trap describes what a bug cost rather than what the repo
contains.

**What it cost to keep.** In the measured DRA-70/71/72 window (PRs
#580–#607, 2026-09-12 → 09-14), **12 of 24 PRs were `helm/ssc-N` PRs** —
half of all repository PR traffic was governance artifact. Each carried its
own merge wait on top of the product PR's.

## 4. Cutover 2 — a signed plan authorizes its whole slice sequence

**The rule.** When Helm SIGNs a plan, that signature authorizes **every
slice the plan declares, in the order it declares them, on green gates**.
Merging D(n) is what starts D(n+1). Nobody writes a LIVE ASK asking for
permission to begin a slice the plan already named, and Helm does not issue
per-slice "AUTHORIZE dra-N-dX after land".

**Helm stops the train with a HOLD, not by withholding authorization.** An
*objection* blocks; *absence of attention* no longer does. Holds live in
exactly one place — `HELM.md` — and a live hold naming this work binds
regardless of what any plan authorizes. That asymmetry is the whole safety
property: the stop signal is explicit and greppable, and silence is no
longer indistinguishable from a stop.

**Where the seam is.** The authorization is scoped to *what the plan
declared*. An executor that finds its slice exceeding the plan's declared
files, behaviour or boundary **stops and escalates** — that is the
exception path working, not a failure of it. So does a guard failure, a
cross-lane conflict, a repeated flake, a public reply's posture, or
anything on the consequence list. The webhook wake
(`helm-back-channel.yml`) is unchanged and is what these use.

**What it cost to keep.** In the same window, three Helm coverage gaps
produced merge stalls of 3.5–6.4 h that account for **15.3 of the 18.2
total wait-hours — 84%** — all of it planned work parked at the boundary of
Helm's availability. Governance Wait Ratio was 0.40–0.60. Meanwhile
**Helm's intervention rate was 100% and its pre-merge change rate was ~0%**:
every ruling in the window was KEEP/ACK, with zero pre-merge substantive
changes to an already-signed slice. The quality catches in that window came
from planning last-looks, executor surveys and the guard suite — not from
the pre-merge SIGN on a planned slice.

## 5. What is NOT changed by either cutover

- **CI is still the merge bar.** `build-and-test` + `e2e-windows`. A local
  green is not a CI green.
- **The guard suite is untouched.** `ArchitectureTests`,
  `RetiredCardsTests`, `GameCommandsTests`, the isolation tests, every trap
  guard.
- **David's consequence list is untouched**, including the release go. No
  plan authorizes a slice into it.
- **Holds are untouched.** `HELM.md` is still the one place; only Helm
  lifts; a hold still names who lifts it and when.
- **Scribe's posture rule is untouched.** A public reply beyond a routine
  signed thread reply still waits for Helm — only the route the signature
  arrives by changed.
- **`DECISIONS.md` is untouched as the reporting duty.** Approval by
  exception has always come with a logging duty; removing a signature step
  makes that log more load-bearing, not less.

## 6. How these get judged

Both are tagged ExO experiments per the DRA-73 plan §10.1, with the §6
metric that judges each:

| Experiment | Judged by | Baseline | Target after M2 |
|---|---|---|---|
| `ssc-retirement` | *PRs + Helm touches per slice*, net of *veto rate* and *rework rate* | 2.3 PRs/slice, ≥2 Helm touches/slice | ~1.05 PRs/slice, <0.3 touches/slice |
| `whole-sequence-auth` | *Governance Wait Ratio* and *Autonomous Correct Completion Rate*, net of *escaped defect rate* | GWR 0.40–0.60, ACCR 0% | GWR <0.15, ACCR >70% |

A benefit is always stated net of its quality cost. If T1 veto or rework
exceeds ~5%, the boundary tightens — the dashboard moves it, not opinion.
Rollback for either is one hold plus a revert of this document's rules; no
information is destroyed by adopting them.
