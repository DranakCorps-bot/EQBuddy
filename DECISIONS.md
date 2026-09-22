# Decisions

**How to read this** — one dated `##` block per decision: what was chosen, what it could have gone to instead, and why. **This file is not in date order** — read the ISO date in each heading, never the position.

**Where the archive is** — older blocks are rotated **verbatim** into [`docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md`](docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md). Nothing is ever deleted; an archived block never revives a hold and never commissions work.

**What never rotates** — an open ask, an unexpired PARK or HOLD, and a standing rule stay in this file at any age, however old they are.

**Last cut** — 2026-09-21 by DRA-294, under the Helm arm (c) discharge floor ruled in DRA-282 Q2 item 4 (tip PR #773, `9121470c`): dropped the file from 55,411 B to the 32,768 B floor. Ten blocks moved verbatim to the archive — `DRA-199`, `DRA-180 D2`, `DRA-180 D3`, `DRA-164 D1–D3`, `DRA-149 D5`, `DRA-149 D4`, `DRA-149 D2`, `DRA-164 D4`, `DRA-181 D4`, `DRA-180 D1` — each re-checked by hand against line 7's floor and found to hold no open ask, unexpired PARK/HOLD or standing rule resident IN THIS FILE: DRA-164 D1–D3 item 8's republish question and DRA-164 D4 item 1's SIGN-as-PR-comment question are narrative, not markers, and both are overtaken by continued practice since (PR-comment SIGNs have landed routinely, e.g. DRA-262); DRA-180 D2/D3's "FABLE.md item not drained" notes describe `FABLE.md`'s own live state, not a hold here. The `exo-experiment:` STANDING block (re-pinned by DRA-231) and the DRA-161 Helm-LOCK entry are both unaffected and stay — DRA-161's LOCK is independently live and restated in `HELM.md` ("Soft/Planner may Soft file a fresh amended DRA-55 plan"), but nothing forced its removal to meet the floor so it was kept here too rather than risk-judged out. `DRA-262 D2` (2026-09-21) stays as the current tip. Full text of all ten: `docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md`.

## 2026-09-20 — STANDING: the `exo-experiment:` tag index (re-pinned by DRA-231)

**This block never rotates.** Plan §10.1 makes the tag below the thing the M0-exit doctrine
capture cites, and `Get-Experiments` in `scripts/exo-metrics.ps1` regenerates the "Experiments in
flight" table in `docs/ops/exo-dashboard.md` by reading these lines — each tag *and the judging
clause running from it to the next blank line* — out of **this file**. DRA-231's cut moved the five
2026-09-14 entries that first carried them into the archive, so the tags are re-pinned here
**verbatim** under DRA-144's never-rotate floor: all six experiments they name are still in flight.
Tags registered after that cut are appended below the re-pinned six and live under the same floor:
`challenger-seat` (DRA-300) is the first.
The full entries, with their calls and evidence, are in
`docs/ops/claude-archive/channels/2026-Q3/DECISIONS.md` — follow a tag there for the reasoning, and
keep it here for as long as the experiment is open. Retire a tag by graduating the experiment, never
by rotating this block.

`exo-experiment: merge-sync` — judged by *merge-to-close latency*: the wall time
from a PR merging to its linked issue reaching a terminal state. The observed
failure is days (EXO-HARDEN-A2, EQ-V2-HOME-CATCHUP sat `in_review` long after the
work was on `main`), and the target is minutes, because the run starts on the
merge event. Stated net of the **wrong-close count** — issues this job moved to
`done` that a human then reopened. A sync that closes the wrong card is not
faster than the drift, it is just more confident, and the same "state net of the
harm" shape the seat-mutex tag uses for false blocks.

`exo-experiment: seat-mutex` — judged by *rework rate* and *PRs per delivered slice*
(baseline 3.7% and 2.15), because a duplicate executor spends both: #566/#568 cost one
full run and produced a second PR for one slice. Stated net of the **false-block count**
kept in `.claude/soft-seats/README.md`'s evidence list — a mutex that refuses work that
should have started is not cheaper than the collision, it is just quieter.

`exo-experiment: metrics-baseline` — judged by *whether a later window's claim can be
checked against it without re-deriving the window*: concretely, whether the §10.3
"current vs baseline" column fills from `docs/ops/exo-baseline.json` alone at the M2
checkpoint, without any §6 KPI being recomputed by hand. Stated net of the count of
metrics still reading `unmeasured`.

`exo-experiment: channel-rotation` — judged by *rework rate* (baseline 3.7%), counting
a channel-file clobber, a silently truncated append or a mojibake re-encode as rework,
because that is the class trap 60 records three times in six days and the only §6 term
this change can move. **Stated net of the effect that is NOT a §6 metric at all:** the
bytes an agent must read before it can append correctly. That is the reason the rotation
was worth doing and there is no KPI for it, so a later graduation entry cites the rework
rows and says the primary benefit went unmeasured rather than mapping it onto a number
it did not move.

exo-experiment: ssc-retirement — judged by *PRs + Helm touches per slice*
(baseline 2.3 PRs/slice, ≥2 touches/slice), stated net of *veto rate* and
*rework rate*.

exo-experiment: whole-sequence-auth — judged by *Governance Wait Ratio* and
*Autonomous Correct Completion Rate* (baseline GWR 0.40–0.60, ACCR 0%),
stated net of *escaped defect rate*.

`exo-experiment: challenger-seat` — judged by *the two-direction challenge count* that §8 of
`purpose/CHALLENGER_ROLE_CHARTER.md` defines, both sides kept because a one-sided count is how
`seat-mutex` above failed. **Direction A — the challenge that was owed and never came:** a plan
shipped, the premise failed, and either no wake fired or the assessment said PROCEED; loud when it
lands, and counted from the failure backwards. **Stated net of direction B — the challenge that
cost more than it bought:** a wake fired, the plan changed, and the change bought nothing. §8 names
direction B as *the direction a red team fails silently in*, because every individual challenge is
defensible and nobody files a complaint about excess rigour — left uncounted, it is how the
Challenger becomes a mandatory review gate while reporting green, which is the same one-sided defect
the `seat-mutex` tag records. **Kill criterion, stated in advance:** after **ten** C1–C4 wakes, a
direction-B count exceeding the direction-A count means the seat is **DROPped, not tuned** — the bar
is a rate and not an anecdote. **No reading yet, and this tag claims none:** the §8 30-day baseline
(over the preceding 30 days of `done` cards, the count of SIGNs later reversed or narrowed, and of
plans whose stated premise was contradicted by what shipped) is **not frozen**; it is owed on its own
card, **DRA-301**, and §8 requires it *before the first wake* — the seat has already taken one, so it
is late. Wake count today is **1 of 10** (DRA-299, 2026-09-22, exit 0). Registered from the commit
that stood the seat up, per §8, so the metric exists before the readings do rather than after.

## 2026-09-17 — DRA-161 / EXO-HARDEN-A1e: the rotated archive copies are OUT of scope for mojibake repair, permanently

**Seat:** Executor, carrying out the Planner ruling on DRA-160 (2026-09-17 09:50Z). This entry and one
paragraph added to the archive's own `README.md` are the whole change — **the three archived ledgers were
not touched; their blobs are sha-identical either side of this commit (`022fdea4`, `7eada6af`,
`fef3a1f9`), and the only file changed under `docs/ops/claude-archive/` is that README** — no transform
was run against any archived path, and the PR's `git diff --stat` shows only those two files.

**Verdict: OUT. All three files under `docs/ops/claude-archive/channels/2026-Q3/`, not now and not on the
next sweep.** The counts that raised DRA-160 are real — 56,979 cp437 depth-1 plus 14 depth-2 in
`HELM-FEEDBACK.original-flattened.md` (4,926,243 bytes, blob `fef3a1f9`), 5,429 cp1252 in the archived
`HELM-FEEDBACK.md` (1,915,606 bytes, blob `7eada6af`), 1,527 cp1252 in the archived `FABLE-FEEDBACK.md`
(1,000,117 bytes, blob `022fdea4`) — each measured off `/git/blobs/{sha}` with the decoded byte length
asserted against the tree's `size`, which is the only read that does not hit the contents-API false-clean
trap. They are real and they are not a defect to repair. **A future mojibake sweep reads this entry
instead of re-raising the card.**

### The Helm LOCK is not spent by this, and it stays live

The governing sentence is the DRA-55 Helm SIGN (2026-09-16 19:53Z), quoted here verbatim rather than
paraphrased, and re-read off the source comment during carry-out rather than trusted from the relay:

> Helm SIGN DRA-55 plan (ops PR #10). Executor owns slice 1 producer fix then slice 2 ledger repair.
> **Soft LEAVE inventing archive repair without separate Helm ruling.** BEVEL.md marker line stays.

That LOCK forbids *repairing* the archive without a Helm ruling. It does not require a Helm ruling to
*decline* to repair: leaving the archive untouched is the LOCK's own default state, so this verdict
affirms the LOCK rather than lifting it. No LIVE ASK was opened and Helm's door stays unspent.

**The LOCK remains live.** Anyone who wants these files repaired later still needs a real, separate Helm
ruling. This entry neither grants one nor pre-empts one — it records why nobody should bother asking.

### Three files, three different reasons — not one bucket

**1. `HELM-FEEDBACK.original-flattened.md` — it is corrupt on purpose, and two artifacts already said so.**
It holds 56,979 of the ~57k cp437 occurrences on the card, and it exists so DRA-75's "nothing was lost"
claim can be checked **against the bytes** instead of taken on trust, and so `channel-wipe-guard`'s
ARCHIVE exemption can see the entries that moved. It is also the cp437 detector's only clean-checkout
fixture — real corrupt bytes with no git archaeology needed, as DRA-55's intake comment named it. The
archive README has said *"Do not try to read it; do not edit it"* since the day it landed. Those 56,979
markers are the exhibit, not the damage.

**2. `HELM-FEEDBACK.md` — it is a git blob carried byte-verbatim, and every marker sits inside the
verbatim region.** Byte arithmetic, re-derived during carry-out rather than copied from the ruling:
`f4af3b5f` resolves as commit `f4af3b5f5ac6dc1b06290a478278bd9d19681d90`; `HELM-FEEDBACK.md` at that
commit is blob `8e18b203fcbe42a20d255f477f386b5d283afcf2`, **1,909,798 bytes**. That blob is byte-contained
in the 1,915,606-byte archive copy **at offset 5,808, with zero bytes after it** — the archive file is
exactly `[5,808 bytes of header + recovered PR #564 entry]` + `[the f4af3b5f blob, whole]`. All 5,429
cp1252 occurrences are inside the verbatim tail; none are in the prepended part. So a repair pass edits
the verbatim region, and that breaks two live assertions: the README's own claim (L84-85) that the
recovered history is carried verbatim, and `channel-rotate.py verify`, which per README L140-143 asserts
that the archive contains the `f4af3b5f` blob verbatim. It also buys nothing — git still holds blob
`8e18b203` with all 5,429 markers intact. Repairing the copy does not repair the history; it only makes
the copy stop matching the history it exists to document.

**3. `FABLE-FEEDBACK.md` — rotation moved these bytes, it did not create them.** The pre-rotation live
`FABLE-FEEDBACK.md` at `c3a40c1e` (parent of rotation commit `8f9e3205`) is blob
`51b18b02101c5210b94892a652549872f7f9a0b2`, 1,170,568 bytes, carrying **1,527** cp1252 occurrences — the
identical count now in the 1,000,117-byte archive copy. Those bytes were already permanent in git before
the archive existed.

### The principle, stated once so the next sweep quotes it instead of re-deriving it

**A rotated archive copy is not an independent site of corruption.** Rotation is a one-way move of bytes
that are already immutable in git history. Repairing a rotated copy removes zero corruption from the
record — it only makes the archive diverge from the revisions it was cut from, and it spends the fixtures
and byte-identity assertions that were deliberately built on that sameness. Count archive markers as
*copies of* live-surface markers when scoping a sweep, never as their own findings.

Repair pays on the **live** surface, and that work is done and guarded: DRA-119 slice A (PR #657, merged
2026-09-17 07:25Z) made `channel-wipe-guard.ps1` check 4 fail CI on cp437 at both depths, and slice B
(PR #659, merged 2026-09-17 09:40Z) repaired 77 lines while preserving 4 quoted ones. Rotation is
one-way, so nothing in the archive can reach a live file. Nothing is at risk from this verdict.

### The default this could have gone the other way on

Repair all three, on the grounds that ~64k mojibake occurrences in the tree is obviously bad and the fix
is a script that already exists (`scripts/demojibake.py`). That reading counts markers instead of reading
what each file is for, and it would have destroyed a SIGNed checkability exhibit, a detector fixture, and
two byte-identity assertions in order to change nothing about how much corruption git holds. The tell is
that the archive numbers do not move the live numbers in either direction — which is what makes them the
wrong surface to measure.

## 2026-09-21 — DRA-262 D2: two calls the signed plan did not make

**1. A 2.0.0 What's-new highlight was made TRUE rather than left to ship self-contradicting.**
DRA-66's entry (still unreleased) ends *"If your achievements dump has already named your
classes, that answer wins and the room says so — run the dump again if it is out of date."*
D1 reversed the first half and D2 deletes the sentence the second half points at, so shipping
both entries would put two opposite promises about one control in one release. I struck that
one clause; the rest of the highlight is byte-identical, and the new entry carries the dump
story. **It could have gone the other way**: leave it, on the grounds that D2's declared duty
was one new entry and editing a neighbouring highlight is scope. Chosen against because
"every entry TRUE" is the standing rule and the falsehood is one my own change created.

**2. The plan's optional chip tick in the E2E was DECLINED, on a measurement.**
`SetStatedClasses` has one writer in the app — the chip's own `onClick` inside `HomeRoom` —
and the suite may not press a control. Reaching it needs a FIFTH `DebugHooks` rendezvous, and
each of the four that exist was authorized separately. The plan gated the ask on "cheaply";
this is not. **It could have gone the other way**: build the probe, on the grounds that the
plan floated it. Chosen against because a new debug rendezvous is machinery the slice did not
declare. Displacement stays proven in `CharacterClassesTests` (D1) and the E2E asserts
`shellHomeClassDoor == 1` beside `shellHomeClassChips == 0` — prove-failed by restoring the
early return: `door=0 chips=0`, red on the first half, which is what the chip count alone
could never have seen.
