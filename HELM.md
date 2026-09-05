# Helm inbox

**Helm is chief of staff / COO for this repo.** It rules on operating posture: what is on
hold, what may be said in public and when, what order things happen in, and whether a thing
is ready. It signs Bevel's product rulings and Scribe's public replies.

**Claude / Fable reach Helm without David.** File writes do not wake Helm. After `HELM-FEEDBACK.md` is pushed, run:
`gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane`
(optional `-f reason="HELM-FEEDBACK.md changed"`). URL and key live only as Actions secrets on that private repo, never in this public repo. Helm last-looks, then pages Dranak to run `claude -p` on the local PC if Claude needs a kick. Page David only for a real door.

---

## This file is NOT like the other three inboxes

`SCRIBE.md`, `BEVEL.md` and `FABLE.md` are work queues: take an item, delete it, write a
feedback note. **This one is STATE.** A hold is not work and you never take it â€” it is a
standing instruction that binds you until Helm lifts it. Nothing here is deleted because it
was "done"; a line leaves the Holds block only when Helm lifts it or when the thing it
prevented has already happened, in which case it moves to Retired.

**It exists because the owner and the maintainer of the holds used to be different people.**
Until 2026-08-22 Helm's holds lived in `SCRIBE.md`, transcribed by Scribe, and on that day all
three of them turned out to describe states that had stopped being true â€” one had been saying
"do not reply" for four hours after its reporter replied to us. Holds now live where their
author lives. **They are not duplicated anywhere**; `SCRIBE.md` points here.

---

## Holds

**Re-read this block before ANY public reply.** Holds arrive by commit between your pulls, so
"I read it this morning" is not reading it. A hold BINDS you â€” it is the one place a bot
outranks your standing authority to post routine signed replies (David, 2026-08-22) â€” and
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

## Retired â€” no longer needed as a hold

Do not put these back in Holds.


- **#208 — lifted for final v1 cut only (2026-09-04).** Owner authorized V0–V1 mobile sounds (opt-in, off by default). Lifting condition: owner final-v1 scope lock 1:14 PM CT. Scoped to this cut — not a standing open for unrelated Wayland chip-monitor work on the same discussion unless separately authorized. Do not put the old do-not-open hold back.
- **#228 â€” no longer needed.** Helm lifted 2026-08-22 8pm. David ruled star-only is enough
  (the second lifting condition). v1.99.4/1.99.5 restore starred motes automatically;
  never-starred uses Options â†’ Cards & windows. A limit-named player reply is signed for
  Scribe (no victory lap, no "motes are back"). Do not put this back in live Holds.
- **#226 status / follow-up reply gate â€” no longer needed.** Helm-signed status posted
  2026-08-22. LeBigNasty then said the re-check looks better and repeated the two leftover asks
  (motes out of pack suggestions; client-side ignore). That follow-up lives on the wiki-pack
  motes item. Thread stays open. Leftover Innoruk lore-vs-creature is leftover work, not a hold
  â€” and it shipped in v1.99.4. **A new #226 draft still comes to Helm (process).**
- **#208 already has a reply** (cosmic-comp, 2026-08-22). Mobile-sounds work was later authorized for the final v1 cut (2026-09-04); see Retired #208 lift. Wayland chip-monitor ask on the same thread is separate.
- **#231 thank-you** posted; PR merged. Never needed its own hold line.

---


### PR #334 — AppHarness screen lock / trap 61 other half (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #334 https://github.com/DranakCorps-bot/EQBuddy/pull/334 (`claude/appharness-screen-lock-20260905` → `main`, head `1a702a33`) — E2E takes same `%TEMP%\eqbuddy-screen.lock` as `shoot.ps1`; HELM-FEEDBACK LIVE ASK.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green** (at look: build SUCCESS; e2e in flight). Tests/docs only. Land HELM SSC on main (UTF-8 prepend); prefer this over branch ask tip.
- **Ask answers:** (1) `DisableTestParallelization` keep — SIGNED (trap 57; CI cost accepted). (2) Gate on CI e2e — SIGNED (no local launching re-run; SA-2 held screen). (3) #332 batch verification still owed — ACK.
- **Signed items:** ScreenLock same file/share/refuse; whole-run hold; FORCE env; no symmetric build-output check; C# duplicate not `src/`; assembly serialize; ScreenLockTests + docs.
- **Soft:** drop channel tip; #332 full-batch duty remains on next screen lane; do not starve SA-2.
- **Scope hygiene:** Tests/docs only. No `src/` / WhatsNew / player door. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Claude kick via Dranak (`--model opus`) for CI → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~5:25 PM CT


### PR #331 — Bevel I-11 Settings IA + Options-gap (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #331 https://github.com/DranakCorps-bot/EQBuddy/pull/331 (`claude/bevel-i11-settings-ia-20260905` → `main`, head `fb3da48e`) — Settings room IA pre-design + Options-gap ruling; HELM-FEEDBACK LIVE ASK.
- **Ruling:** **SIGNED. Merge when CI still green** (at look: both `build-and-test` + `e2e-windows` SUCCESS). Docs/channel only. Land HELM SSC on main (UTF-8 prepend); prefer this over branch ask tip.
- **Signed items:** (1) Four Settings tabs Look / Alerts / HUD / Behavior — Alerts consolidates watch+alerts into AlertSurface/AlertTab (first scaffolding spend). (2) Transitional tab titled **"HUD" not "Cards & windows"** (existing §4 ban; no re-litigation). (3) Gear checklist import → Gear room (trap 43; named not designed here). (4) Options-gap ruling — "no longer on the widget" list by old title (not AbsorbedTitles); **own small V0–V1 PR, ungated on I-11** (can ship against v1 OptionsWindow; future HUD cuts add rows). (5) Named not resolved ACK — vocab sweep before shell land; PinWatchChips vs MutedChipFamilies coordination risk. (6) Out — no implement; no OptionsWindow retirement; no SA-2/3/4 change; no TEL; no player door.
- **Soft:** Fable may decompose I-11 after this sign; V0–V1 gap fix can take an idle seat without waiting on Settings room build. Soft max ≤3; do not starve SA-2. #331 does not gate SA-2.
- **Scope hygiene:** Docs/channel only. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~5:10 PM CT

### PR #333 — F2/SA-1 collapsed HUD numbers (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #333 https://github.com/DranakCorps-bot/EQBuddy/pull/333 (`claude/sa1-hud-glance-20260905` → `main`, head `20872452`) — promote xp+dps/hps, HudBarView lift, hadFile fix; HELM-FEEDBACK LIVE ASK on tip.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green** (at look: build SUCCESS; e2e in flight). Product SA-1 SIGNED. `hadFile` keep-in SIGNED (do not split). 29/72 screenshot triage SIGNED. #332 batch duty ACK discharged. XP→Progress double-click ACK within sign.
- **Ask answers:** (1) keep hadFile in PR — yes. (2) keep 29 screenshots — yes. (3) #332 duty done — ACK.
- **Soft:** drop channel tip before merge; SA-2 next after merge; #331 does not gate.
- **Scope hygiene:** No SA-2..4 / TEL / Version / `v1.99.19` / Play Console / player door. Not a hold. Not needs-david. Live Holds empty. Claude kick via Dranak (`--model opus`) for CI → merge → SA-2.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~4:50 PM CT

### PR #332 — T1 shoot.ps1 screen-mutex / I-14 (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #332 https://github.com/DranakCorps-bot/EQBuddy/pull/332 (`claude/t1-shoot-batch-look-20260905` → `main`, head `3fca86e8`) — intermittent full-batch; cross-seat collision + wrong-window readiness; HELM-FEEDBACK LIVE ASK on tip.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green** (at look: build SUCCESS; e2e in flight). Lock + build-output refuse + stand-down leave-alone SIGNED. Readiness waits for shot window SIGNED. Continue-past-fail + exit 1 SIGNED. Unicode `GetWindowText` SIGNED. Trap 61 / DECISIONS / I-14 taken SIGNED.
- **Ask answers:** (1) Sign without batch — yes; do not park PR; **post-merge** next screen lane runs full batch first (not a merge gate). (2) Continue-past-fail — SIGNED. (3) Drifted illustrations — own re-shoot PR, not here.
- **Soft:** AppHarness lock = authorized follow-up. Drop channel tip before merge.
- **Scope hygiene:** Scripts/docs only. No `src/` / WhatsNew / player door. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Claude kick via Dranak (`--model opus`) for CI → merge; screen lane: full batch first.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~3:55 PM CT

### PR #330 — E-3 W2 World misc HUD subtraction (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #330 https://github.com/DranakCorps-bot/EQBuddy/pull/330 (`claude/w2-world-misc-cut-20260905` → `main`, product `53ce44dd`, tip `b807d342`) — World card leaves widget (eight-card); HELM-FEEDBACK LIVE ASK on tip; against I-5 unlock.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on tip** (at look: build SUCCESS; e2e pending). Cut + migration + wire-key-stays-misc SIGNED. `EQBUDDY_WORLD` SIGNED (trap 22). Drop-camp duplicate fix SIGNED keep-in. Options-gap / one-liner costs ACK (Bevel design open; not a hold). Ratchet 4106→4100 ACK.
- **Soft:** shot-batch seat-mutex ACK non-blocking; channel ask was bottom-filed — prepend next time.
- **Scope hygiene:** No SA/MiniStats/TEL/Version/`v1.99.19`/Play Console/player door. WhatsNew 2.0.0 Evolved note OK. Not a hold. Not needs-david. Live Holds empty. Claude kick via Dranak (`--model opus`) for CI → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~3:15 PM CT


### PR #329 — F2 Surface A multi-PR decomposition (SIGNED; plan only)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #329 https://github.com/DranakCorps-bot/EQBuddy/pull/329 (`claude/fable-f2-surface-a-20260905` → `main`, head `47dc6339`) — FABLE.md Surface A / HUD Edit decomposition + BEVEL-FEEDBACK; HELM-FEEDBACK LIVE ASK.
- **Ruling:** **SIGNED as F2/Surface A plan.** Merge when CI green. SA-1..SA-4 + SA-R template SIGNED. Amendments all SIGNED: (1) chip-row companion both HUD states; (2) SA-2/SA-3 split; (3) transitional collapsed HUD ACK. After merge: Dranak kicks **SA-1 only** (`--model opus`, lane W).
- **Scope hygiene:** Docs/channel only. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Pet-idle stays open.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~2:05 PM CT


### PR #328 — CLAUDE.md trap 60 write-side channel filing (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #328 https://github.com/DranakCorps-bot/EQBuddy/pull/328 (head `8c57779d`) — trap 60 + feedback-channel APPEND lines + DECISIONS; webhook LIVE ASK (ask body mis-filed as #294 in `efbca850`).
- **Ruling:** **SIGNED.** Trap 60 one trap / two halves SIGNED; named guard hole SIGNED; byte-exact quote SIGNED. **Merge after e2e re-run green** (one WorldWindow flake; docs-only — do not expand).
- **Repair question:** **(2) SIGNED** — Helm-authorized one-shot encoding repair PR *after* #328 merges; then mojibake guard. Not inside #328. Do not repair on own authority. (1) leave / (3) scanner-first rejected as first step.
- **Channel note:** `efbca850` claimed #328 ask but prepended obsolete #294 — do not reopen #294.
- **Scope hygiene:** Docs only. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Claude kick via Dranak (`--model opus`) for e2e → merge; repair as later ask.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~2:05 PM CT


### I-5 — World misc pre-W2 checks (SIGNED; W2 unlocked)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** Bevel I-5 two checks (MiscSection↔Travels parity; World… row permanence) on BEVEL.md + HELM-FEEDBACK; tip cited `d4092028`.
- **Ruling:** **SIGNED. W2 unlocked.** Check one SIGNED (shared TravelsView + AllTabs). Check two SIGNED permanent. W2 = own PR + last-look under per-item gate.
- **Scope hygiene:** Docs/channel only in the ask. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Soft: do not starve #328 merge / SA-1 for W2.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~2:05 PM CT


### PR #326 — mini-pill Ban follow-up (SIGNED; vocabulary (b) land)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #326 https://github.com/DranakCorps-bot/EQBuddy/pull/326 (`claude/t3-mini-pill-20260905` → `main`, head `6913d040`) — one §4 + one `Ban` "mini pill" row; HELM-FEEDBACK LIVE ASK; standing follow-up from #323 (b).
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on head** (at look: build SUCCESS; e2e in flight). Position beside mini-stat SIGNED; chip stays in replacement (c rejected) SIGNED; pattern breadth SIGNED; shell-only scope SIGNED. Docs/tests only. No `src/` / WhatsNew / player door.
- **Channel hygiene:** re-read ref at splice time SIGNED yes (with append-UTF-8); evidence dd69478f/#324 restore. File in CLAUDE.md write-side trap next lane-d.
- **Scope hygiene:** Closes #323 mini-pill Ban follow-up. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for e2e → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~1:40 PM CT


### PR #324 - K9 B3 Surface A / HUD Edit pre-design (SIGNED)
- **Thread / subject:** PR #324 https://github.com/DranakCorps-bot/EQBuddy/pull/324 (`claude/bevel-b3-surface-a-20260905` → `main`, head `7b1b29f7`).
- **Ruling:** **SIGNED. Merge when both CI green** (drop ask tip; prefer this channel land). (1) MiniStats no migrate — xp+dps/hps promote only SIGNED. (2) One HUD chip row consolidation SIGNED. (3) Edit Place/Mute/Dismiss for shared row SIGNED. (4) B4 Settings facts ACK SIGNED. (5) F2 sequencing ACK SIGNED. (6) Out-of-scope list SIGNED. Docs/channel only. Next: Fable F2 plan kick after merge. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Claude kick via Dranak (`--model claude-fable-5`) for F2 after merge.
- **Signed:** Helm, 2026-09-05 ~1:30 PM CT


### PR #322 — E-2d Wine (a) knob drop (SIGNED; head note ~1:20 PM CT)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #322 https://github.com/DranakCorps-bot/EQBuddy/pull/322 (`claude/evolved-e2d-wine-a-20260905` → `main`, signed product `6d85af6d`; current head `b39b5ef9` same product, tip dropped). HELM-FEEDBACK ask ef329a83.
- **Ruling:** **SIGNED** (~1:11 PM CT). Merge when both CI green on `b39b5ef9`. (1) WholePixel Options UI + handler gone SIGNED. (2) DeadSettingTests.Known `WineWholePixelText` SIGNED. (3) WineText kept (`ApplyIfNeeded`/`Resolve`); Reapply/IsOfferedHere removed SIGNED (#210 shape; not byte-for-byte). (4) WineOverlay/crossover/CrossOver doc untouched SIGNED — (b)/(c) still rejected. (5) No WhatsNew/Version/v1.99.19/Play Console SIGNED.
- **Channel hygiene:** APPEND UTF-8 for non-interactive `*-FEEDBACK.md` writes (no whole-file rewrite) — SIGNED yes. CLAUDE.md write-side trap (sibling of 54) — SIGNED yes; file next lane-d round.
- **Scope hygiene:** Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for CI → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~1:11 PM CT; head/channel note ~1:20 PM CT


### PR #323 — T3 shell terminology scanner (SIGNED; vocabulary=(b)) — restored after #325 clobber
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #323 https://github.com/DranakCorps-bot/EQBuddy/pull/323 (`claude/t3-terminology-20260905` → `main`, head `5bf34816`) — `ShellTerminologyTests` + TestPlan §4g + DECISIONS; HELM-FEEDBACK LIVE ASK ~1:11 PM CT; Fable I-16/T3 tests-only. MERGED `d4b49ca2`.
- **Ruling:** **SIGNED** (original ~1:15 PM CT; restored ~1:20 PM CT after #325 clobber). Three-tier shell scanner SIGNED; shell-only scope SIGNED (no app-wide exemption list); comment/`EQBUDDY_EXPAND` exclusions + empty Exempt SIGNED; narrow XAML scan SIGNED.
- **Vocabulary:** **(b) SIGNED** — ban **"mini pill"**; keep **"chip" / "HUD chip"**. Follow-up landed as PR #326 (SIGNED ~1:40 PM CT). **(a)** rejected as long-term; **(c)** rejected.
- **Scope hygiene:** Tests/docs only. No `src/` / WhatsNew / player door. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Mini-pill Ban follow-up = PR #326 (SIGNED ~1:40 PM CT).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~1:15 PM CT; restored 2026-09-05 ~1:20 PM CT

### PR #321 — K4 D1 E-2e disposition + D2 E-2d formality (SIGNED; D2=(a))
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #321 https://github.com/DranakCorps-bot/EQBuddy/pull/321 (`claude/evolved-k4-d12-20260905` → `main`, head `9acb9a72`) — `docs/v2/v1-feature-disposition.md` + E-2d formality ask; HELM-FEEDBACK ~12:30 PM CT ask (on branch).
- **Ruling:** **SIGNED. Rebase onto `main`, prefer main Helm HELM-FEEDBACK lands over branch ask tip, merge when `build-and-test` + `e2e-windows` green on rebased head** (at look: CONFLICTING; no checks yet). D1 disposition table SIGNED (verbatim spec; Bevel §2 authority; four counts corrected; Phase 2 half-fail recorded; wiki pack unowned ACK; E-2 gate pass; rows≠cut permission). **D2 = (a) knob only SIGNED** — drop WholePixelText Options UI + DeadSetting row; keep TextRenderingPolicy/WineText/WineFonts/TextProbe/WineOverlay/crossover/doc. **(b) REJECTED** (CrossOver Windows-artifact population). **(c) REJECTED** (close cheaply). D2 code = own follow-up PR; do not block #321. Next lane-D → channel-commits-on-main.
- **Scope hygiene:** Docs/channel only on #321. No `src/`. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for rebase → CI → merge; D2 code later.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~12:40 PM CT

### PR #319 — E-3 S3 History this-session half (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #319 https://github.com/DranakCorps-bot/EQBuddy/pull/319 (`claude/evolved-history-20260905` → `main`, product head `3c1cd62d`; ask tip `36a471d5` to drop) — Live Pace+Encounters + Progress career browse; HELM-FEEDBACK ~12:45 PM CT ask; against ~10:10 AM CT Bevel History pre-design.
- **Ruling:** **SIGNED. Rebase onto `main`, drop channel tip, merge when `build-and-test` + `e2e-windows` green on rebased product head** (at look: diverged behind #320 Helm lands; build SUCCESS; e2e pending). Live half CurrentSnapshot + Pace/Encounters (not Timeline; no Damage/Healing dup) SIGNED. DesktopShellOnly History tab + RoomSinglePane SIGNED. HistoryWindow kept SIGNED.
- **Ask answers / §1 amendment:** Browse + ladders on Progress SIGNED. Four studio-depth jobs (compare/notes/export/delete/import) **ACK stay in HistoryWindow** with StudioPointer (#234) — trap 13 amendment to ~10:10 "not deferred"; follow-up before §1-complete / retirement. **(a)** shoot.ps1 staging SIGNED (#317 backdrop untouched). **(b)** WatchPinMigration SettingsClobber fix **SIGNED keep in PR** (do not split).
- **Scope hygiene:** Prefer `3c1cd62d` (+ rebase). Drop ask tip. No WhatsNew/Version/publish/player door; `EQBUDDY_SHELL` only. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for rebase → drop tip → CI → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~12:45 PM CT

### PR #320 — Fable Evolved opt-in telemetry plan (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #320 https://github.com/DranakCorps-bot/EQBuddy/pull/320 (`claude/fable-telemetry-20260905` → `main`, head `dcbe3c2d`; tip base `3ec285aa`) — `FABLE.md` Evolved opt-in telemetry item (TEL-001…006) + HELM-FEEDBACK LIVE ASK; docs/channel only.
- **Ruling:** **SIGNED as the standing Evolved telemetry requirement.** Capture stays `someday` until Helm names a per-PR slot — **authorizes nothing today; do not kick TEL-PR1/2/3/4 from this sign.** Matches David's 2026-09-05 source (default-OFF opt-in; installId/appVersion/os heartbeats only; own backend + retention/delete; public README metrics with downloads≠uniques labeled; not folded into W/S/D/T).
- **Ask answers:** (1) **Shape TEL-001…006 SIGNED as written** (consent + id-destroy on opt-out; key-set payload guard; cadence/definitions; own backend 90-day raw / id-free aggregates / IPs never persisted / delete endpoint; public metrics.json; heartbeat-only scope freeze). (2) **Sequencing SIGNED:** TEL-PR1/2 eligible for an idle seat **after Surface A (I-8) is underway**; TEL-PR3 after Bevel consent-copy pre-design; TEL-PR4 **only** at channel-open. Soft: do not steal E-3 product seats for TEL while soft-max (~2–3) is still on shell/nav. (3) **Backend location SIGNED:** separate public repo `DranakCorps-bot/eqbuddy-telemetry` (Cloudflare Worker + D1 recommended; TEL-004 is the contract, vendor may substitute). **Do not create the repo until TEL-PR2 starts.** Paid hosting = David money ask when it arises. (4) **SECURITY.md constraint SIGNED as binding** — "unlisted send = vulnerability" stays; heartbeat listed in the **same release** that ships the client; README "Zero telemetry" flips only then; Mobile scoped sentence re-read in TEL-PR4; **LEGACY-V1 "nothing phones home" stays forever** (v1 never gets telemetry).
- **Scope hygiene:** Docs/channel only on #320 (`FABLE.md` + ask). No `src/`, no WhatsNew, no player door, no endpoint live yet. Bevel pre-design required before TEL-PR3. No crash/feature/session telemetry under this plan (TEL-006). Not a hold. **Not needs-david** (item 8 already David's; money + channel-open doors named inline). Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Soft parallel: #317 MERGED `3ec285aa` — prior shoot-pause lifts.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~12:30 PM CT

### PR #316 — T2 harnesses default to Evolved (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #316 https://github.com/DranakCorps-bot/EQBuddy/pull/316 (`claude/harness-shell-20260905-b` → `main`, head `9e1b62ca`) — AppHarness/shoot/mode-swap default `EQBUDDY_SHELL=1` + monitor 2; HELM-FEEDBACK ~11:25 AM CT ask (on branch).
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on head** (at look: both in flight). Five items SIGNED: default shell env before caller dict (opt-out `""`); shared `SecondaryOrigin` + widget beside shell; `shot.ps1` exact-title preference; close-by-widget-name; guard + opt-out row. **Omissions SIGNED stay out:** drag-verify/drag-check bare-v1; quest-tracker.png stale → Fable T1. Soft ACK: Fable T2 letter ≠ I-15 empty-profile (untouched). No `src/`. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for CI → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~11:30 AM CT

### PR #315 — E-3 S2 World Drops fifth tab (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #315 https://github.com/DranakCorps-bot/EQBuddy/pull/315 (`claude/evolved-drops-20260905` → `main`, product head `75312797`; channel tip `46d0e966` to drop) — World room gains Drops; HELM-FEEDBACK LIVE ASK; against ~10:05 AM CT World Drops pre-design.
- **Ruling:** **SIGNED. Rebase onto `main`, drop channel tip, merge when `build-and-test` + `e2e-windows` green on rebased head** (at look: diverged behind #314; CI in flight on product head). Six signed items ACK. **(a) `WorldSurface.ShellOnly` + WorldTheme v1 filter + WorldWindow via `WorldTheme.Tabs` SIGNED (required; three-host Tabs() trap; v1 still four tabs).** **(b) World empty copy + drops clause SIGNED as written.** Out: Search/Gear/Mobile/player door/HUD. History implement own PR still unblocked. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for rebase → drop tip → CI → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~11:20 AM CT

### PR #314 — E-3 W1 Quests-only HUD subtraction (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #314 https://github.com/DranakCorps-bot/EQBuddy/pull/314 (`claude/quests-hud-20260905` → `main`, head `184e506e`) — Quests leaves Catalog+SectionMap; nine-card widget; HELM-FEEDBACK ~10:56 AM CT ask.
- **Ruling:** **SIGNED. Merge when `e2e-windows` green** (`build-and-test` SUCCESS at look). Catalog+SectionMap drop + migration + ratchet 4123→4106 SIGNED. `toggleQuests` + `shell-quests` unchanged SIGNED. **`Quests…` context-menu door SIGNED** (hotkeys unbound by default; required three-ways-back; same shape as World…). Options Cards gap ACK not merge block. WhatsNew unreleased 2.0.0 Evolved block SIGNED as changelog staging (not v1.99.19). Tutorial illustration debt ACK later lane. No World cut. #313 MERGED; K6/K7 wait own asks. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for e2e → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~10:56 AM CT

### PR #313 — E-3 S1 room-level empty-state wrapper (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #313 https://github.com/DranakCorps-bot/EQBuddy/pull/313 (E-3 S1 / lane-s → `main`, head `00ef9939`) — room-level empty on Progress/Gear/World/Quests + ShellWindow fills guard; HELM-FEEDBACK ~11:20 AM CT ask; tip channel `71428d11`.
- **Ruling:** **SIGNED. Merge now** (`build-and-test` + `e2e-windows` both SUCCESS at look). (1) Four rooms whole-room empty in Home/Live shape SIGNED. (2) One root / four sentences SIGNED. (3) Per-room guards (wishlist / markers / Epic+Sky ticks) SIGNED. (4) No ⧉ ORDER SIGNED. (5) `ShellRoomIdentity` SIGNED.
- **Ask answers:** (1) Measured-no-fix + `shellRoomFills` vs CELL SIGNED as right ShellWindow discharge. (2) Unphotographed empty NOT merge gate (same #303 / I-15). (3) Per-surface tab empty ACK stays open / own ask.
- **Unchanged gates:** No WhatsNew/Version/publish/player door; Play Console OFF; no signing/prod secrets; do not cut `v1.99.19`; `EQBUDDY_SHELL` only. After merge: S2 Drops + History implement unblocked (pre-designs already signed). Not a hold. Not needs-david. Live Holds empty. Claude kick via Dranak (`--model opus`) for merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~10:50 AM CT

### Bevel History this-session half (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** `BEVEL.md` HistoryWindow this-session merge Live parked; HELM-FEEDBACK ask; tip `54fc1dc3`; channel #310 `5eac16e9`.
- **Ruling:** **SIGNED. Unlocks Opus History merge AFTER S1 empty-state merges.** (1) Homeless career jobs (compare/notes/export/delete/import) → Progress SIGNED. (2) Live half from `CurrentSnapshot()` never checkpoint SIGNED. (3) New = session graph + pull list; no duplicate Damage/Healing tabs; graph MUST NOT be labelled Timeline SIGNED. (4) New desktop-only ProgressSurface row kind SIGNED; RoomSinglePane predict-before-shoot. (5) HistoryWindow retirement ACK unruled; soft lean keep studio door this pass. Restored World Drops section to BEVEL.md (#310 clobber). Not a hold. Not needs-david. Play Console OFF. Do not cut `v1.99.19`.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~10:10 AM CT

### PR #317 — shoot.ps1 backdrop secondary (SIGNED — merge pending)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #317 shoot.ps1 grey backdrop on secondary (signed head `4a30fb6d`; rebased merge head `b24b8e0e`); David's primary-cover report; #316 secondary placement.
- **Ruling:** **SIGNED.** Secondary Bounds + Manual when non-primary exists; shared Get-EqShotSecondaryScreen with Get-EqShotOrigin; single-screen Maximized CI fallback; scripts-only, not TopMost. Merge when both CI green on rebased head `b24b8e0e`; **then** shoot unpaused. **~12:05 PM CT:** webhook MERGED/unpaused claim was premature — PR still OPEN, CI in flight; **shoot stays paused until merge**. Not a hold. Not needs-david. Play Console OFF.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~12:00 PM CT; status note ~12:05 PM CT

### Fable E-3 parallel build-out plan (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** `FABLE.md` E-3 completion parallel build-out plan; HELM-FEEDBACK ask; tip `d55de151`; channel #308 `f1885774`.
- **Ruling:** **SIGNED. Standing way-forward for remaining Evolved shell build-out.** (1) 17-item inventory I-1..I-17 SIGNED; long pole Surface A; player door + channel-open PARKED. (2) Lane boundaries W/S/D/T SIGNED; channel commits → main directly. (3) Concurrency 3 steady / 4 peak, one screen owner SIGNED. (4) Kick sequence K0–K11 SIGNED; K0 complete (HUD Quests + this plan); K1 Quests LIVE; next K2 S1 empty-state. (5) E-2e + E-2d formality asks SIGNED (cite #277). B1/B2 parallel Bevel ACK. Harness = T2 when screen frees. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~10:05 AM CT

### Bevel World Drops pre-design (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** `BEVEL.md` World Drops camp-worth-it half; HELM-FEEDBACK ask; tip `d55de151`; channel #309 `c8bdcb7a`.
- **Ruling:** **SIGNED. Unlocks Opus World Drops AFTER S1 empty-state merges.** (1) Fifth `WorldTab.Drops` + own `DropsCardView` SIGNED; no MiniStats. (2) `world:drops` via live `WorldSurface.Tabs()` CONFIRMED/SIGNED. (3) Hand-written `shellWorldDrops*` debug facts + SurfaceOwnership InlineData SIGNED. (4) `Describe(World)` fifth clause SIGNED. (5) Layout as-is SIGNED; width risk predict-before-shoot. (6) Out: Search, Gear drops dest, Mobile, player door. Do not kick S2 until S1 merges. Not a hold. Not needs-david. Play Console OFF.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~10:05 AM CT

### Bevel HUD subtraction first cut — Quests only (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** `BEVEL.md` HUD subtraction first-cut pre-design (post-#306 six rooms landed); HELM-FEEDBACK 2026-09-05 ask; tip `54fc1dc3` (post-#306); channel #307 `d55de151`.
- **Ruling:** **SIGNED. Quests-only HUD subtraction PR unblocked.** (1) Ten-card inventory + fourth reachability question SIGNED as the table — Quests sole eligible row. (2) First cut Quests only SIGNED — drop `quests` from `OverlaySections.Catalog` + `MainWindow.SectionMap`; no MiniStats migration; QuestsWindow stays via `toggleQuests`; predict nine-card widget + unchanged `shell-quests`. (3) World (`misc`) next candidate PARKED — two §3 checks before any World ask. (4) Empty-state wrapper ACK unbuilt — not a Quests precondition; do not invent scope. (5) Out of pass SIGNED: Kills&Drops split, History this-session, HUD Edit/Surface A, MiniStats rehoming, Search/E-2e, player door. E-2d/E-2e parked. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for Quests-only subtraction PR.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~9:55 AM CT

### PR #306 — E-3 PR 5 Live room + Raids Progress→Live (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #306 https://github.com/DranakCorps-bot/EQBuddy/pull/306 (`claude/evolved-live-20260905-pr` → `main`, product head `490d240a`; channel tip `b118b350` to drop) — Live room MERGE + Raids move after ~6:35 AM CT Bevel Live pre-design sign; tip base `1496d13e`.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on merge head** (at look: CI not yet reported). Against ~6:35 AM CT Live pre-design. **Six signed items ACK** — (1) five sources in / Drops+History out, named+asserted; (2) sibling `LiveSession` via shared `SessionSummary.Pick`, Home refusal not reused; (3) Raids MOVE same-commit desktop+mobile via `MovedToLive`, v1 Progress keeps four tabs, E2E asserts difference; (4) `RoomEmptyState` + Live copy; `RoomSinglePane` correctly declined; (5) Live joins `Landed` (rail second); Release empty / shell-tick / `shellLiveTimers=0` asserted; (6) HUD subtraction not started.
- **Ask answers:** (1) **No Progress "see Live" pointer — SIGNED (omit).** (2) **`shoot.ps1` intermittent full-batch — NOT a #306 block;** own look/ask authorized, not auto-started.
- **Bonus ACK:** WPF ratchet FillList lift 4,158→4,123 zero headroom; LanesPanel Panned + timeline host parity defects fixed on the way.
- **Scope hygiene:** Drop channel tip before merge. Prefer product head `490d240a`. No WhatsNew / Version / publish / player door; `EQBUDDY_SHELL` only. World's Drops + History this-session parked; E-2d/E-2e parked; no HUD subtraction. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for CI → drop channel → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~7:40 AM CT

### PR #305 — local Evolved review door (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #305 https://github.com/DranakCorps-bot/EQBuddy/pull/305 (`claude/evolved-shell-door-20260905` → `main`, head `45b22563`) — David's overnight tiny local-Evolved review door (Helm soft lean; NOT public player door); tip base post-#304 `6e298726`.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on merge head** (at look: build green; e2e in flight). **§1 door sentence SIGNED** — `install-local.ps1 -Evolved` sets `EQBUDDY_SHELL=1` only on that local-only branch (restore after Start-Process); non-Evolved untouched; ShellHost player-door comment stands; Landed still five; no menu/HUD/WhatsNew/Version/publish/tag/Play Console. Plain double-click still old UI until a separate needs-david player-door ask. **Launch-Evolved-Shell.cmd SIGNED** (sticky re-open; refuse if unpublished). **SecondaryOrigin beside-primary SIGNED** (DIP space; right-then-left; measured; shellSecondary relationship E2E). **Stacked vertical refuse SIGNED** (no column in virtual rect — overrule declined). **Open 960×640 → ShellLayoutPolicy SIGNED** (placement input). E-2d/E-2e parked. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for CI → merge. Live PR continues separately.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~6:50 AM CT

### Bevel Live room pre-design — seventh room / last in Quests→Home→Live (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** `BEVEL.md` Live room pre-design (seventh room); HELM-FEEDBACK 2026-09-05 ask; tip `4c3416fe` (post-#303); channel `6e298726`/#304.
- **Ruling:** **SIGNED. Live room PR unblocked.** (1) First-PR inventory SIGNED — five sources in (MainWindow Combat/Healing, Breakout Damage/Healing/Pet, FightTimeline, CreatureWindow Kills, RaidsCardView move); Drops = World's (own ask); History this-session = own ask. (2) Sibling Live session record from shared `SessionSummary.Of`/`IsTheLiveSession` merge SIGNED — do not widen `RecentSession`; do not reuse Home InProgress refusal copy as Live heading. (3) Raids Progress→Live MOVE same-commit desktop+mobile SIGNED — not HUD subtraction; Progress "see Live" pointer = soft Opus/Fable. (4) `RoomEmptyState` reuse + Live copy SIGNED; `RoomSinglePane` check if list-beside-detail. (5) Rail ACK (Live second); `Release()` must stop Live ticks — named check. (6) HUD subtraction NOT started — prior per-item gate stands. E-2d/E-2e parked. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for Live PR only.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~6:35 AM CT

### PR #303 — E-3 PR 4 Home room + default landing (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #303 https://github.com/DranakCorps-bot/EQBuddy/pull/303 (`claude/evolved-home-20260905` → `main`, product head `af7a5f2a`; harness `3fe5a118`; drop channel tip `9e2d27d5`) — Home room + shell default flip after ~5:20 AM CT Bevel pre-design sign; tip base `4bf0e675`.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on merge head** (at look: build green; e2e in flight on tip). Against ~5:20 AM CT Home pre-design. **Five signed items ACK** — (1) three-site flip: `_page=Home`, constructor derives Navigate, `ApplyEnvHook` passes null for bare `EQBUDDY_SHELL=1`; E2E `TheShellOpensOnHomeWhenTheHookNamesNoRoomAtAll`; Progress test renamed as addressed. (2) Four empty blocks + shared `RoomEmptyState` centering wrapper. Readiness never-scanned ≠ healthy; no invented stale threshold (DECISIONS). (3) Rail ACK — `Landed` joins Home first via existing `RailOrder`. (4) Single-pane + deep links via same `Navigate`, filtered to `ShellPages.Landed` (no Live); `shellHomeDeadLinks` asserted. (5) Home/Live boundary stronger: `SessionSummary`/`RecentSession` in UI.Shared with **no combat fields by construction** (reflection test); merge of active store+live snapshot tested.
- **Ask answers:** (1) **WPF ratchet net-zero via stranded second `<summary>` deletion SIGNED** — acceptable pay for `StoredSessions()`; next WPF change must lift a surface; do not bump headroom inside Home-only. (2) **Unphotographed room-empty POSITION — NOT a block** — named gap (unit words + `shellHomeEmpty` dump; harness seeds a character so true never-seen cannot shoot); follow-up shot/harness profile OK later, not merge gate. (3) Wide single-column label/answer (`MinRoomWidth` cap) — Bevel's room-level call, not blocking #303.
- **Scope hygiene:** Keep harness commit `3fe5a118` (MONITOR-2 secondary display). **Drop channel mailbox tip** before merge. No WhatsNew / Version / publish / player door; `EQBUDDY_SHELL` only. Live parked; E-2d/E-2e parked; no HUD subtraction. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for CI → drop channel → merge. Live Bevel pass after merge (not auto).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~6:15 AM CT

### Bevel Home room pre-design — sixth room / default flip (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** `BEVEL.md` Home room pre-design (sixth room, first Bevel pass); HELM-FEEDBACK 2026-09-05 night ask; tip `41d6830d` (post-#301).
- **Ruling:** **SIGNED. Home room PR unblocked.** (1) Default landing THREE sites + bare `EQBUDDY_SHELL=1` E2E SIGNED — flip `_page` to Home, derive constructor Navigate, neutralize ShellHost Progress literal, rename addressed Progress test. (2) Empty-state four blocks + room-level centering wrapper as first real consumer SIGNED; do not collapse never-scanned vs healthy Readiness. (3) Rail order ACK (already Home-first). (4) Single-pane + deep links via same Navigate + Landed filter SIGNED. (5) Home/Live boundary SIGNED — no combat numbers on Home; session-summary fact in Core/UI.Shared; no raid/faction glance. Live parked for own Bevel pass. E-2d/E-2e parked. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for Home PR only.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~5:20 AM CT


### PR #301 — E-3 PR 3 Quests room lift (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #301 https://github.com/DranakCorps-bot/EQBuddy/pull/301 (`claude/evolved-quests-20260905` → `main`, head `c578baab`) — Quests LIFT (not move) after #300/`ae6947be`; against ~11:15 PM CT Quests-lift unlock.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on head** (at look: build green; e2e in flight). QuestsView extraction + thin QuestsWindow + QuestsRoom endorsed; rail four rows (Quests between Gear and World via RailOrder+Landed) endorsed. **Five-rule inventory ACK** — all five surface-owned / stayed put; **Alt+Tab correction ACK** (not a Quests rule; fifth is Turn-ins detail pane). **`SplitRoomWidth` 640→700 SIGNED** (picture-proven first-consumer; 400-wide list + 220 reward tiles; MinRoomWidth rule; stays clear of RailLabelWidth 720). Soft: stale `RoomSinglePane` param comment still says "no room expresses this yet" — drop or fix before merge (not a product block). **Character caption soft-endorsed** ("Quest Tracker — Name" as dim room caption; trap 26); Bevel may overturn as room-chrome — not needs-david. No HUD subtraction; QuestsWindow not retired; Progress default untouched; Home/Live parked for own Bevel passes. E-2d/E-2e parked. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Drop channel mailbox tip before merge; Helm lands on main. Claude kick via Dranak (`--model opus`) for CI → drop channel → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-05 ~12:15 AM CT

### Bevel E-3 rooms pre-design — Quests/Home/Live order + HUD subtraction + empty-state (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** `BEVEL.md` E-3 rooms pre-design (Quests next → Home → Live; HUD per-item subtraction; empty-state room/canvas); HELM-FEEDBACK 2026-09-05 ask; tip `02f67dc5` (post-#300 `ae6947be`).
- **Ruling:** **SIGNED. Quests lift unblocked.** Order Quests→Home→Live SIGNED (IA-satisfied first). HUD subtraction **per item** SIGNED (room on rail + chip shipped + screenshot parity; Live cards ≠ Live room PR). Empty-state: room centers position; canvas treatment per-surface; Gear "no dump yet" same when touched — SIGNED. Home PR must flip `ShellWindow` default off Progress — named requirement. Quests: inventory five presentation rules; shoot RoomSinglePane at 640 both sides. Home/Live parked for own Bevel passes. E-2d/E-2e parked. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for Quests lift only.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~11:15 PM CT


### PR #300 — E-3 PR 2 World + Gear rooms (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #300 https://github.com/DranakCorps-bot/EQBuddy/pull/300 (`claude/evolved-e3-pr2-20260905` → `main`, product head `ae6947be`; channel tip `cbc143e5`) — World + Gear rooms into shell after #299/`a4af2822`.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on head** (at look: build green; e2e in flight). World+Gear MOVE endorsed; Quests LIFT held for own PR; rail three rows; `IShellRoom.Release` + `ShellDumpFacts` endorsed. **Rooms-before-HUD amendment SIGNED** (FABLE literal "PR after host" yields to E-3 findability gate — do not force HUD as PR 2). **`MinRoomWidth` 520 SIGNED** (picture-tested; do not raise to max opening width). **Stars stay on v1 windows SIGNED** (retirement blockers in headers). `MainWindow` net-zero replace endorsed. Drop channel mailbox tip before merge; Helm lands on main. Illustration-lock non-reproducibility ACK (shot-refresh must reckon; harness freeze = own ask). E-2d/E-2e parked. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for CI → drop channel → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~10:55 PM CT


### PR #299 — E-3 PR 1 shell host (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #299 https://github.com/DranakCorps-bot/EQBuddy/pull/299 (`claude/evolved-e3-20260904` → `main`, head `b2f8bdfb`) — shell host + rail + Search chrome + Progress room; against ~9:25 PM CT Bevel nav sign.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green** (both green at look). Six nav points endorsed. **`ShellPage` = compile-time `PageFor` join into rail enum, NOT collapse of CompanionSurfaces.All (11 phone screens stay; World PR 4 Travel≠Map stands)** — amends ~9:25 "single source" wording. ProgressWindow not retired (second host + row-count asserts) SIGNED. `EQBUDDY_SHELL` only / no player door SIGNED. Separate shot-refresh docs PR authorized (own PR, no `src/`, not blocking). E-2d/E-2e parked. E-3 PR 2 after merge + own ask. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~10:15 PM CT


### Bevel Evolved shell nav pre-design — E-3 gate (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** `BEVEL.md` Evolved shell nav pre-design (answers FABLE.md E-3); HELM-FEEDBACK ~8:20 PM CT ask; tip `a40f33a8` (E-2c merged).
- **Ruling:** **SIGNED. E-3 unblocked.** Chrome = HistoryWindow native (not Progress overlay). Rail not tabs; no disabled unshipped rows; Search title-row + Ctrl+K palette; one `page:room` nav path; two degrade axes + MinWidth/MinHeight floor. **`ShellPage` join (PageFor total fn) for rail + mobile Screens — required; amended ~10:15 PM CT on #299 (not collapse of CompanionSurfaces.All).** Search chrome may land with E-3; disposition index waits on E-2e (do not block Progress host). First E-3 PR = host + nav + Progress only. E-2d/E-2e stay parked until own asks. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for E-3.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~9:25 PM CT


### PR #298 — E-2c Avalonia remove (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #298 https://github.com/DranakCorps-bot/EQBuddy/pull/298 (`claude/evolved-e2c-20260904` → `main`, head `b064f58b`) — pipeline then deletion + docs; #296/`24642fda` + #297/`2d25cdf0` already on main.
- **Ruling:** **SIGNED. Merge when `build-and-test` + `e2e-windows` green on head.** Pipeline/deletion/docs split endorsed; E-1 evolved CI + `check.ps1` `evolved` survive; Avalonia lane gone; disposition EXECUTED; ClassSourceWriters drop-with-file; WPF ratchet **4,273 stands**; TestPlan Manual — §6 honest; Don Thompson credit rewrite (not written out); Wine KEEP untouched. **Protection: AUTHORIZED to drop `build-avalonia-linux` from required contexts at merge** (same motion; else forever-waiting). **`e2e-windows` required NOT YET** — after clean green on main post-merge, not tonight (#296 tick-freeze lock risk). **Guard check 4 KEEP SIGNED** (trigger match, prove-fail, #297 shape). E-2d/E-2e parked until merge + own asks. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for CI → protection drop → merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~8:05 PM CT


### PR #297 — V1 `-EvolvedLocal` no-installer rider (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #297 https://github.com/DranakCorps-bot/EQBuddy/pull/297 (`claude/evolvedlocal-no-installer` → `main`, head `76bd5ffe`) — Fable V1 defect 1 as own tiny PR (parallel to #296).
- **Ruling:** **SIGNED. Merge when `build-and-test` + `build-avalonia-linux` green.** Soft lean confirmed: skip ISCC + installer sign + setup `.sha256`; keep portable zip + hash; app-exe signing stays. **Fourth guard token SIGNED** (ACTS not filename; guard was green on the hole). **Leftover 2.x installer NAMED not deleted SIGNED** (loud warn, no `Remove-Item`). Scripts/docs only; no `src/`. e2e unrelated — re-run only if red. #296 path unchanged (e2e re-run → three green → merge); E-2c stays parked until #296 merges. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for merge.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~7:35 PM CT

### PR #296 — E-2b scanners (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #296 https://github.com/DranakCorps-bot/EQBuddy/pull/296 (`claude/evolved-e2b-scanners` → `main`, head `93703e15`) — successor to closed #295 after #294 deleted its base.
- **Ruling:** **SIGNED. Merge when all three CI green on head.** Scanners + disposition docs only (19 files, no `src/`). FocusHide vacuous-fix replacement, SurfaceOwnership WPF re-point, ClassSourceWriters keep-until-E-2c, Architecture tombstone (4,273 stands) all endorsed. **Eight-green bar does NOT carry** from #294 — one full green of all three jobs is enough. At look: unit/Avalonia green; e2e failed on tick-freeze (`SessionGoesLive`, app stopped ticking / `logPending=141`) — **re-run e2e only**, do not expand #296. E-2c parked until this merges. Soft lean for separate V1 `-EvolvedLocal` PR: skip ISCC/sign/setup-hash, keep zip+hash. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for e2e re-run + merge + E-2c.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~7:30 PM CT

### PR #294 — E-2a disposition + e2e un-gate (SIGNED)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #294 https://github.com/DranakCorps-bot/EQBuddy/pull/294 (`claude/evolved-e2-20260904` → `main`, head `dd29074b`) — re-ask after eight consecutive greens.
- **Ruling:** **SIGNED. Merge now.** Eight+ consecutive greens verified on `dd29074b` (nine runs: eight dispatch + one `pull_request`). Disposition table + shipping-lane ports + e2e un-gate endorsed. One-moment dump (`PaintOneMoment` / `surfacesBehind` as assert / `tick` abort) endorsed — prior equality sail-past blocker cleared. Avalonia assembly parallelization fix **stays in #294** (do not split; same bar-reachability call as wiki flake). MainWindow 4,699/4,700. Nothing deleted; no WhatsNew/Version/publish. **#295 E-2b unparked** after this merge (then E-2c). V1 `-EvolvedLocal` ISCC rider = own tiny PR (parallel with E-2b OK). Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`) for merge + E-2b.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~7:20 PM CT

### Mailbox 6:30 PM CT — #294 still unsigned; #295 E-2b PARKED (2026-09-04)
- **Kind:** posture / gate
- **Thread / subject:** Post-6pm mailbox. PR #294 head `62af8f69` still flaking E2E equality `WaitForDump` (e.g. `33927715046`); PR #295 E-2b opened stacked on #294 against the signed gate.
- **Ruling:** **#294 NOT SIGNED** (prior ~5:50 stands; eight consecutive greens unmet; no re-ask yet). **#295 PARKED** — do not last-look or merge until #294 is signed; do not retarget to main; do not start E-2c. No new Scribe/Bevel intake. Live Holds empty. Play Console OFF. Not needs-david. Do not cut `v1.99.19`. Claude on #294 only; kick via Dranak (`--model opus`) if idle.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~6:30 PM CT

### PR #294 — E-2a disposition + e2e un-gate (NOT signed — superseded ~7:20 SIGNED)
- **Kind:** sign-off / posture / gate (blocked)
- **Thread / subject:** PR #294 https://github.com/DranakCorps-bot/EQBuddy/pull/294 (`claude/evolved-e2-20260904` → `main`, heads `a9928175` → `56782e55`) — E-2a Avalonia disposition table + un-gate `e2e-windows`.
- **Ruling:** **NOT SIGNED. Do not merge.** Disposition table shape + shipping-lane ports + un-gate *intent* endorsed. Blocker: PR's own eight-consecutive-green bar unmet; `SessionGoesLive` / `KillThenLoot` still flake on equality `WaitForDump` sail-past after `ingestDone` settle (runs `33924866579`, `33925423795`). Fix assert shape, post eight greens on PR, re-ask. **Notes:** MainWindow 4,699/4,700 hard constraint acknowledged (E-3 lift). V1 `-EvolvedLocal` ISCC rider = own tiny PR after #294 mergeable / parallel with E-2b — not folded into E-2b/c; does not block E-2b once E-2a signed. Do not start E-2b until E-2a signed. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~5:50 PM CT (reject)

### Fable E-0/E-1 review — GO on E-2 (2026-09-04 ~4:40 PM CT)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** Fable `claude-fable-5` executed-diff last-look (`FABLE-FEEDBACK.md` ~4:35; HELM-FEEDBACK ask). Main review commit `f725abfd`. #292 MERGED `ac4d12ca`; #293 E-1 MERGED `c4d41edf`.
- **Ruling:** **GO on E-2.** Plan stands. Gate met (#275 CONFIRMED ~3:40; E-0 complete; E-1 landed; Fable review clears). Amendments endorsed (`LegacyNoticeRenderTests` E-2a row; E-1 evolved CI + `check.ps1` `evolved` survive E-2c). **V1 rider:** skip ISCC under `-EvolvedLocal` (own small PR, not the deletion diff). Start at E-2a. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Claude kick via Dranak (`--model opus`).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~4:40 PM CT

### PR #293 — E-1 Evolved local-only mechanism (sign-off)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #293 https://github.com/DranakCorps-bot/EQBuddy/pull/293 (`claude/e1-evolved-local` → `main`, head `7cd8562a`) — Evolved local-only E-1 under ~3:50 authorization.
- **Ruling:** Signed. Merge when both CI green (Avalonia green; build-and-test flake `EqlWikiMobsTests.NoMoreThanTwoFetchesAreEverInFlight` re-run, do not fix product). Mechanism endorsed: major≥2 refusal before publish; `-EvolvedLocal` subtractive only; OneDrive + `gh release create` + `/SILENT` in one region; signing unchanged; guard before Version bump; numeric 2.0.0; `install-local.ps1 -Evolved` portable-only; LEGACY-007 WhatsNew 2.0.0 links v1.99.18; v1.99.18 notice untouched; CI guard step endorsed; both DECISIONS calls endorsed. Residual `release-assets.yml` = E-2. After merge: **E-1 landed → E-2 may start** (#275 already confirmed). Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets. Also merge signed #292 when its CI green.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~4:12 PM CT

### PR #292 — fold-sentence (b) CLAUDE.md (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #292 https://github.com/DranakCorps-bot/EQBuddy/pull/292 (`claude/fold-sentence-b` → `main`, head `0c745f3b`) — tiny CLAUDE.md follow-up for fold-sentence ruling (b); E-1 started.
- **Ruling:** Signed. Merge when both CI green (Avalonia green; build-and-test flake `SettingsClobberTests.AForeignWriteBetweenLoadAndSaveIsReported` re-run, do not fix product). Exactly ruling (b): folded card's NAME on absorbing card; no own row; Motes exception; does not prejudge #251. Not (a)/(c). No WhatsNew. E-1 already authorized and in flight (local Authenticode EvolvedLocal in scope; Play Console / Tag / Prerelease / prod secrets OFF). Not a hold. Not needs-david. Live Holds empty. After E-1 lands: E-2 per sequence. Do not cut `v1.99.19`.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~3:55 PM CT

### PR #291 — E-0d docs truth + illustration lock + fold-sentence (b) (sign-off)
- **Kind:** sign-off / posture / ruling
- **Thread / subject:** PR #291 https://github.com/DranakCorps-bot/EQBuddy/pull/291 (`claude/e0d-docs-truth` → `main`, head `fc98fcac`) — Evolved local-only E-0d; fold-sentence ruling ask.
- **Ruling:** Signed. Both CI green at look. Merge now. README / FeatureGuide / options-cards.png / illustration lock endorsed. **Fold-sentence (b):** correct `CLAUDE.md` three-ways-back to "folded card's *name* returns on the absorbing card in Options → Cards & windows"; not (a)/(c); does not prejudge #251. Land (b) as tiny CLAUDE.md follow-up before E-1. E-0 complete after #291 merge. **E-1 authorized** under signed Evolved local-only plan (local Authenticode for `install-local.ps1 -Evolved` in scope; Play Console / Tag / Prerelease / prod secrets still OFF). Not a hold. Not needs-david. Live Holds empty. After (b)+E-1: E-2 per sequence. Do not cut `v1.99.19`.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~3:50 PM CT

### PR #290 — E-0c LEGACY-002 offline gate proof + #275 checklist confirm (sign-off)
- **Kind:** sign-off / posture / gate
- **Thread / subject:** PR #290 https://github.com/DranakCorps-bot/EQBuddy/pull/290 (`claude/e0c-gate-proof` → `main`, head `aa3d125f`) — Evolved local-only E-0c / #275 LEGACY-002 painted proof; #275 checklist confirmation ask (E-2 gate).
- **Ruling:** Signed. Both CI green at look. Merge now. Offline gate proof endorsed (Avalonia render + mutation prove-fail + read-only endpoint observation; wire proof deferred to channel-open). **#275 Phase 0 checklist CONFIRMED** for E-2 gate: LEGACY-002/007 judgement calls endorsed (release-time rows stay release-time). Not a hold. Not needs-david. Live Holds empty. Play Console OFF. After merge: E-0d → E-1 → E-2 (Avalonia remove) per signed sequence. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~3:40 PM CT

### PR #289 — E-0b legacy-v1 + protections (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #289 https://github.com/DranakCorps-bot/EQBuddy/pull/289 (`claude/e0b-legacy-branch` → `main`, head `fff60f93`) — Evolved local-only E-0b / #275 LEGACY-004/005.
- **Ruling:** Signed. Merge when both CI green (`build-and-test` + `build-avalonia-linux`). Docs catch-up only. Out-of-tree: `legacy-v1` tip verified `dbcfb3a1` (= `v1.99.18`); ruleset LEGACY-004 verified active (tag `v1.99.18`, deletion + non_fast_forward, no bypass). Branch protection: no delete/force/`enforce_admins`, no required CI/reviews — endorsed (preserved not maintained; patch door left open). #288 already merged. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. After merge: E-0c → E-0d. Do not start E-2 / Avalonia remove until Helm confirms #275 checklist. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~3:27 PM CT### PR #288 — E-0a re-pin v1.99.18 + final-tag guard (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #288 https://github.com/DranakCorps-bot/EQBuddy/pull/288 (`claude/e0a-legacy-repin` → `main`, head `b3ca7814`) — Evolved local-only E-0a / #275 LEGACY-001 re-pin.
- **Ruling:** Signed. Merge when both CI green (`build-and-test` + `build-avalonia-linux`). Re-pin `LEGACY-V1.md` + README Legacy section to live `v1.99.18` endorsed (#284 pre-auth). Guard check 4 (newest `v1.*` in link or prose; loud skip if no tags) endorsed; prove-fail on pre-fix tree endorsed. No WhatsNew. No player string / LEGACY notice change. E-0b still owns *"Phase 0 work… not done yet"* rewrite. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. After merge: E-0b → E-0c → E-0d as separate PRs. Do not start E-2 / Avalonia remove until Helm confirms #275 checklist. Do not cut `v1.99.19`. Do not touch Play Console / signing / prod secrets.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~3:25 PM CT

### Fable Evolved local-only development plan — last-look signed (2026-09-04 ~3:15 PM CT)
- **Kind:** sign-off / posture
- **Thread / subject:** `FABLE.md` newest `ready` item — EQBuddy Evolved LOCAL-ONLY development start (owner GO ~2:52 PM CT); commit `094bff3f`; HELM-FEEDBACK last-look ask.
- **Ruling:** Signed. **Gate proof 4 reconciled:** offline proof + deferred wire-proof **opens E-2** after E-0 checklist evidence; LEGACY-002 = code landed, wire proof deferred to channel-open; **no real prerelease** (local-only). Sequencing confirmed: E-0 → E-1 → Helm confirms #275 → E-2 → Bevel nav → E-3. E-1 OneDrive structural refusal endorsed (no publish-2.x switch; signing unchanged). E-0a re-pin + final-tag guard endorsed. E-0b `legacy-v1` before E-1 bump endorsed. E-0d repo-markdown only (tour/`v1.99.19` still closed). `WineFonts.cs` + `TextProbeWindow.cs` KEEP. Delete `release-assets.yml` on Evolved mainline endorsed. Not a hold. Not needs-david. Live Holds empty. Play Console OFF. Claude takes E-0 via Dranak; each PR Helm last-look. Do not start Avalonia remove until checklist confirmed. Do not touch Play Console / signing / prod secrets.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~3:15 PM CT

### Bevel Evolved staging IA pass #2 — door 2 retired + illustration lock (2026-09-04 ~3:08 PM CT)
- **Kind:** sign-off / posture
- **Thread / subject:** Bevel pass on main `103d8fec` — `BEVEL.md` Evolved staging IA pass #2; addendum on `docs/BEVEL-v2-staging-critique.md` retiring §6 door 2; Evolved illustration-recipe lock candidate.
- **Ruling:** Signed. **Door 2 CLOSED** — keep shipped LEGACY notice verbatim; no outstanding voice pass. Doors 1 and 3 still locked. Destination (HUD + one shell) unamended. **Illustration lock signed** for Evolved: capture-with-recipe or do not ship (trap 22). Stale `v1.99.18` tour/README weighed — **do not reopen final v1 bag; no `v1.99.19` without owner.** Evolved must not port stale tour/README. Not a hold. Not needs-david. No implement. Live Holds empty. Play Console OFF. Soft: Bevel pre-design line on PR bodies (guidance).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~3:08 PM CT

### Public replies — #208/#264/#252/#273 shipped-status (2026-09-04 ~2:28 PM CT)
- **Kind:** public-reply / process
- **Thread / subject:** Shipped-status posts after `v1.99.18` (Scribe posted ~2:23 PM CT without HELM-FEEDBACK draft filing).
- **Ruling:** Retro-SIGNED #208 / #264 / #273 as posted. **#252** needs signed follow-up (caveat: hide once more if already restored). Process: drafts must land in HELM-FEEDBACK and wait for SIGNED before post. Live Holds empty. Not needs-david. Do not restore #208 hold.
- **Condition:** n/a (process)
- **Signed:** Helm, 2026-09-04 ~2:28 PM CT

### Ship — v1.99.18 LIVE (2026-09-04 ~2:20 PM CT)
- **Kind:** ship / posture
- **Thread / subject:** GitHub release/tag `v1.99.18` published (Play Console OFF). Target `dbcfb3a1` (bag includes `#287`/`abf55a94` and prior final-v1 items).
- **Ruling:** **LIVE.** Final v1 bag closed and tagged. Live Holds empty. Play Console OFF. No public `#208`/`#264`/`#252` replies until Helm-signed Scribe drafts. Do not start Phase 1 / remove Avalonia. Do not open `#261`/`#262`. Evolved local-only until owner says ready. Not needs-david for further tag work.
- **Condition:** n/a (process)
- **Signed:** Helm, 2026-09-04 ~2:20 PM CT
- **Update 2:28 PM CT:** #208/#264/#273 retro-signed as posted; #252 follow-up draft signed in HELM-FEEDBACK.

### PR #287 — #208 Mobile sounds (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #287 https://github.com/DranakCorps-bot/EQBuddy/pull/287 (`claude/208-mobile-sounds` → `main`, head `584a0e23`) — final v1 bag #208 (sbaum23). Bevel #283 lock on main.
- **Ruling:** Signed. **MERGED** on main as `abf55a94` (2026-09-04 ~1:49 PM CT). Matches Bevel lock line-by-line (one Mobile sounds toggle, default Off, helper literal, Mobile-only, no sample/per-event/volume/force-On). Policy in `UI.Shared/MobileAlertSounds`; wire is switch+count on envelope; first-touch unlock; WPF MainWindow stays 4,699 (no ratchet bump). WhatsNew FIXED endorsed. Soft: SettingsClobber flake — re-run only. Not a hold. Not needs-david. Final v1 bag product work complete (#252/#264/#208 on main). **Next: tag/release `v1.99.18` from main (Play Console OFF).** No public #208/#264/#252 replies until Helm-signed drafts. Do not start Phase 1 / remove Avalonia. Do not open #261/#262. Do not touch Play Console / signing / prod secrets. Live Holds empty (#208 Retired for this cut).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~1:46 PM CT; merged confirmed ~1:50 PM CT

### PR #286 — #264 pairing Wi-Fi vs ethernet IP (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #286 https://github.com/DranakCorps-bot/EQBuddy/pull/286 (`claude/264-pairing-wifi-ip` → `main`, head `33c80982`) — final v1 bag #264 (brhanson2-cyber).
- **Ruling:** Signed. **MERGED** on main as `3b6fff2f` (2026-09-04 ~1:43 PM CT). Two halves both endorsed (Wi-Fi tiebreak + pairing address picker). No public #264 reply until Helm-signed draft after tag unless asked. Not a hold. Not needs-david. Remaining before tag `v1.99.18`: merge **#287** (#208). #252 already on main via #285. Do not start Phase 1 / remove Avalonia. Do not touch Play Console / signing / prod secrets. Live Holds empty (#208 Retired for this cut).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~1:40 PM CT; head bump ~1:41–1:42 PM CT; merged confirmed ~1:46 PM CT

### PR #285 — #252 cards reset stay hidden (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #285 https://github.com/DranakCorps-bot/EQBuddy/pull/285 (`claude/252-cards-reset` → `main`, head `432c17c7`) — final v1 bag #252 (TiconaX).
- **Ruling:** Signed. **MERGED** on main as `a223c628`. Core-only fix both lanes. Delete `ApplyDefaultGearSection` endorsed; do not restore destroyed HiddenSections endorsed; WhatsNew "hide once more" blunt line endorsed. Soft: #251 Faction out of bag — if it returns later, drop AbsorbedCardKeys in same commit (idempotence guard). No public reply until Helm-signed draft after ship. Not a hold. Not needs-david. Remaining before tag `v1.99.18`: merge #286 (#264) then #208 mobile sounds. Do not start Phase 1 / remove Avalonia. Do not touch Play Console / signing / prod secrets. Live Holds empty (#208 Retired for this cut).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~1:35 PM CT; merged confirmed ~1:37 PM CT

### PR #284 — P0-3 LEGACY-006/007 support matrix + legacy-notice-guard (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #284 https://github.com/DranakCorps-bot/EQBuddy/pull/284 (`claude/p0-3-legacy-docs` → `main`, head `66e3460b`) — #275 Phase 0 item P0-3.
- **Ruling:** Signed. Docs + `scripts/legacy-notice-guard.ps1` only; no product behaviour. **Ask 1 endorsed:** keep asset links on published `v1.99.17` until bridge tag exists; prose may name planned `v1.99.18`; re-pin on publish. **Ask 2 endorsed:** bridge WhatsNew highlight on unreleased 1.99.18 with Don Thompson + quasarj by name, no URL. LEGACY-005 = v1/MIT only; Evolved ARR. Merge when both CI green (`build-and-test` was green at look; Avalonia `EqlWikiMobsTests` concurrency flake — re-run, do not "fix" product). Not a hold. Not needs-david. After merge: Final v1 bag implement next (#208 per Bevel #283 lock, then #264, #252). Do not tag until those three land. Do not start Phase 1 / remove Avalonia. Do not touch Play Console / signing / prod secrets. Live Holds empty (#208 Retired for this cut).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~1:25 PM CT

### Final v1 scope LOCKED — owner 2026-09-04 1:14 PM CT (priority)
- **Kind:** priority / posture
- **Thread / subject:** Final v1 bag for tag `v1.99.18`
- **Ruling:** Owner locked final v1 scope. **In bag only:** already on main (#271 Sky, #270 Band B, #279 P0-1, #282 P0-2, #274 XP/#273) plus authorize V0–V1 now for **#208 mobile sounds (opt-in/off)**, **#264 Wi-Fi vs ethernet pairing IP**, **#252 cards reset to Gear+Motes**. **Out of bag:** #261, #262, and any other waiting/someday — do not open. **#208 HOLD lifted** for this mobile-sounds work only (see Retired). After those three merge + CI green: tag/release `v1.99.18` from main (Play Console OFF). P0-3 may continue as Phase 0/#275 bridge docs only — not a product-scope expand; not a blocker on the three. No Phase 1. No Avalonia remove. Not needs-david for routine merges inside this bag. Claude kicks via Dranak; each PR Helm last-look.
- **Condition:** n/a (owner lock for this cut)
- **Signed:** Helm, 2026-09-04 1:14 PM CT (owner)

### PR #282 — P0-2 LEGACY-002 non-Windows update notice (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #282 https://github.com/DranakCorps-bot/EQBuddy/pull/282 (`claude/p0-2-legacy002` → `main`, head `a78f8c65`) — #275 Phase 0 item P0-2 / LEGACY-002.
- **Ruling:** Signed. Merge when both CI green (`build-and-test` + `build-avalonia-linux`). One shared `LegacyPlatformUpdatePolicy` (record not bool) + six call sites endorsed. Windows unchanged; 1.x patches still offered; non-Windows never offered major-2. `GitHubLegacyReleasePage` = running build tag (not hard-coded bridge) endorsed — 404 risk on a premature literal; P0-3 may pin later. No WhatsNew here — bridge entry is P0-3 with Don Thompson + quasarj credits. WPF ratchet 4,214→4,273 (min fit, one line headroom) endorsed; next WPF change lifts a surface. Not a hold. Not needs-david. Do not start Phase 1 / remove Avalonia / tag / publish real prerelease. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds). After merge: P0-3 next.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~1:15 PM CT

### PR #279 — P0-1 release.ps1 -Prerelease (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #279 https://github.com/DranakCorps-bot/EQBuddy/pull/279 (`claude/p0-1-prerelease` → `main`, head `a5a0e09b`) — #275 Phase 0 item P0-1 / bridge release mechanics.
- **Ruling:** Signed. Merge when both CI green (`build-and-test` + `build-avalonia-linux`). First look: unit/`check` green; Avalonia render failed once on `ZoneWindowsRenderTests.MapCircleMenuConfirmsThenRemovesTheSpawnPoint` (headless thread-affinity cleanup) — flake, not this PR; re-run, do not "fix" product code. Branch is 4 commits behind main (#276 docs only; no file overlap) — rebase or merge-base fine. Scope endorsed: `[switch]$Prerelease` → conditional `--prerelease` on `gh release create`; refuse `-Prerelease` without `-Tag`; pin `UpdateChecker` `/releases/latest`; `ParseRelease` nulls `v2.0.0-beta1` and still accepts `v2.0.0` (trap 39). Hypothesis on `releases/latest` exclusion stays labelled until P0 gate proof 4. No WhatsNew (tooling). OneDrive + local `/SILENT` deliberately uncovered. Do not publish a real prerelease here. Do not start P0-2 until this merges. Do not start Phase 1 / remove Avalonia / tag. Not a hold. Not needs-david. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~12:30 PM CT

### PR #278 — Bevel v2 staging UX critique on main (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #278 merge de2fd6f6 (head 360f89f6) — Bevel Helm-signed v2 staging UX critique (HUD + one shell). Docs: BEVEL-FEEDBACK / BEVEL.md / docs/BEVEL-v2-staging-critique.md.
- **Ruling:** Bevel UX destination **locked on main**. Parallel to Phase 0; not blocking Phase 0 docs/code design. Not a hold. Not needs-david. Do not tag. #208 stays live (do not open mobile sounds).
- **Signed:** Helm, 2026-09-04 ~12:06 PM CT

### Evolved license constraint — #277 amend + docs #276 (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #277 amend `cacda888` (FABLE.md license constraint) + docs PR #276 https://github.com/DranakCorps-bot/EQBuddy/pull/276 head `d5019645` (`LICENSE-EVOLVED.md` + PRODUCT/LEGACY/README/ROADMAP).
- **Ruling:** Signed. Owner line stands: Evolved proprietary ARR; published 1.x MIT stays; LEGACY-005 fork invite = v1 only. Phase 0 must not assume Evolved is MIT/forkable; no license code in Phase 0. #276 is the public language — merge when CI green (Avalonia ThemeBodyCap flake on docs-only head is not a docs block; re-run). Prior #277 plan sign (~12:05) still stands; rebase #277 onto main, drop HELM-FEEDBACK channel commit, keep `cacda888`, then merge. Not a hold. Not needs-david. Do not tag. Do not start Phase 1 / remove Avalonia. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~12:07 PM CT

### PR #277 Fable v2 Phase 0–2 plan — last-look signed
- **Kind:** sign-off / posture
- **Thread / subject:** PR #277 https://github.com/DranakCorps-bot/EQBuddy/pull/277 — Fable technical decomposition (#275 / charter §25 item 6). Head plan `cba10e27` + license amend `cacda888`; drop channel note `d4679a79` before merge. Superseded for license wording by the ~12:07 entry above.
- **Ruling:** Signed. Phase 0 `ready`; Phase 1 BLOCKED on Phase 0 gate; Phase 2 is Bevel seam sketch only. Charter may land under `docs/v2/`. Wine/CrossOver (P1-4): drop three Options knobs; keep `TextRenderingPolicy` + `WineText`; overlay/crossover scripts go with platform cut. LEGACY-007 whatsnew-style guard: yes. Tag/branch protection on bridge + `legacy-v1`: yes when they exist. P1-3 workflow-ref: verify before delete vs guard. Merge #277 after dropping HELM-FEEDBACK.md; then Claude may start Phase 0 PRs (P0-1 first) as origin PRs against main. Do not start Phase 1 / remove Avalonia / tag. Not needs-david. #208 stays live (do not open mobile sounds).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-09-04 ~12:05 PM CT

### EQBuddy v2 Phase 0 / #275 — Windows-only posture (sign-off)
- **Kind:** posture / sign-off
- **Thread / subject:** #275 https://github.com/DranakCorps-bot/EQBuddy/issues/275 — v2 Phase 0 / LEGACY bridge. Charter rev 1.1 (2026-09-04). Closed as not-planned (v2 Windows-only / legacy preserve): #6 #7 #50 #53 #58 #254 → pointed at #275.
- **Ruling:** Windows-only v2 is **owner-approved** and the **only supported** line (Windows desktop + Mobile hosted by Windows). **Do not take v1 down** — final v1 stays downloadable/usable; we stop supporting it further. Phase 0 / #275 is open. Bevel UI/UX revisit (HUD + one Windows shell) is staging in parallel — not blocking Phase 0 docs/code design. **Avalonia removal blocked** until Phase 0 gate (LEGACY checklist). Fable: technical decomposition of Phase 0–2 only (charter §25 item 6) — plan, not implement, until Helm signs. Do not debate product direction. Do not tag. Do not touch Play Console / signing / prod secrets. Live hold still only #208 (do not open mobile sounds). **Not needs-david** for in-charter Phase 0 engineering.
- **Signed:** Helm, 2026-09-04 11:50 AM CT

### PR #274 — on main (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #274 merge `c5f05400` (head `feed95dc`); prior sign 2026-09-04 10:05 AM CT
- **Ruling:** Merged on main. Loop closed. Do not tag. Do not start #250. Do not fold #264. Do not retouch 320-cap / #240. Do not touch Play Console / signing / prod secrets. WhatsNew 1.99.18 FIXED kept. Not needs-david. #208 stays live (do not open mobile sounds).
- **Signed:** Helm, 2026-09-04 10:08 AM CT

### PR #274 — #273 bonus-XP XpRx (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #274 `claude/273-bonus-xp` head `feed95dc` → main. Bonus-weekend `XpRx` parse for discussion #273.
- **Ruling:** Signed. Merge now (both CI green at look). Optional non-capturing `(with a bonus)` between noun and `!` endorsed; WhatsNew 1.99.18 FIXED line stays; no props bump; no tag. Not a hold. Not needs-david. Do not start #250. Do not fold #264. Do not retouch 320-cap / #240. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds).
- **Signed:** Helm, 2026-09-04 10:05 AM CT


### #273 bonus-exp XP reg break (sign-off)
- **Kind:** sign-off / authorize
- **Thread / subject:** #273 brhanson2-cyber — bonus XP weekend changed XP message; EQBuddy registers zero XP. https://github.com/DranakCorps-bot/EQBuddy/discussions/273
- **Ruling:** Evidence gate cleared (literal line 9:08 AM CT: `You gain experience (with a bonus)! (3.200%)`). **Authorized V0–V1 now** on `XpRx` in `LogParser.cs` — weekend live (through ~Sep 7), players losing XP tracking. Thank-you for the paste signed (Scribe may post as drafted). Do not write FABLE.md. Do not fold into #264. Do not tag. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds). Not needs-david. Claude kick via Dranak.
- **Signed:** Helm, 2026-09-04 9:52 AM CT (supersedes 8:50 AM waiting)

### PR #271 — on main (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #271 merge `db4514da` (head `4ca921ce`); prior sign 2026-09-03 1:20 PM CT
- **Ruling:** Merged on main. Loop closed. Do not retag. Do not start #250. Do not retouch 320-cap / #240. Do not touch Play Console / signing / prod secrets. Soft chrome left as filed. Not needs-david. #208 stays live (do not open mobile sounds). Bevel docs PR #272 (mailbox lock only) may merge.
- **Signed:** Helm, 2026-09-03 1:18 PM CT

### PR #271 — Sky bags / folds / Alt+Tab (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #271 `claude/sky-completion-folds-alttab` head `4ca921ce` → main at `d0dfa235`. Sky bags / folds / Alt+Tab.
- **Ruling:** Signed (pre-merge). Auto-mark on ownership; Ready unlocked caveat annotate-not-hide; three band folds session-only default OPEN; Sky inventory ⧉ OK (does not reopen #243 Inventory annotate); Alt+Tab main-widget fix. Soft chrome left. Not a hold. Not needs-david. Do not start #250. Do not retouch 320-cap / #240. Do not tag. #208 stays live (do not open mobile sounds). Superseded for next-step by the on-main ruling above.
- **Signed:** Helm, 2026-09-03 1:20 PM CT

### PR #270 — #243 Band B Detail leads with caveat (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #270 `claude/243-bandb-detail` code `cb9ed926` → main. Core Band B Detail reordered to lead with caveat.
- **Ruling:** Signed. Merge when both CI checks green. WhatsNew: yes — add unreleased 1.99.18 one-liner + Directory.Build.props bump on the branch before merge; do not tag. Stale mobile-sky-leftovers.png: not a block; re-shoot after. Drop HELM-FEEDBACK.md and BEVEL-FEEDBACK.md from the PR before merge. Not a hold. Not needs-david. Do not fold #250/320-cap. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds).
- **Signed:** Helm, 2026-09-02 1:25 PM CT

### Bevel 1pm 2026-09-02 phone ports (sign-off)
- **Kind:** sign-off
- **Ruling:** #243 Band B Detail Core string leads with caveat; no `.sub` widen. #240 phone fold stays device-local. Claude may land Core-only #243 string. Do not tag. #208 untouched.
- **Signed:** Helm, 2026-09-02 1:13 PM CT

### PR #269 — #243 PR 2 phone Sky bands (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #269 `claude/243-sky-pr2` head `54d8a136` → main. Phone Sky leftover bands from same Core join as desktops.
- **Ruling:** Signed. Merge when CI green (both checks green at look). Dump-via-row-ids (not WrittenAt) endorsed — PR 1 #3 was outcome, not mechanism. Non-tickable group + ChecklistPrint note endorsed. #243 track complete after merge. Do not tag. Do not fold #250/320-cap. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds). Not a hold. Not needs-david.
- **Signed:** Helm, 2026-09-02 8:15 AM CT

### PR #268 — #243 PR 1 desktop Sky bands (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #268 `claude/243-sky-pr1` head `9fc1b862` (code `47996b4e`) → main. Desktop Sky leftover bands under Ready; SharedBank InBank fold from #265.
- **Ruling:** Signed. Merge when CI green (both checks green at look). Words-in-Core / character-classes-before-lens / dump-stamp-in-signature all endorsed. Drop HELM-FEEDBACK.md from the PR before merge. PR 2 phone may start on signature (not only after merge). Not a hold. Not needs-david. Do not tag. Do not fold #240/#250/320-cap. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds).
- **Signed:** Helm, 2026-09-02 7:30 AM CT

### #264 pairing NIC (sign-off)
- **Kind:** sign-off
- **Ruling:** #264 waiting not authorized (mobile pairing URL uses ethernet IP, not Wi-Fi). Talking is not #208. Do not implement. Do not write FABLE.md. Thank-you signed. #208 untouched.
- **Signed:** Helm, 2026-09-02 7:19 AM CT

### PR #267 — #240 PR 2 phone Level-ups (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #267 `claude/240-levelups-pr2` head `2583fbd0` → main. Phone Experience Level-ups fold from same LevelHistory rows.
- **Ruling:** Signed. Merge when CI green (both checks green at look). Device-local fold (not ShowLevelUps) endorsed. No MaxRows cap endorsed. Fingerprint-via-label endorsed. WhatsNew phone sentence stays. #240 track complete after merge. Do not tag. Do not fold #243/#250/320-cap. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds). Not a hold. Not needs-david.
- **Signed:** Helm, 2026-09-02 7:05 AM CT

### PR #266 — #240 PR 1 desktop Level-ups fold (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #266 `claude/240-levelups-pr1` head `ba8fc873` → main. Desktop Level-ups fold (WPF+Avalonia) drawing LevelHistory; WhatsNew 1.99.17 + version bump in PR.
- **Ruling:** Signed. Merge when CI green (both checks green at look). No MOVED badge endorsed. WhatsNew/props bump stay (320-cap no-WhatsNew was track-scoped). PR 2 phone may start on signature (not only after merge). Not a hold. Not needs-david. Do not tag. Do not fold #243/#250/320-cap. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds).
- **Signed:** Helm, 2026-09-02 6:42 AM CT

### PR #265 — #243 PR 0 branch (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #265 `claude/243-leftover-sky` head `9f45c56` → main. Diverged second cut of Core already on main at `6470c625`.
- **Ruling:** Close without merge. Superseded by accepted on-main PR 0. Do not rewrite Core API via this PR. Fold SharedBank InBank/GearLocker (and optional Line/headings helpers + TestPlan rows) into #243 PR 1 off main. Not a hold. Not needs-david. Claude continues PR 1 for last-look. Do not tag. Do not fold #240/#250/320-cap. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds).
- **Signed:** Helm, 2026-09-02 6:20 AM CT

### #243 PR 0 on-main disclosure (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #243 PR 0 `6470c625` on main (SkyLeftovers Core + AutoImportOutcome + 16 tests). Claude disclosed: should have been a PR; offered revert.
- **Ruling:** Accepted on main as-is. Do not revert / rewrite into a PR. Process miss on the record; PR 1+ must be PRs for last-look. Keep two-session split (#243 / #240). Claude may continue #243 PR 1 (desktop Sky bands). Do not tag. Do not fold #240/#250/320-cap. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds). Not needs-david.
- **Signed:** Helm, 2026-09-02 6:18 AM CT

### PR #263 — #240 PR 0 LevelHistory (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #263 `claude/240-levelups-pr0` head `ed46a40` → main. Core LevelHistory + tests + DECISIONS wall-clock gap.
- **Ruling:** Signed. Merge when CI green. Not a hold. Not needs-david. Claude continues PR 1 (desktop fold) after merge; bring each PR for last-look. Do not tag. Do not fold #243/#250/320-cap. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds).
- **Signed:** Helm, 2026-09-02 6:15 AM CT

### #243 / #240 presentation (sign-off)
- **Kind:** sign-off
- **Ruling:** Bevel presentation signed. #243 leftover Sky: two bands (A `No longer needed`, B `Other classes still want`); no Inventory annotate in V1. #240 Level-ups fold under Experience, default folded, SincePrevious tooltip-only. Tracks separate. Claude may implement. #208 untouched.
- **Signed:** Helm, 2026-09-02 6:03 AM CT

### #243 / #240 Fable plans (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** Fable V1 plans in FABLE-FEEDBACK.md — #243 Sky leftovers (tvongaza); #240 LevelHistory / xp timestamps (joeymavity)
- **Ruling:** Both plans posture-signed. Not a hold. Not needs-david. David V0–V1 auth 2026-08-29 still stands. Do not implement until Bevel product last-looks the presentation PRs. Do not fold into each other, #250, or 320-cap. Do not tag. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds). Optional #240 status reply signed for Scribe (Experience session line still exists; durable Level-ups fold planned).
- **Signed:** Helm, 2026-09-02 5:55 AM CT

### #261 / #262 intake (sign-off)
- **Kind:** sign-off
- **Ruling:** #261 waiting not authorized (debuff + Hot / GINA; do not lock hot-ready; do not fold into #94/#237; ask self vs others in the thank-you). #262 waiting not authorized (transparent server-status widget; new surface; talking is not #208). Thank-yous signed. Do not implement. Do not write FABLE.md. #208 untouched.
- **Signed:** Helm, 2026-09-01 1:10 PM CT

### PR #259 / #260 — on main (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #259 merge `2bb669be`; PR #260 merge `442e1160`; prior sign `78ee51ba`
- **Ruling:** Both on main. 320-cap track complete. Loop closed. Do not retag. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds). #250/#243/#240 not folded. No WhatsNew until release cut. Not needs-david. No more 320-cap work.
- **Signed:** Helm, 2026-08-31 5:40 PM CT

### PR #259 / #260 theme-body 320-cap PR 1-2 - signed
- **Kind:** sign-off / posture
- **Thread / subject:** PR #259 `claude/320-cap-pr1` head `f9d29d7d`; PR #260 `claude/320-cap-pr2` head `d98ebf4f` (base PR1).
- **Ruling:** Merge #259 to main, retarget #260 to main, merge #260. Monitor-granted ContentHeight via SectionMaxHeight endorsed. NestedBodyCap + keep-inner-scroller (trap 37/34) endorsed. 125%/chrome FYI for Bevel, not a block. Do not tag. #208 untouched. Not needs-david. Track complete after #260.
- **Signed:** Helm, 2026-08-31 5:30 PM CT

### PR #258 ThemeBodyCap arithmetic (320-cap PR 0) â€” signed
- **Kind:** sign-off / posture
- **Thread / subject:** PR #258 `claude/320-cap-pr0` head `1c822725`. ThemeBodyCap + ten tests. No UI callers yet.
- **Ruling:** Merge PR #258 (CI green). Chrome correction endorsed: `ContentHeight` is the SectionScroll viewport, so otherVisibleChrome is other cards' headers + this card's header/tab strip + in-stack margins only â€” do not subtract title bar / KPI / status again. Floor 320, ceiling 640, sibling bodies excluded, whole units â€” all stay. Claude continues PR 1 then PR 2; bring each for last-look. Do not tag. #208 untouched. Not needs-david.
- **Signed:** Helm, 2026-08-31 5:00 PM CT

### Theme-body 320-cap plan â€” Bevel signed, Claude authorized (sign-off)
- **Kind:** sign-off / posture
- **Ruling:** Bevel product last-look signed. Claude may implement PR 0â€“2. #250 Motes/SectionScroll OUT. #243/#240 stay Fable queue. Do not tag. #208 untouched. Not needs-david.
- **Signed:** Helm, 2026-08-31 4:47 PM CT

### v1.99.16 - shipped (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** tag `v1.99.16` at `d74bcb28`. GitHub release published. David's conditional go ("if no issues, ship").
- **Ruling:** Shipped. Loop closed. Do not retag. Do not touch Play Console / signing / prod secrets. A tag does not lift a hold: #208 stays live (do not open mobile sounds). In this tag: #253 watch-pin migration must-fix (gated + `UI.Shared/WatchPinMigration` with tests) and the weekly knowledge refresh (917 spell hovers). #252/#254 stay waiting not authorized. Scribe drafts shipped-status for #253 HiramDucky (Helm signs before post). No more 1.99.16 work.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-31 4:40 PM CT

### Theme-body 320-cap plan (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** Fable plan in FABLE-FEEDBACK.md answering Helm 2026-08-29 ask; Bevel product last-look still owed
- **Ruling:** Plan answers the four open inputs. Formula `clamp(ContentHeight - otherChrome, 320, 640)`, NaNâ†’320 always, ceiling 640 pre-scale (2x floor); SectionMaxHeight stays stack owner; GearCardView window-hosted 320 is PR 2 alone; Avalonia HeightGrip parity dissolved (already exists). #250 standalone Motes / SectionScroll stay OUT of this track (David 2026-08-29). Do not implement until Bevel signs the product last-look. #243 leftover Sky and #240 xp timestamps stay next in the Fable queue (separate research passes). Not a hold. Not needs-david. #208 untouched.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-31 4:40 PM CT

### PR #257 â€” on main (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #257 follow-up to #256; title-keyed description fallback; no KnownGaps
- **Ruling:** Merged at `b9c9d67d`. Loop closed. Catalog 1,353 described of 1,353. KnownGaps not written (premise was false). Title fallback after spellname index signed. Guard stays 100%. #246 qty=3 via ITEM_QTY_CORRECTIONS signed. Wiki notes in-repo; nothing self-publishes. Option 2 stays parked. PR #256 closed unmerged. Do not retag. Do not touch Play Console / signing / prod secrets. #208 stays live (do not open mobile sounds). No more #256/#257 work. Not needs-david.
- **Signed:** Helm, 2026-08-31 2:32 PM CT

### PR #257 knowledge refresh / KnownGaps premise false (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #257 follow-up to #256; title-keyed description fallback; no KnownGaps list
- **Ruling:** Merge PR #257 (head `9d26a9ed`) when GitHub checks are green. Do not merge #256 (close as superseded). KnownGaps not written â€” premise was false; do not build an empty exemption list. Title fallback after spellname index signed. Guard stays 100%. #246 qty=3 via ITEM_QTY_CORRECTIONS signed. Wiki notes in-repo, nothing self-publishes. Option 2 stays parked. Do not tag. #208 untouched. Not needs-david.
- **Signed:** Helm, 2026-08-31 2:25 PM CT

### PR #256 knowledge refresh / 24 no-prose spells (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** PR #256 eqlwiki harvest; KhazamSpellRow rename; 24 spells with no wiki prose
- **Ruling:** Do not merge PR #256 as submitted. Dual-template + spell-page description fallback on main (`9dbb542`) signed. Unblock via curated KnownGaps for the 24 (reason: no eqlwiki prose); do not weaken the description guard; do not use `effects` as description without Bevel. Wiki-first paste-ready for the 24 in parallel. Preserve #246 cask qty=3. Re-harvest then open clean PR for Helm last-look. Do not tag. #208 untouched. Not needs-david.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-31 2:05 PM CT

### #253 PR #255 â€” last-look signed (sign-off)
- **Kind:** sign-off / posture
- **Ruling:** Merge PR #255. Group-pin migration gated on WatchPinsMigrated, both lanes. Version staged 1.99.16. Do not tag. #208 untouched. #252/#254 stay waiting. Trap-47 extract is optional later Fable plan, not this PR.
- **Signed:** Helm, 2026-08-30 8:03 AM CT

### #253 PinWatchChips migration (sign-off)
- **Kind:** sign-off / posture
- **Ruling:** must-fix. V0â€“V1 authorized. Gate ungated group-pin migration on WatchPinsMigrated (WPF + Avalonia). Thank-you signed. Do not tag. Do not open #208. Not needs-david.
- **Signed:** Helm, 2026-08-30 5:20 AM CT

### #252 / #254 intake (sign-off)
- **Kind:** sign-off
- **Ruling:** #252 waiting not authorized (card reset Gear&loot+Motes). #254 waiting not authorized (macOS AltTab contributor; Don/Avalonia later). Thank-yous signed. #208 untouched.
- **Signed:** Helm, 2026-08-30 5:20 AM CT

### #250 / #243 / #240 V0â€“V1 authorized (sign-off)
- **Kind:** sign-off
- **Thread / subject:** David 2026-08-29 7:49 PM CT authorized V0â€“V1 for #250, #243, #240
- **Ruling:** David 2026-08-29 7:49 PM CT authorized V0â€“V1 for #250 (Motes/section-scroller, not theme-body 320), #243 leftover Sky after dump, #240 xp timestamps. #251 stays no-card. #208 stays held. Not a hold. Not in 1.99.15. Do not tag. Do not restore Holds.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-29 7:49 PM CT

### v1.99.15 â€” shipped (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** tag `v1.99.15` at `ee2f777`. GitHub release published. David's in-session go.
- **Ruling:** Shipped. Loop closed. Do not retag. Do not touch Play Console / signing / prod secrets. A tag does not lift a hold: #208 stays live (do not open mobile sounds). #250/#251/#243 not in this tag. No reporter on either 1.99.15 feature (both David's in-session asks) so Scribe owes nothing new for this tag. #241/#246 shipped-status drafts from 1.99.14 posted/signed (Helm signs before post). V1 follow-up noted for a future loop: `release.ps1`/`check.ps1` guard relating top What's-new to existing tags. No more 1.99.15 work.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-28 9:50 PM CT

### v1.99.14 â€” shipped (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** tag `v1.99.14` at `b4efb35`. GitHub release published. David's re-check-then-go.
- **Ruling:** Shipped. Loop closed. Do not retag. Do not touch Play Console / signing / prod secrets. A tag does not lift a hold: #208 stays live (do not open mobile sounds). #250/#251/#243 not in this tag (thank-yous already posted for #250/#251). Scribe drafts shipped-status for #241 DasGud and #246 jlcrisp (both credited in What's-new); posted/signed. No more 1.99.14 work.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-28 8:36 PM CT

### #250 / #251 thank-you (sign-off)
- **Kind:** sign-off
- **Thread / subject:** #250 Paineless motes dropdown / window stretch; #251 skwayb faction list
- **Ruling:** Thank-yous signed (Scribe drafts). Claude may post as written, #250 then #251, as DranakCorps-bot. Do not wait for Grok Scribe host. Do not rewrite. Do not implement. Do not restore a standalone Faction card. Do not fold into #227/#228 or each other. Not holds. Bevel owns 320-cap vs pop-out and motes-vs-faction restore. #208 untouched. Not in v1.99.14.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-28 8:10 PM CT


### #226 follow-up draft (sign-off)
- **Kind:** sign-off
- **Thread / subject:** #226 LeBigNasty leftover asks
- **Ruling:** Scribe posts a thank-you that the two leftovers (pack mote filter; client-side ignore) are captured. No promises. Not a close.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-22

### #235 import Apply (sign-off)
- **Kind:** sign-off
- **Thread / subject:** #235 LeBigNasty Import achievements button
- **Ruling:** Claude posts the signed follow-up tonight (Scribe already posted the capture thank-you). The button is not dead. Apply (0) is grey because the preview already marked everything. Authorize a small wording fix so a zero-apply preview says so on the button itself. No date. Not #101. Not a hold.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-23 evening

### World Bevel amendment (sign-off)
- **Kind:** sign-off
- **Thread / subject:** World pre-design amendment (Bevel 2026-08-26 9:06 PM)
- **Ruling:** Does not reopen the six. Two executor notes. (1) Map already shows named sidebar + canvas countdowns; lift that chrome with MapView; do not strip it. Camps tab is the full editable list. Still no second float. (2) Hide overlay chips only while World's Camps tab is visible. Stay up on Map/Path/Travels and when World is closed. Double-click a chip opens World on Camps. Overlay otherwise untouched. Launcher still cannot drop deaths. #208 untouched.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-26 9:07 PM CT

### World Bevel pre-design (sign-off)
- **Kind:** sign-off
- **Thread / subject:** World theme six questions (Bevel 2026-08-26 9:05 PM)
- **Ruling:** Signed all six. Not a hold. Simultaneity: chips + phone/tablet are enough; do not reshape PR 2; do not keep MapWindow/SpawnsWindow as a second float; do not fold the phone. Inline: no row moves (Travels Full; Map, Camps, Path Glance; default Travels). Launcher taken. Tabs: Map Â· Camps Â· Path Â· Travels (not Routes, not Camps & timers). Card title World, key `misc`. Drop camp marker: window chrome on every tab plus inline Full Travels; cog dies in that same PR. Glance strings in UI.Shared, never a countdown or canvas. PR 2-4 follow this table after PR 0/1. #208 untouched.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-26 9:06 PM CT

### World plan last-look (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** World theme plan (Fable 5, on `fable-world-plan`)
- **Ruling:** Last-looked. Signed. Not a hold. Not `needs-david`. PR 0 and PR 1 authorized (Core `WorldSurface` + `TravelPlan`; view lifts both lanes; no presentation change). PR 2-4 wait for Bevel on the six pre-design questions (simultaneity and the inline table can reshape the window). `misc` key stays; phone keeps map + spawns separate; ZoneShare stays a desktop dialog; counts never countdowns. Claude does not start Alerts. #208 untouched. #241 later.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-26 8:58 PM CT

### World theme is next (sign-off / posture)
- **Kind:** sign-off / posture
- **Thread / subject:** next theme: World (Travels & Deaths + Map, Spawns, Travel, ZoneShare)
- **Ruling:** David's call tonight (question tool). World over Alerts. Roadmap direction, already made. Not reopened. Fable plans. Claude does not start World until a plan is `ready`. Not a hold. Do not start Alerts. Do not open #208. #241 V2 stub stays independent and later. Claude's line counts and phone-parity notes are a place to look; Fable re-measures. Bevel pre-design is required before any presentation PR / four-surface fold. Card key `misc` vs name Travels & Deaths is a plan question, not a door tonight.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-26 8:42 PM CT

### v1.99.13 World â€” shipped (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** tag `v1.99.13` at `92d6a1c`. GitHub release published. David's in-session go.
- **Ruling:** Shipped. Loop closed. Do not retag. Do not touch Play Console / signing / prod secrets. A tag does not lift a hold: #208 stays live (do not open mobile sounds). #241/#243 stay waiting, not in this tag. Spawn-cue still unspent: the next loop that touches MainWindow.xaml.cs takes it first. Phone Map-panel drop button is later Bevel V1. No reporter status-reply owed (no originating thread). No more 1.99.13 work.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-27 9:36 AM CT (confirmed tag `v1.99.13` / release)

### #246 Blackburrow Brewers qty 1 vs 3 (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #246 jlcrisp Blackburrow Brewers â€” catalog shows 1 Blackburrow Cask; quest needs 3
- **Ruling:** On main via PR #247 (`fea697f`). Scope holds: Blackburrow Brewers cask qty 1â†’3 in harvest + catalog only; Rogue Redemption qty 1 left alone; CatalogSanityTests pin; harvest parser untouched. Not wiki-data. Do not tag. Do not touch Play Console. #208 untouched. Separate from #241. Status reply only if a reporter asks (draft to Helm first).
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-27 6:40 PM CT (supersedes 6:35 PM merge-authorize; confirm landed)


### #243 leftover Sky items after an inventory dump (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #243 tvongaza Sky inventory audit
- **Ruling:** Waiting, not authorized. Different ask from #241 (leftover-item audit vs have-count mismatch). Do not fold. Not wiki-data. Do not implement. Do not write FABLE.md. Scribe 5am thank-you may post. No leftover list promised. #208 untouched. #241 PR 1-3 are on main; do not fold #243 into them.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-27 5:16 AM CT
- **Superseded for planning:** Helm 2026-09-02 5:55 AM CT — plans posture-signed; implementation still gated on Bevel (see #243 / #240 Fable plans sign-off above).

### #241 Beastlord Sky Test have-counts (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #241 DasGud Quest data: Beastlord Sky Test: Windhowl/Spirit Render
- **Ruling:** PR 1â€“2 on main via PR #248 (`8b9bc71`). PR 3 on main via PR #249 (`e115d7a`). Do not reopen. Matches Bevel map (one Status IconLine provenance sentence both lanes; footer rewrite; no â§‰ / SurfacesNeedingACommand / phone sentence). Not a hold. Do not fold #243. Do not tag. Do not touch Play Console. #208 untouched. Epic master-check consume stays future.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-27 7:35 PM CT (supersedes 7:25 PM; PR #249 confirmed on main)


### #239 expand/minimize hit-target (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #239 disberon expand then second-click starts a session
- **Ruling:** "Do not implement tonight" was night-scoped posture, not a hold. That night has passed. Not adding #239 to Holds. Authorized as V0â€“V1: right-edge anchoring across the mini/full mode swap, both WPF and Avalonia lanes, arithmetic in `UI.Shared/WidgetMetrics.cs` (trap 1), not inline in a window. Diagnosis accepted (MiniRoot Auto vs NormalRoot 320, SizeToContent WidthAndHeight, SetMode never moves Left; Expand and Minimize are both second-from-right; magnitude is content-dependent). Loop-close 2026-08-26: built and staged in 1.99.12 (`4c193d10`) by eqbuddy-d8. Scope matches (RightAnchoredLeft in WidgetMetrics, both lanes, mode-swap-verify.ps1). Status posted 2026-08-26 (Scribe) https://github.com/DranakCorps-bot/EQBuddy/discussions/239#discussioncomment-18166662 #208 untouched. #237 stays waiting.
- **Condition:** n/a (process, not a hold). Lifted the night-scoped "do not implement" by expiry.
- **Signed:** Helm, 2026-08-26 6:20 AM CT

### #237 false slow 60% (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #237 selflesshero false "slowed by 60%"
- **Ruling:** Claude's 8:30 AM evidence is accepted. Chip / voice / Combat / phone all read the same tracker, so the surface question cannot identify the catalog row. A chip of exactly `Slowed 60%` is one row (`Your life force drains away.`, ancient breath 60/60). Do not implement. Do not restore #94. Next public reply asks for the verbatim log line immediately above the alert, not the surface. Scribe posts the signed follow-up. Item stays waiting / not authorized.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-25 8:30 AM CT

### #234 Guk nameds (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #234 atrzonkowski Guk nameds vs Mob Farming / Kills by Creature
- **Ruling:** Evening 8/23: Claude posted the signed question. Morning 8/24 6:22 CT: reporter answered nested under that question â€” own killing blow, solo instance, no pet. Group-member split ruled out for this instance. Real miss. Extra nameds Frenzied Ghoul, Bloodthirsty Ghoul also absent. Same ticket, not a values-line change, not a new heading. Claude may take the miss. Do not post another reply (Claude is in the thread). Do not start group-kill product work.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-24 6:22 AM CT (amends 2026-08-23 evening)

## Item shape, for anything that is not a hold

- **Kind:** `hold` Â· `lift` Â· `sign-off` Â· `priority` Â· `posture` (what may be said publicly)
- **Thread / subject:** the discussion number or the thing being ruled on
- **Ruling:** what it is, in Helm's words
- **Condition:** what would change it â€” *"after a ship that actually restores the card"* is the
  model. **A hold with no lifting condition is one nobody can ever satisfy**, and it is worth
  asking for one.
- **Signed:** Helm, and the date

## What Helm does NOT decide

The [consequence list](CLAUDE.md) is David's, and Helm does not stand in for him on it â€” the
release go, the values line, money, roadmap direction, privacy. Helm's authority is posture and
sequencing: *when* a true thing is said, and *whether* work starts. If a Helm ruling appears to
settle something on David's list, that is a question for David, not an instruction to follow.

**And a Helm claim about what the CODE contains is a place to look, never a fact** â€” the same
rule that governs Scribe and Bevel. On 2026-08-22 a Helm ruling was justified with "window
Wealth is coin too" when the window's Wealth tab still drew three blocks. The ruling was right
and its reason was wrong; the executor changed what was asked for and handed the reason back.