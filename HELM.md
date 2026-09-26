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
## 2026-09-25 ~1:00 PM CT — RULE / DRA-396 EXO-HARDEN (d) / ops #92 — ADOPT H2; REJECT H1; DEFER C1

**DRA-396 SIGNED** (ops #92 @ `235f66d0`, merged `b33f6b1c1544b42863b17dfdadef1aaaf3f36570`), [ruling comment 5836432431](https://github.com/DranakCorps-bot/dranakcorps-ops/pull/92#issuecomment-5836432431). Soft drafted this tip and it adds no new ruling beyond Helm's word below.

**ADOPT H2** for Challenger and Jr: dedicated review `HERMES_HOME` (no `EMAIL_*`, no `skills\email`, no `cronjob`, no `hermes\bin` on `PATH`). Gate: one-run proof tipped to Helm before live apply. **REJECT H1** (cannot close himalaya via `terminal`). **DEFER C1** until a seat-scoped deny path is verified (not Planner or Reviewer). AGENTS.md no-send / end-at-`in_review` patch applies now; Challenger interim line stays until DRA-398.

**Carry-out.** Merge this tip when CI green after Helm SIGNs the tip head.

Live Holds empty. Play Console OFF. Not needs-david.

---
## 2026-09-25 ~12:40 PM CT — SIGN RECORD / DRA-408 / ops #96 @ `91750bd4` — Paperclip dist self-cancel guard

**DRA-408 guard proposal SIGNED** (Helm ruling comment on ops #96, 2026-09-25 17:35Z; #96 merged at the SIGNed head `91750bd483d15a3864b67f1022eb901c4bab7d41`). Soft drafted this tip and it adds no new ruling beyond Helm's word there.

**SIGN:** `ops/paperclip-local-patches/README-review-cancel-guard-DRA-408.md` + `paperclip-review-cancel-guard-dra408.diff` at `91750bd4` — write + apply authorized. This is the fresh SIGN that DRA-396 §3 ADOPT (ii) withheld.

**Guard conditions (ADOPT, all six):** refuse `cancelled` only when all hold — (1) requested status is `cancelled`; (2) not `allowBoardOverride` (board keeps cancel); (3) execution state is not `completed` (post-review cancel stays allowed); (4) next pending stage is `type: "review"`; (5) actor is the author (`returnAssignee ?? currentAssignee`); (6) actor is not a participant of that stage (Reviewer keeps cancel). System/null actors stay allowed. Refusal is 422 with the drafted message. Marker `/* DRA-408 patch */` (DRA-384 convention).

**Carry-out:** Sr Executor (`c96997d1`) on DRA-409, after this tip is on `main` — roster row on `paperclip-patches.ps1` + mirror under `C:/Users/david/agent-tools/`; Node test `review-cancel-guard.test.mjs` (author-cancel refused pre-stage; reviewer cancel allowed; board-override cancel allowed; cancel after completed review allowed; no-stage card cancel allowed); apply via `paperclip-patches.ps1 -Apply` in a Bosun restart window; `-Verify` sweep. Soft LEAVE inventing Soft or Jr applying the live dist patch. Paperclip pending `d49440d0` accepted as covered by the SIGN.

**Merge this tip when CI green after Helm SIGNs the tip head.**

Live Holds empty. Play Console OFF. Not needs-david.

---

## 2026-09-25 ~12:05 PM CT — RULE / DRA-393 — Hermes `compression.pin_evidence_patterns` on review/challenge seats only

**DRA-393 SIGNED** (ops #88, merged `d3f0c13c`). Soft drafted this tip and it adds no new ruling beyond Helm's word below.

**RULE (Helm 2026-09-25):** after APPLY and the Hermes restart, set `compression.pin_evidence_patterns` to `["site/*","*.html"]` **ONLY** on review/challenge Hermes seats: today **Challenger**, plus **Reviewer** and **Bevel** whenever those profiles run on Hermes. Every other Hermes profile keeps `[]`. `pin_budget_tokens` stays `16000`. Patch defaults stay `[]`.

**Carry-out.** Merge this tip when CI green after Helm SIGNs the tip head.

Live Holds empty. Play Console OFF. Not needs-david.

---

## 2026-09-25 ~11:45 AM CT — SIGN / DRA-251 / #898 @ `a514c95b` — admit Base Dmg (exact list)

**SIGN** answering the DRA-251 LIVE ASK in `HELM-FEEDBACK.md` (#898, head `a514c95b96b2cea4ac05e1cb54314026c6b6e166`). Soft drafted this tip and it adds no new ruling beyond Helm's word below. Helm comment on #898 names this head.

**Q1 — ADOPT admit.** `Base Dmg:` is admitted as damage in `ItemStatsBlock`. One true row (`Keg Mallet`, Base Dmg 9 / Atk Delay 30) joins the weapon half of `ItemDominance`.

**Q2 — ADOPT exact spelling list, never a pattern.** Product change is `case "DMG": case "BASE DMG": dmg = value;` only. Soft LEAVE inventing regex / `.* Dmg:` pattern widening — that would overwrite true base damage with elemental/bane bonus numbers on the 11 named shipped records that also carry plain `DMG:`.

**Q3 — ADOPT pin the whole six-key census.** Assert against the committed catalog: `{DMG: 1648, Base Dmg: 1, Bane Dmg: 4, Cold Dmg: 3, Fire Dmg: 2, Poison Dmg: 2}`; admitted set exactly `{DMG, Base Dmg}`; four elemental keys as **named committed negatives**; Keg Mallet `Dmg = 9`, `Ratio = 0.3`. Prove-failed on the pre-change parser.

**Done bar ADOPT** as proposed in `docs/plans/DRA-251.md` (five items). That is now the card's bar.

**challenge ACK:** `dra-251-base-dmg -> NOT-ENGAGED (no C-test fires) as of 2026-09-25` STANDS. No Challenger wake.

**Route.** Soft merge #898 with `--match-head-commit` at `a514c95b96b2cea4ac05e1cb54314026c6b6e166` when `build-and-test` + `e2e-windows` green (re-tip if head moves). After #898 on `main`, Soft seats **Sr Executor** (`claude-opus-5-5`) for the product change (one case label + census test + `WeaponProc.cs` prose). Soft LEAVE inventing Jr / self-SIGN / pattern / Play / Founder mailbox.

**Out of scope.** No Play Console / signing / prod secrets. No elemental/bane as modelled quantity. Harvest PARKED. Soft LEAVE inventing new Live Holds.

**Soft carry-out.** (1) Helm SIGNs this tip head; merge tip when CI green. (2) Merge channel #898 when CI green at pinned head. (3) Seat Sr Executor for implement PR; tip that PR for Helm SIGN before merge. Discharge Paperclip DRA-251 / LOOP CLOSED the LIVE ASK once this tip and #898 are on `main`.

Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor.

---

## 2026-09-25 ~12:15 AM CT — RULE / DRA-53 night-15 / #892 — SIGN #691 thank-you; route Sr Executor review

**RULE** answering the DRA-53 night-15 LIVE ASK in `HELM-FEEDBACK.md` (#892, head `e94d0db76a58a0b5a097164b22ac31d6e4291a2e`). Soft drafted this tip and it adds no new ruling beyond Helm's word below.

**Q1 — thank-you: SIGN as drafted.** Scribe's `SCRIBE.md` #691 thank-you (filed 2026-09-19, resubmitted 2026-09-20) is **SIGNED** as written:

> Hi hateborne — thank you for PR #691 and for the log-replay evidence in the body; that's a very complete shape for this. Captured and sent on for review.
>
> — EQBuddy team

Scribe may post that as `DranakCorps-bot` on #691. No promises, dates, pricing, or ToS. Soft LEAVE inventing a louder variant or a merge promise in the thank-you.

**Q2 — disposition: Sr Executor review.** Route #691 to **Sr Executor** (`claude-opus-5-5`) for code review + last-look before any merge. Soft: rebase #691 onto current `main` first (mergeable_state dirty at look), then seat Sr. Soft LEAVE inventing Jr implement, Fable plan-from-PR, self-merge, or decline-without-review. Do not fold into #243 / #241 / #210 / #435. After Sr returns, tip the review outcome for Helm SIGN before merge. Soft LEAVE inventing merging #691 under this tip alone.

**Status ACK (not re-litigated).** DRA-4 parent sweep stays retired-while-blocked (night-14 RULE STANDS). PR #783 remains Sr-owned (re-SIGN needed if the branch amends past pin `e6e1ddf`); Soft LEAVE inventing a Helm ruling on #783 from this tip.

**Out of scope.** No Play Console / signing / prod secrets. No Founder page. No inventing bots. Soft LEAVE inventing new Live Holds for #691.

**Soft carry-out.** (1) Merge this tip when CI green after Helm SIGNs the tip head. (2) Merge channel #892 when CI green (additions-only). (3) Scribe posts the SIGNed thank-you on #691. (4) Rebase #691 onto `main`, seat Sr Executor for review, tip outcome for Helm SIGN. Discharge Paperclip DRA-53 night-15 ask / LOOP CLOSED the LIVE ASK once this tip and #892 are on `main`.

Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor.

---

## 2026-09-24 ~3:09 PM CT — COPY SIGN / DRA-363 / #885 @ `edabbffc` — merge HOLD until launch release

**COPY SIGN** draft PR #885 (TEL-PR4) at head `edabbffced0f6cb25f6fe0ab48a8c9513133ba59` (`edabbffc`), [comment 5821460490](https://github.com/DranakCorps-bot/EQBuddy/pull/885#issuecomment-5821460490). Consequence item 3. Covers **copy only** — **NOT a release go**, tag or channel. Soft-drafted; no new ruling; Helm last-look.

**MERGE HOLD on #885 @ `edabbffc`.** Stays draft; no undraft, no merge until the launch release David gates (DRA-336 §2 / §3). The HOLD lives on the PR + this tip, not a Live Holds row.

**Locks STAND.** Three-field payload (`installId` / `appVersion` / `os`, TEL-002). Telemetry opt-in. `peakConcurrent` OFF the landing — README badges only.

**Carry-out.** Merge channel PR #886 (the COPY SIGN ask) on green; merge this tip on green. Soft does not undraft #885, touch Play / signing / the Founder mailbox, or Jr self-SIGN.

Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor.

---

## 2026-09-24 ~1:30 PM CT — LIFT + SIGN / DRA-373 D2 / #878; SIGN #881 CTA RULE

**LIFT** soft HOLD on #878 (prior SIGN @ `db5fcf8c` WITHDRAWN). **SIGN** #878 at `895bb046bfcc13c388c8e646842a077522886841`. `--match-head-commit` when `build-and-test` + `e2e-windows` green (re-tip if head moves). Helm comment on #878.

**CTA RULE STANDS (Founder 2026-09-24 1:08 PM CT via Helm; supersedes 12:15 PM variant-B).** Evolved **coming soon**, **no download button**, **no v1 link** (no `releases/latest`, no tag/channel, no "1.x available today"). Variant A stays FORBIDDEN until a public Evolved installer exists. Soft LEAVE inventing channel/tag/signing from this SIGN. Guard `LandingSourceClaimsTests.TheLandingIsComingSoonAndNeverLinksV1` (+ committed negative). LICENSE footer MIT for published 1.x is licensing, not a download — STANDS.

**ADOPT** #880 on #878 (hero `shell-helper-throughput`; hunt K2 fallback `shell-world-drops` + copy; `shell-home` BlueGrey). Route honesty + topbar-only Ko-fi STAND. K1/K2/K3 STAND under coming-soon framing.

**Carry-out.** Merge #881 when CI green @ amended head; then #878 @ `895bb046` when both CI green. Pages publishes on D2 merge. Soft LEAVE inventing Jr / self-SIGN / Play / Founder mailbox / variant A.

Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor.

---

## 2026-09-24 ~12:50 PM CT — SIGN / DRA-376 (DRA-373 D1) / #876 — relocate HowEQBuddyWorks; REJECT #875; CLOSE #874

**SIGN** PR #876 (DRA-373 D1 / card DRA-376) at head `f10e29a463ed3b4f46cf4bbd020f7f6309789cd8` (`f10e29a4`). Soft merge with `--match-head-commit` at that sha when `build-and-test` + `e2e-windows` green (re-tip if head moves). Signed on #876 (Helm comment, 2026-09-24 ~12:50 PM CT). Soft drafted nothing new in this tip — Helm last-look.

**Scope ACK.** Two files only: new `docs/HowEQBuddyWorks.md` + one `docs/FeatureGuide.md` pointer. `site/` untouched (D1/D2 coexistence stands). Existing `docs/screenshots/` only; no new `site/assets/`. Docs-only; no app code; no Play / signing / prod secrets; no release-go coupling (D1 merges freely per plan).

**Corrections ADOPT (moved, then corrected).** Helper answers live (Farm Gear → *Upgrade what I wear*; Level Up ranks your camps). Track → map ring. Evidence floor (`ZoneHistory.MinHours` = 0.25 h) named. Scorecard gains the upgrade/who-drops row. **Step 5 honesty STANDS on #876:** travel is wiki zone-graph hops; it does **not** know which teleports *this* character has unlocked (`ZoneGraph` / `TravelPlan` have no unlock input). Soft LEAVE inventing shipping the unlock claim.

**REJECT SIGN on #875** at `ea82dc387394535d79270c0c8e1224223ec99121`. Same D1 intent, but step 5 still says the route uses "travel you have actually unlocked" — unfinished work described as shipped. Soft **CLOSE #875 without merge** (branch kept); #876 is the vehicle.

**CLOSE #874 without merge.** CONFLICTING LIFT-only tip; LIFT+SIGN already on `main` via #873 / tip at top of this file. Soft LEAVE inventing re-landing the prior-SIGN STANDS wording that #873 superseded.

**Carry-out.** Soft: merge #876 when both CI green @ `f10e29a4`; close #875 and #874 without merge; merge this tip when CI green; then seat **Sr Executor (`claude-opus-5-5`)** on DRA-373 **D2** per signed plan (variant B CTA allowed before Founder v2 go; K1/K2/K3 STAND). Soft LEAVE inventing Jr / self-SIGN / Play / Founder mailbox.

Live Holds empty. Play Console OFF. Not needs-david. Claude kick YES Bosun Soft Executor.

---

## 2026-09-24 ~12:15 PM CT — LIFT + SIGN / DRA-373 / #871 — landing streamlining plan; ADOPT K1/K2/K3

**LIFT** soft HOLD on merge — challenge condition satisfied at head `e3378bbdb65ce63fc224703748e5316f1b9ffee4` (`e3378bbd`). Recorded on #871 [comment 5818724263](https://github.com/DranakCorps-bot/EQBuddy/pull/871#issuecomment-5818724263) (2026-09-24T17:14Z). Soft drafted this tip and it adds no new ruling.

**SIGN** plan head `e3378bbdb65ce63fc224703748e5316f1b9ffee4` (`e3378bbd`). Soft merge #871 with `--match-head-commit` at that sha when `build-and-test` + `e2e-windows` green. Prior pin `0c19ab5a` is **superseded** — Soft STOP match-head on the old SIGN. Signed on #871 [comment 5818733458](https://github.com/DranakCorps-bot/EQBuddy/pull/871#issuecomment-5818733458) (2026-09-24T17:14Z). Soft CLOSED #872 without merge.

**Slug ACCEPT:** live `challenge: dra-373-landing-streamline -> PROCEED-WITH as of 2026-09-24` is the same key as the walk's `dra373-landing-streamline` / Helm's earlier ask. Soft LEAVE inventing a rewrite commit for hyphen spelling. Guard form wins.

**PROCEED-WITH conditions ADOPT (all three folded in plan):**
1. **K3 / dated variant-B:** if Founder v2 go is not signed within **7 days** of D2 merge-ready, Sr flips CTA to variant B and merges D2. If the go fires first, the go is D2's merge trigger.
2. **K2 / named fallbacks:** `shell-helper-gear` → `gearloot-gear.png`; `shell-helper-throughput` → `shell-world-drops.png`; Soft LEAVE inventing imageless cards — hold D2 and escalate if both fail.
3. **K1 / D2-merge re-verify:** D2 PR body cites the v2 tag/commit and verifies both graduated present-tense claims are in shipped 2.0.0 (`Core/Recommendations.cs` gear + hunt, `Core/GearTargets.cs`); else present tense drops / screenshot framing / variant B.

**CTA RULE STANDS:** Soft LEAVE inventing primary "Download EQBuddy Evolved" (variant A) on live Pages before public Evolved installer. Soft **may** land D2 with **variant B** anytime before Founder v2 go (7-day trigger is the floor if waiting). Soft LEAVE inventing that this SIGN opens Evolved channel/tag/signing.

**Carry-out:** After #871 on `main`, Soft seats Sr Executor (`claude-opus-5-5`) D1→D2 per plan; tip implement PRs for Helm SIGN. Soft LEAVE inventing Jr / Founder mailbox / Play / self-SIGN.

**Out of scope.** No Play Console / signing / prod secrets. No Founder page. Soft LEAVE inventing new rulings in this tip.

Live Holds empty. Play Console OFF. Not needs-david.

---
## 2026-09-24 ~8:38 AM CT — RULE / DRA-296 / #867 — ADOPT (1) archive append for #757 + #738; `#738` HOLD RETIRED

**RULE** on #867 (Helm comment `5815205698`, 2026-09-24T13:38Z), answering the DRA-296 LIVE ASK in `HELM-FEEDBACK.md`. Soft drafted this tip and it adds no new ruling.

**ADOPT (1) archive append. REJECT (2) live top. REJECT (3) record-only.** Both stranded entries land **verbatim**, own-entry bytes only, in `docs/ops/claude-archive/channels/2026-Q3/HELM.md` under a dated `LATE LANDING APPENDED (DRA-296)` pass header: #757 (DRA-53 / ops #50 SIGN tip, night-11 correction) and #738 (DRA-252 / #737 RULE). `new.startswith(old)` holds. This live file's top stays newest-first; the 2026-09-20/21 tips are not resurrected above 2026-09-24. The vehicle is #869.

**#738: close without merge**, with the same disposition as #757. Branches `helm/rule-dra252-737` and `helm/dra53-ops50-sign-20260921-0134` are kept. The head's 246 cp1252 hits and heading demotion stay off Part A. Soft LEAVE inventing a new merge path for #738. #757 stays closed.

**`#738` HOLD — RETIRED.** Its prevented act was merging the dirty tip into live `HELM.md`. The archive append plus close-without-merge discharges that door.

**Substance already on `main` STANDS.** DRA-252 KEEP gate 4 LAST and the later CONFIRM (a) on #791 stand, as does the DRA-53 night-11 SIGN substance carried via ops #50 and later night tips. This landing is archival placement only.

**Out of scope.** No live-top prepend, no Play Console / signing / prod secrets, no invented bots, no Founder page.

Soft: Helm SIGNs this tip head; merge it when CI is green. Merge #869 when CI is green (incl. channel-wipe-guard). Discharge Paperclip DRA-296 / pending `e6e1ce10`. LOOP CLOSED the DRA-296 LIVE ASK once this tip and #869 are on `main`. Live Holds empty. Play Console OFF. Not needs-david.

---

## 2026-09-24 ~2:41 AM CT — SIGN / DRA-236 / ops PR #79 — window-3 ledger gains `Arm at exit`; nights 13–14 tabled

**SIGN** ops `#79` at head `223c926bb0597385c682951b95b12f2985f96975` (`223c926`), merge only with `--match-head-commit` at that sha; additions-only (+23/−0) on `docs/ops/PAPERCLIP-INTAKE-midnight-planner.md`. Signed on ops `#79` (Helm T2 comment, 2026-09-24T07:41Z). Soft drafted this tip and it adds no new ruling.

**Card gate — column change SIGNed.** Window-2's 8-column format (`d9d2ef7f` §3, amended `755d792f`) STANDS, untouched byte-for-byte. Window 3 is a new table (not a gate — ops `#72` STANDS) with those eight columns plus a 9th, **`Arm at exit`**. Soft writes rows; Soft does not take a window verdict.

**Q1 — Row labels: KEEP `reconstructed`.** The Row cell names when the *table row* was written. Soft does not retense the Row cell to match the night-14 prose label.

**Q2 — Arm caveat: ACK; no miss.** **ADOPT** as standing Arm-column procedure: read back `executionPolicy.monitor.nextCheckAt` **after** the pass's last write (summary / email), quote value + read time (or `none` + reason). The arm stays out of the verdict (night-7 precedent STANDS).

**ACK — nights 13–14 tabled** (09-23 MISS, arm `none`; 09-24 success, 70 `tool_use` both channels, arm `2026-09-25T05:20:00.000Z`).

**Out of scope.** No kick-prompt edit, no monitor-notes rewrite, no window-2 settled-count touch, no `EXO-PLAYBOOK.md` edit, no Play Console / signing / prod secrets, no Founder page.

Soft: merge this tip when CI green; then merge ops `#79` @ `223c926bb0597385c682951b95b12f2985f96975`; discharge Paperclip DRA-236 / pending `aa6b1996`. Live Holds empty (`#738` HOLD STANDS). Play Console OFF. Not needs-david.

---

## 2026-09-24 ~2:31 AM CT — ACK/SIGN / DRA-362 / #865 — TEL-PR3 client; both departures ADOPTed

**ACK / SIGN** PR #865 (DRA-362 TEL-PR3) at head `cc683aa13f974a38d0047d055679b0173e022398` (`cc683aa1`). Signed on #865 [comment 5809554623](https://github.com/DranakCorps-bot/EQBuddy/pull/865#issuecomment-5809554623) (2026-09-24T07:13Z). Soft drafted this tip and it adds no new ruling.

**Soft merged #865** as `b355c610ca7e6fa36f87bd471298b02d2e3fd6dd` (`b355c610`) after `build-and-test` + `e2e-windows` SUCCESS on the signed head. Sequence gate passed: TEL-PR1 #858 + TEL-PR2 `eqbuddy-telemetry#1` already on `main`.

**Departure 1 — ADOPT.** Empty `TelemetrySender.BaseUrl` + `telemetryPrompt=noEndpoint` until a host exists (protects once-per-install). **DRA-369** Cloudflare free-tier deploy is Founder door later — Soft LEAVE inventing deploying host / paging Founder from this tip.

**Departure 2 — ADOPT.** Omit Bevel's parenthetical *"(say so, don't let it be a surprise)"* from the drawn §B OFF label — implementer note, not player copy. Every other §8.3 string stays verbatim under `TelemetryCopyTests`.

**Out of scope.** No Play Console / signing / prod secrets. No TEL host deploy. No DRA-369 Cloudflare. No Founder mailbox / page. No TEL-PR4. Soft LEAVE inventing new rulings in this tip.

Live Holds empty. Play Console OFF. Not needs-david.

---

## 2026-09-24 ~12:14 AM CT — C-1 READ RECORDED / DRA-360 / #858 — TEL-A consent copy; §8.3.1 rows 1–14 RULED

**C-1 READ RECORDED** on PR #858 [comment 5808133793](https://github.com/DranakCorps-bot/EQBuddy/pull/858#issuecomment-5808133793) (2026-09-24T05:14Z). Helm read Bevel's TEL-A copy (DRA-359, `6611cb61`) as folded in `docs/v2/telemetry.md` §8.3 at `51864fa8`. That is the one human read beyond authorship the DRA-336 SIGN made binding. Soft drafted this tip and it adds no new ruling.

**Rows 2–3 cost:** **REJECT** `TelemetryEverSent` (no fourth settings key; §7's three keys + `DeadSettingTests` stand). **ADOPT** one OFF text true both for a player who never opted in and after an opt-out.

**§8.3.1 rows 1–14, all Helm-RULED:** 1–3 FALSE → AMEND (id kept on the machine AND in stored heartbeats; one OFF heading; one dimmed-Delete tooltip). 4 Incomplete → AMEND (also sends once on Delete). 5 ADOPT `Not now` (TEL-001 unchanged). 6 ADOPT link line above buttons (`docs/v2/telemetry.md` until TEL-PR4). 7 ADOPT path fill. 8 ADOPT fifth §D string. 9 ADOPT five gap-free relative-time forms. 10 ADOPT State 1–3 shape. 11 ADOPT one Behavior view. 12 ADOPT `will try again`. 13 ACK Bevel's State 2. 14 ADOPT 8-hex id prefix, seeded ON fixture. Bevel's layout: ADOPT one paragraph + nested three-item list. Amends carried on #858 attributed to C-1 / Helm, not Bevel.

**Merge gate:** #858 merges when rows 1–3 are amended on the branch and `build-and-test` + `e2e-windows` are green. TEL-PR2 stays parallel; TEL-PR3 waits on TEL-PR1 + TEL-PR2 (TEL-A delivered).

**Out of scope.** No Play Console / signing / prod secrets. No TEL-PR2/PR3 implement under this read. No Founder page.

Live Holds empty. Play Console OFF. Not needs-david.

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

## Older rulings moved — [`docs/ops/claude-archive/channels/2026-Q3/HELM.md`](docs/ops/claude-archive/channels/2026-Q3/HELM.md)

**Every ruling this file no longer carries is in the archive, verbatim, and nothing was
deleted.** Six passes so far, all APPEND-only:

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

- **Pass 6 - DRA-154, 2026-09-25.** The ten dated tips from 2026-09-22 ~7:10 AM CT
  (DRA-332 / PR #823) through 2026-09-23 ~8:28 PM CT (DRA-355 / PR #860) moved with
  `scripts/channel-rotate.py rotate --cutoff 2026-09-24 --hold 'Live instruments re-pinned' --force --apply`;
  every 2026-09-24 tip stays live, and the standing items those tips carried are re-pinned
  verbatim in **Live instruments re-pinned — pass 6** at the top of this file. The pass-4 and
  pass-5 re-pin blocks were HELD LIVE. **64,536 B -> 44,152 B** before this block and bullet
  were added, LF-normalised UTF-8, against the 65,536 B ceiling. The archive grew 1,208,250 B ->
  1,229,167 B (append, +20,917 B: the 20,384 moved bytes plus the pass marker); prior-pass
  bytes were re-read as a prefix and are untouched. `scripts/channel-size-baseline.psd1` is
  UNCHANGED (TWO-file shape; HELM.md carries no grandfather row). Unblocks the DRA-53
  night-15 tip #893, which crossed 64 KiB at base `43797dc0`.

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
