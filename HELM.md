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

- **#208 â€” do not open.** Waiting, not a must. Mobile sounds opt-in/off; nothing built.
  Talking to sbaum23 is not the hold; starting the work is.

Public-reply check-in is process, not a Holds line. New-thread thank-you still comes to Helm.
First-run / "weird flow" findings file on BEVEL.md without waiting on Helm. A public promise of review or a fix still comes to Helm before it posts.

## Wakes and Claude kick

- Helm cannot start Claude. Dranak runs `claude -p` on David's Windows PC, pointed at this repo / HELM.md + HELM-FEEDBACK.md.
- Claude and Fable wake Helm with: `gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane` (optional `-f reason="HELM-FEEDBACK.md changed"`). Secret is not in this repo.
- A GitHub push to HELM-FEEDBACK.md is not a wake unless that POST happens.

## Retired â€” no longer needed as a hold

Do not put these back in Holds.

- **#228 â€” no longer needed.** Helm lifted 2026-08-22 8pm. David ruled star-only is enough
  (the second lifting condition). v1.99.4/1.99.5 restore starred motes automatically;
  never-starred uses Options â†’ Cards & windows. A limit-named player reply is signed for
  Scribe (no victory lap, no "motes are back"). Do not put this back in live Holds.
- **#226 status / follow-up reply gate â€” no longer needed.** Helm-signed status posted
  2026-08-22. LeBigNasty then said the re-check looks better and repeated the two leftover asks
  (motes out of pack suggestions; client-side ignore). That follow-up lives on the wiki-pack
  motes item. Thread stays open. Leftover Innoruk lore-vs-creature is leftover work, not a hold
  â€” and it shipped in v1.99.4. **A new #226 draft still comes to Helm (process).**
- **#208 already has a reply** (cosmic-comp, 2026-08-22). The remaining live hold is on opening
  the WORK, not on talking to the reporter.
- **#231 thank-you** posted; PR merged. Never needed its own hold line.

---



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
