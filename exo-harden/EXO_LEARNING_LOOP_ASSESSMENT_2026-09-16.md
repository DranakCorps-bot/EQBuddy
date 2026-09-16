# EXO-HARDEN — Learning-loop assessment (DRA-98)

**Seat:** fable-dra98-learning-loop (Planner, claude-fable-5). Plan/review only.
**Date:** 2026-09-16. **Brief:** `EXO_LEARNING_LOOP_REVIEW_2026-09-15.md` (Founder commission).
**Verdict in one line:** the loop is three-quarters built and the review being challenged
missed the newest quarter — `EXO-PLAYBOOK.md` (approved 2026-09-14, the day before the
proposal) already implements LAB → EVIDENCE → STANDARD with more rigor than the proposal
asks for. The genuinely missing quarter is BOOTSTRAP, and the second real defect is that
the Corps' own ops repo runs below the lab's hygiene standard.

**Evidence base and its limits.** Read in full: `dranakcorps-ops` (fresh clone at
`cd176db`), `EXO-PLAYBOOK.md` (control-plane), EQBuddy `docs/ops/` (README,
verification-ladder, flake-ledger, exo-dashboard/baseline, execution-flow), `CLAUDE.md`
trap list, Constitution + amendments, 90-Day Pilot (targeted), retrospectives, the DRA-53
midnight-monitor intake doc, and root listings of five sibling repos. **Not verified from
this seat:** Paperclip card state and DRA-53 comment stream (no API access here) — the
monitor's liveness is attested only by ops PRs #7/#8 (nights 1–3, through 2026-09-13) and
its email rule. One claim below is flagged where that matters.

---

## A. Candid assessment

**Governance — strong, and measurably real.** Approval-by-exception is not prose: the
frozen baseline (`exo-dashboard.md`, PR #616) measured Helm intervention at 100% of slices
with ~0% pre-merge change at routine tiers, and 84% of total wait-hours parked at
authorization boundaries — and the org then *deleted the boundary* (DRA-74 SSC retirement,
DRA-74 whole-sequence auth) rather than writing about it. The five M0 slices ran
back-to-back same-day with zero SSC PRs. That is governance change driven by its own
measurement, which is the thing most orgs only claim.

**Throughput — improving against a number written down first.** GWR 0.49 frozen before the
cutover; targets (<0.15) set before the change; verdicts deferred to M2/M3 because "a
verdict written today would be vibes with a table around it." Correct discipline.

**Learning inside the lab — exceptional.** The 82-row trap list follows the stated
progression (incident → lesson → executable guard → compact rule) and the guards prove-fail.
The flake ledger is in genuine daily use: rows added through 2026-09-15, dispositions
honored, "passed on rerun is an observation" enforced against real temptation (the #632
red on a data-only diff was filed, not "fixed"). The ledger even records its own process
failures recurring ("the row-44 mistake repeated verbatim, four rows below the warning") —
that honesty is what makes the evidence trustworthy.

**Evidence quality — the differentiator.** `unmeasured ≠ 0` (trap 81), union-not-sum wait
accounting (a ratio that can exceed 1 measures its own double-count), net-of-quality-cost
results, frozen baselines. Doctrine entries cite file + section + PR window.

**Recursive improvement — real, with receipts.** The instrument's own defects became
doctrine (traps 74, 78, 80, 81); the midnight monitor's first live failure (Hermes script
path, 2026-09-11) was caught by its backstop, corrected, codified as an additions-only
rule, and verified live two nights running. The cited "strongest evidence" in the
commission is accurate.

**Cross-project transfer — weak. This is the real gap.** Today's inheritance is
point-in-time copy-paste: HomeFinances borrowed `FABLE.md` on 2026-08-27 (and repurposed
it as a Founder-door file — a reasonable adaptation that nothing records or feeds back);
FamilyCalendar carries its own model-roles CLAUDE.md with no Corps baseline; FWPuzzle has
nothing. There is no channel by which a correction to a practice reaches a project that
copied the practice earlier. The playbook's transferability criterion (§10.4) is the right
bar, but nothing yet *consumes* the entries.

**Complexity — well-resisted, with one growing exception.** No Dranak OS, no scheduler, no
dashboard beyond one generated markdown file. The exception: **document fragmentation.**
Three lifecycle vocabularies now coexist — Constitution Rule 5
(Observe→…→Standardize-or-Roll-Back), OPS_IMPROVEMENT (Experiment→Observed→Proven→
Standard→Retired), and the playbook (Experiment→Evidence→ADOPT/ADAPT/DROP) — and two
status stores, of which the older (OPS_IMPROVEMENT's table, frozen at 2026-09-08 with
everything "Experiment") is stale against the newer. This is EQBuddy's trap 4 ("one fact,
two producers") operating at the org level.

**Ops-repo hygiene — below the lab's own standard.** Three concrete findings:
1. `README.md` still teaches "David marks `approved`" (retired by Amendment 1.2,
   2026-08-22) and "Founder is the courier both ways" (retired by the Helm webhook).
   A new agent bootstrapping from the README inherits a three-week-dead model.
2. `purpose/EXO-AGENT-ROLES.md` — a Founder lock (2026-09-08) — exists **only as an
   uncommitted file on local disk**; it is in no commit on any branch of the remote.
3. `purpose/PORT_REGISTRY.md` exists in **two divergent copies** (committed vs. local),
   with different registries, both claiming Founder/Owner authority from the same day.
   The lab's rule is "holds live in exactly one place"; the Corps repo violates its
   sibling ("durable truth lives in the repo") twice over.

**Founder attention — the trend is right.** Courier role retired by the webhook; the
consequence list holds; the Aug retros measured waiting-on-David explicitly and the
follow-ups attacked it. The commission itself arriving as a challenge-this brief rather
than an instruction is the model working.

## B. Challenge to the proposed diagnosis

**Correct:** promotion/inheritance is the incomplete edge; experiment statuses are stale;
learning inside EQBuddy is real and not merely documented.

**Materially incomplete:** the review evaluated a snapshot that no longer existed.
`EXO-PLAYBOOK.md` (DRA-73 §10, approved by the Founder 2026-09-14) already implements
LAB → EVIDENCE → STANDARD: tagged experiments, a named judging metric at landing, evidence
net of quality cost from a frozen instrument, graduation verdicts at checkpoints, DROP
recorded with numbers so a failure is not retried blind, adoption preconditions, rollback
shapes, Helm pre-merge review, and a transferability criterion stricter than the
proposal's ("a second project must adopt from the entry alone, without excavating EQBuddy
history"). Three ADOPT entries exist with real evidence. **Adopting the proposal's
Experiment→Observed→Proven→Standard ladder verbatim would add a fourth vocabulary and
deepen the fragmentation it diagnoses.** Keep the playbook's lifecycle; retire the others
into it.

**Understated:** the proposal treats stale statuses as a recognition failure. The specific
mechanism gap is sharper: the playbook graduates experiments **at DRA-73 M-checkpoints**,
so the four practices that grew up *outside* DRA-73 (A′/E′/C′/Context, greenlit 2026-09-08)
have **no scheduled graduation moment at all**. They are stale because nothing owns asking
about them — a calendar hole, not a cultural one.

**Wrong in a detail worth correcting:** the commission credits a "nightly organizational
review" as an operating strength. The 22:00 nightly debrief produced two retro files
(Aug 25–26) and stopped; its *successor* — the DRA-53 midnight maturity monitor — is the
live mechanism (verified nights 1–3 through 2026-09-13; later nights unverified from this
seat). The Aug retros were excellent while they ran (they produced the "operating prose
only goes one way" finding that became doctrine entry 2/3), and their die-off unrecorded
in the ops repo is itself a small instance of the drift problem.

**One inversion:** the risk named is "documentation mistaken for learning." The evidence
shows mostly the opposite failure — *learning outran documentation* (statuses stale
because doing outpaced recording). Except in the ops repo itself, where the README is
documentation pretending the old model still runs.

## C. Experiment maturity review

| Experiment | Recommended status | Field evidence |
|---|---|---|
| **A′ one-seat claim** | **Principle: Proven. Implementation: keep Experiment** (playbook already schedules the seat-mutex verdict at M2/M3 — respect it) | Duplicates measured and then refused: #566/#568 collision → any-live-seat refusal (DRA-76); two-clone duplicate (DRA-87, both timestamps recorded) → cross-clone registry (DRA-102); stale-worktree script copy measured same-card-same-second (DRA-107, Helm-signed 2026-09-16). False-block count: 0. Three corrective iterations in eight days says the *mechanism* is still churning; the *principle* (one active executor; explicit-mode exceptions; observable ownership; the remote is the only check that crosses everything) has survived every iteration unchanged. |
| **E′ live-state isolation** | **Proven → promote as principle** | Unconditional redirect since 2026-09-07 (trap 68: "an inherited variable is not a decision"); one named door that fails the suite by design; child-process half (trap 69, `IsolatedLaunchPolicy`); standing audit doc. Both docs still carry "EQBuddy lab experiment, not a Corps-wide standard" — the label is now the only thing keeping it un-promoted. |
| **C′ verification ladder + flake ledger** | **Proven → strongest candidate for first Standard** | Continuous multi-seat field use through 2026-09-15; dispositions honored under temptation (two docs-only reds refused product "fixes"; a rerun-green explicitly not closed); ladder classes referenced in live rules and plans (V0–V3 routing). The portable principles: verify to consequence, CI stays authoritative, a flake gets named not re-run away, a new guard prove-fails. |
| **Context split (live manual vs archive)** | **Mechanism Proven; Standard only with the ratchet** | Split executed 2026-09-08; progression operating (traps 72–82 added post-split as compact rows with novels archived). Caveat the promotion must carry: the declared measure "live-file size" is moving the *wrong way* (CLAUDE.md's live tables have grown substantially since the split). The size ratchet is scheduled at M2 (doctrine entry 3, rule 2); promote the mechanism with the ratchet as an adoption precondition, or the standard ships with its own known failure mode. |
| **B cheaper-model pilot** | **Parked — correctly. Keep parked** | Only partial, informal evidence (Researcher=Qwen research-only in EXO-AGENT-ROLES — itself an uncommitted doc). No named task class, no quality bar. The bar in OPS_IMPROVEMENT stands. |
| **D one transient channel** | **Retire as framed — overtaken** | The candidate (HELM-FEEDBACK wakes) was solved differently: the webhook wake exists and channel cost was attacked by rotation (DRA-75) + the byte-budget finding (doctrine entry 2). Record as absorbed, with the pointer, so it is not retried blind. |

## D. Minimum viable improvement (prefer existing artifacts — all of this is edits, no new machinery)

1. **One home for doctrine.** Move `EXO-PLAYBOOK.md` from the control-plane repo to
   `dranakcorps-ops` root; leave a pointer stub. Rationale: Amendment 1.1 says
   organizational operating docs live only in ops; CURRENT_ORG says the control-plane is
   "courier only — do not turn it into" more; a 13 KB doc with zero code dependencies is
   a one-commit move. Helm T2 review applies (doctrine posture).
2. **One producer for status.** Replace OPS_IMPROVEMENT's status column with pointers to
   playbook entries; add one line to both files naming the playbook as the single status
   producer and its lifecycle as the single vocabulary (Constitution Rule 5 stays the
   *spirit*; the playbook is the *mechanism*). This is trap 4's remedy applied to the org.
3. **Prove the promotion path on a non-M0 practice.** Write the C′ playbook entry (and
   the E′ one) from the evidence in section C, through the normal PR + Helm SIGN route.
   This is the test case that the graduation mechanism works for practices that grew up
   outside DRA-73 — if the entry can't be written from EQBuddy's docs alone, §10.4 says
   the capture failed, and we learn that now.
4. **BOOTSTRAP, smallest form.** Add a "New-project baseline" section to the playbook
   (not a new file, not a template repo): a new Dranak Corps project (a) adopts every
   ADOPT-verdict entry as a default, (b) records in its own CLAUDE.md one manifest line —
   `Corps baseline: EXO-PLAYBOOK entries <list> as of <date>` — and (c) ADAPT entries are
   adopted per their preconditions or their skip is logged in the project's DECISIONS
   file. **The manifest line is the correction channel:** when a standard changes or
   retires, the affected projects are one grep away, and a stale inheritance is visible
   instead of silent. Existing projects are NOT mass-migrated — apply the baseline the
   next time a project is actively worked (see G).
5. **Fix the ops README + declare authority order.** Correct the two retired teachings
   (approval marks; Founder-as-courier); add one line: Constitution + amendments bind;
   CURRENT_ORG is the working picture; README is orientation only. Commit or reconcile
   the local-only Founder-locked docs (`EXO-AGENT-ROLES.md`; the two `PORT_REGISTRY.md`
   copies need a human call on which rows are true — flagged for Helm/Founder,
   recommended base: the committed copy, which carries the observed-live scan).
6. **One added question in the existing midnight protocol** (additions-only, per its own
   rule): *"Did any experiment cross a promotion or retirement threshold on tonight's
   evidence? If yes, propose the playbook entry (ops PR for Helm SIGN). 'No' is a valid
   answer."* This closes the calendar hole (B above) without a new ceremony, and the
   existing LEAVE invent-to-poke discipline guards against manufactured promotions.

## E. The operating flow (end to end, no oral tradition required)

1. **Observation** — an incident, friction, or success in a lab project produces its
   normal artifacts: trap row, flake-ledger row, DECISIONS line, retro finding.
2. **Evidence** — the practice that answers it lands tagged as an experiment naming its
   judging metric; the instrument (`exo-metrics.ps1` dashboard, ledgers, guard suites)
   accumulates results net of quality cost.
3. **Promotion** — at an M-checkpoint, or when the midnight monitor's promotion question
   fires, the Planner drafts a playbook entry (What was tested / Evidence / Benefit /
   ADOPT–ADAPT–DROP / Preconditions / Rollback). Helm SIGNs pre-merge. Founder is asked
   only when the entry touches the consequence list or governance boundary. No promotion
   from one success; no verdict without the metric.
4. **Inheritance** — a new project's first session applies the New-project baseline:
   adopt ADOPT entries, write the manifest line, log ADAPT skips. An existing project
   picks up the baseline the next time it is actively worked.
5. **Feedback** — the inheriting project's evidence flows back through the same
   channels; a standard that fails elsewhere gets its entry narrowed (ADAPT), or a DROP
   with the second project's numbers. Retirement is a verdict change, greppable to every
   manifest that cites the entry.

## F. Immediate actions, prioritized

**Now (this card / next Planner session):**
1. D.1 + D.2 + D.5 as one ops PR — doctrine home, single status producer, README truth.
   (Smallest set that removes the trap-4 fragmentation and the bootstrap-a-dead-model
   hazard.)
2. D.3 — the C′ and E′ playbook entries, as the promotion path's proof case.
3. D.6 — the one-line midnight-protocol addition.

**Needs more evidence (do not act yet):**
4. D.4's first *consumer* — write the baseline section now (it is a paragraph), but its
   proof is the next genuinely new project or next actively-worked sibling; do not
   retrofit five repos to manufacture the demonstration.
5. Seat-mutex and the M0 cutovers keep their scheduled M2/M3 verdicts — do not promote
   early on the strength of this review.

**Parked:**
6. B (cheaper-model pilot) stays parked per OPS_IMPROVEMENT's bar.
7. Any tooling that *enforces* the manifest line (a check that greps sibling repos) —
   attractive, but it is a guard aimed at a failure that has not happened yet; revisit
   after the first real inheritance.

## G. What not to do

- **No `DRANAK_STANDARDS.md`.** A second registry beside the playbook recreates the
  two-producer defect this review found. The playbook *is* the registry.
- **No fourth lifecycle vocabulary.** Adopt the playbook's; retire the others into it.
- **No template/scaffold repo, no project generator.** A scaffold is a point-in-time copy
  with better marketing — it goes stale exactly the way HomeFinances' FABLE.md did. The
  manifest line + entries-readable-alone is the anti-staleness mechanism.
- **No mass retrofit of sibling repos.** Five repos updated in one sweep produces five
  untested inheritances and no evidence; apply at next active work, one at a time.
- **No standards database, policy engine, compliance dashboard, or new permanent agent.**
  Nothing in the evidence demands one; the whole loop above runs on GitHub + Paperclip +
  the two existing scheduled processes.
- **No promotion quotas.** The midnight question must keep "no" as a valid answer, or it
  becomes invent-to-promote — the same failure the monitor's own LEAVE list already names
  for pokes.

---

## LIVE ASK — Helm

**To: Helm** (via `HELM-FEEDBACK.md` + webhook wake, per standing process — this file is
the seat-local record; the ask is not actioned from a launcher).

DRA-98 Planner assessment is ready for SIGN. Requested ruling, in order:

1. **SIGN / amend the assessment** (sections A–C) — in particular the challenge verdict
   that the playbook supersedes the proposal's registry+ladder, and the status
   recommendations in C.
2. **SIGN the now-actions** (F.1–F.3): the doctrine-home move + single-status-producer +
   README-truth ops PR; the C′/E′ playbook entries as the promotion proof case; the
   one-line midnight-protocol addition. All are ops-doc edits, reversible, no product
   code, no consequence-list doors touched. Executor seat (Opus) stays LEAVE until your
   SIGN per the card.
3. **Flag to Founder (consequence-adjacent, his call, not blocking 1–2):** the
   uncommitted Founder-locked `EXO-AGENT-ROLES.md` and the divergent `PORT_REGISTRY.md`
   copies — which copy is true, and whether local-only Founder locks should be treated
   as un-issued until committed.

— Fable (Planner, DRA-98)
