# Execution flow — how a slice gets from a signed plan onto `main`

DRA-73 plan §7 **M0**, adopted 2026-09-14. Two process cutovers, docs-only,
**reversible by a HOLD**. Since DRA-571 (2026-09-30, under DRA-563) the live
flow is the one in §2: state and holds are in
[HANDOFF.md](../../HANDOFF.md), signing a plan is Planner routing it (owner,
next seat, acceptance), Dranak decides posture, and Reviewer sign-off is the
merge review. The compact copy is in [CLAUDE.md](../../CLAUDE.md). This file
is the detail and the evidence. There is no Helm tip, SIGN, last-look or wake.

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

DRA-571, aligned with the CLAUDE.md wording in DRA-570. **Signing a plan is
Planner routing it** — owner, next seat, acceptance — once, for every slice
the plan declares. It is not a Helm stamp.

```
Fable plan PR → Planner signs it (owner, next seat, acceptance)
  — the signature covers every slice the plan declares, in order
→ plan merges → executor claims the seat for D1 → PR → gates green
→ Reviewer sign-off → merge
→ executor takes D2 on that merge … through Dn
   (escalate seat → Planner → Dranak for a departure, an outgrown
    boundary, a guard failure, a cross-lane conflict, or posture.
    A consequence-list door goes on to David through the board's
    Founder-ask flow. Dranak stops the train with a HOLD in HANDOFF.md.)
```

Per slice: **1 PR, and Reviewer sign-off on that PR.** No Helm tip, SIGN,
last-look or wake. A hold is a dated line in `HANDOFF.md`, which Dranak and
Planner read. Dranak lifts a hold, or the Founder where the consequence list
applies. Silence is not a hold.

## 3. Cutover 1 — no more `helm/ssc-N` PRs

**The rule.** A ruling lands as a **PR review**, a **card comment**, or a
**`HANDOFF.md` commit**. There is no branch and no pull request whose only
cargo is signature prose. Do not open a `helm/ssc-N` PR, ask for one, wait
on one, or carry "land the SSC" on a posture list. The 2026-09-14 mechanism
was a Helm PR review and/or a direct commit to `HELM.md`; that file is now a
pointer at `HANDOFF.md` (DRA-569). It is not a step.

**Why the audit trail survived dropping the SSC PR.** The thing an SSC PR
was buying was a durable, timestamped record of the ruling. A PR review *is*
that record — immutable, timestamped, attributed, indexed by GitHub, and
attached to the diff it judges rather than to a second branch that has to
be cross-referenced. Prose that has to stay findable lives in `HANDOFF.md`.
Additions-only discipline on the channel files is unchanged (see
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

**The rule.** When Planner signs a plan, that signature authorizes **every
slice the plan declares, in the order it declares them, on green gates**.
Merging D(n) is what starts D(n+1). Nobody writes an ask for permission to
begin a slice the plan already named, and nobody issues per-slice
"AUTHORIZE dra-N-dX after land".

**Dranak stops the train with a HOLD, not by withholding authorization.** An
*objection* blocks; *absence of attention* no longer does. Holds live in
exactly one place — `HANDOFF.md` — and a live hold naming this work binds
regardless of what any plan authorizes. Dranak lifts a hold, or the Founder
where the consequence list applies. That asymmetry is the whole safety
property: the stop signal is explicit and greppable, and silence is no
longer indistinguishable from a stop. Until 2026-09-30 the stop was a Helm
HOLD in `HELM.md`; the measured cost of that shape is the paragraph below,
kept as history.

**Where the seam is.** The authorization is scoped to *what the plan
declared*. An executor that finds its slice exceeding the plan's declared
files, behaviour or boundary **stops and escalates** — seat, then Planner,
then Dranak. That is the exception path working, not a failure of it. So
does a guard failure, a cross-lane conflict, a repeated flake, or a public
reply's posture. Anything on the consequence list goes on to David through
the board's Founder-ask flow. The `helm-back-channel.yml` webhook was how
the retired Helm path was woken. It is not a step in this flow.

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
  green is not a CI green. Both have been REQUIRED status checks on `main`
  since 2026-09-20 (DRA-228, with `enforce_admins` on); before that only
  `build-and-test` was, and the second green depended on whoever merged
  holding it up by hand.
- **The guard suite is untouched.** `ArchitectureTests`,
  `RetiredCardsTests`, `GameCommandsTests`, the isolation tests, every trap
  guard.
- **David's consequence list is untouched**, including the release go. No
  plan authorizes a slice into it.
- **Holds live in `HANDOFF.md`.** Dranak lifts one, or the Founder where the
  consequence list applies. A hold still names who lifts it and when. A
  shipped fix does not lift a hold. `HELM.md` is a pointer, not a second list.
- **Public-reply posture is Dranak's.** A routine signed thread reply is
  still the standing authority; a hold in `HANDOFF.md` outranks it. There is
  no Helm tip and no wake before a reply. A public reply beyond a routine
  signed thread reply is consequence-list work.
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

**The dashboard is [exo-dashboard.md](exo-dashboard.md)**, generated by
`scripts/exo-metrics.ps1`, and the baseline column above is now a *measured*
frozen reading rather than the estimate this document shipped with. The two
differ slightly and the measured one wins: 2.15 PRs per delivered slice (not
2.3) and 2.1 Helm touches per delivery slice, over PRs #580–#607. Governance
Wait Ratio measured 0.49 against the estimated 0.40–0.60 band, ACCR 0% as
estimated, and **exactly half** of the window's PR traffic was `helm/ssc-N`
carriers — the claim in §3 above, confirmed by the instrument rather than by
the count that motivated it.

Two numbers in §3 and §4 are the hand-measurement that argued for these
cutovers, and the dashboard computes their successors differently (it unions
overlapping waits instead of summing them, and it clamps a standing lane's lead
time to the window). Where they disagree, the dashboard's definition is the one
the M2 comparison must use, because it is the one that was frozen.

Since DRA-571 the live path has no Helm touch. `scripts/exo-metrics.ps1`
still counts ruling commits, with the cutover dated: `HELM.md` when the
window starts before 2026-10-01, `HANDOFF.md` when the window reaches
2026-09-30, and both when it crosses that day. The baseline window
(#580–#607) is entirely before the cutover, so its Helm-touch reading is
unchanged. "Helm touches" in the table above is that window's metric name,
not a step in §2.

---

## 7. The Jr/Sr executor lanes (DRA-179)

The capability-cost router decides **which executor takes a delivery a signed
plan has already authorized**. It sits BELOW CLAUDE.md's V0–V1 / V2–V3 table
and changes nothing about what reaches Fable at all. The rubric and the ten
banned-Jr surfaces are doctrine and live in the ops `EXO-PLAYBOOK.md`
(registered there as the `jr-sr-router` experiment, D1); the compact live rule
is the `route:` line in [CLAUDE.md](../../CLAUDE.md). **This section is the
mechanics** — the seat, the CLI-only rule, and the review gate (D2).

The model mix is the Founder's signature (2026-09-17) and nothing here
re-decides it: Planner = Fable; Jr = Qwen 3.8-27B on `route: routine`; Sr =
Opus 5.1 on `route: hard`, and on every Jr review.

### 7.1 Seat discipline

- **A Jr executor claims the card's seat like any other executor** — through
  the RESOLVED `scripts/claim-seat.ps1` form CLAUDE.md's *Commands* section
  documents (`git rev-parse --path-format=absolute --git-common-dir`), and it
  releases at close. A new lane is new call sites, which is exactly the
  population traps 70/82 and DRA-107 were measured on: the relative
  invocation runs whichever copy the worktree happens to carry.
- **The claim key is the card, `DRA-<n>`, and the lane is not part of it.**
  There is no `-jr` work item and no per-lane spelling: two lanes on one card
  is two seats on one card, and `claim-seat.ps1` refuses that for Jr exactly
  as it refuses it for Sr. A challenger or a disjoint slice still says so with
  `-Mode`, unchanged.
- **Sr's review is NOT a second claim.** Reviewing is not executing, and a
  reviewer that claimed the card would either be refused or have to declare a
  mode it is not in. So the gate block below is the reviewer's only artifact,
  and the number of seats a card carries is unchanged by the router.

### 7.2 CLI only, both lanes

Jr runs as a CLI, like every seat here. **No model API integration is built
for either lane, and no shipped EQBuddy code path calls Qwen or any model** —
this is a dev-time lane and it is invisible to the product. A change that adds
a model client, key, endpoint or dependency is therefore not a `routine`
delivery by construction: it is banned-list items 1 and 6, and it routes Sr
before anybody argues about the size of the diff.

### 7.3 The Jr review gate

**A Jr PR does not merge without an Sr review, and the enforcement mechanism
is a CHECKLIST** — picked 2026-09-17 (DRA-179; the tip is in the archived
`HELM.md`), against the alternative of GitHub branch protection. Two reasons
it is the right mechanism here rather than a weaker stand-in for the other
one: the org pushes through ONE bot identity, so *"require a review from
somebody else"* has nobody to name without inventing a second GitHub
identity — which both that ruling and the plan's non-goals LEAVE — and an
unread rejection teaches people to route around a gate instead of reading
it. **Nothing here
changes repository settings.**

The gate rides the PR BODY. A Jr PR carries this block; **Sr ticks the last
box in its review**, and that box is what the merge carrier reads:

```markdown
### Jr lane gate — DRA-179
- [ ] `route: routine` — the tag the signed plan wrote for this delivery
- [ ] Seat `<seat-id>` on `DRA-<n>`, claimed through the resolved `claim-seat.ps1` form
- [ ] No banned-Jr surface touched — contact would have stopped and escalated to Sr
- [ ] CLI only — no model API, key, endpoint or dependency; no shipped path calls a model
- [ ] `build-and-test` + `e2e-windows` green
- [ ] **Sr reviewed — Sr ticks this, never Jr** (Sr: `<who>`)
```

**The merge rule is one sentence: an unticked last box is a merge that does
not happen.** The first five are Jr's own statement about its own slice; the
sixth is the gate. A PR that arrives with all six already ticked is a PR whose
gate was not read — what is required is the review, not the tick.

**What a checklist buys, and what it does not.** It refuses nothing by
itself: an unticked box is *visible*, in the same place CI's red is, to the
carrier deciding to merge. It does not have branch protection's property of
being unskippable, so a skipped gate is an incident to report rather than an
impossibility, and D4's measurement rows are where a skipped one surfaces
afterwards. Saying that out loud is the point — a checklist described as
unskippable would be the lie, and the next person would stop checking.

**Why the gate is not a pull_request_template.md.** A template fills the
GitHub WEB form. Every PR in this repo is created by `gh pr create --body …`,
which renders no template, so a template would carry this gate on exactly the
PRs that do not need it and on none of the ones that do — trap 78's shape, a
check aimed at nothing that still reports clean. The block above is cited,
pasted and guarded (`DocumentationTests`) instead.

### 7.4 Escalation, and what is NOT landed here

- **Jr stops and hands the delivery to Sr** on banned-list contact found
  mid-slice, a gate failure it cannot explain, or a slice outgrowing its
  declared boundary. That is the stop-and-escalate seam every signed plan
  already rests on; an escalation is the lane working, not a failure of it.
- **Sr's review DEPTH — quick-pass versus deep — is D3**, and this section
  does not decide it. Until D3 lands, Sr reviews and says in the review what
  it did.
- **Measurement is D4.** No row is added to §6's table here: those baselines
  are DRA-73's frozen window, and a router row with no reading behind it would
  be a number nobody measured (trap 81).
