## 2026-09-26 ~10:15 AM CT — SIGN RECORD / DRA-47 / #924 @ `6c662731`

**N3 PR A SIGNED** ([comment 5847271194](https://github.com/DranakCorps-bot/EQBuddy/pull/924#issuecomment-5847271194)); merged `6690ae35`. Four departures ADOPTed: binding shim, Core routing, adopt-once, PR B split. PR B stays its own track.

**Soft: merge this tip on green after Helm SIGNs its head.** Not needs-david.

---
## 2026-09-25 ~11:30 PM CT — SIGN RECORD / DRA-409 / ops #98 @ `e662df39` — DRA-408 roster row + review-cancel-guard test (prep only)

**DRA-409 prep SIGNED** — Helm SIGN on [ops #98](https://github.com/DranakCorps-bot/dranakcorps-ops/pull/98) at head `e662df39` ([comment 5843178457](https://github.com/DranakCorps-bot/dranakcorps-ops/pull/98#issuecomment-5843178457)); merged `bd425ac020ad32cc8f2bf882d8792990ff0a6aef` (11:32 PM CT, ops has no CI). Guard substance SIGNed on ops #96 @ `91750bd4`; all six guard conditions stand. No new ruling.

**HOLD on live Apply.** No `-Apply`, no agent-tools mirror, no Paperclip restart from this merge. Those wait for (1) Reviewer approving DRA-408 and (2) a Bosun-declared restart window. Sr does not restart Paperclip.

**Soft: merge this tip when CI green after Helm SIGNs the tip head.** Live Holds empty. Play Console OFF. Not needs-david.

---
## 2026-09-25 ~11:11 PM CT — SIGN RECORD / DRA-440 — DRA-379 D1-now implement (#915), hold-at-push-wide stands

**DRA-440 implement SIGNED** — Helm SIGN on [EQBuddy #915](https://github.com/DranakCorps-bot/EQBuddy/pull/915) at head `2b482bc7` ([comment 5843052012](https://github.com/DranakCorps-bot/EQBuddy/pull/915#issuecomment-5843052012)), answering the DRA-440 tip (channel #916). Channel tip #916 ACKed at head `02ddf921`. Soft drafted this tip; it adds no ruling beyond Helm's comment there.

**Authority.** D1-now under the DRA-379 plan SIGN (with AMENDs) on #912 @ `6abc1fd2`. No Q1–Q5 re-open. **Q4 hold-at-push-wide STANDS.** The only `site/**` change allowed (and made) is the `maxConcurrentUsers` scope-honesty fix (stale sentence now names `/report` + "landing tile waits for the public Evolved release"); no hero tile, no Q5 footer paint, `index.html` + `landing.js` untouched. ADOPT stated deviation: top-level `asOf` left alone; snapshot date lives in `scope.weeklyActive`.

**Merged (both on `build-and-test` + `e2e-windows` green, pinned heads).** #915 @ `2b482bc7` → `65c4967863391c2bcce439c67a89c0e1c762c9ec` (11:14 PM CT; tree identical to the SIGNed head). #916 @ `02ddf921` → `79defac5ac9f3c41611c145fde73a1e679980cad` (11:25 PM CT).

**Held slice STANDS.** `weeklyActive` tile + Q5 footer wait for the Founder's push-wide / public Evolved go (or explicit early-authorize); that PR stays draft, tipped for Helm SIGN before undraft/merge.

**Soft: merge this tip when CI green after Helm SIGNs the tip head.** Then LOOP CLOSED on the DRA-440 tip; discharge Paperclip DRA-440 / DRA-379 D1-now.

Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor.

---
## 2026-09-25 ~10:14 PM CT — SIGN RECORD / DRA-379 — landing hero opt-in telemetry, hold-at-push-wide (plan #912)

**DRA-379 plan SIGNED (with AMENDs)** — Helm SIGN on [EQBuddy #912](https://github.com/DranakCorps-bot/EQBuddy/pull/912) at head `6abc1fd2`, answering the LIVE ASK (channel #913). Soft drafted this tip; it adds no ruling beyond Helm's comment there.

**Rulings.** Q1 ADOPT one tile: `weeklyActive`, "Playing this week", note "opt-in installs only · a lower bound"; four-tile band; `/report` as navigation only. Q2 ADOPT committed snapshot via new `scripts/landing-telemetry.ps1` (one writer of `site/metrics.json` + hero `.n` together); REJECT live cross-origin fetch; REJECT auto-commit cron. Q3 ADOPT Evolved downloads tile OUT entirely (no coming-soon tile; supersession waits for push-wide + real assets on its own card). Q4 REJECT paint-now at N=1; ADOPT hold-at-push-wide — public hero paint of opt-in figures waits for the Founder's public-Evolved / push-wide go (or an explicit Founder early-authorize). Q5 #885's footer sentence verbatim, riding with the tile (same ship as Q4); #885's `site/` hunk stays MERGE-HELD with its README/SECURITY half. Challenge ACK: `dra-379-landing-telemetry-hero -> NOT-ENGAGED (no C-test fires)` stands; no Challenger wake. The plan's "Helm ruling 2026-09-25 9:45 PM CT" citation is NOT on the record; what stands is Founder 2026-09-24 2:33 + 5:11 PM CT and `docs/v2/telemetry.md` §10.

**D1 AMEND (route: hard — Sr Executor).** Now (authorized on the SIGN): `scripts/landing-telemetry.ps1`; `LandingSourceClaimsTests` re-key + committed negatives; `docs/v2/telemetry.md` §5 pointer; challenge-line-guard must-list row; optional stale-scope-sentence fix naming `/report` — no hero tile. Held for the push-wide go: the first committed snapshot painting the `weeklyActive` tile + Q5 footer (draft PR until the go; tip before undraft).

**Soft: merge this tip when CI green after Helm SIGNs the tip head.** Plan #912 merges `--match-head-commit 6abc1fd2` on green; channel #913 on green.

Live Holds empty. Play Console OFF. Not needs-david (Founder gate already on record — the SIGN enforces it). Claude kick YES Bosun Soft Executor.

---
## 2026-09-25 ~9:24 PM CT — OK / DRA-407 H2 proof — APPLY step 4 on Challenger + Jr

**DRA-407 H2 proof ACCEPTED** (Paperclip confirmation `f360414b` accepted; ops #92 comment [5842348270](https://github.com/DranakCorps-bot/dranakcorps-ops/pull/92#issuecomment-5842348270)). Soft drafted nothing here — this tip records Helm's OK only.

**Gate met.** Throwaway DRA-412 one-run proof (`h2-proof` rev `d6101b40`): adapter `execute()` with `HERMES_HOME=%LOCALAPPDATA%/hermes-review`, `hermesCommand=.../hermes-agent/venv/Scripts/hermes.exe`, `Path`/`PATH` minus `hermes/bin` — exit 0 (~94s); in-run `himalaya` NOT_FOUND; control arm (live seat) FOUND `hermes/bin/himalaya.exe`.

**APPLY step 4** (Sr Executor `c96997d1`): set `hermesCommand` plus env with `HIMALAYA_CONFIG` kept, `HERMES_HOME`, and `Path`/`PATH` (minus `hermes/bin`) on Challenger `ed169d99` and Jr `80e720ca` only; re-GET both; quote on DRA-407. Soft LEAVE inventing root-home edits, EMAIL_* wipe outside the review home, C1 deny, himalaya uninstall, Founder page.

**ADOPT H2** from DRA-396 / ops #92 STANDS. Soft: merge this tip when CI green after Helm SIGNs the tip head.

Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor (tip) + wake_assignee Sr for apply.

---
## 2026-09-25 ~6:36 PM CT — SIGN RECORD / DRA-423 / ops #100 @ `6c7f96ed` — A1 batched Helm wake (A4 enqueue clock; A8 pulse bridge)

**DRA-423 SIGNED** (ops #100 @ `6c7f96ed97bed33fddffa92528b4f38f7903244c`, merged `4bb0dc282ade0179253af59b625a625fea8b154a`), [SIGN comment 5841109927](https://github.com/DranakCorps-bot/dranakcorps-ops/pull/100#issuecomment-5841109927). DRA-415 slice 4 / Part A A1 amendment. The executor drafted this tip and it adds no new ruling beyond Helm's word there.

**SIGN:** `purpose/HELM_BOARD_SYNC_AND_NO_STALE.md` (A1 re-headed + appended Founder HELM ORDER block, 2026-09-25 1:33 PM CT on DRA-415; the 2026-09-16 LOCK rule 1 restatement stays unedited, the amendment block is the operative rule) + `README.md` HELM.md bullet (batched wake, same three off-cycle classes) at `6c7f96ed`.

**ADOPT both judgment calls:** (1) **A4 enqueue clock** — for a batched ask the *enqueue* timestamp is the firing time A4 reads; an off-cycle fire records its own. (2) **Bridge until `helm-notify`** — until DRA-415 slice 1 lands, a pending interaction rides the A8 pending-interactions pulse; the same three off-cycle classes apply unchanged.

**Operative A1:** non-urgent pending interactions enqueue via `helm-notify` (same four fields) and flush at most 9 AM / 1 PM / 5 PM / 9 PM CT. Off-cycle same-turn fire only for (1) needs-david; (2) security; (3) a merge blocked on Helm SIGN >2 hours. Soft LEAVE inventing a second webhook or firing the ops stub copy as a wake.

**Carry-out.** #100 merge applies docs only; `helm-notify` is NOT applied here (stays in DRA-415 slice 1). Merge this tip when CI green after Helm SIGNs the tip head; the DRA-423 confirmation is discharged once both are on `main`.

Live Holds empty. Play Console OFF. Not needs-david.

---
## Live instruments re-pinned — pass 7 (DRA-443, 2026-09-26)

**Read this with passes 6, 5, and 4.** Fourteen dated tips older than the live DRA-409, DRA-440, DRA-379, DRA-407, and DRA-423 records moved, including every 2026-09-24 tip. Pass 6's line that those tips stay live is superseded.

### From **RULE / DRA-396 EXO-HARDEN (d) / ops #92** (2026-09-25 ~1:00 PM CT)

**REJECT H1** (cannot close himalaya via `terminal`). **DEFER C1** until a seat-scoped deny path is verified (not Planner or Reviewer). AGENTS.md no-send / end-at-`in_review` patch applies now; Challenger interim line stays until DRA-398.

### From **SIGN RECORD / DRA-408 / ops #96** (2026-09-25 ~12:40 PM CT)

**Guard conditions (ADOPT, all six):** refuse `cancelled` only when all hold — (1) requested status is `cancelled`; (2) not `allowBoardOverride` (board keeps cancel); (3) execution state is not `completed` (post-review cancel stays allowed); (4) next pending stage is `type: "review"`; (5) actor is the author (`returnAssignee ?? currentAssignee`); (6) actor is not a participant of that stage (Reviewer keeps cancel). System/null actors stay allowed. Refusal is 422 with the drafted message. Marker `/* DRA-408 patch */` (DRA-384 convention).

### From **RULE / DRA-393** (2026-09-25 ~12:05 PM CT)

**RULE (Helm 2026-09-25):** after APPLY and the Hermes restart, set `compression.pin_evidence_patterns` to `["site/*","*.html"]` **ONLY** on review/challenge Hermes seats: today **Challenger**, plus **Reviewer** and **Bevel** whenever those profiles run on Hermes. Every other Hermes profile keeps `[]`. `pin_budget_tokens` stays `16000`. Patch defaults stay `[]`.

### From **COPY SIGN / DRA-363 / #885** (2026-09-24 ~3:09 PM CT)

**MERGE HOLD on #885 @ `edabbffc`.** Stays draft; no undraft, no merge until the launch release David gates (DRA-336 §2 / §3). The HOLD lives on the PR + this tip, not a Live Holds row.

### From **LIFT + SIGN / DRA-373 D2 / #878** (2026-09-24 ~1:30 PM CT)

**CTA RULE STANDS (Founder 2026-09-24 1:08 PM CT via Helm; supersedes 12:15 PM variant-B).** Evolved **coming soon**, **no download button**, **no v1 link** (no `releases/latest`, no tag/channel, no "1.x available today"). Variant A stays FORBIDDEN until a public Evolved installer exists. Soft LEAVE inventing channel/tag/signing from this SIGN. Guard `LandingSourceClaimsTests.TheLandingIsComingSoonAndNeverLinksV1` (+ committed negative). LICENSE footer MIT for published 1.x is licensing, not a download — STANDS.

### From **RULE / DRA-296 / #867** (2026-09-24 ~8:38 AM CT)

**`#738` HOLD — RETIRED.** Its prevented act was merging the dirty tip into live `HELM.md`. The archive append plus close-without-merge discharges that door.

### From **SIGN / DRA-236 / ops PR #79** (2026-09-24 ~2:41 AM CT)

**Q2 — Arm caveat: ACK; no miss.** **ADOPT** as standing Arm-column procedure: read back `executionPolicy.monitor.nextCheckAt` **after** the pass's last write (summary / email), quote value + read time (or `none` + reason). The arm stays out of the verdict (night-7 precedent STANDS).

### From **SIGN / DRA-53 night-14 / ops PR #78** (2026-09-24 ~12:14 AM CT)

**ADOPT — kick script-path rule.** Unattended jobs must not depend on whatever branch a dispatch-lane checkout holds at fire time. Resolve soft-seat scripts from `origin/main` blobs (materialize into a tools dir), record `soft_seat_tools: origin-main | checkout-fallback`, checkout is announced fallback only. Dry-run + prove-fail ACK; night-15 `last-kick.json` is live proof.

**ADOPT — unblock must re-REGISTER.** Auto-blocking the monitor card nulls `executionPolicy` and a blocked card refuses re-arm (422). The unblock pass must re-REGISTER kind + recoveryPolicy + notes (≤500 chars) + nextCheckAt, not merely re-arm. Night-14 repair ACK (armed to `2026-09-25T05:20:00Z`).

### From **DRA-262 implement SIGN** (2026-09-20 ~9:03 PM CT)

- Prior DRA-262 AMEND ACK (~1:40 PM CT / #741) **STANDS**. DRA-252 **KEEP gate 4 LAST STANDS**; does not re-gate v2.0.0. **DRA-272 stays OUT** as signed (fixtures carry `General: Level`).

### From **DRA-209 / PR #699** (2026-09-19)

- **DRA-209 / PR #699** (2026-09-19) — BEVEL-FEEDBACK.md F3 rotation APPROVED. It also
  carries **KEEP: the DRA-144 Helm-only route for later `*-FEEDBACK.md` rotations**, which
  is still live and is named here so archiving the tip does not bury it.

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

## Older rulings moved — [`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md)

**Pass 7 — DRA-443, 2026-09-26.** The passes 1–6 pointer moved verbatim to the archive. `scripts/channel-size-baseline.psd1` unchanged. **32766 B** LF, at or under 32,768 B (was 64,731 B). Dated-tip append 25,123 B, sha `3840c3e29574b36c`.

---


**Helm is chief of staff / COO for this repo.** It rules on operating posture: what is on
hold, what may be said in public and when, what order things happen in, and whether a thing
is ready. It signs Bevel's product rulings and Scribe's public replies.

**Claude / Fable reach Helm without David.** File writes do not wake Helm. After `HELM-FEEDBACK.md` is pushed, run:
`gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane`
(optional `-f reason="HELM-FEEDBACK.md changed"`). URL and key live only as Actions secrets on that private repo, never in this public repo. Helm last-looks, then pages Dranak to run `claude -p` on the local PC if Claude needs a kick. Page David only for a real door.

---

## This file is NOT like the other three inboxes

`SCRIBE.md`, `BEVEL.md` and `FABLE.md` are work queues: take an item, delete it, write a
feedback note. **This one is STATE.** A hold is not work and you never take it — it is a
standing instruction that binds you until Helm lifts it. Nothing here is deleted because it
was "done"; a line leaves the Holds block only when Helm lifts it or when the thing it
prevented has already happened, in which case it moves to Retired.

**It exists because the owner and the maintainer of the holds used to be different people.**
Until 2026-08-22 Helm's holds lived in `SCRIBE.md`, transcribed by Scribe, and on that day all
three of them turned out to describe states that had stopped being true — one had been saying
"do not reply" for four hours after its reporter replied to us. Holds now live where their
author lives. **They are not duplicated anywhere**; `SCRIBE.md` points here.

---

## Holds

**Re-read this block before ANY public reply.** Holds arrive by commit between your pulls, so
"I read it this morning" is not reading it. A hold BINDS you — it is the one place a bot
outranks your standing authority to post routine signed replies (David, 2026-08-22) — and
**only Helm lifts one. A shipped fix does not.**

A HOLD names something we are prevented from doing. If the prevented thing has already
happened, the hold is no longer needed: move it to Retired. Do not leave a live hold that
points at finished work.


Public-reply check-in is process, not a Holds line. New-thread thank-you still comes to Helm.
First-run / "weird flow" findings file on BEVEL.md without waiting on Helm. A public promise of review or a fix still comes to Helm before it posts.

## Wakes and Claude kick

- Helm cannot start Claude. Dranak runs `claude -p` on David's Windows PC, pointed at this repo / HELM.md + HELM-FEEDBACK.md.
- Claude and Fable wake Helm with: `gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane` (optional `-f reason="HELM-FEEDBACK.md changed"`). Secret is not in this repo.
- A GitHub push to HELM-FEEDBACK.md is not a wake unless that POST happens.

## Retired — no longer needed as a hold

Do not put these back in Holds.


- **#208 — lifted for final v1 cut only (2026-09-04).** Owner authorized V0–V1 mobile sounds (opt-in, off by default). Lifting condition: owner final-v1 scope lock 1:14 PM CT. Scoped to this cut — not a standing open for unrelated Wayland chip-monitor work on the same discussion unless separately authorized. Do not put the old do-not-open hold back.
- **#228 — no longer needed.** Helm lifted 2026-08-22 8pm. David ruled star-only is enough
  (the second lifting condition). v1.99.4/1.99.5 restore starred motes automatically;
  never-starred uses Options â†’ Cards & windows. A limit-named player reply is signed for
  Scribe (no victory lap, no "motes are back"). Do not put this back in live Holds.
- **#226 status / follow-up reply gate — no longer needed.** Helm-signed status posted
  2026-08-22. LeBigNasty then said the re-check looks better and repeated the two leftover asks
  (motes out of pack suggestions; client-side ignore). That follow-up lives on the wiki-pack
  motes item. Thread stays open. Leftover Innoruk lore-vs-creature is leftover work, not a hold
  — and it shipped in v1.99.4. **A new #226 draft still comes to Helm (process).**
- **#208 already has a reply** (cosmic-comp, 2026-08-22). Mobile-sounds work was later authorized for the final v1 cut (2026-09-04); see Retired #208 lift. Wayland chip-monitor ask on the same thread is separate.
- **#231 thank-you** posted; PR merged. Never needed its own hold line.

---

The 122 `### PR #…` sign-off entries that used to sit under this heading — 2026-08-24
through 2026-09-06, none of them a hold — moved to
[`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md)
on 2026-09-18 under DRA-154. The five retired-hold lines above did not move and never will.

---

## Item shape, for anything that is not a hold

- **Kind:** `hold` Ã‚Â· `lift` Ã‚Â· `sign-off` Ã‚Â· `priority` Ã‚Â· `posture` (what may be said publicly)
- **Thread / subject:** the discussion number or the thing being ruled on
- **Ruling:** what it is, in Helm's words
- **Condition:** what would change it — *"after a ship that actually restores the card"* is the
  model. **A hold with no lifting condition is one nobody can ever satisfy**, and it is worth
  asking for one.
- **Signed:** Helm, and the date

## What Helm does NOT decide

The [consequence list](CLAUDE.md) is David's, and Helm does not stand in for him on it — the
release go, the values line, money, roadmap direction, privacy. Helm's authority is posture and
sequencing: *when* a true thing is said, and *whether* work starts. If a Helm ruling appears to
settle something on David's list, that is a question for David, not an instruction to follow.

**And a Helm claim about what the CODE contains is a place to look, never a fact** — the same
rule that governs Scribe and Bevel. On 2026-08-22 a Helm ruling was justified with "window
Wealth is coin too" when the window's Wealth tab still drew three blocks. The ruling was right
and its reason was wrong; the executor changed what was asked for and handed the reason back.
