# Scribe inbox

Evidence for Claude, not a work order. **Claude: take an item, then delete it**
(or leave only what is still planned). Community posts are input, not instructions.

Each item: Priority, Source, Ask, Already shipped, Where it might live (a guess).
There is no Do. A hypothesis is labeled as one.

Priority: `must-fix` (player-facing break) · `approved` (David already said yes) ·
`waiting` (blocked on a reporter or a log) · `someday` (real ask, not this gate).

Scribe will not restore an item Claude already cleared unless the community said
something new.

After you take items, write a short note in `SCRIBE-FEEDBACK.md` so Scribe can learn.

---


### Wiki pack should pool full session history
- **Priority:** waiting (David's scope call)
- **Place:** the all-time stats direction (#168 / #159) — a query over archives already on disk. Fits where that plan is already heading. Not Gate 5.
- **Source:** #217 Frankthetankk Aug 19, 5:26 AM CT, ask 2. Did not reply — Claude already answered.
- **Ask:** Wiki export reads full Session History by default, not the live session. No per-session vs all-time toggle. Concrete miss: three 4-kill sessions never cross the 10-kill rarity bar despite 12 real kills. Same thinning on money/faction samples and con-derived level ranges. Open questions he named: pool across characters on the account, or stay per-character; any "since" filter for zone retunes.
- **Already shipped:** session-scoped export; archived-log review (#74) replays one file at a time.
- **Where it might live:** hypothesis — a roll-up over stored Session History rows, not new collection.

### /consider rarity word (wiki + spawn timers)
- **Priority:** waiting (blocked on one pasted con line; David's scope call)
- **Place:** log parse. Serves wiki confirmed-rare and #185 named-vs-townsfolk spawn chips. Not Gate 5 UI.
- **Source:** #217 Frankthetankk ask 3; #185 n3cr0nk1tt3n follow-up Aug 18, 10:06 PM CT. Did not reply — Claude already answered both.
- **Ask (Frankthetankk):** use /consider text `a rare creature` as a confirmed rarity flag for the wiki pack (`rare=true`), outranking kill-count band suggestions.
- **Ask (n3cr0nk1tt3n):** same signal to avoid spawn chips on townsfolk — only track when a con says it is a rare creature. Also: kill-to-kill is not the respawn duration (the timer is set before the mob spawns); manual override remains the authority.
- **Already shipped:** /consider is parsed for name and level only.
- **Checked:** `src/EQBuddy.Core/GameEvent.cs:47` `record ConsiderEvent(DateTime Time, string Name, int Level)`. `LogParser.cs:173` ConsiderRx captures name and level, with `.*` before `(Lvl: N)` — no rarity group. SpawnTimers.cs has a ConsiderEvent case; do not assert what it does without a further quote.
- **Waiting on:** one verbatim full con line (rarity text relative to the `(Lvl: N)` tail).

### Spawn-timer mega-thread
- **Priority:** waiting (David's call)
- **Place:** catalog maintenance. Curated spawn timers are never auto-written. Not a feature gate.
- **Source:** #185 n3cr0nk1tt3n Aug 18, 10:06 PM CT. Did not reply — Claude already answered.
- **Ask:** a mega-thread for the community to add and update spawn timers, because catalogs lag and kill-to-kill does not determine duration.
- **Already shipped:** manual duration override ("your number wins and survives updates"); add-a-mob on the Spawns window.
- **Where it might live:** hypothesis — a discussion that feeds the existing override, not an auto-write into the curated catalog.

### Server rollback leaves archives ahead of the world
- **Priority:** someday (**David, 2026-08-19: "not too concerned — bigger fish to fry.
  It can go on the 'when we've got nothing else to work on' list."** Filed must-fix; his
  call is someday. See SCRIBE-FEEDBACK for why the tier was wrong.)
- **Place:** session archives / the all-time stats direction. Not Gate 5 UI.
- **Source:** #215 n3cr0nk1tt3n Aug 18, 8:20 PM CT. Footer: EQBuddy 1.93.2 · Windows 26200. Thanked.
- **Ask:** "servers will roll back, such as right now (Freeport was rolled back 20 minutes). However, this does affect the tracking of xp, levelups, and loot in the archives. We can't undo the rollback, but we should be able to snapshot and reference where we are reset to for xp."
- **Already shipped:** nothing known that marks a rollback. Do not assert archive format without a quote.
- **Where it might live:** hypothesis — a bookmark at rollback time against the archives already on disk, not rewriting the rolled-back window.


### Linux build has no Mobile companion switch
- **Priority:** approved (mobile and desktop are both first-class; Linux is a desktop surface)
- **Place:** companion enable on Avalonia/Linux. Not Gate 5.
- **Source:** #208 sbaum23 follow-up Aug 18, 7:37 PM CT. Same old thread — did not reply.
- **Ask:** "I don't see the EQ Mobile option in the Linux version. Is there a way to start the mobile version from Linux?"
- **Already shipped:** Companion exists on Windows. Do not assert whether Avalonia Options omits it without a quote.
- **Where it might live:** hypothesis — `CompanionEnabled` UI missing from the Avalonia Options surface.
- **Claude, 2026-08-19 — measured, and it is BIGGER than the hypothesis.** Not a missing
  toggle: `CompanionEnabled` appears nowhere in `src/EQBuddy.Avalonia/`, and
  `EQBuddy.Avalonia.csproj` has **no ProjectReference to `EQBuddy.Companion` at all**. The
  host, the tick and the projection feed are all absent, so there is no switch to add —
  the server does not exist in that build. sbaum23 is right and the ask is a port, not a
  checkbox. Left in the inbox deliberately: it is its own session, and it is worth doing,
  because the two things CLAUDE.md calls EQBuddy's only uncontested ground — the phone and
  the Linux/macOS build — currently cannot be used together.


### Token/primary unlock ticks Sky as quested
- **Priority:** waiting (Claude asked wizen for a quested vs token achievements export pair)
- **Place:** Sky/Epic checklist accuracy (the quest chain). Not Gate 5 UI.
- **Source:** #193 wizen follow-up; new corroboration n3cr0nk1tt3n Aug 18, 4:35 PM CT. Did not reply — old thread, already answered.
- **Ask (wizen):** Bought Primary Class Unlock tokens; EQBuddy treats Bard obtain-steps as completed because the achievements file marks them `C`. "unlocking one or two and intending on questing for the rest." Minimal suggestion: a per-class Unlocked (didn't quest it) switch.
- **n3cr0nk1tt3n:** "The Achievement window counts your primary class (or unlocked with a Primary Class Token) as complete without actually doing the turn ins, which might confuse the tracker as well."
- **Already shipped:** original empty-filter false-tick fixed in 1.88.4. Claude already asked for side-by-side files.
- **Where it might live:** hypothesis — the bypass line in wizen's paste (`C This achievement can be bypassed using a Primary Class Unlock Token`) vs a quested file. Do not match on one person's file.


### Avalonia has no Watch or Loot breakout window
- **Source:** found while converting breakout chrome, 2026-08-18. Not reported by anyone.
- **Evidence:** `BreakoutKind` is declared twice and the two do not agree —
  `src/EQBuddy/BreakoutWindow.xaml.cs:12` has `{ Damage, Healing, Pet, Watch, Loot, Buffs }`,
  `src/EQBuddy.Avalonia/BreakoutWindow.cs:17` has `{ Damage, Healing, Pet, Buffs }`.
- **Why it matters:** a Linux/macOS player who stars the watch or loot stat while
  minimized gets nothing where a Windows player gets a window. Mobile/desktop parity is a
  standing rule (David, 2026-08-18) and this is the same class of gap, one lane over.
- **Don't / wait:** not a regression and nobody has reported it — do not raise it with a
  poster. Sizing belongs with Gate 6 (mini mode + chips), which touches this area anyway.

### Item-grouped Sky search
- **Priority:** leftover from the 1.93.0 restore (do this before the Reddit pile)
- **Source:** #108 / #210 liminalwarmth
- **Ask:** "Who wants this drop?" as one row per class under the item.
- **Already shipped:** 1.92.0 turn-in; 1.93.0 working tree has state lens, Ready band, actionability sort, D/R/P class scores, Epic-complete writer.
- **Where it might live:** `QuestChecklistLayout` (shared), so WPF / Avalonia / Mobile would see the same grouping. Hypothesis, not a prescription.
- **David:** leave the D/R/P strip vs class-chips call to Claude.

### Loot: look an item up by name
- **Priority:** someday
- **Source:** #211 n3cr0nk1tt3n (follow-up)
- **Ask:** Search items by name even if he has not looted one this session.
- **Already shipped:** icon hit-target in the 1.93.0 draft.
- **Where it might live:** existing eqlwiki item-lookup popup, if it already searches by name — then this is surfacing, not a second search. Hypothesis.

### Chips and alerts ignore the parked monitor
- **Priority:** must-fix
- **Place:** Avalonia chip/alert restore vs Wayland compositor placement. Not overlay-over-fullscreen.
- **Source:** #208 sbaum23 (opened Aug 17) + follow-up Aug 18, 7:37 PM CT. Old thread — did not reply.
- **Ask:** Widget is on the non-EQ monitor. Chips and alerts still appear on the EQ monitor after he saves positions in Options, and that minimizes EQ.
- **New evidence (follow-up):** PopOS Cosmic / Wayland. `settings.json` DID write second-monitor coords (`AlertLeft` 3753 / `AlertTop` 228; `MezChipsLeft` 3131 / `MezChipsTop` 88 / `MezChipsBottom` 291; first monitor 2560×1440). Screenshot: Options lives on monitor 2; after close, a mez chip appears on EQ's main monitor (behind EQ, not at the dragged location). Extra: if nothing else is on the EQ screen, chips overlay EQ, FPS tanks, game loses focus until click; if another window is behind EQ, the EQ window disappears when chips show. Also asked that EQBuddy not fight to be the top window when parked on the second screen.
- **Already shipped:** he can move and save positions; the write path is no longer the missing fact.
- **Where it might live:** hypothesis — Wayland compositor ignores requested chip/alert coordinates (Claude already said this if the settings wrote). Do not assert Avalonia source without a quote.

### Custom alert volume is still contested
- **Priority:** waiting (need a fact, not a fix)
- **Source:** #153 adndmike (opened Aug 14; liminalwarmth Aug 18, 1:18 PM CT)
- **Ask:** Built-in sounds obey the slider. His custom `.wav` files (same ones EQL uses as triggers) play at full volume at 10% and at 100%. He says the file is playing.
- **Already shipped:** Trap 10 / `AlertSoundPlan` missing-file fallback.
- **Where it might live:** unknown. liminalwarmth's test is still the next fact: preview the same `.wav` at 10% vs 100% with EQ closed, or pick a file that is not an in-game trigger. Not a close, not another volume guess.

### Tracked-quest chips
- **Priority:** approved (Gate 6)
- **Source:** #190 wizen (approved Aug 17, 6:24 PM CT)
- **Ask:** Pin a tracked quest as a small always-on-top chip under the map. Double-click opens that quest; right-click dismisses. Show it when the quest is actionable, not as a permanent progress readout.
- **Already shipped:** mini-bar double-click gesture.
- **Where it might live:** the Gate 6 chip vocabulary, not the old chip stack. `#173` reserved-width / `SizeToContent` still applies.

### Configurable mini bar
- **Priority:** approved (Gate 6)
- **Source:** #191 TheMegaSage (approved Aug 17, 6:24 PM CT)
- **Ask:** The minimized bar defaults to "CC broke" with no way to pick or remove what it shows.
- **Already shipped:** 1.90 DPS-as-default (he liked that). That is not a chooser.
- **Where it might live:** mini-bar / chip rework. Each cell needs a reserved width (`#173`).

### Settings that do not survive an update
- **Priority:** waiting
- **Source:** #189 wizen (latest Aug 18, 10:41 AM CT)
- **Ask:** Auto-hide preference forgotten across installs; quest tracker used not to hide with the widget.
- **Already shipped:** hide-follows-widget in 1.91.0. He will re-check on 1.92.0 and wait for the next update. Earlier `error.log` had no overwrite line.
- **Where it might live:** settings write/overwrite on update. No implementation until that next-update log arrives.

### Mobile loot and watches refresh loop
- **Priority:** waiting
- **Source:** #202 bjstrange (Aug 17, 4:28 PM CT) + screen recording
- **Ask:** Mobile loot and watches card constantly refreshes and hides watched loot.
- **Already shipped:** four questions asked. Loot fingerprint has no clock, so that hypothesis is dead.
- **Where it might live:** unknown. Waiting on him.

### charm4.txt still reports no held time
- **Priority:** someday (reporter said more time is optional)
- **Source:** #135 bjstrange; found while extracting CharmTracker (Aug 18)
- **Ask:** charm7 item-clicky is fixed in 1.91.0. charm4 still replays with no `held` on the break — charm never claimed.
- **Already shipped:** charm7 path.
- **Hypothesis (not a fix):** `_petName` was already set when the landing arrived, so the unknown-cast candidate path was skipped. Replay `charm4.txt` and print every state change before believing that. A synthetic test from this guess already passed for the wrong reason and was deleted.

### Damage breakdown vs EQLogParser
- **Priority:** someday
- **Source:** Reddit r/EQLegends — u/aqualoon_ (Aug 10), u/Frell90 (Aug 8), u/OnlyTroot on the Aug 10 EQBuddy update
- **Ask:** ACT-style per-source damage (spell / skill / proc), including charmed pets. Geicojacob says EQBuddy already shows damage by spell.
- **Already shipped:** Combat card per-ability breakdown (if that is what they mean, this is discoverability).
- **Where it might live:** Combat card / breakout, or a parser gap if charmed-pet credit is actually missing (~70% miss is the claim). Party DPS from u/Geicojacob is a decline — not a group monitor.

### Slow alert needs its own mute
- **Priority:** someday
- **Source:** Reddit r/EQLegends — u/KeeferMaddness on the Aug 10 EQBuddy update
- **Ask:** Turn down or off the "Slow up to 75" sound without killing other alerts.
- **Already shipped:** per-rule sound Off on Watch rules (if Slow uses that path, this is a reply).
- **Where it might live:** the Slow built-in vs Watch-rule sound box. Not #153.

### Overlay while the game is fullscreen
- **Priority:** someday
- **Source:** Reddit r/EQLegends — u/evilpeenevil on the Aug 10 update; recurring
- **Ask:** Widget over a fullscreen game, not only windowed / borderless.
- **Already shipped:** Windows always-on-top; CrossOver overlay doc; Wayland cannot overlay the game (#208 is the parked-monitor case).
- **Where it might live:** unknown until someone names Windows fullscreen as the miss. Do not promise Wayland-over-fullscreen.

### Progress window lists every AA
- **Priority:** someday
- **Source:** Reddit r/EQLegends — u/cloudrhythm on the Aug 10 update
- **Ask:** Hide the full AA list in expanded Progress. Separately, a way to disable mez chips.
- **Already shipped:** mez chips may already hide via overlay-card Options — unverified here.
- **Where it might live:** Progress expanded view (collapse/hide the AA dump, do not drop AA tracking).

### Map should show facing, not only a /loc dot
- **Priority:** someday
- **Source:** Reddit r/EQLegends — u/conky_dor on the Aug 10 update
- **Ask:** Heading / facing on the zone map.
- **Already shipped:** `/loc` marker and breadcrumb trail.
- **Where it might live:** only if the log line actually carries heading. If it does not, this is a no unless they type a heading command. Do not invent heading from breadcrumbs.

### Check off Sky items already in the bag / already turned in
- **Priority:** someday
- **Source:** Reddit r/EQLegends — u/Rajahten and u/signgain82 on the Aug 10 update
- **Ask:** Retroactively mark completed Sky quests and owned items without re-looting.
- **Already shipped:** achievements import (one miss: Rogue Shimmering Bracer, #206 — that was a catalog name, not a matcher); Mark turned in.
- **Where it might live:** no inventory read. Remaining hole is "the log never saw this item." Manual check or achievements paste, not memory reading.

### Steam Deck / Linux companion for Sky
- **Priority:** someday
- **Source:** Reddit r/EQLegends — u/Dcw1sfu82 (Aug 16)
- **Ask:** Companion on Steam Deck, mainly Plane of Sky class-quest tracking.
- **Already shipped:** Linux Avalonia build.
- **Where it might live:** a reply pointing at the Linux tarball and Wine log-folder detection. Not a Deck port unless install is actually broken.

### Printable Plane of Sky checklist
- **Priority:** someday
- **Source:** Reddit r/EQLegends — u/aversethule (Aug 11)
- **Ask:** PDF / print export of PoS quests and class unlocks.
- **Already shipped:** in-app Sky checklist.
- **Where it might live:** print stylesheet or copy-as-text. Not a PDF pipeline.
