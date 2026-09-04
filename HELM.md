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


