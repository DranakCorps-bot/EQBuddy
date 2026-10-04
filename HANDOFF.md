# HANDOFF.md — live state: holds, standing rules, live instruments

Retired 2026-09-23 (Helm SIGN, DRA-146 / PR #836): moved verbatim to [docs/ops/claude-archive/channels/2026-Q3/HANDOFF-legacy.md](docs/ops/claude-archive/channels/2026-Q3/HANDOFF-legacy.md); the working handoff is Paperclip cards + wake payloads.

**This file is the live STATE of EQBuddy's operating posture** (DRA-569, 2026-09-30, under
DRA-563 / DRA-529). It replaces root `HELM.md` as the one place holds live. `HELM.md` is now a
pointer here.

- **Readers and flow.** Dranak and Planner read this file for state and handoffs. Reviewer
  signs off on the PRs that change it. **There is no Helm gate.** A PR that edits this file
  merges on Reviewer sign-off and green `build-and-test` + `e2e-windows`, like any other.
- **Who lifts a hold.** Dranak does, or the Founder where the
  [consequence list](CLAUDE.md#what-needs-david-and-what-does-not) applies. The lift is
  recorded in this file as a dated line under **Retired**. A shipped fix does not lift a hold.
- **The name is reused on purpose.** A different file used this name before 2026-09-23: the
  long working-handoff note that DRA-146 retired. It is verbatim at
  [`docs/ops/claude-archive/channels/2026-Q3/HANDOFF-legacy.md`](docs/ops/claude-archive/channels/2026-Q3/HANDOFF-legacy.md).
  The line under the title is its retirement pointer, byte for byte.
- **Ruling history** is in
  [`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md).
  That covers every tip `HELM.md` carried, and the complete final `HELM.md` is appended at the
  end as the DRA-569 retirement pass. An archived line never revives a hold and never
  commissions work.
- **Verbatim means verbatim.** Every block headed *(verbatim)* is copied from `HELM.md` at
  `b8598475` byte for byte, and names the tip it came from. There are two exceptions. The
  mojibake in the Retired and Item-shape sections was repaired (`→`, `·`), and the Item
  shape's two Helm-only lines were re-worded to the flow above. Words like "Helm SIGNs",
  "only Helm" or "Soft" inside a quoted ruling are the record of how it was made, not today's
  flow. Today's flow is this list. CLAUDE.md and the ops docs are re-worded by sibling cards
  under DRA-563.
- **Not carried.** `HELM.md`'s *What Helm does NOT decide* and *This file is NOT like the
  other three inboxes* sections were not carried; both are in the archive. The second one's
  rules are restated above and in the Holds preamble, with one addition: **holds are not
  duplicated anywhere else.** `SCRIBE.md`'s older mentions of `HELM.md` are records of past
  re-reads, and that name now lands on the pointer file.
- **Size.** This file is in both channel guards' rosters (`state` tier) under the 64 KiB
  ceiling. When it nears the ceiling, rotate it per DRA-154. Never trim it. **Open asks, holds
  and standing rules never rotate.**

---

## Holds

**Re-read this block before ANY public reply.** Holds arrive by commit between your pulls, so
"I read it this morning" is not reading it. A hold BINDS you. It is the one place a recorded
hold outranks your standing authority to post routine signed replies (David, 2026-08-22).
**Only Dranak lifts one, or the Founder where the consequence list applies, and the lift is
recorded here. A shipped fix does not.**

A HOLD names something we are prevented from doing. If the prevented thing has already
happened, the hold is no longer needed: move it to Retired, with the date and who recorded it.
Do not leave a live hold that points at finished work.

### From **COPY SIGN / DRA-363 / #885 @ `edabbffc` — merge HOLD until launch release** (2026-09-24 ~3:09 PM CT) — the tip's body, whole (verbatim)

**COPY SIGN** draft PR #885 (TEL-PR4) at head `edabbffced0f6cb25f6fe0ab48a8c9513133ba59` (`edabbffc`), [comment 5821460490](https://github.com/DranakCorps-bot/EQBuddy/pull/885#issuecomment-5821460490). Consequence item 3. Covers **copy only** — **NOT a release go**, tag or channel. Soft-drafted; no new ruling; Helm last-look.

**MERGE HOLD on #885 @ `edabbffc`.** Stays draft; no undraft, no merge until the launch release David gates (DRA-336 §2 / §3). The HOLD lives on the PR + this tip, not a Live Holds row.

**Locks STAND.** Three-field payload (`installId` / `appVersion` / `os`, TEL-002). Telemetry opt-in. `peakConcurrent` OFF the landing — README badges only.

**Carry-out.** Merge channel PR #886 (the COPY SIGN ask) on green; merge this tip on green. Soft does not undraft #885, touch Play / signing / the Founder mailbox, or Jr self-SIGN.

Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor.

*Measured 2026-09-30 (DRA-569). This pass carried the hold and did not lift it. #885 is
CLOSED, unmerged (2026-09-28T20:35Z). Its closing comment says it was "Superseded by #947,
merged 2026-09-28", which took the signed TEL-PR4 copy verbatim, and that it "shipped with
EQBuddy Evolved 0.1 Beta (v2.0.0)". That reads as the prevented act having happened. If Dranak
confirms it, move this hold to Retired.*

### From **SIGN RECORD / DRA-409 / ops #98** (2026-09-25 ~11:30 PM CT) (verbatim)

**HOLD on live Apply.** No `-Apply`, no agent-tools mirror, no Paperclip restart from this merge. Those wait for (1) Reviewer approving DRA-408 and (2) a Bosun-declared restart window. Sr does not restart Paperclip.

*Measured 2026-09-30: Paperclip DRA-409 is `blocked`; DRA-408 is `done`.*

### From **SIGN RECORD / DRA-379 — plan #912** (2026-09-25 ~10:14 PM CT) (verbatim)

**Rulings.** Q1 ADOPT one tile: `weeklyActive`, "Playing this week", note "opt-in installs only · a lower bound"; four-tile band; `/report` as navigation only. Q2 ADOPT committed snapshot via new `scripts/landing-telemetry.ps1` (one writer of `site/metrics.json` + hero `.n` together); REJECT live cross-origin fetch; REJECT auto-commit cron. Q3 ADOPT Evolved downloads tile OUT entirely (no coming-soon tile; supersession waits for push-wide + real assets on its own card). Q4 REJECT paint-now at N=1; ADOPT hold-at-push-wide — public hero paint of opt-in figures waits for the Founder's public-Evolved / push-wide go (or an explicit Founder early-authorize). Q5 #885's footer sentence verbatim, riding with the tile (same ship as Q4); #885's `site/` hunk stays MERGE-HELD with its README/SECURITY half. Challenge ACK: `dra-379-landing-telemetry-hero -> NOT-ENGAGED (no C-test fires)` stands; no Challenger wake. The plan's "Helm ruling 2026-09-25 9:45 PM CT" citation is NOT on the record; what stands is Founder 2026-09-24 2:33 + 5:11 PM CT and `docs/v2/telemetry.md` §10.

**D1 AMEND (route: hard — Sr Executor).** Now (authorized on the SIGN): `scripts/landing-telemetry.ps1`; `LandingSourceClaimsTests` re-key + committed negatives; `docs/v2/telemetry.md` §5 pointer; challenge-line-guard must-list row; optional stale-scope-sentence fix naming `/report` — no hero tile. Held for the push-wide go: the first committed snapshot painting the `weeklyActive` tile + Q5 footer (draft PR until the go; tip before undraft).

**Q3 and Q4 RETIRED 2026-10-04 (Dranak, DRA-640).** See Retired. Q1 and Q2 in the verbatim above stay. The Q5 push-wide wait rode with Q4 and is spent with it.

### From **SIGN RECORD / DRA-440 — #915** (2026-09-25 ~11:11 PM CT) — Q4 hold-at-push-wide stands (verbatim)

**Authority.** D1-now under the DRA-379 plan SIGN (with AMENDs) on #912 @ `6abc1fd2`. No Q1–Q5 re-open. **Q4 hold-at-push-wide STANDS.** The only `site/**` change allowed (and made) is the `maxConcurrentUsers` scope-honesty fix (stale sentence now names `/report` + "landing tile waits for the public Evolved release"); no hero tile, no Q5 footer paint, `index.html` + `landing.js` untouched. ADOPT stated deviation: top-level `asOf` left alone; snapshot date lives in `scope.weeklyActive`.

**Held slice STANDS.** `weeklyActive` tile + Q5 footer wait for the Founder's push-wide / public Evolved go (or explicit early-authorize); that PR stays draft, tipped for Helm SIGN before undraft/merge.

**This restatement of Q4 RETIRED 2026-10-04 (Dranak, DRA-640).** See Retired. The verbatim above stays as the record.

### Public-reply process (verbatim, from the HELM.md Holds block)

Public-reply check-in is process, not a Holds line. New-thread thank-you still comes to Helm.
First-run / "weird flow" findings file on BEVEL.md without waiting on Helm. A public promise of review or a fix still comes to Helm before it posts.

---

## Rulings recorded here since DRA-569

Each item uses the **Item shape** at the foot of this file.

### 2026-10-01 — DRA-128 graduation: `ssc-retirement` ADAPT, `whole-sequence-auth` HOLD (DRA-671)

- **Kind:** `sign-off` (ssc-retirement) · `hold` (whole-sequence-auth, a hold on its verdict;
  it prevents graduating or dropping the experiment, not any public reply or merge)
- **Thread / subject:** DRA-128, the two M0 experiments due a verdict on
  [`docs/ops/exo-dashboard.md`](docs/ops/exo-dashboard.md) Reading 2 (PRs #619–#643, PR #644)
- **Ruling:**
  1. **`ssc-retirement`: GRADUATE, verdict ADAPT**, naming caveats 3 (the PRs/slice drop is
     largely mechanical; the judged term is Helm touches per slice, 2.1 to 1.62) and 4 (0/23
     rework bounds the true rate below about 4%; it does not prove 0, and adopters keep
     counting veto and rework). The missed touches target (1.62 vs frozen <0.3) is named and
     is not a hold: per-slice ruling frequency is whole-sequence-auth's claim. ADAPT
     precondition: the adopting project must already have a committed, auditable ruling
     ledger (a HELM.md equivalent with SIGN-as-commit). Doctrine: ops `EXO-PLAYBOOK.md`
     entry 7. Decision record: `DECISIONS.md`, 2026-10-01.
  2. **`whole-sequence-auth`: HOLD**, stays in flight, no verdict.
- **Condition** (lifts the whole-sequence-auth hold; verbatim):
  1. DRA-134 (Sr Executor) completes: standing Scribe triage sweep live and DefectConventionStart set. Instrument half is already merged (DRA-135).
  2. The first window whose last merge is at least 14 days (DRA-133 lag floor) past the convention start gets a reading where the escaped-defect row prints a rate or a named honest status other than NoConvention.
  3. A fresh T2 ruling on that reading judges GWR/ACCR net of the measured term, and may re-set the <0.15 / >70% targets if they were joint M0-bundle aspirations.
- **Signed:** Dranak, 2026-10-01, ACCEPT (comment `609a0b92` on DRA-549) of Planner
  recommendation `e6169a75`. Landed by DRA-671.

---

## Standing rules carried from the 2026-09-24 .. 2026-09-26 tips (verbatim)

The dated tips themselves are in the archive. Each line below is a rule, ADOPT or park that no
later tip discharged, copied with its source tip named.

### From **LIFT + SIGN / DRA-373 D2 / #878** (2026-09-24 ~1:30 PM CT) — CTA rule

**CTA RULE STANDS (Founder 2026-09-24 1:08 PM CT via Helm; supersedes 12:15 PM variant-B).** Evolved **coming soon**, **no download button**, **no v1 link** (no `releases/latest`, no tag/channel, no "1.x available today"). Variant A stays FORBIDDEN until a public Evolved installer exists. Soft LEAVE inventing channel/tag/signing from this SIGN. Guard `LandingSourceClaimsTests.TheLandingIsComingSoonAndNeverLinksV1` (+ committed negative). LICENSE footer MIT for published 1.x is licensing, not a download — STANDS.

### From **SIGN / DRA-376 (DRA-373 D1) / #876** (2026-09-24 ~12:50 PM CT) — step 5 honesty

**Corrections ADOPT (moved, then corrected).** Helper answers live (Farm Gear → *Upgrade what I wear*; Level Up ranks your camps). Track → map ring. Evidence floor (`ZoneHistory.MinHours` = 0.25 h) named. Scorecard gains the upgrade/who-drops row. **Step 5 honesty STANDS on #876:** travel is wiki zone-graph hops; it does **not** know which teleports *this* character has unlocked (`ZoneGraph` / `TravelPlan` have no unlock input). Soft LEAVE inventing shipping the unlock claim.

### From **SIGN / DRA-53 night-14 / ops PR #78** (2026-09-24 ~12:14 AM CT) — unattended-job rules

**ADOPT — kick script-path rule.** Unattended jobs must not depend on whatever branch a dispatch-lane checkout holds at fire time. Resolve soft-seat scripts from `origin/main` blobs (materialize into a tools dir), record `soft_seat_tools: origin-main | checkout-fallback`, checkout is announced fallback only. Dry-run + prove-fail ACK; night-15 `last-kick.json` is live proof.

**ADOPT — unblock must re-REGISTER.** Auto-blocking the monitor card nulls `executionPolicy` and a blocked card refuses re-arm (422). The unblock pass must re-REGISTER kind + recoveryPolicy + notes (≤500 chars) + nextCheckAt, not merely re-arm. Night-14 repair ACK (armed to `2026-09-25T05:20:00Z`).

**RULE — DRA-4 / ruling 2 parent sweep.** **RETIRE** ruling 2's parent-verifies-child check as a required nightly liveness layer **while DRA-4 remains blocked** (`monitorNextCheckAt` frozen at `2026-09-17T05:40:00Z` since 09-17). Soft LEAVE inventing unblocking DRA-4 from this tip. Stack until DRA-4 is next eligible: kick + DRA-53's own monitor only. When DRA-4 next becomes `in_progress`/`in_review`, Soft re-REGISTERS the 00:40 monitor (same unblock-must-re-REGISTER rule) — that restores ruling 2.

### From **SIGN / DRA-236 / ops PR #79** (2026-09-24 ~2:41 AM CT) — Arm-column procedure

**Q2 — Arm caveat: ACK; no miss.** **ADOPT** as standing Arm-column procedure: read back `executionPolicy.monitor.nextCheckAt` **after** the pass's last write (summary / email), quote value + read time (or `none` + reason). The arm stays out of the verdict (night-7 precedent STANDS).

### From **RULE / DRA-393** (2026-09-25 ~12:05 PM CT) — Hermes evidence pinning

**RULE (Helm 2026-09-25):** after APPLY and the Hermes restart, set `compression.pin_evidence_patterns` to `["site/*","*.html"]` **ONLY** on review/challenge Hermes seats: today **Challenger**, plus **Reviewer** and **Bevel** whenever those profiles run on Hermes. Every other Hermes profile keeps `[]`. `pin_budget_tokens` stays `16000`. Patch defaults stay `[]`.

### From **RULE / DRA-396 EXO-HARDEN (d) / ops #92** (2026-09-25 ~1:00 PM CT)

**ADOPT H2** for Challenger and Jr: dedicated review `HERMES_HOME` (no `EMAIL_*`, no `skills\email`, no `cronjob`, no `hermes\bin` on `PATH`). Gate: one-run proof tipped to Helm before live apply. **REJECT H1** (cannot close himalaya via `terminal`). **DEFER C1** until a seat-scoped deny path is verified (not Planner or Reviewer). AGENTS.md no-send / end-at-`in_review` patch applies now.

### From **SIGN RECORD / DRA-423 / ops #100** (2026-09-25 ~6:36 PM CT) — batched wake

**ADOPT both judgment calls:** (1) **A4 enqueue clock** — for a batched ask the *enqueue* timestamp is the firing time A4 reads; an off-cycle fire records its own. (2) **Bridge until `helm-notify`** — until DRA-415 slice 1 lands, a pending interaction rides the A8 pending-interactions pulse; the same three off-cycle classes apply unchanged.

**Operative A1:** non-urgent pending interactions enqueue via `helm-notify` (same four fields) and flush at most 9 AM / 1 PM / 5 PM / 9 PM CT. Off-cycle same-turn fire only for (1) needs-david; (2) security; (3) a merge blocked on Helm SIGN >2 hours. Soft LEAVE inventing a second webhook or firing the ops stub copy as a wake.

---

## Live instruments re-pinned — pass 6 (DRA-154, 2026-09-25)

**Read this block with passes 5 and 4 below it.** Pass 6 moved the ten dated tips this file
carried from 2026-09-22 ~7:10 AM CT (DRA-332 / PR #823) through 2026-09-23 ~8:28 PM CT
(DRA-355 / PR #860) into [`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md),
verbatim. Every tip dated 2026-09-24 stays live. Each carry-out those tips ordered was checked
before the move: EQBuddy #823, #856, #860, #848 and #836 are merged; #847 is closed; ops #72, #73, #74,
#76 and #77 are merged; `WorldEra.Current = "Classic"` is on `main` (DRA-180 D5); the D2.3 "live
rule" note is in `docs/plans/DRA-352.md`; and root `HANDOFF.md` is the pointer (DRA-146). The
exceptions are the standing items below, re-pinned **in Helm's own words, byte for byte**, with
the tip each came from named beside it. A re-pin is the live instrument; the archived tip is the
reasoning.

### From **RULE / DRA-332 / PR #823** (2026-09-22 ~7:10 AM CT) — standing park + tip-vehicle check

- **Q2 — REJECT a Live Hold invent for a phantom sweeper.** Soft named no identified un-draft-and-merge actor outside seats following Helm instructions. No HOLD naming PRs, no label gate, and no new door invented for an actor Soft has not identified. Standing park **STANDS** as already ruled (DRA-326 / Cond-B lift): Soft may draft a `HELM.md` tip; Helm SIGNs (comment naming head, or this file); Soft merges. Soft HOLDs merge of any Soft-drafted `HELM.md` tip until that SIGN exists. Soft may file a separate card if #811 / #820 still need an identified merger; Soft does not invent the mechanism in this tip.
- Standing tip-vehicle check from that same #821 ruling **STANDS**: Soft-drafted `HELM.md` tips that carry DRA-132 relay substitution in a prohibition slot HOLD for Helm AMEND before land (not a CI must-list order here).

### From **SIGN / DRA-336 / #856** (2026-09-23 ~6:20 PM CT) — TEL launch locks

**Locks that stand (Founder AUTHORIZE 2026-09-22 + plan §1/§5)**
- Off by default forever until the player says yes; decline (Esc / ✕ / Not now) is the default action.
- Prompt fires once per install; no nag on update; Options toggle is the only way back in.
- No dark pattern; payload frozen at TEL-002's three fields; TEL-006 scope freeze (no crash/events).
- No Play Console; no on-by-default; no payload beyond TEL-002.
- LEGACY-V1 "nothing phones home" stays true forever.

- **TEL-PR4** (DRA-363) rides the launch release David already gates; Helm signs that public copy separately (consequence item 3).

### From **ACK / process** (2026-09-23 ~5:44 PM CT) — D5a, WorldEra Classic, Researcher-first

- **D5a ADOPT Ask 1 stands** (era → band → who before the sweep cap).
- **WorldEra Classic STANDS.** Ask 2 Epics ADOPT on #854 is **VACATED**. Founder word = **Classic** (`QuestEraLadder` spelling), not an Epics gloss. Soft LEAVE inventing an Epics D5 or merging an Epics tip. `WorldEra.Current` stays Classic until Researcher or Founder updates it. eqlwiki is not the world-clock source; Researcher keeps the curated WorldEra. Founder ~5:49 CT: Classic STANDS; Epics content startable but not finishable (not a ladder move to Epics). Soft LEAVE inventing blocking D5 for this — P4 already answered Classic.
- **Researcher-first.** Researcher owns any fact a simple online search can settle — that is the role's purpose. Planner must factor Researcher into plans for those asks (route Researcher wake / Soft lookup before Helm or Founder). Soft LEAVE inventing Founder mailbox or chat for searchable facts. Founder only for judgment, spend, and true ambiguity.

## Live instruments re-pinned — pass 5 (DRA-154, 2026-09-23)

**Read this block with pass 4 below it.** Pass 5 moved all nineteen dated tips this file
carried from 2026-09-21 ~5:22 AM CT (DRA-287 / PR #766) through 2026-09-22 ~12:38 AM CT
(DRA-110 / PR #797) into [`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md),
verbatim; the 2026-09-23 DRA-345 SIGN above stays as the current tip. Every carry-out
those tips ordered was verified discharged before the move — the tip PRs and ask PRs
merged or closed as discharged, `granted_mode` shipped in `scripts/claim-seat.ps1`
(DRA-110), the DRA-330 gloss is live at both its sites, the DRA-327 quarantine note and
remeasure landed in the archive README, the S3 guard (`scripts/challenge-line-guard.ps1`)
is on `main` with Cond-B lifted, and `max_tokens: 8192` is live in the hermes config
(DRA-292) — EXCEPT the items below, re-pinned here **in Helm's own words, byte for
byte**, with the tip each came from named beside it. A re-pin is the live instrument;
the archived tip is the reasoning. The marker strings inside the DRA-132 re-pins are
deliberate mentions (DRA-325's classification), not defects.

### From **SIGN / DRA-53 night-12 / ops PR #65** (2026-09-22 ~12:12 AM CT) — the #795 merge is still owed

- **ACK inherited wake #692.** Soft tip EQBuddy #795 substance ADOPTed (MERGE after Soft rebase; Jr stays unprovisioned / D1 fails closed to Sr). Soft merge #795 when CI green; Soft Executor rebase-then-merge #692. No Jr kick.
- Measured at this pass (2026-09-23): **#692 MERGED; #795 still OPEN** — the #795 merge is the undischarged half.

### From **LIFT / DRA-309 Cond-B / DRA-319** (2026-09-22 ~2:35 AM CT), as AMENDed by **RULE / DRA-326** (~4:20 AM CT) — standing lift-authorship rule

- **Who writes the lift tip — RULED.** Soft reports the measured green (HELM-FEEDBACK LOOP CLOSED / DRA-319 @ `8f28bc2d` did that). **Only Helm SIGNs the `HELM.md` lift** — Soft does not invent a lift without Helm SIGN. Soft may **draft** the HELM.md tip for Helm last-look; **Helm SIGNs**; Soft merges. That draft-PR path is the normal state-write path, not Soft inventing doctrine; DRA-305 §5's "no edit by Soft" for `HELM.md` stands. The prior SIGN's "Soft tip that names the lift" means Soft tips the measured condition via HELM-FEEDBACK (and may draft the HELM.md tip for SIGN), not Soft merging an unsigned lift.

### From **RULE / DRA-132 / PR #785** (2026-09-21 ~9:57 PM CT) and **RULE / DRA-132 follow-up / PR #789** (~11:15 PM CT) — DRA-132 is still open

- The live doctrine line, as AMENDed by the follow-up:

> A Founder LOCK or standing prohibition that arrived through the Soft LEAVE inventing relay is not a quotable source in any surface — Paperclip comment, card description, or seat instruction bundle. Reconstruct to a durable correct copy with a relay note naming the substitution and its count (and for board sources, the originating comment id or a pinned fetched-at); treat that copy — never the corrupted surface — as the readable authority. Quoting the corrupted form verbatim is a defect.

- **Q3 — ADOPT interim reading rule.** Until the PUT lands, every seat **MUST** reconstruct `Soft LEAVE inventing` / `Soft LEAVE inventing inventing` as the negation it replaces when reading its own charter (and any other corrupted standing instruction). No reading the corrupted form literally. No treating the interim as the permanent fix — DRA-312 still carries the in-place reconstruction.
- **Q2 — ADOPT write path as proposed.** Helm rules the reconstructed text (last-look). Sr Executor PUTs via `agents:configure` with sent==got read-back under the DRA-302/303 grant. Planner **may draft** the reconstruction for Helm last-look; no Planner self-serving rewrite of its own authority bounds. Card **DRA-312** is AUTHORIZEd to leave `backlog` for that draft→last-look→PUT chain only.
- **Q2 — item 2 STANDS in substance; Soft paging HELD.** The Founder-facing half is **Helm's courier**, not Soft's. No paging Founder from DRA-132 or any of the ten. Helm surfaces the relay-negation defect once (one mail) as a high-consequence door on the Paperclip instance David owns. The corrupted "do not page" reconstruction is **not** authority to bury the defect; the done-bar "surfaced to Founder" is discharged by Helm's one surface, not by Soft chat-page. (The ~11:45 PM CT DRA-252 tip restated it: *"prior DRA-132 courier still owed once — no second page."*)
- No expanding the ten Founder-LOCK copies into the 114/88 board-wide sweep without a separate ask; Planner may triage agent-adopted phrasing without Helm.

---

## Live instruments re-pinned — pass 4 (DRA-277, 2026-09-21)

**Read this block first.** Pass 4 moved all six dated tips this file carried
(2026-09-18 through 2026-09-21) into [`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md), verbatim. Every one of them carried
something still open, so the open thing is re-pinned here **in Helm's own words, byte for
byte**, and the tip it came from is named beside it. A re-pin is the live instrument; the
archived tip is the reasoning. Neither is a work queue, and an archived line never revives
a hold.

### From **DRA-180 D3 / PR #694** (2026-09-18 ~11:35 PM CT)

- The **#685 whole-sequence SIGN is unchanged by this land** — it still authorizes D1 → D2 → D3 → D5 in order on green gates with D4 disjoint-parallel-eligible, and merging D3 does not re-open, re-SIGN or narrow it.
- **BOUNDARY KEEP — the Core producer is P3's, and it stays.** D3 needed a **Core** change rather than words alone, and that is **KEPT as built** — **The shared generic gates stay untouched and Farm Materials stays unaffected**. An anchor is reported ONLY when nothing of its survived and an anchor nothing dominates stays `NoCatalogUpgrade` — **KEEP**.
- **RE-KICK `dra175-rotate` — the patch is MISSING.** The ~6:40 PM CT tip AUTHORIZEd an Executor `dra175-rotate` carry-out of the append-safe FABLE-only patch; the branch on the remote carries the LIVE ASK and nothing else, so that AUTHORIZE is **undischarged** and Soft **re-kicks it** as its own seat. It is NOT folded into DRA-154 and does not gate this rotation.
- **D5 is BLOCKED until the P4 Founder one-word WorldEra (+ whether eqlwiki states it on a citeable page) is answered on Helm's normal mailbox cadence.**

### From **DRA-216 / PR #705** (2026-09-19 ~5:03 PM CT) — #705 merged `90b5a866`

- **PARK S8 and S9** (and the S12.3 / S24 AC 5–7,9,10 / S26 AC 10–11 acceptance that depends on them) for this DRA-216 program. Do **not** authorize eqlwiki harvest or any other tier/exaltation data capture in this land. Honest surfaces may say comparison unavailable; do not invent `+N` math or unverified exaltation compatibility. **SIGNED** the D1–D6 sequence as filed (DRA-217 Jr/routine; DRA-218..222 Sr/hard; D5 after D4).
- DRA-201 Jr Executor carry-out: until that proof, **D1 fails closed to Sr** under DRA-179 (do not invent a second Jr seat).

### From **DRA-216 D7 / PR #709** (2026-09-19 ~7:49 PM CT) — #709 merged

- **KEEP §7.1** — Planner review is per slice as each lands; a slice is not finished for D7's purposes until its review findings are closed; a post-merge finding returns on the same card as a fresh branch off `main` (do not invent a floating follow-up nobody sequences). D1–D6 proceed unchanged under the #705 SIGN.
- **KEEP §7.2** — the private Founder report email is pre-authorized when the program is honestly done for D7's purposes; route Helm first, then Dranak; it is not a consequence-list public-comms door.
- **REJECT** that the card's *"then push the changes to the live environment on my desktop"* **is** the release go. **KEEP §7.3 fail-closed:** the go is explicit and contemporaneous; the 2026-09-19 card line is intent, written before any D1 line existed, and does not decide what the work turned out to be. Order stays gates green → Fable reviews the release → then ask David for the ship word in the step-2 email. Planner still may not tag or sign without a Helm ruling.

### From **DRA-232 / PR #719** (2026-09-19 ~11:35 PM CT) — LIVE ASK discharged; rotate carried by DRA-277

- **KEEP** the 64 KiB ceiling and both guard arms. … **REJECT (b)** per-file ceilings as the fix — that is a self-granted exemption, and check B already refuses it.
- **(c) for `HELM.md`, `DECISIONS.md`, and `FABLE.md`** — split at source. (`FABLE.md` shape: index + `docs/plans/DRA-nn.md`; HELM/DECISIONS: live STATE + current tip, dated history to existing archive.) … Dated tips rotate into the existing `docs/ops/claude-archive/channels/` path; rotation is a move/rename, not a trim. … a named non-Executor seat implements; never on a feature branch. (**AMENDED 2026-09-21 / DRA-282:** discharge floor — live file must land at or under 50% of ceiling = 32,768 B LF-normalized UTF-8; shape alone does not discharge.)
- **(a) for `SCRIBE.md`, `FABLE-FEEDBACK.md`, `BEVEL.md`, and as INTERIM for HELM/DECISIONS until (c) lands** — rotation is a per-file headroom trigger, not weekly. (**AMENDED 2026-09-21 / DRA-287:** `FABLE.md` moved to (c). **AMENDED 2026-09-21 / DRA-282:** WARN band is three median appends.) Default: WARN when remaining band is 2% of ceiling or **three** median appends, whichever is larger; the rotate seat claims before the file is red.
- **(d) ADOPT as Helm tip format** — one ruling, short. … No 7 KB walls on a HELM tip. (**AMENDED 2026-09-22 / DRA-327:** short-form residual decoded; see tip above.)

### From **DRA-241 / PR #724** (2026-09-20 ~3:05 AM CT) — #724 merged; the Sr proc slice is still owed

- **ADOPT (a) — report only, never price.** Soft/Planner **write the DRA-241 done bar TO this ruling**, then Soft **route DRA-241 to Sr Executor** for ONE slice: `ItemStatsBlock.Effect` + a structural `CombatProc` reading off the committed `ItemCatalog.json.gz` block, scoped to records that carry a `DMG:` line, with anything unrecognised **reported by name and refusing nothing** (`WeaponHands.Unadmitted` rule, verbatim).
- Caveat ONCE per block, never per row (trap 73): EQBuddy cannot say what a proc is worth.
- May a proc ever REFUSE an offer? — **NO: annotate, never refuse.**

### From **DRA-262 implement SIGN** (2026-09-20 ~9:03 PM CT, on #751) — SUPERSEDES the ~8:36 PM CT packet ACK on implement

- **Implement IS authorized.** Helm, verbatim: *"**ADOPT** plan #751 as written. Implement authorized for **D1 → D2** (`route: hard`), in order, on green gates."* The ~8:36 PM CT packet ACK's earlier line withholding implement authorization is **superseded and not live** — it is preserved verbatim in the archived tip and must not be read out of the archive as a live instrument.
- Prior DRA-262 AMEND ACK (~1:40 PM CT / #741) **STANDS**. DRA-252 **KEEP gate 4 LAST STANDS**; does not re-gate v2.0.0. **DRA-272 stays OUT** as signed (fixtures carry `General: Level`).
- Slice state at this land: **D1 #752 MERGED** `aa1350fe`, **D2 #756 MERGED** `92e08647` 2026-09-21T07:41:05Z — D2 was *in flight* when Helm last-looked #759 at ~2:40 AM CT and landed four minutes after that ruling posted.
- Undischarged: `rebase-then-merge #738` when green — **#738 still OPEN**, and **#738 HOLD STANDS**.

### From **DRA-53 night-11 ACK** (2026-09-21 ~12:06 AM CT) — carry-out **DISCHARGED**

- **ACK — night-10 STANDS. Soft carry-out is the door, not a re-rule.**
- All three ordered moves are done, measured 2026-09-21: ops #10 closed without merge
  05:14:15Z, ops #35 closed without merge 05:14:16Z, ops #12 merged 05:18:14Z. The tip's
  own "Soft carry-out undischarged / HIGH" line is spent, which is why the tip rotates.
- Still open as a permission, not an order: Soft/Planner **may Soft file a fresh amended DRA-55 plan** if mojibake repair is still wanted.


---

## The live rulings the re-pins rest on (verbatim)

An archived line is history: it never revives a hold and it never commissions work.

- **PR #685 / DRA-180 + DRA-181, SIGNED 2026-09-17 ~9:10 PM CT** — the whole-sequence
  authorization this file's top tip says STANDS: D1 → D2 → D3 → D5 on green gates, D4
  disjoint-parallel-eligible, P1–P5 ADOPTed, WorldEra ABSENT until the Founder word.
- **PR #684 / DRA-179, SIGNED 2026-09-17 ~8:55 PM CT** — Jr/Sr `route:` tags are LIVE,
  an untagged delivery fails closed to Sr, and the ten banned-Jr surfaces bind.
- **DRA-175 second-rotate, RULED 2026-09-17 ~6:40 PM CT** — the append-safe FABLE-only
  patch, and the `dra175-rotate` AUTHORIZE the top tip re-kicks.
- **PR #663 / DRA-164 and PR #649 / DRA-149** — the two older sequences neither this
  rotation nor the top tip marks PASS.

From **DRA-209 / PR #699** (2026-09-19), as HELM.md named it when the tip was archived:

- **DRA-209 / PR #699** (2026-09-19) — BEVEL-FEEDBACK.md F3 rotation APPROVED. It also
  carries **KEEP: the DRA-144 Helm-only route for later `*-FEEDBACK.md` rotations**, which
  is still live and is named here so archiving the tip does not bury it.

---

## Wakes and Claude kick

*Historical, carried verbatim so the wake path stays findable. These lines describe the Helm
flow this file replaces; they are not today's instruction. There is no Helm gate. The DRA-563
sibling cards re-word or retire this section when they re-word CLAUDE.md.*

- Helm cannot start Claude. Dranak runs `claude -p` on David's Windows PC, pointed at this repo / HELM.md + HELM-FEEDBACK.md.
- Claude and Fable wake Helm with: `gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane` (optional `-f reason="HELM-FEEDBACK.md changed"`). Secret is not in this repo.
- A GitHub push to HELM-FEEDBACK.md is not a wake unless that POST happens.

## Retired — no longer needed as a hold

Do not put these back in Holds.


- **#208 — lifted for final v1 cut only (2026-09-04).** Owner authorized V0–V1 mobile sounds (opt-in, off by default). Lifting condition: owner final-v1 scope lock 1:14 PM CT. Scoped to this cut — not a standing open for unrelated Wayland chip-monitor work on the same discussion unless separately authorized. Do not put the old do-not-open hold back.
- **#228 — no longer needed.** Helm lifted 2026-08-22 8pm. David ruled star-only is enough
  (the second lifting condition). v1.99.4/1.99.5 restore starred motes automatically;
  never-starred uses Options → Cards & windows. A limit-named player reply is signed for
  Scribe (no victory lap, no "motes are back"). Do not put this back in live Holds.
- **#226 status / follow-up reply gate — no longer needed.** Helm-signed status posted
  2026-08-22. LeBigNasty then said the re-check looks better and repeated the two leftover asks
  (motes out of pack suggestions; client-side ignore). That follow-up lives on the wiki-pack
  motes item. Thread stays open. Leftover Innoruk lore-vs-creature is leftover work, not a hold
  — and it shipped in v1.99.4. **A new #226 draft still comes to Helm (process).**
- **#208 already has a reply** (cosmic-comp, 2026-08-22). Mobile-sounds work was later authorized for the final v1 cut (2026-09-04); see Retired #208 lift. Wayland chip-monitor ask on the same thread is separate.
- **#231 thank-you** posted; PR merged. Never needed its own hold line.
- From **RULE / DRA-296 / #867** (2026-09-24 ~8:38 AM CT), verbatim: **`#738` HOLD — RETIRED.** Its prevented act was merging the dirty tip into live `HELM.md`. The archive append plus close-without-merge discharges that door.
- **DRA-379 Q3, DRA-379 Q4, and DRA-440's restatement of Q4 — RETIRED 2026-10-04 (Dranak, DRA-640).** The prevented acts have happened, so these holds are no longer needed. Q3 kept the Evolved downloads tile out until push-wide plus a real asset on its own card: v2.0.0 went public on 2026-09-28, the Evolved installer exists, the Founder said on 2026-09-29 "Instead of total installs, I would like to show downloads", and #961 merged 2026-09-30 (`d80d543eb5170bc26b24ea1b28eb955448372375`). `site/index.html` on main paints `data-live="evolvedDownloads"`. Q4 held public hero paint of the opt-in figures until that same push-wide go; main paints `usageHoursAllTime`, `peakDailyActive`, `peakWeeklyActive`, and `peakConcurrent`, and the `livecap` under the strip states downloads-not-people and the opt-in lower bound (the Q5 sentence that rode with Q4). DRA-440's "Q4 hold-at-push-wide STANDS" lifts with Q4. Q1 and Q2 in the DRA-379 block stay live ADOPT rulings. DRA-363 and DRA-409 are not this lift.

The 122 `### PR #…` sign-off entries that used to sit under this heading — 2026-08-24
through 2026-09-06, none of them a hold — moved to
[`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md)
on 2026-09-18 under DRA-154. The five retired-hold lines above did not move and never will.

---

## Item shape, for anything that is not a hold

- **Kind:** `hold` · `lift` · `sign-off` · `priority` · `posture` (what may be said publicly)
- **Thread / subject:** the discussion number or the thing being ruled on
- **Ruling:** what it is, in the words of whoever ruled
- **Condition:** what would change it — *"after a ship that actually restores the card"* is the
  model. **A hold with no lifting condition is one nobody can ever satisfy**, and it is worth
  asking for one.
- **Signed:** who ruled or lifted (Dranak, or the Founder where the consequence list applies), and the date
