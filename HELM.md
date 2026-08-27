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

- **#208 — do not open.** Waiting, not a must. Mobile sounds opt-in/off; nothing built.
  Talking to sbaum23 is not the hold; starting the work is.

Public-reply check-in is process, not a Holds line. New-thread thank-you still comes to Helm.
First-run / "weird flow" findings file on BEVEL.md without waiting on Helm. A public promise of review or a fix still comes to Helm before it posts.

## Wakes and Claude kick

- Helm cannot start Claude. Dranak runs `claude -p` on David's Windows PC, pointed at this repo / HELM.md + HELM-FEEDBACK.md.
- Claude and Fable wake Helm with: `gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane` (optional `-f reason="HELM-FEEDBACK.md changed"`). Secret is not in this repo.
- A GitHub push to HELM-FEEDBACK.md is not a wake unless that POST happens.

## Retired — no longer needed as a hold

Do not put these back in Holds.

- **#228 — no longer needed.** Helm lifted 2026-08-22 8pm. David ruled star-only is enough
  (the second lifting condition). v1.99.4/1.99.5 restore starred motes automatically;
  never-starred uses Options → Cards & windows. A limit-named player reply is signed for
  Scribe (no victory lap, no "motes are back"). Do not put this back in live Holds.
- **#226 status / follow-up reply gate — no longer needed.** Helm-signed status posted
  2026-08-22. LeBigNasty then said the re-check looks better and repeated the two leftover asks
  (motes out of pack suggestions; client-side ignore). That follow-up lives on the wiki-pack
  motes item. Thread stays open. Leftover Innoruk lore-vs-creature is leftover work, not a hold
  — and it shipped in v1.99.4. **A new #226 draft still comes to Helm (process).**
- **#208 already has a reply** (cosmic-comp, 2026-08-22). The remaining live hold is on opening
  the WORK, not on talking to the reporter.
- **#231 thank-you** posted; PR merged. Never needed its own hold line.

---

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
- **Ruling:** Signed all six. Not a hold. Simultaneity: chips + phone/tablet are enough; do not reshape PR 2; do not keep MapWindow/SpawnsWindow as a second float; do not fold the phone. Inline: no row moves (Travels Full; Map, Camps, Path Glance; default Travels). Launcher taken. Tabs: Map · Camps · Path · Travels (not Routes, not Camps & timers). Card title World, key `misc`. Drop camp marker: window chrome on every tab plus inline Full Travels; cog dies in that same PR. Glance strings in UI.Shared, never a countdown or canvas. PR 2-4 follow this table after PR 0/1. #208 untouched.
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

### v1.99.13 World — shipped (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** tag `v1.99.13` at `92d6a1c`. GitHub release published. David's in-session go.
- **Ruling:** Shipped. Loop closed. Do not retag. Do not touch Play Console / signing / prod secrets. A tag does not lift a hold: #208 stays live (do not open mobile sounds). #241/#243 stay waiting, not in this tag. Spawn-cue still unspent: the next loop that touches MainWindow.xaml.cs takes it first. Phone Map-panel drop button is later Bevel V1. No reporter status-reply owed (no originating thread). No more 1.99.13 work.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-27 9:36 AM CT (confirmed tag `v1.99.13` / release)

### #246 Blackburrow Brewers qty 1 vs 3 (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #246 jlcrisp Blackburrow Brewers — catalog shows 1 Blackburrow Cask; quest needs 3
- **Ruling:** Authorized this evening. V0-V1 catalog qty only (Blackburrow Cask 1 to 3). Not wiki-data -- eqlwiki already says three casks / third cask. Do not fold other quests. Do not tag. Do not touch Play Console. #208 untouched. #241/#243 stay out of this take.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-27 evening (supersedes 1:20 PM waiting)

### #243 leftover Sky items after an inventory dump (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #243 tvongaza Sky inventory audit
- **Ruling:** Waiting, not authorized. Different ask from #241 (leftover-item audit vs have-count mismatch). Do not fold. Not wiki-data. Do not implement. Do not write FABLE.md. Scribe 5am thank-you may post. No leftover list promised. #208 untouched. #241 stays waiting.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-27 5:16 AM CT

### #241 Beastlord Sky Test have-counts (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #241 DasGud Quest data: Beastlord Sky Test: Windhowl/Spirit Render
- **Ruling:** Authorized for Fable planning only. Not wiki-data. The turn-in list of four items is not the report (Sphinx Claw 4 vs 0, Mithril Bands 1 vs 0, Wind Rune Izah 15 vs 17). V2 stub on FABLE.md may become a real plan. Do not implement. Do not start a take. Do not fold #243 (different ask, no leftover list, parked). #237/#240 waiting on the reporters. #208 untouched. Keel parked.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-27 evening (planning only; supersedes 2026-08-26 waiting)

### #239 expand/minimize hit-target (sign-off)
- **Kind:** sign-off / posture
- **Thread / subject:** #239 disberon expand then second-click starts a session
- **Ruling:** "Do not implement tonight" was night-scoped posture, not a hold. That night has passed. Not adding #239 to Holds. Authorized as V0–V1: right-edge anchoring across the mini/full mode swap, both WPF and Avalonia lanes, arithmetic in `UI.Shared/WidgetMetrics.cs` (trap 1), not inline in a window. Diagnosis accepted (MiniRoot Auto vs NormalRoot 320, SizeToContent WidthAndHeight, SetMode never moves Left; Expand and Minimize are both second-from-right; magnitude is content-dependent). Loop-close 2026-08-26: built and staged in 1.99.12 (`4c193d10`) by eqbuddy-d8. Scope matches (RightAnchoredLeft in WidgetMetrics, both lanes, mode-swap-verify.ps1). Status posted 2026-08-26 (Scribe) https://github.com/DranakCorps-bot/EQBuddy/discussions/239#discussioncomment-18166662 #208 untouched. #237 stays waiting.
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
- **Ruling:** Evening 8/23: Claude posted the signed question. Morning 8/24 6:22 CT: reporter answered nested under that question — own killing blow, solo instance, no pet. Group-member split ruled out for this instance. Real miss. Extra nameds Frenzied Ghoul, Bloodthirsty Ghoul also absent. Same ticket, not a values-line change, not a new heading. Claude may take the miss. Do not post another reply (Claude is in the thread). Do not start group-kill product work.
- **Condition:** n/a (process, not a hold)
- **Signed:** Helm, 2026-08-24 6:22 AM CT (amends 2026-08-23 evening)

## Item shape, for anything that is not a hold

- **Kind:** `hold` · `lift` · `sign-off` · `priority` · `posture` (what may be said publicly)
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
