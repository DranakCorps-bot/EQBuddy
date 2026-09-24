## 2026-09-22 ~8:10 AM CT — RULE / DRA-335 / ops PR #72 — make the ADOPT reachable from the unattended-autonomy entry

**Q1 — ADOPT (a).** Update the L958 verdict line to **ADOPT** with a dated pointer to EQBuddy #799 / `69d4b172`, and keep the original ADAPT paragraph beneath it as the adopted content. The #799 ruling ADOPTs *the ADAPT shape already ruled on the first reading (DRA-124 / DRA-100)*; overwriting that paragraph would delete the description of what was adopted. **REJECT (b)** rewrite-in-ADOPT-voice. **REJECT (c)** leave-as-written — a reader of `EXO-PLAYBOOK.md` alone would still see an open ADAPT on an experiment Helm has closed.

**Q2 — ADOPT (a).** Reconcile the codename in the entry: an alias line that the experiment was **also ruled under** `exo-experiment: midnight-planner`. The registered name stays **`unattended-autonomy`** (what DRA-112 registered and DRA-124 / DRA-129 read against). **REJECT (b)** rename the registered entry — that breaks the back-links. **REJECT (c)** amend the `HELM.md` tip instead — the tip's intake-doc codename stands; the playbook entry is the join.

**Hunk C — SIGN as drafted.** The second-reading bullet *"The verdict move is not taken here"* stays **unrevised** (dated record; no retensing). Append the separate **Taken 2026-09-22, elsewhere (DRA-335)** pointer to #799 / `69d4b172` and back to the Verdict line.

**SIGN** ops PR #72 head `2d5fa06b9d22cd31cb42e33001765e253e7f4e81` — all three hunks (alias, verdict line, taken-elsewhere). Soft merge with `--match-head-commit 2d5fa06b9d22cd31cb42e33001765e253e7f4e81`, then verify on `main` by content (new Verdict line and alias line present), not by the merge flag alone. No reopening DRA-129. No third window as a gate. No Play / Desktop / Pages / tag / signing / prod secrets.

Soft: merge this tip when CI green; merge ops #72; discharge Paperclip DRA-335 / pending `4fd6bebf-063b-40b8-8124-6ec99aac51ff`. Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor (tip merge + ops #72).

---
## 2026-09-24 ~12:14 AM CT — SIGN / DRA-53 night-14 / ops PR #78 — first dark night since window 1; kick claim-path fixed; monitor re-registered; DRA-4 parent check retired while blocked

**SIGN** ops `#78` at head `f077268ff6c91eb15c5f4bf6dd0c9f12c6c6a049` (`f077268`), merge only with `--match-head-commit` at that sha; additions-only (+49/−0) on `docs/ops/PAPERCLIP-INTAKE-midnight-planner.md`. Signed on ops `#78` (Helm T2 comment, 2026-09-24 ~12:14 AM CT); this tip records it.

**ACK — night 13 MISS counted.** First always-email miss and first lost night since window 2. Named causes stand: (a) kick `CLAIM_FAIL` — soft-seat scripts resolved through a dispatch-lane checkout on `jr/dra339-fade-chips-toggle` @ `a68d2afc` (scripts MISSING on that commit); (b) 00:20 backstop into claude-fable-5 weekly quota (DRA-100 cause class STANDS; second occurrence 09-16 / 09-23; confirming reads only).

**ADOPT — kick script-path rule.** Unattended jobs must not depend on whatever branch a dispatch-lane checkout holds at fire time. Resolve soft-seat scripts from `origin/main` blobs (materialize into a tools dir), record `soft_seat_tools: origin-main | checkout-fallback`, checkout is announced fallback only. Dry-run + prove-fail ACK; night-15 `last-kick.json` is live proof.

**ADOPT — unblock must re-REGISTER.** Auto-blocking the monitor card nulls `executionPolicy` and a blocked card refuses re-arm (422). The unblock pass must re-REGISTER kind + recoveryPolicy + notes (≤500 chars) + nextCheckAt, not merely re-arm. Night-14 repair ACK (armed to `2026-09-25T05:20:00Z`).

**RULE — DRA-4 / ruling 2 parent sweep.** **RETIRE** ruling 2's parent-verifies-child check as a required nightly liveness layer **while DRA-4 remains blocked** (`monitorNextCheckAt` frozen at `2026-09-17T05:40:00Z` since 09-17). Soft LEAVE inventing unblocking DRA-4 from this tip. Stack until DRA-4 is next eligible: kick + DRA-53's own monitor only. When DRA-4 next becomes `in_progress`/`in_review`, Soft re-REGISTERS the 00:40 monitor (same unblock-must-re-REGISTER rule) — that restores ruling 2.

**ACK — Q6 readings moved (no verdict).** `unattended-autonomy`: 09-23 MISS / 09-24 success recorded pending DRA-236's table. `jr-sr-router`: Jr-routed runs exist; zero delivered output on first two — pass bar stays DRA-179 D4.

**ACK — carry-out stall (night-12 watch).** Ops `#72` SIGNed 2026-09-22T13:11Z still OPEN; EQBuddy `#757`/`#738` still OPEN. Lesson: a carry-out card is only as alive as its assignee's runtime.

**Soft carry-out HIGH/NOW:**
1. Merge this tip when CI green; then merge ops `#78` @ `f077268ff6c91eb15c5f4bf6dd0c9f12c6c6a049` with `--match-head-commit`.
2. Rebase-then-merge stuck tip `#829` (CI green, CONFLICTING) then ops `#72` @ `2d5fa06b9d22cd31cb42e33001765e253e7f4e81` with `--match-head-commit` (SIGN STANDS).
3. Rebase/merge or tip-drop other stuck SIGNed tips (`#840`, `#824`, `#795`) in dependency order; tip-drop/close `#757` if night-11 content is already on `main` or superseded; **`#738` HOLD STANDS** — Soft LEAVE inventing merge while that HOLD stands.

Live Holds empty. Play Console OFF. Not needs-david. No Founder page. Claude kick YES Bosun Soft Executor.

---

## 2026-09-23 ~8:28 PM CT — SIGN / DRA-355 / #860 — DRA-352 D2+D3 Options trim; D2.3 departure ADOPTed

**SIGN** PR #860 (`opus/dra355-impl` @ `2644da23`) — DRA-355 implements DRA-352 **D2 + D3** (Options → Cards & windows + Alerts & chips). Signed on #860 [comment 5805821787](https://github.com/DranakCorps-bot/EQBuddy/pull/860#issuecomment-5805821787) (2026-09-24T01:28Z). Merge when `build-and-test` + `e2e-windows` green (at look: build SUCCESS; e2e IN PROGRESS). Soft merges with `--match-head-commit` at `2644da2362487888faad5e7a8b33e1b6a6a86c86` (re-tip if head moves).

**Helm LOCK D2.2 ACK.** Retired `OverlaySections` DROP with its surface (Founder 2026-09-23; tipped in plan). `CLAUDE.md` three-ways-back records the subtracted-card arm as retired. Context-menu doors unchanged. Spot-checked pin writer (`BreakoutAutoOpen`), ✕ transient close (OE-7), traps 20/26.

**D2.3 departure — ADOPT (HOLD declined).** Plan text said chip-open clears `DisabledBreakouts`. Executor did not implement that. **RULED:** with the pin as the sole durable writer of auto-open, a summon that also wrote would make every chip PEEK a lasting edit — the permanence OE-7 removed from the double-click. Chip summon stays peek-only; ✕ stays transient close; **pin is the only re-enable path**. Soft: on a later docs touch, amend the D2.3 plan sentence so "opening clears disable" is not read as live (Executor note already states the departure; this tip RULES it). A HOLD to put plan wording back is **declined**.

**D3 ACK.** Founder screenshot cuts: banner/voice paragraphs + Options Buff-set editor removed (Buffs window remains the editor); Track-spawns / mez how-tos → ⓘ; one EQLWiki line under Mez durations; rows / `MezDurationsView.Commit` kept.

**Out of scope.** D4+; Play Console / signing / prod secrets; Desktop republish.

**Soft:** merge #860 when both CI green; then merge this tip when CI green. Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor (merge when green).

---

## 2026-09-23 ~6:20 PM CT — SIGN / DRA-336 / #856 — TEL for launch (TEL-001 amendment + PR sequencing)

**SIGN** `docs/plans/DRA-336.md` (TEL for launch — TEL-001 amendment + PR sequencing). Signed on #856 [comment 5804558295](https://github.com/DranakCorps-bot/EQBuddy/pull/856#issuecomment-5804558295) (2026-09-23T23:19Z); this tip records it. Soft drafted it and it adds no new ruling.

**Carriers:** LIVE ASK DRA-364 / PR #856 (merged `54830220`); umbrella DRA-336; walk DRA-358; plan landed via #849.

**Walk accepted:** `challenge: dra-336-tel-launch-amendment -> PROCEED-WITH (C1) as of 2026-09-23`. Condition **C-1 is binding:** one human read beyond authorship (Founder or Helm) of the consent copy (TEL-A) **before TEL-PR1 merges**. Do not merge TEL-PR1 without that recorded read on the PR.

**What this SIGN authorizes**
1. **TEL-A** (DRA-359, Bevel) — consent copy first; already underway.
2. **TEL-PR1** + **TEL-PR2** (DRA-360/361, Sr, `route: hard`) — parallel after this SIGN; each still needs its own green CI + pre-merge last-look where the slice PR asks for one.
3. **TEL-PR3** (DRA-362) after TEL-A + TEL-PR1 + TEL-PR2.
4. **TEL-PR4** (DRA-363) rides the launch release David already gates; Helm signs that public copy separately (consequence item 3).

**Locks that stand (Founder AUTHORIZE 2026-09-22 + plan §1/§5)**
- Off by default forever until the player says yes; decline (Esc / ✕ / Not now) is the default action.
- Prompt fires once per install; no nag on update; Options toggle is the only way back in.
- No dark pattern; payload frozen at TEL-002's three fields; TEL-006 scope freeze (no crash/events).
- No Play Console; no on-by-default; no payload beyond TEL-002.
- LEGACY-V1 "nothing phones home" stays true forever.

**HOLD SIGN on DRA-337 / #847 — RETIRED.** Challenger returned on this lane; #847 closed unmerged as duplicate of DRA-336. Do not reopen #847 for SIGN.

**Out of scope under this SIGN.** No release/tag/channel-open. No Play Console / signing / prod secrets. No paid backend tier (money door, asked when real). No implement before TEL-A arrives for PR3's copy, and nothing implements TEL-PR1/PR2 until Soft seats Sr on green cards.

**Soft:** merge this tip when CI is green; discharge DRA-364 / unblock DRA-360+361; LOOP CLOSED the DRA-364 LIVE ASK once the tip is on `main`. Live Holds empty. Play Console OFF. Not needs-david.

---

## 2026-09-23 ~5:44 PM CT — ACK / process — D5a stands; WorldEra Classic (Epics VACATED); Researcher-first

**ACK.** Founder process LOCK 2026-09-23 ~5:44 PM CT (broadens the ~5:40 WorldEra Researcher-first). Soft drafted this tip; it adds no new ruling. Helm SIGNs; Soft merges.

**D5a ADOPT Ask 1 stands** (era → band → who before the sweep cap).

**WorldEra Classic STANDS.** Ask 2 Epics ADOPT on #854 is **VACATED**. Founder word = **Classic** (`QuestEraLadder` spelling), not an Epics gloss. Soft LEAVE inventing an Epics D5 or merging an Epics tip. `WorldEra.Current` stays Classic until Researcher or Founder updates it. eqlwiki is not the world-clock source; Researcher keeps the curated WorldEra. Founder ~5:49 CT: Classic STANDS; Epics content startable but not finishable (not a ladder move to Epics). Soft LEAVE inventing blocking D5 for this — P4 already answered Classic.

**Researcher-first.** Researcher owns any fact a simple online search can settle — that is the role's purpose. Planner must factor Researcher into plans for those asks (route Researcher wake / Soft lookup before Helm or Founder). Soft LEAVE inventing Founder mailbox or chat for searchable facts. Founder only for judgment, spend, and true ambiguity.

**Out of scope.** No Play, Desktop republish, or Founder page. No inventing WorldEra beyond Classic.

**Soft:** merge this tip after Helm SIGNs, when CI is green. Live Holds empty. Play Console OFF. Not needs-david.

---

## 2026-09-23 ~5:19 PM CT — ACK / DRA-180 D5 — P4 WorldEra answered Classic; D5 BLOCK LIFTED

**ACK.** Founder P4 answered 2026-09-23 ~5:19 PM CT (Helm chat): **WorldEra.Current = Classic** (`QuestEraLadder` spelling). **Source** = the Founder statement. eqlwiki is not the world-era source (EQ Legends ≠ EQ Live). Researchers keep the curated WorldEra (Kunark expected December).

**D5 BLOCK LIFTED.** The pass-4 re-pin from DRA-180 D3 / #694 — D5 BLOCKED until the P4 Founder one-word WorldEra is answered on Helm's mailbox cadence — is **spent**. Soft implements D5: set `WorldEra.Current="Classic"` and a Source citing Founder 2026-09-23, not eqlwiki. Sr / never-Qwen for this curated fact.

**#848.** ACK is already on [comment 5803772277](https://github.com/DranakCorps-bot/EQBuddy/pull/848#issuecomment-5803772277) (WorldEra ask re-file into `HELM-FEEDBACK.md`, additions-only). Merge when CI is green at head `e4f1186e`. Measured: already merged at that head — do not re-merge.

**HOLD SIGN** on DRA-337 / #847 until Challenger returns. Do not self-SIGN.

**Out of scope.** No WorldEra beyond Classic, no eqlwiki harvest, no Play / Desktop republish, no Founder page.

Soft drafted this tip; Helm SIGNs; Soft merges. Live Holds empty. Play Console OFF. Not needs-david.

---

## 2026-09-23 ~10:29 AM CT — SIGN / DRA-351 / ops PR #77 — DRA-349 implement: SPEC §6 ruling line + challenger-seat evidence 4/10 (Reading A)

**SIGN** the DRA-349 implement at head `b2046f4bb38378bad87ef4d837dffae063242b19` (`b2046f4b`), merge only with `--match-head-commit` at that sha; no amend, rebase, or force-push after this SIGN. This is the pre-merge T2 SIGN the ops `#76` RULE tip owed. Signed on ops `#77` [comment 5797695814](https://github.com/DranakCorps-bot/dranakcorps-ops/pull/77#issuecomment-5797695814) (2026-09-23T15:29Z); this tip records it. Soft drafted it and it adds no new ruling.

**Scope check against DRA-349 Reading A (ops `#76` RULE comment 5797472993): pass.** (1) `purpose/CHALLENGER_PROCESS_GATE_SPEC.md` §6 gets the Reading A accrual line (clock 4/10; a Founder charter amendment supersedes). (2) In the `EXO-PLAYBOOK.md` challenger-seat evidence bullet, "not ruled" is replaced with **Four count toward the ten**, citing that RULE. (3) The opening cite is bumped to `61d7cc96` / `ad9364d7` with italic history (standing double-write).

**Out of scope, confirmed clean.** Charter bytes are untouched. Nothing changes in `CURRENT_ORG.md`, the router, D3/D4, the DRA-301 baseline, seat config, the EQBuddy product, Play, Desktop, signing, or prod secrets. The kill-criterion bullet still reads "ten C1–C4 wakes", with §6 now glossing the phrase. That was flagged, not ordered.

**Walk.** `challenge: dra349-c5-kill-bar -> PROCEED-WITH as of 2026-09-23` was already folded at plan. No new Challenger walk is owed on this carry-out.

**Soft:** merge this tip when CI is green; then merge ops `#77` at `b2046f4bb38378bad87ef4d837dffae063242b19` with `--match-head-commit`; then discharge Paperclip DRA-351 pending confirmation `6ee3b594`. Live Holds empty. Play Console OFF. Not needs-david. No Founder page.

---

## 2026-09-23 ~10:16 AM CT — RULE / DRA-349 / ops PR #76 — a C5 Challenger wake counts toward charter §8's ten-wake kill bar

**RULE: Reading A — accrual.** Charter §8's "ten C1–C4 wakes" means ten gate wakes under the **ADOPTed live trigger set**, which is now C1–C5 (SPEC §3, Helm ADOPT 2026-09-21). **Clock: 4/10** (DRA-299 C2, DRA-306 C2, DRA-315 C2, DRA-346 C5). **REJECT Reading B** — no separate C5 counter, no second kill ledger for the same experiment. Plan SIGNed at head `d8b3eaadc9e6985d1adb5cea9b59e3b648e8bb07` (`d8b3eaa`). Ruled on ops `#76` [comment 5797472993](https://github.com/DranakCorps-bot/dranakcorps-ops/pull/76#issuecomment-5797472993) (2026-09-23T15:16Z); this tip records it — Soft-drafted, no new ruling.

**Charter bytes untouched.** Interpretation only, recorded downstream (SPEC §6 metric block + `EXO-PLAYBOOK.md` challenger-seat entry); `CHALLENGER_ROLE_CHARTER.md` is not amended and §§2–5/§9 stay Q6-frozen. A Founder-SIGNed charter amendment restating §8 would supersede.

**Walk.** `challenge: dra349-c5-kill-bar -> PROCEED-WITH as of 2026-09-23` (DRA-350); conditions folded at rev 2 — no overrule.

**Soft:** merge this tip when CI green; merge ops `#76` at `d8b3eaadc9e6985d1adb5cea9b59e3b648e8bb07` with `--match-head-commit` (plan record only); discharge Paperclip DRA-349 pending confirmation `c072842c`. Planner seats Sr for the implement PR (SPEC §6 metric line + playbook evidence retense); Helm pre-merge T2 SIGN still owed at that head. Live Holds empty. Play Console OFF. Not needs-david. No Founder page.

---

## 2026-09-23 ~9:16 AM CT — SIGN / DRA-347 / ops PR #74 — T2 implement: register `exo-experiment: challenger-seat`

**SIGN** the DRA-345 implement at head `5684e75c7e29c3d59fb80a80c817c7781e17a50c` (`5684e75`), merge only with `--match-head-commit` at that sha. This is the separate pre-merge T2 SIGN the ops `#73` tip (plan @ `178dbb1`) owed. Signed on ops `#74` [comment 5796476747](https://github.com/DranakCorps-bot/dranakcorps-ops/pull/74#issuecomment-5796476747) (2026-09-23T14:16Z); this tip records it — Soft-drafted, no new ruling.

**Scope check against the `#73` SIGN — pass.** `EXO-PLAYBOOK.md` entry after `jr-sr-router` restating SPEC §6/§7 D1+D2; **walk condition (C5 / DRA-346) folded** — citation line names SPEC commit `3b2dbf8c` (blob `79247a51`), registered 2026-09-23 on DRA-345. `CURRENT_ORG.md` Paperclip seats (presence-not-content) + D7 first-use qualify + header date. `purpose/README.md` points, does not restate. SPEC status header retense only; D3/D4 untouched.

**Out of scope — confirmed clean.** No D3/D4 router insert, no EQBuddy file, no second Challenger seat, no Play / Desktop / signing / prod secrets, charter §§2–5/9 untouched.

**P2 — KEEP.** Model-lock table stays; live verification passed (Planner `claude-fable-5`, Jr `qwen3.8:27b`, Sr `claude-opus-5-5` live — lock copy still reads 5.1 after the 2026-09-23T11:03Z board flip).

**ACK, not ruled:** whether a C5 wake counts toward the charter's ten-C1–C4-wake kill bar — its own card. **ACK, out of scope:** SPEC body relay corruption at prohibition sites — its own A6 repair card; not edited under this SIGN.

**Conflict.** Ops `#72` (DRA-335) still open on a different playbook span. If `#72` lands first, rebase `#74` and re-ask for SIGN at the new head; no merge past a dirty rebase.

**Soft:** merge this tip when CI green; then merge ops `#74` at `5684e75c7e29c3d59fb80a80c817c7781e17a50c` with `--match-head-commit`; discharge Paperclip DRA-347 pending confirmation `cea69de`. Live Holds empty. Play Console OFF. Not needs-david. No Founder page.

---

## 2026-09-23 ~8:50 AM CT — SIGN / DRA-345 / ops PR #73 — ExO alignment plan: surface Challenger on Corps ops

**SIGN this plan** at head `178dbb110e05efd56eb5c27260fbe83439c2d954` (`178dbb1`) as the authorized shape for the DRA-345 implement PR. Plan-only PR; nothing from `#73` lands in live docs. Implement is a separate Sr PR (`route: hard -> Sr (T1)`), still behind its own Helm pre-merge T2 SIGN.

**Walk.** Challenger returned `challenge: dra345-exo-align -> PROCEED-WITH (C5) as of 2026-09-23` on DRA-346 comment `c7dd2423` (2026-09-23T13:36Z). One condition — playbook entry cites the SPEC revision it restates (implement-time SPEC commit hash + DRA-345 date) — is folded into plan §3.1 at this head. No overrule. No reopening the walk.

**Force question (§2) — ADOPT as written.** Gate semantics in force since ops `#58` (`9c8faf51`) + DRA-305; **D1/D2/D5 discharged by the implement PR under this SIGN** (cite this tip); D3/D4 untouched, still gated on the D4 remeasure; SPEC stays sole C1–C5 authority; EQBuddy `CLAUDE.md` stays a pointer (untouched on this card). Retense the SPEC header in the implement PR so public force matches Soft-loop.

**Implement scope under this SIGN (one Sr PR, four files):**
1. `EXO-PLAYBOOK.md` — register `exo-experiment: challenger-seat` (DRA-291/DRA-304, registered on DRA-345) after `jr-sr-router`, restating SPEC §6/§7 D1+D2 so §10.4 is dischargeable from the entry alone; **citation line binding** (walk condition).
2. `CURRENT_ORG.md` — Paperclip seats section (Planner / Sr / Jr / Challenger role under Planner, presence-not-content); D7 qualify A′ seat-mutex "challenger" on first use; bump header date.
3. `purpose/README.md` — index charter + gate SPEC + pointer to DRA-302 §6.4 subset.
4. `purpose/CHALLENGER_PROCESS_GATE_SPEC.md` — §2 header retense only.

**Optional P2 — KEEP** as its own droppable commit: model-lock rows (Jr = Qwen 3.8-27B, Sr = Opus 5.5, Planner = Fable CLI) verified at implement time against live `adapterConfig` and founder-lock copies — not from plan memory. Soft may drop the commit only if live verification fails; no inventing locks from this tip.

**Explicitly out of scope.** No D3/D4 router insert. No EQBuddy file. No second Challenger seat. No Play / Desktop / signing / prod secrets. No charter §§2–5/9 edits. No Corps-wide lease/scheduler. SPEC D6 (no verdict named `HOLD`) and D7 (qualify every "challenger" on first use) bind.

**Conflict note.** Open ops `#72` (DRA-335) edits a different EXO-PLAYBOOK span; implement branch from tip and rebase if `#72` lands first. `#73` itself is one new plan file — mergeable now.

**Soft:** merge EQBuddy this tip when CI green; then merge ops `#73` at head `178dbb110e05efd56eb5c27260fbe83439c2d954` with `--match-head-commit`; discharge Paperclip DRA-345 pending confirmation `b42bea22` / LIVE ASK; Planner seats Sr for the implement PR under this SIGN (separate pre-merge SIGN still owed). Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor (tip merge + ops `#73` merge).

Founder AUTHORIZED the EXO-HARDEN align card 2026-09-23 ~8:11 AM CT; this SIGN discharges that plan door. No Founder page.

---

## 2026-09-23 ~8:44 AM CT — SIGN / DRA-146 / PR #836 — RETIRE HANDOFF.md (a)

**SIGN (a) RETIRE.** Root `HANDOFF.md` (248,286 B, blob `026b6265f91b…`, last touched `c821ddda` 2026-08-31) leaves the live channel. The working handoff is Paperclip cards + wake payloads (DRA-26 plan rev 3 §2). The Founder's 2026-09-21 bar was Planner-proposes / Helm-last-looks; this is that last-look. No Founder page.

**REJECT (b) KEEP.** `HANDOFF.md` does not join the card-B rotation set. A 248 KB root file that no live seat reads adds rotation work and gives nothing back.

**Carry-out, in order.** (1) Soft merges this tip after Helm SIGNs this tip PR. **No move PR merges before that.** (2) A separate PR makes a verbatim, byte-safe move of `HANDOFF.md` → `docs/ops/claude-archive/channels/2026-Q3/HANDOFF-legacy.md`. No bytes are deleted from the moved content, and a one-line pointer stays at the old path. (3) CI is green, including `channel-wipe-guard.ps1`. (4) The CLAUDE.md trap-list citation and the DRA-26 §5 authority line that cite `HANDOFF` survive through the pointer. Read-only `DECISIONS.md` history and archive copies stay as history. (5) Soft discharges Paperclip DRA-146 once the tip and the move are both on `main`.

**Out of scope.** No Play Console, Desktop, signing, prod secrets, Pages, tag, harvest, Founder page or `src/` product invention. No reopening DRA-26 authority wording beyond the surviving citations.

Ruling: [PR #836 comment](https://github.com/DranakCorps-bot/EQBuddy/pull/836#issuecomment-5795964873). Soft drafted this tip on that order; Helm SIGNs, Soft merges. Live Holds empty. Play Console OFF. Not needs-david.

---

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

## Older rulings moved — [`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md)

**Every ruling this file no longer carries is in the archive, verbatim, and nothing was
deleted.** Four passes so far, all APPEND-only:

- **Pass 1 — DRA-154, 2026-09-18.** Moved 144 dated tips and 134 older `### ` sign-off
  entries, because the file was 1,047,518 B against a 64 KiB policy and past the
  grandfather band `scripts/channel-size-guard.ps1` measures.
- **Pass 2 — DRA-230, 2026-09-20.** Moved the 5 discharged tips dated 2026-09-18 to
  2026-09-19 named below. **91,642 B → 48,639 B** LF-normalised UTF-8, the unit the guard
  measures, against the 65,536 B ceiling — 16,897 B of headroom, and the HELM.md
  grandfather row left `scripts/channel-size-baseline.psd1` in the same PR. The archive
  grew 1,039,255 B → 1,082,908 B (append, +43,653 B); its 144 pass-1 entries were
  re-read byte-for-byte afterwards and are untouched.

- **Pass 3 — DRA-154 / DRA-267, 2026-09-21.** Moved two discharged 2026-09-20 tips
  named below (DRA-267 / PR #745 RULED+AMENDED; DRA-53 night-10 / ops #10 #12 #35 RULED).
  Night-11 carry-out closed #10+#35 and merged ops #12, so those tips are history.
  **64,730 B → 47,297 B** LF-normalised UTF-8 against the 65,536 B ceiling (18,239 B
  headroom) so night-11 tip PRs #753/#754 can rebase under the ceiling. The archive
  grew 1,082,908 B → 1,101,120 B (append, +18,212 B); pass-1 and pass-2 bytes were
  re-read as a prefix and are untouched. scripts/channel-size-baseline.psd1 is
  UNCHANGED: HELM.md has carried no grandfather row since DRA-154, so adding one is
  forbidden (rows only ever leave; three-file form is row-conditioned).

- **Pass 4 — DRA-277, 2026-09-21.** The DEEP cut DRA-144 asked for: **all six** remaining
  dated tips (2026-09-18 ~11:35 PM CT through 2026-09-21 ~12:06 AM CT) moved, and the live
  instrument each one carried re-pinned verbatim at the top of this file instead of the
  whole tip being held. **56,021 B → 18,697 B** LF-normalised UTF-8 against the
  65,536 B ceiling. The archive grew 1,097,452 B → 1,141,999 B (append, +44,547 B, the
  moved bytes exactly). `scripts/channel-size-baseline.psd1` is UNCHANGED and this PR
  changes exactly two files: HELM.md has carried no grandfather row since DRA-154, so the
  row-conditioned rotation shape here is the TWO-file one and re-adding a key would be a
  check-B red.

- **Pass 5 — DRA-154, 2026-09-23.** All nineteen dated tips from 2026-09-21 ~5:22 AM CT
  (DRA-287 / PR #766) through 2026-09-22 ~12:38 AM CT (DRA-110 / PR #797) moved; the
  2026-09-23 DRA-345 SIGN stays as the current tip and the three still-open instruments
  those tips carried are re-pinned verbatim in **Live instruments re-pinned — pass 5**
  at the top of this file. **63,065 B → 30,266 B** LF-normalised UTF-8 against the
  65,536 B ceiling — at or under the 32,768 B arm-(c) discharge floor (DRA-282 Q2).
  The archive grew 1,146,637 B → 1,186,872 B (append, +40,235 B: the moved bytes plus its
  pass marker); prior-pass bytes were re-read as a prefix and are untouched.
  `scripts/channel-size-baseline.psd1` is UNCHANGED: HELM.md has carried no grandfather
  row since DRA-154 pass 2, so the row-conditioned rotation shape here is the TWO-file
  one and re-adding a key would be a check-B red.

An archived line is history: it never revives a hold and it never commissions work.

**What did NOT move, at any age:** the Holds block, the Wakes and Claude-kick block, the
Retired-hold lines, the item shape and what Helm does not decide — all below. The Holds
block is empty and that is a live fact, not an omission.

**What pass 2 HELD LIVE past its own cutoff, because each is the only live home of
something still open** — a pass that eats a live ruling has failed:

- **DRA-216 D7 / PR #709** and **DRA-216 / PR #705** (2026-09-19) — #705 is the **PARK of
  S8/S9** and the **SIGN of D1–D6 (DRA-217..222)**, and D2–D6 are still in flight.
- **DRA-180 D3 / PR #694** (2026-09-18) — **D5 is BLOCKED on P4, which stays DEFERRED**
  (WorldEra ABSENT until the Founder word), and the `dra175-rotate` RE-KICK it issued is
  still **undischarged**. Nothing dated later discharges either.

**What pass 3 HELD LIVE past its own cutoff:**

- **DRA-262 packet-complete ACK** (2026-09-20 ~8:36 PM CT) — D2 still in flight after D1 #752.
- **DRA-241 / PR #724** (2026-09-20 ~3:05 AM CT) — Sr product proc slice still owed.
- The three pass-2 holds above (DRA-216 D7 / #709, DRA-216 / #705 PARK S8/S9, DRA-180 D3).

**What pass 3 moved (both discharged in their own words):**

- **DRA-267 / PR #745** (2026-09-20 ~5:10 PM CT) — ADOPT (a) landed via #749.
- **DRA-53 night-10 / ops #10 #12 #35** (2026-09-20 ~12:10 AM CT) — night-11 carry-out closed #10+#35 and merged #12.

**What pass 4 moved (all six; each tip's live instrument is re-pinned at the top):**

- **DRA-53 night-11 / ops #10 #12 #35** (2026-09-21 ~12:06 AM CT) — carry-out discharged.
- **DRA-262 packet-complete ACK** (2026-09-20 ~8:36 PM CT).
- **DRA-241 / PR #724** (2026-09-20 ~3:05 AM CT).
- **DRA-216 D7 / PR #709** (2026-09-19 ~7:49 PM CT).
- **DRA-216 / PR #705** (2026-09-19 ~5:03 PM CT).
- **DRA-180 D3 / PR #694** (2026-09-18 ~11:35 PM CT).

**Pass 4 moved every tip the pass-2 and pass-3 held-live lists above name**, so those two
lists are a record of what pass 2 and pass 3 did and no longer tell you where to read a
live instrument. The instruments themselves — the #705 PARK of S8/S9, the D1–D6 SIGN, the
D7 §7.1–7.3 KEEPs, the DRA-180 BOUNDARY KEEP, the D5-on-P4 block, the undischarged
`dra175-rotate` RE-KICK, the DRA-241 ADOPT (a) and the DRA-262 KEEP gate 4 LAST — are
re-pinned verbatim in **Live instruments re-pinned — pass 4** at the top of this file.
Read the re-pin for what binds you and the archive for why.

**What pass 5 moved (DRA-154, 2026-09-23):** all nineteen dated tips from 2026-09-21
~5:22 AM CT (DRA-287 / PR #766) through 2026-09-22 ~12:38 AM CT (DRA-110 / PR #797),
each verified discharged in its own words or re-pinned in **Live instruments re-pinned
— pass 5** at the top of this file. The DRA-325 file-wide relay note moved with the
night-12 tip it is appended to; its "every dated entry below this one, down to and
including the `DRA-232 / PR #719` re-pin" span now reads across the archived entries
below it there, EXCEPT that the `DRA-232 / PR #719` re-pin (item (d)) it repaired stays
LIVE in this file's pass-4 block — the note's per-site table cites commits, so nothing
in it depends on position.


`## 2026-09-19 ~5:03 PM CT` (the #705 PARK) reached this pass glued to the end of the D7
tip as `---## 2026-09-19 …`, with no line break, so it was invisible to every heading
reader including `channel-rotate.py` — a standing PARK that no rotation could see or
name. Pass 2 inserted the missing break and changed no word.

**The live rulings the top tip rests on, and where to read them in full:**

Since pass 4 the top of this file is the **Live instruments re-pinned** block rather than a
dated tip; "the top tip" below means that block, and each ruling named here is re-pinned in
it or in the archive.

- **PR #685 / DRA-180 + DRA-181, SIGNED 2026-09-17 ~9:10 PM CT** — the whole-sequence
  authorization this file's top tip says STANDS: D1 → D2 → D3 → D5 on green gates, D4
  disjoint-parallel-eligible, P1–P5 ADOPTed, WorldEra ABSENT until the Founder word.
- **PR #684 / DRA-179, SIGNED 2026-09-17 ~8:55 PM CT** — Jr/Sr `route:` tags are LIVE,
  an untagged delivery fails closed to Sr, and the ten banned-Jr surfaces bind.
- **DRA-175 second-rotate, RULED 2026-09-17 ~6:40 PM CT** — the append-safe FABLE-only
  patch, and the `dra175-rotate` AUTHORIZE the top tip re-kicks.
- **PR #663 / DRA-164 and PR #649 / DRA-149** — the two older sequences neither this
  rotation nor the top tip marks PASS.

**What pass 2 moved, and the one standing rule that went with it.** All five say LIVE ASK
**discharged** or pending **cleared** in their own words; read any of them in the archive:

- **DRA-209 / PR #699** (2026-09-19) — BEVEL-FEEDBACK.md F3 rotation APPROVED. It also
  carries **KEEP: the DRA-144 Helm-only route for later `*-FEEDBACK.md` rotations**, which
  is still live and is named here so archiving the tip does not bury it.
- **DRA-201** (2026-09-19) — Jr Executor `process` → `hermes_local`, model KEEP Qwen
  3.8-27B. Its carry-out is restated in the #705 re-pin at
  the top of this file (pass 4 moved the tip itself).
- **DRA-179 D1 / ops PR #38** (2026-09-19) — the `exo-experiment: jr-sr-router`
  registration; the doctrine itself lives in the ops `EXO-PLAYBOOK.md` and its sequence
  AUTHORIZE in PR #684 above.
- **DRA-196 / PR #695** (2026-09-18) — arm (b) APPROVED, arm (c) REJECTED inside DRA-196
  and re-planned as its own card.
- **Paperclip ACTION NEEDED cleared** (2026-09-18) — DRA-186 APPROVE → Founder; DRA-178
  APPROVE merge #683.

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
