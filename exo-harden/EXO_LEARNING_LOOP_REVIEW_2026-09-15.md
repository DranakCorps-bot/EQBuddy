# EXO-HARDEN — Founder commission: organizational learning → standards → inheritance

**Source:** Founder (David) 2026-09-15 ~8:42 PM CT — ChatGPT proposal to challenge, not rubber-stamp.  
**Seat:** Paperclip Planner (Fable / claude-fable-5 CLI) — plan/review only. Soft LEAVE Opus implement until Helm SIGN.  
**Epic:** EXO-HARDEN.

You are acting as the Dranak Corps ExO Planner (Fable). Review the current Dranak Corps operating model, with particular attention to whether the organization is actually learning from EQBuddy and converting those lessons into reusable standards that future Dranak Corps projects inherit.

Do not assume the recommendations below are correct. Your job is to review, challenge, refine, reject, or incorporate them based on evidence from the current repositories and operating model.

## Core Intent

EQBuddy is intentionally serving as the low-consequence operating laboratory for Dranak Corps.

Desired organizational learning loop:

EQBuddy discovers → EQBuddy proves → Dranak Corps learns → corporate standard changes → future projects inherit

The durable Dranak Corps moat should not be a particular model, tool, or collection of agents. Models are interchangeable.

The value should increasingly come from:

- clearly defined roles and authority
- effective agent-to-agent handoffs
- evidence-driven decision making
- organizational learning
- recursive improvement
- consequence-based governance
- Founder remaining above routine implementation
- proven practices automatically influencing how future products/projects operate

**Question:** Is Dranak Corps currently closing that loop, or are we good at learning inside EQBuddy without yet reliably institutionalizing those lessons for the next project?

## Background Feedback to Review

A recent independent review concluded that the current ExO model has made substantial progress and that the recent governance/throughput changes are real rather than cosmetic.

Areas judged strong included:

- approval-by-exception replacing routine Founder approval
- Paperclip becoming the durable task/work ledger
- clearer separation between Planner, Helm, and execution agents
- simple one-seat/work-item ownership experiments instead of building a large scheduler/control plane
- consequence-sized verification
- verification ladder + flake ledger
- nightly organizational review
- actual failure → observation → correction → codification → re-verification cycles
- deliberate avoidance of a large “Dranak OS” bureaucracy
- continued treatment of EQBuddy as the operating lab

The strongest evidence cited was the recent midnight Planner workflow, where the first live run failed because of an incorrect Hermes script location, the failure was detected by the backstop, diagnosed, corrected, codified into an executable rule, and then verified successfully on subsequent nights.

However, the review identified a potential weakness: the learning exists, but the promotion/inheritance mechanism may still be incomplete.

The current operating-model documentation already defines the lifecycle:

Experiment → Observed → Proven → Standard → Retired

Yet several practices that now have meaningful field evidence still appear to remain classified as Experiment, including:

- simple one-seat work-item ownership
- live-state/profile isolation work
- verification ladder / flake discipline
- live-context vs incident/archive separation

That creates a risk that the organization learns operationally but does not formally recognize when a lesson has matured enough to become part of the Dranak Corps operating baseline.

## Primary Issue to Challenge

The key concern is not that EQBuddy lacks learning loops. It clearly has them.

The concern is: **How does a proven EQBuddy lesson reliably become something that Flossworks, PuzzleWorks, or some future Project X starts with automatically?**

Proposed conceptual architecture: **LAB → EVIDENCE → STANDARD → BOOTSTRAP**

- **LAB:** A project such as EQBuddy tests an approach against real work.
- **EVIDENCE:** Failures, successes, friction, verification results, retrospectives, and operational results.
- **STANDARD:** A cross-project organizational principle or operating practice is promoted into Dranak Corps' proven operating model.
- **BOOTSTRAP:** New projects inherit those proven standards by default instead of rediscovering them independently.

## Important Distinction

Do not automatically standardize implementation details from EQBuddy. Prefer portable organizational principles and contracts over accidental implementation details.

Bad: Every project must copy EQBuddy's claim-seat.ps1.  
Better: A work item should normally have one active executor; parallel execution requires an explicit reason; ownership must be observable.

Bad: Every repository must have EQBuddy's exact verification scripts.  
Better: Verification effort should scale with consequence; CI/main remains authoritative; new executable guards should demonstrate that they fail when the prohibited condition is introduced.

## Specific Areas to Review

Inspect the current Dranak Corps operations repository and relevant EQBuddy operating artifacts. At minimum assess:

- CURRENT_ORG.md
- DRANAK_CONSTITUTION.md
- 90_DAY_PILOT.md
- purpose/OPS_IMPROVEMENT_2026-09-08.md
- Paperclip Phase 0 / every-task operating documents
- nightly Planner / organizational CI process
- retrospectives
- current root README / operating documentation
- EQBuddy: verification ladder, flake ledger, claim-seat / ownership experiment, Fable/Helm feedback artifacts, incident/archive/context management, recent operational failures and corrections

Use actual evidence. Do not infer maturity merely because a process is documented.

## Questions to Answer

1. Is the learning loop genuinely working? Assess Experiment → Observed → Proven → Standard → inherited by future projects. Where strong / where lost / where documentation mistaken for learning?

2. Are experiment statuses current? For each active experiment recommend Experiment / Observed / Proven / Standard / Retired / Parked with field evidence (or missing evidence).

3. Do we need a formal Dranak Corps Standards registry (e.g. DRANAK_STANDARDS.md)? Challenge necessity; prefer existing docs if cleaner.

4. Do we need a New Project Baseline / PROJECT_BOOTSTRAP.md? Challenge the candidate inherited principles list; keep small.

5. How should promotion work? Lightest credible Experiment → Observed → Proven → Standard mechanism. Keep approval-by-exception. Who proposes, what evidence, Helm SIGN?, Founder only for consequence/governance boundary, how retire, avoid standard creep.

6. Should nightly/periodic learning explicitly ask about standards promotion and retirement? Prefer folding into existing nightly Planner review.

7. Is documentation drift undermining the learning model (e.g. root README still teaching Founder plan-approval / Founder-as-courier while Constitution/CURRENT_ORG moved on)? Smallest fix for authoritative truth.

## Design Constraint: Do Not Build Dranak OS

Do not invent a control-plane service, standards database, policy engine, another permanent agent, large governance workflow, dashboard, elaborate telemetry, new scheduler, or mandatory ceremonies unless evidence demands it.

Use GitHub + Paperclip + Planner + Helm. Default: smallest executable mechanism that removes demonstrated friction. Smaller/clearer OS is a success criterion.

## Desired End State

EQBuddy incident/friction/success → lesson → project rule/guard tested → evidence accumulates → Planner flags possible cross-project lesson → Helm evaluates / last-look → Dranak Corps Standard → new-project baseline inherits → Flossworks / PuzzleWorks / Project X start smarter → their evidence feeds back → standard improves/narrows/expands/retires.

## Deliverables

A. Candid assessment (governance, throughput, learning, evidence quality, recursive improvement, cross-project transfer, inheritance, complexity, Founder attention) with reasoning.

B. Challenge the proposed diagnosis — correct / overstated / understated / unnecessary / already solved / contradicted by evidence.

C. Experiment maturity review with evidence.

D. Minimum viable improvement to close EQBuddy learning → Standard → future inheritance (prefer modifying existing artifacts).

E. Proposed operating flow (observation → evidence → promotion → inheritance → future evidence) understandable without oral explanation.

F. Immediate actions — prioritized 3–7 for Helm/Planner; separate now / need more evidence / parked.

G. What not to do — attractive premature complexity.

## Final Decision Standard

Not: more documentation about organizational learning.  
Yes: a future Dranak Corps project demonstrably starts smarter because of evidence from prior projects.

If the current system already accomplishes that, prove it. If not, design the smallest change needed to make it true.

## Soft / Helm constraints for this card

- Soft LEAVE inventing Dranak OS / new permanent agents / dashboards.
- Soft LEAVE Opus product implement from this card.
- Soft LEAVE Pages / Play Console / tag / signing / Desktop from this card.
- LIVE ASK Helm when plan/assessment PR (or ops draft) is ready for SIGN.
- Ownership: Planner. Unclear → Helm → Founder (Founder LOCK ownership escalation).
