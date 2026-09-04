# EQBuddy test plan

**What EQBuddy is expected to do, and how each expectation is held down.** This is the
contract. When behaviour changes, this file changes in the same commit — a test plan
that has drifted from the product is worse than none, because it teaches confidently
wrong things.

Audited at **v1.82.0 (2026-08-14)**: 1,317 unit + 45 Avalonia + 6 E2E, all green.

**How to read the Held-by column**

| Mark | Meaning |
|---|---|
| **Auto** | A test fails if this breaks. Safe to refactor against. |
| **Partial** | The logic is tested; the wiring to the screen is not. |
| **Manual** | Only a human sitting in front of it will notice. See §6 for the pass. |

---

## 1. Reading the log

| Expectation | Held by |
|---|---|
| Every known line type parses to its `GameEvent`; unknown lines are ignored, never guessed at | **Auto** — `LogParserTests`, `SplitOnceParserTests` |
| A growing file is tailed from its last offset, survives truncation and rollover | **Auto** — `LogWatcherTests`, `LogSessionsTests` |
| 60+ minutes of silence ends a session; the next line starts a fresh one | **Auto** — `SessionStatsTests`, `SessionArchiverTests` |
| The active character is followed automatically (whichever log is growing) | **Partial** — detection tested, the 5 s re-scan is not |
| `Log=1` is forced in `eqclient.ini`; stale logs truncate only while the game is closed | **Auto** — `EqConfigTests` |
| A log folder is found from the registry, with Wine/CrossOver prefixes handled | **Auto** — `LogFolderDetectionTests` |

## 2. What the numbers mean

| Expectation | Held by |
|---|---|
| Session DPS = damage ÷ time in combat; wall-clock DPS reported separately and labelled | **Auto** — `SessionStatsTests` |
| Per-ability DPS is that ability's damage ÷ total combat time (contribution rate); burst rate is damage ÷ its own active time | **Auto** — `BreakdownRangeTests` |
| Self-inflicted damage counts as damage taken but opens **no** combat window and **no** encounter | **Auto** — `EncounterTests` |
| A fight closes on the kill line, or after 20 s of silence marked `Timeout` | **Auto** — `EncounterTests` |
| Fights group into a pull when no 10 s lull separates them; live card and History agree | **Auto** — `EncounterTests`, `LastFightTests` |
| **A pull in a never-quiet zone runs long and its DPS is a long average.** Deliberate (#151); per-mob figures live on the Kills card | **Auto** (the rule) / documented limit |
| Pet damage is attributed to the pet, never to your accuracy or crit counters | **Auto** — `SessionStatsTests`, `SpellTrackingTests` |
| A charm cast confirms a pet; an interrupted or faded charm never claims one | **Auto** — `SessionStatsTests` |
| **A charm landing is ours only within that spell's own cast time + 1.5 s** — 9 s for Cajole Undead, 3.9 s for Charm. An instant or unknown cast time keeps the generic 30 s window, never a tighter one | **Auto** — `SpellTrackingTests` |
| **The charm family is decided from wiki slot effects, not names**: Dictate and Thrall of Bones are charms, Allure of Death is not | **Auto** — `SpellTrackingTests` |
| **`/pet who leader` settles ownership both ways**: naming you claims the creature, naming anyone else releases a pet we claimed. Unknown character name never releases (#177) | **Auto** — `SpellTrackingTests` |
| Crit, miss, resist, fizzle and block counts follow the log's own words | **Auto** — `LogParserTests`, `StackingTests` |
| **The Loot card's recent view agrees with the count view about what counts as loot**: every kept acquisition — corpse loot, auto-stored, forage, parcel, crafts, merges — lands on the arrival timeline, newest first | **Auto** — `SessionStatsTests.RecentLootCarriesEveryKeptAcquisitionKindInArrivalOrder`, `LootRowsTests` |
| **Every Loot surface answers the same four questions the same way**: which slice is shown, which order, whether "recent" is offered at all, and what an empty slice says. Four surfaces read one decision — the widget card and its breakout on Windows, the card on Linux/macOS | **Auto** — `LootPresentationTests`, `LootRowsTests` |
| **A stored view or sort EQBuddy no longer recognises falls back to showing everything**, never to a blank card — including `"made"`, the pre-#198 spelling still sitting in profiles | **Auto** — `LootPresentationTests` |
| **The Loot card header and the breakout's subheader report the same two numbers** (drops, and merges+crafts as "+N made") because one function composes both (#131) | **Auto** — `LootPresentationTests` |
| **A charm cast from an ITEM still reports how long it held**: a clicky prints no casting line, so the per-spell arm window has nothing to key on — the pet's own caster-only "Master" tell starts the clock at the landing (#135, charm7.txt, the sixth distinct cause in that thread) | **Auto** — `SpellTrackingTests` |
| **The landing line alone still claims nothing**: "has been charmed." names no caster and prints for a bystander's charm, so it is remembered and never believed until the tell names the same creature | **Auto** — `SpellTrackingTests` |
| **An auto-sold pickup ("looted … and sold it for …") is not session loot**: dismissed at the corpse, it appears in no loot view and never touches the inventory overlay — it is vendor income, and the per-creature drop ledger (wiki packs, Target drops) still credits the mob. Watch rules still count it: a watched pattern is explicit interest | **Auto** — `SessionStatsTests`, `InventoryFileTests`, `JournalTests` |
| **The Progress card and the Progress WINDOW say the same thing** (the breakout float was retired 2026-08-25 — the xp chip opens the window): header ("12.3% xp, +1 lvl (3 new), +2 aa") and pace summary come from one builder (`UI.Shared/ProgressText`); each ding is paced from the previous one, and the "(N new)" cue is omitted when a level unlocked nothing | **Auto** — `ProgressTextTests` |
| **The next level's unlocks are split per class**, with the classes' own order, a shared "Any class" group last for the AA categories the wiki gives no class to, and a spell two classes share under BOTH (David, 2026-08-23; Bevel's lock, Helm-signed) | **Auto** — `LevelUnlockGroupsTests`, `WidgetRenderTests`, `tests/EQBuddy.E2E` |
| **A class that gains nothing at that level keeps its row**, reading "Nothing new at N" with no chevron — dropping it is indistinguishable on screen from that class not being one of yours | **Auto** — `LevelUnlockGroupsTests`, `WidgetRenderTests` |
| **One class draws no expander at all** (names under the heading); more than one opens the first group that has something to show, and that open/shut state is session-only and never a setting | **Auto** — `LevelUnlockGroupsTests`, `WidgetRenderTests` |
| **With no class known the next-level preview is HIDDEN**, rather than falling back to the class-agnostic AAs and jumping forward to whatever level has one — which offered a pet ability five levels away to a character with no pet | **Auto** — `WidgetRenderTests` |
| **Motes per hour appears as one line in the Progress Experience room** (David, 2026-08-23), omitted entirely when nothing has dropped, and built by the same formatter as the Motes card's own header so the two can never disagree | **Auto** — `MoneyAndMotesPresentationTests`, `ProgressThemeTests` |
| **A character can be up to THREE classes at once** (eqlwiki, trio builds), and the log names every class that qualifies rather than only the strongest — two classes played together are both named, where before they cancelled each other out | **Auto** — `ClassInferenceTests`, `CharacterClassesTests` |
| **The achievements dump outranks inference, and the Quest Tracker's picks only WIDEN** — picks may never be what tells the app what the character is (#104's lens rule; Bevel's lock) | **Auto** — `CharacterClassesTests` |
| **Every route that reads an achievements dump records the classes it names** — all three, two of them hand-written per lane | **Auto** — `ClassSourceWritersTests` (curated must-list plus a scan for a fourth route) |
| **A class untouched for two half-lives drops out**, which is what tells an alt-swap (blocks) from a multi-class character (rotation inside a fight) | **Auto** — `ClassInferenceTests` |
| **Every one of the sixteen classes can produce class evidence**, so an inference can always be argued back down (#120) | **Auto** — `ClassInferenceTests` |
| Class evidence decays with a 10-minute half-life: a swap converges on what is being played now, and silence alone never flips a reading | **Auto** — `ClassInferenceTests` |
| A class is named only from 3+ sightings, with two distinct spells where an item could have cast them, and a 2× lead — otherwise **no** inference | **Auto** — `ClassInferenceTests` |

## 3. Knowledge and catalogs

| Expectation | Held by |
|---|---|
| Every zone EQBuddy knows resolves to a real map file (aliases included) | **Auto** — `ZoneMapCoverageTests` |
| Catalogs stay internally consistent; no duplicate or orphaned entries | **Auto** — `CatalogHygieneTests`, `CatalogSanityTests` |
| Wiki lookups try article, case and backtick variants, then a bounded-fuzzy backstop; a merely-similar page is never accepted | **Auto** — `EqlWikiMobsTests` |
| **A redirected lookup records the title the wiki SERVED, not the one requested** | **Auto** — `EqlWikiMobsTests` (regression, #65 — this broke twice) |
| An epithet (`X, the Y`) falls back to the base name rather than proposing a duplicate page | **Auto** — `EqlWikiMobsTests` (#65) |
| **A contribution pack names the zone the creature died in, everywhere in the entry** | **Auto** — `WikiContributionTests` (#65) |
| Rarity labels only appear from 10+ kills | **Auto** — `WikiContributionTests` |
| **A creature whose wiki page could not be read is reported as "not checked yet", never counted as a contribution and never worded as "nothing new"** | **Auto** — `WikiPackPresentationTests`, `WikiPackRenderTests` (#217) |
| **A rare-conned named whose page already has every drop still contributes — the rarity itself** (Bevel, Helm-signed 2026-08-23): its own row kind and export section, the description-field ADD with both counts, headline counted, Copy live; an unread page stays Pending and a lore article is never offered the paste | **Auto** — `WikiPackPresentationTests` (rare-only cases) |
| **The pack pools EVERY stored session plus the live one** (#217 ask 2) — three 4-kill sessions cross the 10-kill rarity bar; (name, zone) keying; tiers fold on the base item with the rate recomputed from pooled numbers; unknown coin/level stays unknown; the live session pools ONCE (excluded by row id AND by identity — the id alone has a first-checkpoint timing hole the staged shot caught doubling every number); the scope line names everyone pooled and the span, never a policy | **Auto** — `MobHistoryTests` (incl. a temp-DB probe test), `WikiPackPresentationTests`, `WikiPackRenderTests`; **Shot** — `wiki-pack` stages two day-shifted stored sessions and the prediction names the scope line |
| Drops by Creature deliberately stays session-scoped — "is this camp worth it" is a different question — and its footer says the pack pools | **Auto** — `WikiPackPresentation.MovedHint` consumed by both lanes |
| **A respawn timer is suggested to eqlwiki ONLY past the agreement bar** (3+ cycles within ±15 % of median, median ≥ 90 s): three agreeing cycles MUST suggest, a scattered sample MUST NOT, fewer than three never do, and a triggered/raid/multi-spawn entry never does whatever the ledger holds; the wiki's own field decides suggest vs "nothing to add" vs "compare, don't overwrite" (never a paste-over — free prose like "Triggered" is a disagreement to SHOW) | **Auto** — `RespawnSuggestionTests` (the plan's acceptance pair included) |
| **Cycles are recorded exactly where a gap passes the honesty gates and never otherwise** — the named's own kill started the clock, same stay, floor and ceiling; a gap the never-loosens rule rejects for the countdown is still a real cycle (or a stable timer could never reach three); a cross-zone-trip gap and a triggered entry never record; imports never write the ledger (its writers are the three `SpawnTimers` learn points, by construction) | **Auto** — `RespawnSuggestionTests` (end-to-end through real log lines), `SpawnCycleLedger` cap test |
| The trusted catalog timers are diffed against the wiki by `scripts/harvests/eqlwiki/respawn-diff.py` — flags only, a REPORT with paste-ready lines, never a wiki edit or a catalog write | **Manual** — run the script; `respawn-report.md` is the artifact (first run 2026-08-26: all 3 trusted Crushbone timers missing from the wiki) |
| An empty contribution pack distinguishes its three causes: nothing looted, the wiki already has it, or the lookups have not landed | **Auto** — `WikiPackPresentationTests` (#217) |
| The contribution pack surface says what it pooled — this session, named character — rather than copying a silent scope | **Auto** — `WikiPackPresentationTests` (rule), `WikiPackRenderTests` (on screen); **Visual** — `shoot.ps1 -Shot wiki-pack` |
| Copy is offered only when there is something to paste, and a disabled Copy LOOKS disabled | **Auto** — `WikiPackPresentationTests`, `WikiPackRenderTests` (trap 17) |
| What the pack window lists is what the clipboard export contains | **Auto** — `WikiPackPresentationTests` — both read the same observations |
| **Searching a checklist groups by ITEM, so one drop several classes want is one block and not one section per class** | **Auto** — `QuestChecklistSearchTests` (#108, regressed by the Gate 2 rebuild); **Visual** — `shoot.ps1 -Shot sky-item-search` |
| A checklist search crosses every class and ignores the class picker and state lens — and says so on screen, because those controls stay visible | **Auto** — `QuestChecklistSearchTests` |
| The mobile page's checklist search carries the same scope wording as Core | **Auto** — `QuestChecklistSearchTests` — the page mirrors the rule in JS, so the strings are pinned equal |
| Item-grouped results stay tickable, and the Sky ready band is never searched as though its rewards were items | **Auto** — `QuestChecklistSearchTests` (#212's lesson) |
| Zone-knowledge share strings round-trip; imports preview every change; wild timers arrive flagged | **Auto** — `SpawnPointLedgerTests` |
| Fuzzy kill matching never bridges name-family siblings: a shared prefix with a different last word is another mob unless one truncates the other | **Auto** — `SpawnTimerTests` (Sol A CWG models, 2026-08-16) |
| Spawn timers only learn from the named's own kills and sightings — a placeholder death restarts the clock but a gap or elapsed measured from it teaches nothing | **Auto** — `SpawnTimerTests` (the 93-second EXG, 2026-08-16) |
| **A TRIGGERED spawn (eqlwiki's own `respawn_time = Triggered`) starts no countdown and — the half a suppression alone misses — teaches nothing from a re-kill gap**; a poisoned `Learned` override heals on the next kill and at load; a typed duration still runs | **Auto** — `SpawnTimerTests` |
| A shared zone archive never lands a duration on a triggered entry OR a raid-instanced one, even with includeFlagged; the preview shows it flagged. The raid half was churn nobody could see — an import wrote a number the next launch's heal removed (Fable, v1.99.2 release review) | **Auto** — `SpawnPointLedgerTests` |
| **`AppSettings.Load` can be asked NOT to persist migrations** — it is a read that writes, and `--textprobe` runs without the single-instance lock, so it must not save a whole-file snapshot under a live widget (trap 13) | **Auto** — `SettingsClobberTests` |
| **Unticking "Show watch chips in the mini dashboard" survives a relaunch even with a rule PINNED** (#253, HiramDucky) — the group-pin migration sits INSIDE the `WatchPinsMigrated` gate on both lanes, still ordered before the per-rule pass. Ungated it re-ran every launch, and since the built-in CC-broke rule is pinned by default it flipped `PinWatchChips` back on for nearly everyone, so the tick-box could never stick | **Manual** — the decision is inline in both `MainWindow` constructors and runs before any surface exists; untick the box, relaunch, confirm it is still unticked |
| The share-import preview shows a REFUSED row as refused, and the "I trust this source" checkbox does not count rows it cannot apply | **Auto** — `SpawnPointLedgerTests` |
| **A theme's body has exactly ONE owner** — inline or window, never both. Popping out collapses the card; clicking the card while the window is up brings the window forward and draws nothing; closing the window leaves the card COLLAPSED, never inline. On Avalonia the widget builds each body once and a control has one visual parent, so this is a crash rather than a layout bug | **Auto** — `ThemeHostTests`, including all 125 three-action sequences |
| Which of a theme's tabs draw a full body inline and which glance (Bevel, Helm-signed 2026-08-22) lives in Core, so moving one is one line both desktops follow; no theme glances every tab | **Auto** — `InlineModeTests` |
| **Alt-swapping two classes never makes the inferred class flicker** (#120) — while a class is being played recency carries it past the 2× lead margin; in the handover the margin is not met and the honest "" comes back. It never names the class you are NOT playing | **Auto** — `ClassInferenceTests` |
| **A triggered row says "triggered" — a DIFFERENT word from "instance"**, because the next action differs (go kill the trigger, versus wait for the instance clock); the detail names the trigger from the catalog's `triggeredBy`; a typed duration makes it an ordinary row again. `TimerSuppression` is an enum, not a bool with two meanings (trap 4) | **Auto** — `TimerViewTests`, `SpawnTimerTests` |
| **A "Player X creating instance <zone> NNNN." line makes the NEXT enter line an instance** — Plane of Sky's enter line is byte-identical to the open world's (Frankthetankk's verbatim log, #109), so the zone gate could never fire there without it; spent on the first enter line so a stale announcement cannot leak onto a later zone | **Auto** — `LogParserTests`, `SpawnTimerTests` |
| Every `spawnType` in the shipped catalog is a known value, and the four Plane of Sky mobs from #109 are typed with a note naming the wiki page | **Auto** — `SpawnTimerTests` |
| **A cleared timer stays cleared when the log is read again** — every `Select` is a full-file ingest, so the kill line that started it replays on the next launch; `Clear` records WHICH kill was dismissed, and persists it. A later kill still starts a fresh timer, and a manual ▶ start always wins | **Auto** — `SpawnTimerTests` |
| **The AUTOMATIC achievements import obeys the auto-grant guard** — not merely because both paths happen to call one method today | **Auto** — `OutputfileAutoImportTests` |
| **A named the catalog lists with a BLANK respawn, in a zone with no default, learns its cycle from a second kill** — 126 ship that way (all 38 in High Keep, 47 in Western Wastes). Until 2026-08-20 they were worse off than a mob EQBuddy had never heard of, which measures its cycle on the second kill | **Auto** — `SpawnTimerTests` |
| **A gap only teaches when the player never left the zone during the countdown.** Changing instance means zoning, so this is also what stops "kill it at D0, change to your own instance, kill it at the spawn point" from measuring a twelve-minute respawn that never happened — every difficulty shares one timer key, and that was two copies of the mob | **Auto** — `SpawnTimerTests` |
| Zoning stops the LEARNING and never the countdown: the named goes on respawning while you are at the bank, and your own instance keeps its state while you are away | **Auto** — `SpawnTimerTests` |
| A timer recovered from the persist file anchors no measurement — it carries no stay, and no evidence must never read as agreement | **Auto** — `SpawnTimerTests` |
| A sighting reached by zoning still completes the chip (the creature IS up where the player stands) and still teaches nothing | **Auto** — `SpawnTimerTests` |
| **An imported timer is recorded as someone else's number**, your own kills replace it, and it cannot smuggle in a stale `Sighted` flag | **Auto** — `ZoneShareTests` |
| A manual ▶ start always replaces a running timer, even backdated | **Auto** — `SpawnTimerTests` |
| A timer coming due during a tick gap (sleep, throttling) still shows DUE for a full linger on the next look — within a 1-hour revival cap | **Auto** — `SpawnTimerTests` |
| A countdown never reads "60s" at the minute boundary | **Auto** — `CountdownTests` |
| **A wiki re-check bypasses a cache the service still calls fresh, and an OFFLINE re-check returns the stale read with its OLD timestamp** — never `Offline`: a known ✦ must not vanish into "not checked" because the network blinked (#226, #217 rule) | **Auto** — `EqlWikiMobsTests` |
| `Forget` drops the cache file keyed on the REQUESTED name — the same key the windows' session memo uses — even when the page was served under a redirect | **Auto** — `EqlWikiMobsTests` |
| **At most two mob lookups are in flight per service** — a thirteen-creature Drops tab used to send thirteen at once to a volunteer wiki | **Auto** — `EqlWikiMobsTests` |
| **Every Drops creature heading carries a wiki re-check ↻ and a freshness caption** — disabled and dim inside the 30 s rule, live otherwise, caption in COARSE buckets so the repaint signature never ticks (trap 8); the tooltip names the SERVED page (trap 3) | **Auto** — `WikiFreshnessTests`; `DropsRenderTests` (Avalonia); `EndToEndTests` `dropsRecheck` (WPF) |
| An offline re-check keeps the OLD read on screen with its age — never "not checked" (the #217 rule) | **Auto** — `WikiFreshnessTests` |
| **Neither window DELETES the cache on the re-check path** — `Forget` before a bypass leaves the offline fallback nothing, so a failed re-check reported `Offline`, `Classify` made it `Unknown`, and the ✦ the button exists to refresh disappeared. Shipped in 1.99.1; found by Fable's H4 last-look, reachable only with the wiki down | **Auto** — `WikiRecheckPathTests` (source scan, both windows), `EqlWikiMobsTests` |
| A LEARNED override on a suppressed entry (raid-instanced or triggered) heals at LOAD, not only on the next kill — otherwise the row printed "3m" beside the word "triggered" | **Auto** — `SpawnTimerTests` |
| **The triggered glance names its trigger only where the name FITS the fixed 150px column** — article stripped, no ellipsis: a clipped "triggered · a spiroc banisher" told the player less than the bare word and looked like a bug (caught by the shot, not a test) | **Auto** — `SpawnTimerTests`, `TimerViewTests` |
| **The pack window's "Re-check N pages" re-reads only the flagged and unread creatures, never the fully known** — bounded on purpose; Copy never re-reads silently; progress reads "checking 3 of 9…" through the 3 s tick | **Auto** — `WikiPackPresentationTests`; `WikiPackRenderTests` (Avalonia); `EndToEndTests` `packRecheck` (WPF) |
| **An icon assertion compares the icon's NAME, never a geometry's `ToString()`** — which is the type name, so every icon equalled every other and the #211 vector assertions could not fail (trap 39); every icon equality carries one negative | **Auto** — `DropsRenderTests` |
| Curated catalogs are never written by automation | **Process** — the weekly refresh PR only flags them |
| The charm catalog is generated, so a new charm or a corrected cast time reaches the arm window without anyone remembering to type it | **Auto** — `SpellTrackingTests`; generated by `charms-harvest.py` in the weekly refresh |

## 4. EQBuddy Mobile

| Expectation | Held by |
|---|---|
| Off by default: a fresh install opens no socket | **Auto** — `CompanionEnableTests`, and through the Avalonia widget that constructs the host — `CompanionWiringTests` |
| **The Linux/macOS build hosts it too** (#208): every source `CompanionSources` declares is wired there, so no surface arrives empty on one platform and full on the other | **Auto** — `CompanionWiringTests` |
| The pairing QR rasterizes with the spec's quiet zone, from one shared constant, in both UIs | **Auto** — `CompanionWiringTests` (`QrRaster`) |
| Turning it on is the only thing that opens the port; a token is minted and rides the URL fragment | **Auto** — `CompanionEnableTests` |
| **The QR carries an address the scanning device can reach**: a real LAN beats a Hyper-V/WSL/VPN adapter (a gateway is the signal), and since #264 Wi-Fi beats ethernet when BOTH are real — the device doing the scanning is on Wi-Fi by definition. No demotion is weakened by that tiebreak; it is smaller than every penalty, so a virtual switch bridged onto Wi-Fi still loses | **Auto** — `LanAddressRankTests` |
| **And the player can override it**, because no rule can know which network the phone is on (#264, "how do I force it to give me a link using the wifi ip"). The pairing window lists every BOUND address under the link — hidden when there is only one, since a choice of one is not a choice — names which is the Wi-Fi one, and remembers the pick. A pin naming an address this machine no longer has falls back to the ranking rather than printing a dead QR | **Auto** — `CompanionPairingAddressTests`, `LanAddressRankTests`; the control's presence on the Avalonia lane — `CompanionWiringTests` |
| A tick with nobody paired builds no projection | **Auto** — `CompanionEnableTests` |
| A device receives only the surfaces the desktop offers **and** it subscribed to | **Auto** — `CompanionSurfaceTests`, `CompanionServerTests` |
| A section only wakes devices watching it; drifting values are excluded from fingerprints | **Auto** — `CompanionProjectionTests` |
| Map geometry is sent once per zone per device | **Auto** — `CompanionMapSourceTests` |
| The breadcrumb trail matches the desktop's fade curve, anchor crumb included | **Auto** — `CompanionMapSourceTests`, `TrailFadeTests` |
| Camp pins and the Named list come from one projected list, so a pin and its row cannot disagree | **Auto** — `CompanionMapSourceTests` |
| Curation applies to the same ledger the desktop edits, and always answers — including when nothing changed | **Auto** — `CompanionCurationTests` |
| An edit made on a device appears on the PC map (via the ledger's `Revision`) | **Auto** — `CompanionCurationTests` |
| The page's fade constants match `TrailFade` | **Auto** — pinned by reading the shipped page |
| Theme reaches the page for every `var(--x)` it uses | **Auto** — `CompanionThemeTests` |
| **A page left open across a PC update reloads itself once** — it never re-fetches otherwise, so the phone would run the JavaScript it downloaded when the tab was opened while both sides report the same version | **Auto** — `CompanionPageUpdateTests` |
| A reload that does not take says so rather than looping | **Auto** — `CompanionPageUpdateTests` |
| **Mobile sounds are off until the owner turns them on** (#208) — one switch in Options → Behavior, beside the pairing button, and a fresh profile is silent | **Auto** — `MobileAlertSoundsTests` |
| A cue needs the listener AND the switch; neither stands in for the other, and the decision is made in one place rather than at each alert site | **Auto** — `MobileAlertSoundsTests` |
| **Every alert that makes a noise on the PC makes one on the phone**, on both lanes — a muted watch rule stays muted on both, because the phone inherits the desktop's own per-rule choice | **Auto** — `MobileAlertSoundsTests` (source scan, both widgets) |
| The cue wakes every paired device whatever it subscribed to, and a quiet tick wakes nobody — the count rides the envelope and never moves on the clock | **Auto** — `MobileAlertSoundsTests` |
| A page plays once per step of the count, never on its first payload and never after the PC restarts — a deadline heard late is worse than silence | **Auto** — `MobileAlertSoundsTests` (reads the shipped page) |
| A silenced phone is TOLD it is silenced; a phone the browser has not unlocked says how to unlock it, in the ⚙ Screens panel and not a modal | **Auto** — `MobileAlertSoundsTests`; **Manual** — tap-once behaviour needs a real browser |
| Desktop alert sounds are untouched by the switch — they keep their own controls, volume and per-rule pickers | **Auto** — `MobileAlertSoundsTests`, `AlertSoundPlanTests` |
| The widget's Quests card shows, opens the Quest Tracker, and keeps both checklists' counts on screen | **Auto** — `EndToEndTests` (EQBUDDY_EXPAND) |
| The old `sky`/`epic` card keys fold onto `quests`, keeping position; hidden only if BOTH were hidden | **Auto** — `OptionsViewModelTests` |
| **The widget's Progress card shows, opens the Progress window, and keeps all five folded headers' numbers on one line** — the PROGRESS THEME (docs/Themes.md) | **Auto** — `EndToEndTests` |
| The old `money`/`faction`/`raids` keys fold onto `progress`, keeping position; hidden only if ALL were hidden; and the fold is idempotent, because `progress` is both a surviving and an absorbed key. **`motes` is NOT one of them** — it is a card again, and a fold that names a live card re-runs forever | **Auto** — `ProgressSectionFoldTests`, `ProgressSurfaceTests` |
| **A card the player hid stays hidden across restarts** (#252, TiconaX). The whole `AppSettings.ApplyMigrations` chain is run twice and the second pass must report no change and move nothing — the defect was two migrations feeding each other across a launch, which no single-migration test can see. Plus the two premises that made it possible: no theme's `AbsorbedCardKeys` may name a key `OverlaySections.Catalog` still offers, and no migration may add a `SectionOrder` key that is not a card | **Auto** — `SectionFoldIdempotenceTests` |
| **The four Progress tabs draw exactly what their five cards drew**: 29 raid rows over 6 zones, 24 sold items with the sold block up, the motes row, five factions — the same numbers pinned on the widget BEFORE the fold | **Auto** — `EndToEndTests` (EQBUDDY_PROGRESS) |
| The ding list appears only once a level is ANNOUNCED, the next-milestone preview once one is KNOWN, and its rows stay folded — unchanged across two moves of that surface | **Auto** — `EndToEndTests` |
| **Level-ups lists every level-up EQBuddy has ever seen for one character** — the store's archived dings merged with the live session's, de-duplicated on (level, time) so a session finalised while the widget is up is not counted twice, newest first | **Auto** — `LevelHistoryTests` |
| The stored half is scoped by the ARCHIVER's identity, and asks for nothing at all rather than for every character when no character is being followed | **Auto** — `LevelHistoryTests` |
| The list is recomputed only on a ding, a session roll or a character switch — never per tick, because it is a SQLite probe over up to a thousand snapshots and the surface paints every second | **Auto** — `LevelHistoryMemoTests` |
| **The Level-ups fold is shut by default** (unlike Skill-ups beside it), its label carries the count and the last ding's date, and the heading is absent entirely for a character that has never dinged | **Auto** — `WidgetRenderTests` (Avalonia), `EndToEndTests` (WPF, `progressLevelUps*`) |
| The gap since the previous level-up is HOVER text only — never a third token on the row, and never an age that ticks | **Auto** — `LevelHistoryTests`, `WidgetRenderTests`, `SurfaceParityTests` |
| The unfolded rows render with stored history behind them | **Manual** — `pwsh scripts/shoot.ps1 -Shot progress-levelups` |
| **EQBuddy Mobile's Experience tab folds the SAME Level-ups rows and the same label** — the phone is sent the merged list rather than the two sources, so it cannot de-duplicate, order or date them differently than the two windows do | **Auto** — `SurfaceParityTests`, `CompanionWireKeyTests` |
| The phone's list is not capped at `MaxRows` like every other list on that wire: the rows are newest-first, so a cap would silently drop the EARLIEST dings (trap 50) | **Auto** — `SurfaceParityTests` |
| A level-up wakes a paired device and the passing of time does not — the fingerprint carries the fold label, which moves on a ding and never on the clock | **Auto** — `CompanionRepaintGateTests` |
| The phone's fold is shut on arrival and its open/shut state is the DEVICE's, never the desktop's `ShowLevelUps` | **Manual** — `pwsh scripts/mobile-harness.ps1 -Snapshot <progress.json> -Screenshot` |
| The Progress theme's tabs, labels and badges come from Core's `ProgressSurface` and UI.Shared's `ProgressTheme`; EQBuddy Mobile offers the same four in the same order with the same badges | **Auto** — `SurfaceParityTests`, `ProgressSurfaceTests`, `WidgetRenderTests` |
| Wealth carries BOTH cards it absorbed — coin and motes, in the badge and in the body | **Auto** — `SurfaceParityTests` |
| **A window whose zoom was never saved keeps its full width** — `TryGetValue`'s `out` default is 0.0, and multiplying by it opened the Quest Tracker at zero width on Linux and macOS | **Auto** — `WindowZoomTests` (Avalonia) |
| The quest surface's tab strip comes from Core's `QuestSurface`; General carries no badge | **Auto** — `CompanionQuestsTests` |
| The quest catalog index ships once per device by stamp, and re-ships when any field changes | **Auto** — `CompanionQuestsTests` |
| **A sticky payload is re-shipped when the device stops holding it** — "sent once" is about the device, but the page carries the copy forward off the PREVIOUS payload, so adding a surface in ⚙ Screens (or the desktop gating one off and back on) left Quests on "Waiting for the quest catalog from the PC…" forever, and the map blank in the same zone | **Auto** — `CompanionQuestsTests`, `CompanionSurfaceTests` |
| **The repaint gate can see a catalog ARRIVE** — its content stays out of the key (1,200 quests), its presence does not, because `setCatalog` is a side effect of the paint | **Auto** — `CompanionRepaintGateTests` |
| The general list's membership and order are Core's `QuestMatcher`'s; dismissed and completed non-repeatables are excluded | **Auto** — `CompanionQuestsTests` |
| Class restrictions are resolved by Core's `QuestClassFilter` at index build — the page checks membership, never parses class text | **Auto** — `CompanionQuestsTests` |
| A device's 📌 and class-picker taps land on the same `QuestLedgerStore` the desktop writes; repeats are not changes | **Auto** — `CompanionQuestsTests` |
| Epic/Sky rows inside the quest surface still tick under their old surface names | **Auto** — `CompanionQuestsTests`, `CompanionSurfaceTests` |
| A same-named attacker while your pet is on HOLD does not break the charm claim | **Auto** — `SpellTrackingTests` |
| Releasing the hold restores the ordinary break rule; another player's pet holding is not our excuse | **Auto** — `SpellTrackingTests` |
| One mez break costs ONE chip, though the game prints both a fade and an awakened line | **Auto** — `MezTrackerTests` |
| Two real breaks in the same second still drop two chips (pairing stays 1:1) | **Auto** — `MezTrackerTests` |
| With no class filter and no tab, a shared drop ticks ONCE and is flagged — never once per class | **Auto** — `SkyLootAutoCheckTests`, `EpicLootAutoCheckTests` |
| A proper-named kill outside the catalog starts tracking; its SECOND kill measures the cycle | **Auto** — `DiscoveredNamedTests` |
| Discovery never fires for articled trash, someone else's kill, or a catalogued family's siblings | **Auto** — `DiscoveredNamedTests`, `NamedMobHeuristicTests` |
| **Discovery honours the #109 instanced-raid-zone gate** — the gate was checked only inside the catalog loop, so a named the catalog did not list was discovered and timed inside a raid instance (found by Fable 5 planning, 2026-08-21) | **Auto** — `DiscoveredNamedTests` |
| **The game's "- a rare creature -" consider marker opens discovery to articled named mobs** — parsed off the line (it used to be swallowed into the NAME, silently missing the sighting), remembered per session, and your kill then discovers it; without the marker, articled kills still never discover | **Auto** — `LogParserTests`, `DiscoveredNamedTests` |
| The named/trash verdict is taken from the RAW name at parse time, before `Normalize` strips the article | **Auto** — `NamedMobHeuristicTests` |
| Sky groups by REWARD (not by turn-in NPC), with ready/in-progress/done and a per-reward count | **Auto** — `QuestChecklistLayoutTests` |
| Every checklist row names the turn-in NPC and the drop location; Epic prefers its source | **Auto** — `QuestChecklistLayoutTests` |
| A tick the loot auto-checker placed itself wears `*` on **all three** surfaces, not just mobile | **Auto** — `QuestChecklistLayoutTests`, `CompanionProjectionTests` |
| The minimized bar draws its ten stat icons as VECTORS, in a fixed order, from one shared table | **Auto** — `MiniBarPresentationTests`; **Shot** — `shoot.ps1 -Shot mini-bar` |
| No setting gains readers without a writer — the signature behind #204, #210 and #212 | **Auto** — `DeadSettingTests` |
| Mobile's Sky list is never narrowed by a setting no player can change, and its ★ Ready band is a summary — no checkbox, never struck through | **Auto** — `SurfaceParityTests` |
| EQBuddy Mobile groups, orders and labels Sky **identically to the desktop**, because both call `QuestChecklistLayout` | **Auto** — `SurfaceParityTests` |
| The state lens (any / open / ready / done) narrows the Sky and Epic tabs, and an unset or unknown state never empties the screen | **Auto** — `QuestChecklistStateTests` |
| A Sky reward holding every piece reads **ready**; an Epic section holding every piece reads **done** — Epic has no hand-in of its own | **Auto** — `QuestChecklistStateTests` |
| Sky is ordered by ACTIONABILITY within a class: ready, then closest-to-done, then untouched, with the turned-in sunk to the bottom | **Auto** — `QuestChecklistStateTests` |
| The Ready band lists every all-pieces-in-hand reward across EVERY class, names its NPC, and is absent when nothing is ready | **Auto** — `QuestChecklistStateTests`; **Shot** — `shoot.ps1 -Shot sky-checklist` |
| Per-class D / R / P counts are drawn, and D+R+P deliberately does not sum to the total | **Auto** — `QuestChecklistStateTests`; **Shot** — `shoot.ps1 -Shot sky-checklist` |
| "Epic complete" WRITES `EpicQuestCompleted`, snapshots what it overwrites, and Reopen restores the player's own ticks | **Auto** — `EpicCompleteWritePathTests`; **Shot** — `shoot.ps1 -Shot epic-checklist` |
| While a class's epic is complete its rows are locked **and look locked** — the undo restores a snapshot, so a tick made meanwhile would be discarded | **Auto** — `EpicCompleteWritePathTests`; **Shot** — `shoot.ps1 -Shot epic-checklist` |
| A clickable inline icon responds across its whole square, not only where the vector is painted | **Manual** — Loot card, click the gap inside the green quest marker (#211) |
| The class-filter button face never grows with the selection, so the mode strip stays on screen | **Auto** — `ClassFilterLabelTests` |
| A quest's status badge and its state rule say the SAME thing, in every theme's palette | **Auto** — `QuestPresentationTests` |
| A finished REPEATABLE whose turn-ins are in hand reads "ready", never "done" | **Auto** — `QuestPresentationTests` |
| Nothing ready says nothing at all — never "0 quests ready to turn in" | **Auto** — `QuestPresentationTests` |
| The Quest Tracker builds a LIST, selects a row, and fills the detail pane beside it | **Auto** — `EndToEndTests` (EQBUDDY_QUESTS), `QuestsRenderTests` |
| The render cap holds at 60 rows and the withheld remainder is counted, never silently dropped | **Auto** — `EndToEndTests` |
| The five per-quest controls (track, done, hide, report) are reachable on the detail pane | **Auto** — `QuestsRenderTests` |
| A mis-clicked checklist tick can be taken back (undo / Ctrl+Z), repeatedly | **Manual** — open Quests → Plane of Sky, tick a row, undo |
| The Quest Tracker's height cap follows the monitor it is on, re-applied as it is dragged | **Manual** — drag it to a shorter second screen; it must not overflow |
| Ctrl+wheel resizes the Quest Tracker WINDOW, and a saved zoom survives reopening it | **Manual** — zoom out, close, reopen; layout must be identical, only smaller |
| Layout: solo panel fills the viewport; chrome shrinks in fullscreen; nothing scrolls sideways | **Manual** — §6, or the harness |

### Mez durations

| Expectation | Held by |
|---|---|
| **A typed mez duration outranks both the learned value and the catalog** — the spawn-timer contract, applied to mezzes (Reddit ask, 2026-08-20) | **Auto** — `MezDurationOverrideTests` |
| Learning continues under a typed value but can never win, so clearing the box lands on what has been observed SINCE rather than what was known that day | **Auto** — `MezDurationOverrideTests` |
| One typed value covers every rank of that spell | **Auto** — `MezDurationOverrideTests` |
| Typed values persist, and are never put through the learned store's healing pass — a player saying "mine is shorter than the book" is allowed to say it | **Auto** — `MezDurationOverrideTests` |
| A corrupt overrides file costs the typed values and nothing else | **Auto** — `MezDurationOverrideTests` |
| **A bare number in a mez box is SECONDS** ("44" = 44s), while a bare number in a spawn box is still MINUTES — the two parsers mean different things on purpose | **Auto** — `MezDurationOverrideTests` |
| The editor exists on BOTH widgets, from one row builder, with the provenance line | **Auto** — `OptionsRenderTests` (Avalonia); **Shot** — `shoot.ps1 -Shot options-mez` |

## 4b. The widget's geometry

| Expectation | Held by |
|---|---|
| The card list covers the same screen height at any UI scale; the last card is always reachable by scrolling | **Auto** — `WidgetMetricsTests` (#144) |
| A dragged height is stored pre-scale and clamped against a cap in the same units | **Auto** — `WidgetMetricsTests` |
| The list never collapses below one card, and no scale can make the cap infinite | **Auto** — `WidgetMetricsTests` |
| A grow-up chip stack holds its bottom edge as chips come and go | **Auto** — `ChipStackAnchorTests` (#122) |
| A closing window (height 0) cannot move the anchor; the stack returns to the same spot across repeated empty/refill cycles | **Auto** — `ChipStackAnchorTests` (#152) |
| A drag moves the anchor; a grow-down stack is never repositioned | **Auto** — `ChipStackAnchorTests` |
| The card list's cap is converted at the point of assignment, in the real app at a real scale | **Auto** — `EndToEndTests` (E2E; #144's other half) |
| The chip anchor is actually wired to the window's events | **Manual** — §6 items 2–3 |
| **Nothing on a timer may change the widget's measured size.** The title-bar CPU/memory readout formats to a fixed shape and its label reserves a fixed width, so a new sample repaints and never asks the windowing system to resize | **Auto** — `PerfReadoutTests`, `WidgetRenderTests` (#173) |
| The readout stays off by default and costs no title-bar width until it is turned on | **Auto** — `WidgetRenderTests` (#112) |
| That an always-on-top widget resizing does not disturb a fullscreen game underneath | **Manual** — §6 item 10; no headless test can see it |
| **The breakout windows never take focus when the minimize pass shows them** — the star was clicked minutes ago; the show happens mid-fight, so both UIs' breakouts declare `ShowActivated` false like every other unprompted surface | **Auto** — `OverlayActivationTests` (source scan, both UIs) |
| Clicking or dragging a WPF chip stack does not activate it either (`WS_EX_NOACTIVATE`, as the alert tile has always had) | **Manual** — the ex-style is applied in `NoActivate.cs`; only a live desktop shows focus |
| **Every theme card expands in place with ONE owner of the body** (Inline themes, complete 2026-08-26): pop-out collapses the card, closing the window leaves it collapsed, the tab follows the player both ways, and a Glance room (Drops, Inventory, Quests/General, Unlocks) draws its line and never builds its full view — on BOTH lanes, where on Avalonia two hosts of one control is a crash, not a layout bug | **Auto** — `ThemeHostTests`, `EndToEndTests` (placement facts for all four themes, named-room opens), `WidgetRenderTests` (crash-class sequences, Glance lines), `SurfaceOwnershipTests` (exemption list EMPTY; factories asserted positively); **Shot** — `theme-inline-kills`, `-kills-glance`, `-loot`, `-quests`, `-quests-epic`, predictions written first |
| An inline Epic/Sky room is ONE class's rows, capped at 12 with "... and N more", read-only — `QuestInline` owns the arrangement so the lanes cannot re-decide it | **Auto** — `EndToEndTests`, `WidgetRenderTests` |
| **An expanded theme body follows the HEIGHT GRIP** (#250, Paineless: *"cannot just expand window size"*): never dragged → 320 exactly, so an untouched widget is pixel-identical; dragged → `clamp(granted − chrome, 320, 640)` where *granted* is what the monitor allowed (not the raw drag) and *chrome* is every visible card's HEADER plus the open card's own strips — never a sibling's body. Ceiling is 2× the floor; `SectionMaxHeight` still owns the stack; overflow still scrolls inside the body and the wheel still reaches the stack (trap 36). Both lanes call one `WidgetMetrics.ThemeBodyCap` | **Auto** — `WidgetMetricsTests` (floor/ceiling/unmeasured/negative/whole-unit, and that the monitor-granted height ≠ the drag at 125%), `ThemeBodyCapRenderTests` (the cap reaches the real `ScrollViewer`, both directions), `EndToEndTests` (a launched app: undragged 320, dragged grows and stays ≤ 640, short drag keeps the floor); **Shot** — `theme-body-dragged` / `-125` against `theme-inline-loot`, predictions written first and the miss recorded beside them |
| **The height grip's tooltip names what a drag actually buys right now** — with every card visible AND an open theme body cut off, "the widget is sizing itself automatically" is false, which is the silent-no-op defect with the switch on the other side; one wording for both lanes, only the gesture name differs | **Auto** — `HeightGripTipTests` (all four branches, plus that every branch still offers the shortening drag and the way back to automatic) |
| **The expand/minimize swap anchors the widget's RIGHT edge**, so the mode toggle (second from right on both bars) stays under the cursor in both directions and habitual click-click toggling is safe (#239, disberon) — the arithmetic is `WidgetMetrics.RightAnchoredLeft` (trap 1: one unit space; Avalonia converts its physical Position), a startup call with no real width leaves the window alone, and no work-area clamp on purpose (a negative Left is a real place on a multi-monitor desk) | **Auto** — `WidgetMetricsTests`; **Harness** — `scripts/mode-swap-verify.ps1` (real mouse, real exe; its first run caught the anchor computing before `UpdateMiniChips`) |
| **A frameless pop-out has a border the mouse can actually find** — the `WM_NCHITTEST` hook is wired from `AllowResize` itself, so a window cannot join the feature and miss the affordance, and a visible accent bar shows exactly when a bottom drag would engage (#238) | **Auto** — `ResizableWindowTests` (source-pinned); **Harness** — `drag-verify.ps1 -Window <any of 8>`, phase D drags with a real mouse |
| **The height follows the content until the player grabs an edge, and only a height the player took is ever stored** — an undragged close persists nothing and removes any junk entry it finds; every pre-2026-08-25 stored height (none of them chosen — the border did not work) is discarded once | **Auto** — `WindowSizingTests`, `ResizableWindowTests`, `AppSettings.MigrateWindowHeights`; **Harness** — `drag-verify.ps1` phases B–E |
| **A pop-out OPENS at the design body height, not at a fraction of the monitor** — a tall screen gave the Quest Tracker a 1,822 px first open; the cap is one shared constant, and a dragged window's body fills whatever the player chose instead (#238) | **Auto** — `WindowSizingTests` (`BodyCap`) |
| **A surface's own nested scroller takes its cap from the HOST, not from a constant** — `GearCardView`'s gear list carried a card-sized 320 into the Gear & Loot window, so dragging that window taller grew the window and left the list alone (trap 36's own flagged loose end). It is re-pointed, not deleted: the auto-tick note, the ⧉ copy of `/outputfile inventory` and the import report stay outside the scroller so a long list cannot bury them (traps 34, 37). Inline it follows `ThemeBodyCap`; in the window it follows `WindowSizing.BodyCap` | **Auto** — `WindowSizingTests` (`NestedBodyCap`: follows the host, pinned chrome always subtracted, never a sliver, unsized host answers the design height), `EndToEndTests` (a launched app: the list cap is derived from the window's body cap and is strictly smaller than it); **Shot** — `gearloot-gear` re-shot, prediction written first |
| **"Keep EQBuddy out of Alt+Tab" only ever ADDS the tool-window bit to focusable windows** — turning it off must not strip the bit that chips, the alert tile and the overlays set for themselves, or every chip joins the switcher (found in #238 review; the guard is the `WS_EX_NOACTIVATE` check in both lanes' `SetToolWindow`) | **Manual** — only a live desktop shows switcher membership; the E2E dump pins the main window's EFFECT (`altTabStyle`) beside the setting |
| **Hiding from Alt+Tab also drops the MAIN window's taskbar button, and only then does the widget actually leave the switcher** (Hateborne, 2026-09-03) — ShowInTaskbar=true is asserted as `WS_EX_APPWINDOW`, which overrides `WS_EX_TOOLWINDOW` for switcher membership, so for a week the style landed on every window and the one window a player could see stayed. The taskbar cost is the one `AltTabPolicy.TaskbarWarning` always promised; satellites are always `ShowInTaskbar=false` and are never touched. Hidden means BOTH bits agree: style on, APPWINDOW off | **Auto** — `EndToEndTests.TheWidgetActuallyLeavesAltTabWhenAskedTo` (real HWND, setting seeded ON: `altTabStyle=1`, `altTabAppWindow=0`, `altTabTaskbar=0`), the OFF-default twin asserts the inverse; `AltTabPolicyTests.HidingFromAltTabIsExactlyWhatCostsTheTaskbarButton` |

## 4e. The design system

Typography, spacing, shape, control size and icon geometry live in `UI.Shared` as data
(`UI.Shared/DesignTokens.cs`, `UI.Shared/IconPaths.cs`) exactly as colour already did, and
each UI composes them into its own resources. The audit that started this
([docs/DesignSystem.md](DesignSystem.md)) counted 13 font sizes over 612 assignments, 174
Thickness tuples and 84 icon glyphs — none of which anything could detect, which is why the
guards below are the deliverable and not a nicety.

| Expectation | Held by |
|---|---|
| Seven type roles, none below the 10pt readable floor, each inking with a real palette key | **Auto** — `DesignSystemTests` |
| A migrated surface carries no literal font size, radius or spacing — 0 and 1 excepted | **Auto** — `DesignRatchetTests` |
| A migrated surface draws icons as vectors, never glyphs (#148, #166 were Wine glyph failures) | **Auto** — `DesignRatchetTests` |
| **The bundled Wine font carries every icon the source can emit** — Wine's DirectWrite reads only the primary font and has no fallback, so a missing glyph is a box | **Auto** — `IconFontCoverageTests` |
| **…and it ships every WEIGHT the app asks for** (Regular 400, SemiBold 600, Bold 700), grouped under one typographic family, each face carrying the whole icon set. A family with no bold face does not fail — WPF SYNTHESISES one, smearing the Regular outlines wider and leaving every bold run mis-fitted (reported from CrossOver on macOS, 2026-08-21) | **Auto** — `BundledFontFaceTests` |
| **…and it keeps `smcp`/`c2sc`**, because `SectionLabel` asks for `Typography.Capitals=AllSmallCaps` on ~40 headings and WPF synthesises no small caps | **Auto** — `BundledFontFaceTests` |
| **…and every face on disk is packed as a `Resource`** — a face the csproj does not carry is a weight WPF still cannot resolve at runtime | **Auto** — `BundledFontFaceTests` |
| **Wine gets whole-pixel glyph positioning and Windows keeps WPF's default.** Wine truncates the fractional advances `Ideal` mode relies on, opening 1–2px gaps *inside* words in text whose font metrics are exactly right; `Display` is the only mode it renders correctly (measured under CrossOver: Ideal split 12 words into 17 pieces, Display into 12, and `TextRenderingMode` changed nothing) | **Auto** — `TextRenderingPolicyTests`; **Probe** — `EQBuddy.exe --textprobe` |
| Every icon parses with a real geometry parser and fills the 24×24 grid without overflowing it | **Auto** — `IconGeometryTests` (Avalonia headless) |
| No two icon names share geometry — the audit's "✓ ×62 and ✔ ×15" defect cannot recur | **Auto** — `DesignSystemTests` |
| Every one of the ~11k shipped items resolves to a real reward silhouette, dirty catalog slots included | **Auto** — `DesignSystemTests` |
| A weapon's SKILL outranks its slot, so a 2H Blunt is never drawn as a sword | **Auto** — `DesignSystemTests` |
| Screenshot capture makes only the window GROUND opaque, never a tint, and is off unless asked | **Auto** — `DesignSystemTests` (`EQBUDDY_OPAQUE`) |
| **A breakdown row's NAME outranks the stat line beside it**: the name is content-sized and CAPPED, so a long stat line can never squeeze a row to its ellipsis (#182 — rows reading "." and "..") **and a short name can never hoard room the stat line needs** (David, 2026-08-19) | **Auto** — `BreakdownRowLayoutTests`; **Shot** — `shoot.ps1 -Shot damage-breakout` |
| **Hovering a breakdown row returns the full name, the full stat line, and the caller's own tooltip** — trimming stays, unreadability doesn't | **Auto** — `BreakdownRowLayoutTests` |
| **The frameless resize band is wide enough to hit**, and still leaves the title row's controls their hit areas | **Auto** — `BreakdownRowLayoutTests`, `ResizeZonesTests` |
| **Every window follows the widget when it auto-hides**, by default — the exceptions are named, so a window added later follows without anyone remembering (#189) | **Auto** — `FocusHideTests` |
| **The sound picker offers every format the OS plays**, from one list both UIs compose their own picker from (#197) | **Auto** — `AlertSoundPlanTests` |
| **A watch-rule sort strip offers the same four options in both UIs, and the stored default lights one of them** — a key spelled differently in one lane would paint a strip with nothing selected | **Auto** — `SortStripTests` |
| **The fight-side chip stack tells its three kinds apart by VECTOR** — a spawn stopwatch, a mez crescent, a slow hourglass — not by an emoji that renders as a box under Wine | **Auto** — `ChipStackTests`, `WidgetRenderTests` |
| **Ticking a breakout window in Options turns it ON** — it stars the stat that opens it, instead of only clearing the ✕-dismissal while the real switch sat unadvertised on a card (the recurring "how do I get the pet damage window" question) | **Auto** — `OptionsRenderTests`; **Shot** — `shoot.ps1 -Shot options-cards` |
| The box reports whether the window would actually OPEN, not merely whether it was dismissed | **Auto** — `OptionsRenderTests` |
| Unticking stops the window and leaves the star, so a mini-pill cell never vanishes as a side effect | **Auto** — `OptionsRenderTests` |
| **Which star opens which window is one table in UI.Shared**, read by both widgets' gates AND by Options — the two answered it separately, which is how a tick box came to exist that could not turn anything on | **Auto** — `OptionsRenderTests` |
| A captured surface is reviewed as a real render, on a seeded session, over a plain backdrop | **Manual** — `pwsh scripts/shoot.ps1` |
| A release note that says something MOVED is marked with a badge and drawn differently from an ordinary change; the marker itself never reaches the screen | `WhatsNewNotesTests` + **Manual** — `pwsh scripts/shoot.ps1 -Shot whats-new` |
| A single card BODY can be photographed without opening every card: `EQBUDDY_EXPAND` takes card keys (`EQBUDDY_EXPAND=kills`) as well as `1` | **Manual** — `pwsh scripts/shoot.ps1 -Shot kills-card` |
| **A card whose body only exists once there is DATA is staged, not shot empty** — the Watch strip needs two rules, the Raids rows need a clear | **Manual** — `pwsh scripts/shoot.ps1 -Shot tracked-card,raids-card` |
| A surface that moved into a WINDOW keeps a way to be photographed: `EQBUDDY_PROGRESS` opens the Progress window on a named tab, as `EQBUDDY_QUESTS` does for the tracker | **Manual** — `pwsh scripts/shoot.ps1 -Shot progress-wealth` |
| **A shot names a PROCESS, not just a title** — four Progress shots share the title `EQBuddy Progress`, and a previous shot's app that is still exiting matches it | **Auto** — `shoot.ps1` passes `-OwnerPid` |
| **A running EQBuddy is stood down before shooting and relaunched afterwards** — it is always-on-top and holds the same titles, so a capture of it is the wrong app in whatever unseeded state it happens to be in. Closed gracefully, so the live session still finalizes into `history.db` | **Auto** — `shoot.ps1` |
| **The quick tour's illustrations show the app that ships** — every page can be opened directly (`EQBUDDY_TOUR=<page>`, 1-based) so the pictures can be looked at without installing and clicking Next. They were a month stale, showing emoji card icons, a card called "Tracked" and no KPI strip, for exactly that reason | **Manual** — `pwsh scripts/shoot.ps1 -Shot tour-widget,tour-combat,tour-watch,tour-mini,tour-history` |
| **A shot's staging list that enumerates an enum by hand grows with it** — `mini-bar` disables every `BreakoutKind`, and when `Progress` joined the enum the shot silently began photographing the Progress breakout instead of the mini bar — the same list needed editing again on 2026-08-25 when `Progress` LEFT the enum | **Manual** — `pwsh scripts/shoot.ps1 -Shot mini-bar`; the capture must be the wide pill, not a window |
| **A shot fixture's `Title` survives the window it names being FOLDED AWAY** - `shoot.ps1` stops on the first failure, so one stale title takes the whole batch down and every shot after it with it. `spawns-window`, `spawns-sky` and `zone-map` matched the three windows World PR 2 deleted, and a batch run died at shot 37 for six days and four releases while single `-Shot` runs kept working | **Manual** - `pwsh scripts/shoot.ps1` with no `-Shot`, which is the only run that proves the rest |
| **A capture pins the palette it is shooting** — `AppTheme`'s brushes are process-wide singletons and `AppThemeTests` walks the whole catalog, so an unpinned capture renders in whatever theme ran last | **Auto** — `WidgetSheetTests` |

## 4f. Surfaces that ask you to run an in-game command

David, 2026-08-14: **every surface that names an in-game command offers a one-click ⧉ copy
of the exact text**, and the text lives once in `UI.Shared/GameCommands.cs`. Restated
2026-08-20 with the case that proved it was not being enforced — the Gear tab told him to
import something and handed him no way to do it: *"That needs to be applied for every
instance of needing the user to execute a command in game for an output file."*

The rule had **one** guard, and it was the wrong shape. `GameCommandsTests` forbade a copy
source from carrying its own literal — which says nothing at all about a surface carrying
no copy source. The gear checklist fell straight through that hole, on both widgets, for as
long as the surface had existed. **A negative assertion cannot see an absence**, so the list
of surfaces is now written down and asserted positively.

**And the command is only half of it.** David, 2026-08-20, after being handed the button:
*"We automatically read the logs, we should automatically read the other files we generate.
I shouldn't have to do a bunch of menu navigation and then folder searching hunting around
for something that can just be lifted directly."* He was right, and the line was already
there — the game prints `Outputfile Complete: <file>` into the log EQBuddy tails, naming the
file, and nothing parsed it. Every other piece existed too: the finder, the auto-check, the
achievements importer. **The defect was a missing seam, not a missing feature.**

| Expectation | Held by |
|---|---|
| The game's own announcement of a dump is parsed, from the line as David's log writes it | **Auto** — `OutputfileAutoImportTests` (quoted verbatim, not described) |
| The filename decides which importer runs; a dump EQBuddy cannot read is named, not guessed at | **Auto** — `OutputfileAutoImportTests` |
| The dump is located from the log folder alone — it lives in the Logs folder's PARENT, and no player ever supplies that | **Auto** — `OutputfileAutoImportTests` |
| A name with a path in it never resolves — the log prints bare names, so anything else was not what we were told about | **Auto** — `OutputfileAutoImportTests` |
| **An import is add-only and reversible**: the Undo puts back exactly what that import changed, leaves the player's own ticks alone, and never removes a witnessed raid kill | **Auto** — `OutputfileAutoImportTests` |
| Re-reading the same dump applies nothing further and offers no second undo | **Auto** — `OutputfileAutoImportTests` |
| **The report says the dump was READ even when nothing changed** — "EQBuddy did nothing" and "EQBuddy never saw your file" are indistinguishable to the player, and only one is a fault | **Auto** — `OutputfileAutoImportTests` |
| Plane of Sky steps group by island, ordered numerically (1.5 sorts where it belongs), with steps that name no island kept ungrouped under "Anywhere on the plane" | **Auto** — `SkyIslandsTests`, `SkyIslandGroupingTests`; **Shot** — `shoot.ps1 -Shot sky-checklist` |
| A step on SEVERAL islands is listed once under "Several islands", or under every island it names — the player's choice, honoured on both desktops and the phone | **Auto** — `SkyIslandGroupingTests` |
| **Repeating a step never changes the score.** Progress counts distinct steps, not rendered rows | **Auto** — `SkyIslandGroupingTests.RepeatingAStepNeverChangesTheScore` (fails on the pre-fix tree) |
| **Every recorded import outcome REACHES a surface.** The achievements one was written and never read for two days, in both UIs: dumps were imported silently, with no report and no Undo | **Auto** — `ImportReportReachesASurfaceTests` (fails 6/11 on the pre-fix tree) |
| The report names what it SKIPPED as well as what it applied — rewards refused by the #101 auto-grant guard, and obtained rewards that matched nothing on the checklist. A guard that stays silent looks exactly like a broken import | **Auto** — `OutputfileAutoImportTests` |
| Something to SAY is not something to UNDO: a run that only skipped still reports, and still offers no Undo button | **Auto** — `OutputfileAutoImportTests` |
| The achievements report is on the **Raids** surface — the one that asks the player to run the command — above the rows rather than after them, so it is not below the fold | **Shot** — `shoot.ps1 -Shot raids-import` |
| Each command has exactly one definition, and it is the text the game expects | **Auto** — `GameCommandsTests` |
| No copy source anywhere in `src` carries its own slash-command literal | **Auto** — `GameCommandsTests` |
| **Every surface that NEEDS a command names it, off `GameCommands`** — a curated list with a reason per entry, both UIs, the way `DeadSettingTests.Known` is curated. Adding a surface that asks for an output file means adding its row | **Auto** — `GameCommandsTests` |
| The ⧉ button is on the POPULATED state too, not only the empty one — the player likeliest to need a fresh dump is the one whose import has gone stale | **Auto** — `EndToEndTests` (`gearCopyCmd`), `WidgetRenderTests` |
| **The button is really on screen, not merely referenced** — an absent control photographs as an unremarkable panel, so it is pinned where controls can be seen rather than reviewed in a picture | **Auto** — `EndToEndTests` (WPF has no unit tests), `WidgetRenderTests` (Avalonia has no E2E) |
| A surface that names an import names **where the import comes from and where to run it** — the gear empty state carries both routes, and the shopping-list one is not the `/outputfile` one | **Auto** — `WidgetRenderTests`; **Shot** — `shoot.ps1 -Shot gearloot-gear-empty` |
| **EQBuddy Mobile shows the command as selectable text, never a copy button** (David, 2026-08-20, asked directly) — a phone's clipboard cannot reach the game on the PC, so a button there is a silent no-op wearing a working control's clothes | **Auto** — `GameCommandsTests`, `CompanionSurfaceTests` |
| The phone never spells a command itself: it arrives on the wire from `GameCommands`. A page-side literal can outlive the PC's by weeks (trap 32 — the page never re-fetches itself) | **Auto** — `GameCommandsTests` |
| **A class name is compared through its canonical spelling, never literally** — the dump writes "Shadowknight", every catalog writes "Shadow Knight", and the literal compare dropped all sixteen Shadow Knight Sky rewards before the guard and before `unmatched` (#238, Hateborne) | **Auto** — `AchievementsImportTests.ShadowknightIsTheSameClassAsShadowKnight` (fails on the pre-fix tree) |
| **`/outputfile faction` is recognised by SUFFIX, never by segment count** — the real filename splices the class code into the middle (`Name_server-ENC-Factions.txt`), which is trap 48's lesson on a different file | **Auto** — `OutputfileAutoImportTests` |
| **The dump-kind dispatch is a switch, not `if (Inventory) else`** — the else meant "everything that is not inventory", so a faction dump was parsed as achievements and wiped the character's unlocked-class list | **Auto** — `OutputfileAutoImportTests.AFactionDumpIsNotAnAchievementsDump` |
| The Unlocks tab reads requirements from the dump itself and curates no catalog of them; a granted unlock's own checkmarks are never evidence (the faction dump is), and an unresolvable faction name is reported, never dropped | **Auto** — `UnlockRequirementsTests` (Hateborne's real dump pair as fixtures) |
| The Sky tab offers the achievements command that feeds it, and a Sky Test turn-in on the Quests tab and the reward on the Sky tab are ONE fact with one store (`SkyQuestCompleted`) | **Auto** — `SkyTestSplitTests`, `GameCommandsTests`, `EndToEndTests` (`questsSkyCopyCmd`) |
| **A `/outputfile inventory` dump trues up quest have-counts, not just gear** (#241, DasGud): present in the dump is its exact count, absent is zero, and a Manual count is superseded — the dump is the game's own statement of possession, strictly better than a log tally that cannot see a hand-in or off-log acquisition | **Auto** — `QuestTrackerTests.DasGud241TripleReconcilesLootedToTheDump` and the surrounding reconcile tests |
| The reconcile runs on the **ingest**, in log order — a loot line after the dump survives, a loot line before it is squared, and a launch replay of the identical event sequence changes nothing (idempotent by a per-character watermark) | **Auto** — `QuestTrackerTests.PreDumpLootIsSquaredPostDumpLootSurvivesAndReplayIsIdempotent`, `SessionStatsQuestReconcileTests` |
| An empty or stale-watermark dump is a no-op — a bad parse must not erase what the ledger already knew | **Auto** — `QuestTrackerTests.AnEmptyDumpIsANoOpAndDoesNotEraseKnownCounts`, `AnOlderDumpThanTheWatermarkIsANoOp` |
| A turn-in (✔ or catch-up) can still lower a count after a reconcile — the negative clamp covers the VERIFIED count, not only the log-derived one | **Auto** — `QuestTrackerTests.ATurnInAfterAReconcileCanLowerAVerifiedCount` (fails on the pre-fix `-Looted`-only clamp) |
| Both widgets wire the resolver beside `QuestStore`, before the watcher starts, reusing `InventoryFile.FindLatest` rather than a second reader | **Auto** — `QuestReconcileWiringTests` |
| **Turning a Sky reward in consumes its turn-in items from the quest ledger** (#241 PR 2) — the ✔ was the log for a hand-in the game never writes, and the Sky tab's own button had never kept that promise. Reopening still does not restore the items, matching "reopen does not untick" | **Auto** — `SkyCompleteToggleTests.TurningInConsumesTheRewardsItemsFromTheLedger`, `ReopeningDoesNotRestoreTheConsumedLedgerItems` |
| A click and the achievements import racing the same reward cannot double-consume the ledger | **Auto** — `SkyCompleteToggleTests.MarkingTwiceConsumesTheLedgerOnlyOnce` |
| **A held Sky item is "no longer needed" only when EVERY class's reward that takes it is turned in** (#243, tvongaza) — a shared Wind Rune with one of six rewards done is not five-sixths of the claim, it is a different claim, and it goes in the separate "Other classes still want" band instead | **Auto** — `SkyLeftoversTests` |
| **The "other classes" band is never produced without a class lens** — no lens is not a wildcard (#193's rule, one surface over): with no classes known there is no "other" to be other than. The "no longer needed" band is a claim about the GAME and is not lens-gated | **Auto** — `SkyLeftoversTests`, `QuestsRenderTests.WithNoClassLensBandBIsSuppressedAndBandAIsNot` |
| **Another quest in the catalog wanting the item keeps it out of "no longer needed"**, and the band says which quest rather than letting the item silently vanish — nothing here destroys anything, so the only cost this feature can impose is a wrong "you can free that". The split Sky Tests are the checklist and never veto their own items | **Auto** — `SkyLeftoversTests.TheHeldBackNoteNamesTheQuestAndDisappearsWhenThereIsNone`, `QuestsRenderTests.AnItemAnotherQuestWantsLeavesTheBandAndIsExplainedUnderIt` |
| The row's words are said ONCE for every surface — `{Item} ×{held} · {where}` and both headings live in Core, so the two desktop bands (and the phone group after them) cannot drift into three answers. Band B's hover says "not yours", never "junk" | **Auto** — `SkyLeftoversTests.TheRowAndTheHeadingsAreSaidOnceForEverySurface`, `EachHeadingCountsExactlyTheRowsItsBandWillDraw` |
| **Band B's hover LEADS with the caveat** — `Not yours — still wanted by {classes}; a Legends character can unlock one later.` (Bevel, Helm-signed 2026-09-02). The phone draws a row's detail as one ellipsised `.sub` line, so leading with "Still wanted by…" truncated away the sentence the second band exists to say. Order is a decision once a renderer can only show the front of the string; fixed in the Core string rather than by widening the page (trap 32). The already-turned-in evidence stays BEHIND the caveat, so truncation eats the least load-bearing end | **Auto** — `SkyLeftoversTests.TheRowAndTheHeadingsAreSaidOnceForEverySurface` (whole string + `StartsWith`), `BandBKeepsItsAlreadyTurnedInEvidenceAfterTheCaveat` |
| **Both desktop lanes DRAW the two bands, under Ready, and each band is absent rather than empty** — including the state every new player is in, where no dump has ever been read. A band that is never added photographs as an unremarkable panel (trap 29) and no negative assertion can see it (trap 34), so presence is asserted on both lanes | **Auto** — `QuestsRenderTests.TheTwoLeftoverBandsAreSeparateAndSayDifferentThings` / `WithNoDumpNeitherBandIsDrawnAtAll` (Avalonia), `EndToEndTests.TheSkyTabListsTheLeftoversTheDumpAndTheTurnInsAgreeOn` / `WithNoInventoryDumpTheSkyTabDrawsNoLeftoverBands` (WPF) |
| **EQBuddy Mobile's Plane of Sky tab draws the same two bands** (#243 PR 2), from the same `SkyLeftovers.Compute` on the same inputs — the phone is the third renderer and invents no heading, row or hover of its own. Non-tickable and carrying no class, so neither a checkbox nor the page's class chips can reach them; the checklist's own done/total is unmoved. **No page change**: `index.html` already draws a `tickable === false` group generically, which matters because a page-side fix can sit unseen on an open phone for weeks (trap 32) | **Auto** — `SurfaceParityTests.ThePhonesLeftoverRowsAreCoresRowsExactly`, `TheTwoBandsAreNeverMixedUnderOneHeadingOnThePhone`, `NoDumpMeansNoBandsOnThePhoneEither`, `BandBIsSuppressedWithNoClassLensOnThePhoneToo`, `ThePagesClassChipsCannotHideTheBandsThatAnswerAboutClasses`, `ALeftoverRowIsNotAChecklistItem`, `TheBandsNeverMoveTheChecklistsOwnCounts`. **Manual** — `pwsh scripts/mobile-harness.ps1 -Snapshot <sky.json> -Screenshot` off `ScreenshotFixtureTests.WriteSkyLeftoverSnapshot`; `docs/screenshots/mobile-sky-leftovers.png` |
| **A fresh `/outputfile inventory` is not a no-op on the phone's Sky tab**, and an unchanged one wakes nobody — the row's held count and location ride the Quests section fingerprint, so the key moves exactly when the band's claim moves. The dump's TIMESTAMP deliberately does not (trap 8). The held-back note joins the same key, because the items it names are the one thing a new dump can change without changing a row | **Auto** — `CompanionQuestsTests.AFreshDumpIsNotANoOpOnTheQuestTab`, `AnIdenticalDumpWakesNobody`, `NothingInTheBandsMovesOnTheClock`, `TheHeldBackNoteAloneIsEnoughToRePush` |
| **Both widgets hand the dump to the quest request.** `CompanionQuestRequest` is init-only properties, so a lane that forgets `Inventory` compiles, serves the tab, and simply never draws the bands — full on Windows, empty on Linux, found by nobody without a phone and the right camp (#210's shape) | **Auto** — `CompanionQuestsTests.BothWidgetsHandTheDumpToTheQuestSurface` (source scan, both lanes) |
| **A shared-bank row is in the BANK, not worn** — `InventoryFile.Entry.InBank` owns the question, and the Gear tab asks it rather than spelling `StartsWith("Bank")` for itself. A `SharedBank1` row used to rank as the worn slot and label itself `worn · SharedBank1` | **Auto** — `GearLockerTests.ASharedBankRowIsLabelledAsTheBankAndNotAsWornGear`, `ASharedBankCopyDoesNotOutrankTheOneActuallyWorn`, `InBankKnowsBothSpellingsAndBothDepths` |
| **The Turn-ins section names its own source** (#241 PR 3, Bevel-signed 2026-08-27): one Status IconLine, not per item — reconciled with nothing logged since names the dump's age, reconciled with movement adds "plus loot since", and an item never dumped reads as a log tally that cannot see hand-ins. Both lanes; phone stays numbers-only, no sentence | **Auto** — `QuestPresentationTests` (`TurnInProvenanceText`) |
| **Owning a Sky reward's FINISHED item marks it turned in on the next inventory dump** (Hateborne, 2026-09-03: Ivory Mask in the bank, still "ready") — the checklist tracks ingredients and nothing ever consulted the reward itself, so a turn-in done before EQBuddy sat ready forever. Ownership is decisive regardless of ingredient state (the game's own criterion is "Obtain X"); bags and bank both count; "+N" tiers fold and "(Exaltation)" copies structurally cannot match; the mark goes through the same Core primitive a click uses, records the ledger completion once, and the import report on the Sky tab NAMES the rewards with an Undo beside it. A repeat dump is a no-op | **Auto** — `SkyRewardAutoCompleteTests` (incl. the real hateborne fixture pair: Cloak of Spiroc Feathers), `OutputfileAutoImportTests` (`AnInventoryDumpMarksOwnedSkyRewardsTurnedIn`, undo, repeat), `ImportReportReachesASurfaceTests` (`LastInventoryImport` reaches both lanes' Sky tab); **Shot** — `sky-leftovers` (report + bands in one frame, predictions written first) |
| **A ready reward for an already-unlocked class carries the caveat** — "{Class} already unlocked — turn in for the item only", worded ONCE in Core (`ReadyNote`/`ReadyDetail`) and rendered by both desktops and the phone's ★ Ready band; absent for a class nobody has unlocked, so the ordinary row is untouched | **Auto** — `SkyRewardAutoCompleteTests` (ReadyNote/ReadyDetail), `SurfaceParityTests.TheReadyBandAnnotatesAnAlreadyUnlockedClassOnThePhoneToo`, `QuestsRenderTests.TheReadyBandAnnotatesAnAlreadyUnlockedClass`; **Shot** — `sky-ready-unlocked` |
| **The three Sky bands fold to one line, session-only** (Hateborne, 2026-09-03) — chevron on the heading, collapsed keeps the Border and the heading-with-count and nothing else, one fold per band, and a fresh window opens expanded because the fold is a FIELD, never a setting (ProgressCardView precedent, Bevel/Helm 2026-08-23). `sky:folded` on `EQBUDDY_QUESTS` is the screenshot-only way to stage the collapsed state | **Auto** — `QuestsRenderTests` (`ClickingABandHeadingFoldsItToOneLineAndClickingAgainReopensIt`, `TheReadyBandFoldsTheSameWayAndAFreshWindowOpensExpanded`), `EndToEndTests` (default-open `questsSky*Open` facts); **Shot** — `sky-folded` |
| **The Sky tab names BOTH dumps that feed it** — the achievements ⧉ and an inventory ⧉ side by side in the tab body (the Unlocks tab's own shape); the header's "scan bags" is chrome, not the tab saying what feeds it | **Auto** — `QuestsRenderTests.TheSkyTabDrawsItsOwnInventoryPrompt`, `EndToEndTests` (`questsSkyCopyCmd=1`, `questsSkyInvCopyCmd=1`) |

## 4d. Settings, and who is allowed to write them

A save serialises the **whole** `AppSettings` from the snapshot loaded at startup, so any
second writer's changes are reverted wholesale. That is cheap and fine with one writer —
and it was silent with two, which is what made #169 so hard to place.

| Expectation | Held by |
|---|---|
| One EQBuddy per profile on **every** platform; a second launch surfaces the running copy rather than starting a twin | **Auto** — `SingleInstanceTests` (#169) |
| An isolated `EQBUDDY_APPDATA` profile still runs alongside a normal install | **Auto** — `SingleInstanceTests` |
| A stale lock that nobody answers never stops EQBuddy from launching | **Auto** — `SingleInstanceTests` |
| **The guard holds across BUILDS, not just within one**: the WPF widget and the Avalonia widget cannot both run on one profile, in either order. A guard implemented per toolkit — a named mutex on one side, a lock file on the other — guards nothing (2026-08-19) | **Manual** — launch both builds at one `EQBUDDY_APPDATA` and confirm the second exits 0 in silence |
| **Standing down is an EXIT, not a crash**: the Avalonia build claims the profile before Avalonia is built, because `Shutdown()` before the main loop threw an unhandled `InvalidOperationException` where the intent was to leave quietly | **Manual** — same launch, check the exit code and stderr |
| A save that is about to overwrite another writer's changes says so in `error.log` instead of reverting in silence | **Auto** — `SettingsClobberTests` (#169) |
| …and says it once per process, not once per save | **Auto** — `SettingsClobberTests` |
| The two hide-the-widget tick-boxes survive a real click, a reopened Options window, and a restart | **Auto** — `OptionsRenderTests` (#169) |

## 4c. Alert sounds

Which clip an alert plays, and at what volume, is decided in `UI.Shared/AlertSoundPlan.cs`
so it can be pinned without an audio device. Both UIs obey the same plan.

| Expectation | Held by |
|---|---|
| A rule's own sound beats the shared choice; a muted rule resolves to nothing; legacy SystemSounds names map onto the palette | **Auto** — `AlertSoundTests` |
| A built-in resolves to this platform's own file; "Off" plays nothing and reports nothing | **Auto** — `AlertSoundPlanTests` |
| **Every audible outcome carries the Options volume** — built-in, custom, and substitute alike. There is no route left that makes a noise the slider cannot reach | **Auto** — `AlertSoundPlanTests` (#153) |
| A custom file that has gone missing substitutes Ding *at the chosen volume* and names the missing file so the UI can say so — never a silent swap | **Auto** — `AlertSoundPlanTests` (#153) |
| A gap in the platform's own sound theme substitutes without blaming the player | **Auto** — `AlertSoundPlanTests` |
| A stored volume of NaN or out of range is clamped rather than handed to the player | **Auto** — `AlertSoundPlanTests` |
| That the plan is actually audible — the player, the device, the clip | **Manual** — §6 item 6 |

## 5. The gap — read this before trusting the suite

**`src/EQBuddy` (the WPF app, 22,029 lines across 74 files) has no automated coverage.
No test project references it.** (Size pinned by `DocumentationSizeTests` — it was still
claiming 14,432 across 37 on 2026-08-24, understating the untested surface by a third.)

**Partly closed 2026-08-14.** The *arithmetic* behind both reported bugs now lives in
`UI.Shared` and is unit-tested — `WidgetMetricsTests` (screen-to-pre-scale conversions,
#144) and `ChipStackAnchorTests` (grow-up anchoring, #122 and #152). Both suites were
verified by reintroducing the original bugs and watching them go red. The windows keep
only the wiring, which is the part a human still has to look at.

Everything below remains **Manual** and can only be caught by a human or a player:

- Window placement, screen guards, and the wiring of the transform to the controls
- Card expand/collapse, section order, star/pin behaviour, the mini pill
- Chip stacks — that the tested anchoring is actually wired to the window events
- Click-through, see-through, focus-hide, tray icon, hotkeys
- The zone map window: rendering, pan/zoom, context menus, camp pins
- Alert banners and sounds at the moment they fire

This is where the risk lives, and it is not theoretical. **Both player-reported bugs of
2026-08-14 — the clipped Epics card (#144) and the drifting chips (#152) — are in this
layer, and no existing test could have caught either.** Both were also unit-mismatch
bugs of the kind a small pure helper *could* pin.

### Recommended, in order of value per effort

1. ~~**Extract the arithmetic, then test it.**~~ **Done 2026-08-14** — see
   `UI.Shared/WidgetMetrics.cs` and `UI.Shared/ChipStackAnchor.cs`. Apply the same move
   to the next window bug rather than fixing it in place: if it is a sum, it belongs in
   `UI.Shared` where it can be pinned.
2. **A WPF render-test project** mirroring `EQBuddy.Avalonia.Tests`, which already
   proves the approach works on this codebase. Would cover placement, scaling and chip
   layout properly.
3. **Extend E2E** past its current 26 scenarios into what the widget *shows*, not just what
   it ingests. **Started 2026-08-14**: the `EQBUDDY_EXPAND` dump now carries `uiScale100`,
   `sectionCapScreen` and `sectionMaxH`, and two scenarios assert the conversion in the
   launched app at 1.6x and at 1.0x. The pattern generalises — **to cover a piece of
   window behaviour, dump the fact and assert it from E2E.** It is cheaper than it looks
   and it is the only route that sees real WPF layout.

   *Note on the road not taken:* a WPF unit-test project (recommendation 2) is harder
   than the Avalonia precedent suggests, because Avalonia ships a headless platform for
   exactly this and WPF does not. E2E already launches the real app; extending it beats
   building a second harness that would fight the desktop.

## 6. Manual pass

Run before a release that touches the widget, and after any change in §5.

Orient first with `pwsh -NoProfile -File scripts/status.ps1`, which lists anything
open and any thread waiting on a reply.

**Setup** (never against the real profile):
```bash
EQBUDDY_APPDATA=<scratch>   # isolated settings/history
EQBUDDY_EXPAND=1            # all sections expanded + a state dump
```
Fixture logs: see [FeatureGuide.md](FeatureGuide.md) §"Testing without playing".

1. **Scale** — drag the size grip from 80% to 160%. Every card stays reachable by
   scrolling at *every* scale; the bottom card is never clipped. (#144 — the cap's
   arithmetic and its wiring are now both automated; this pass is for what neither can
   see: whether it *looks* right.)
2. **Chips, grow-up** — enable grow-upwards, let a mez stack empty completely, mez
   again. The stack returns to the same spot. Repeat five times; it must not walk. (#152)
3. **Chips, grow-down** — same, unmoved.
4. **Multi-monitor** — drag the widget to a portrait secondary screen; height caps
   follow that monitor.
5. **Focus-hide / click-through / see-through** — each toggles cleanly and the tray icon
   always brings it back.
6. **Alerts** — a watch rule fires with banner, sound and colour, and the cooldown
   behaves per matched label. Then, with a **custom** .wav chosen in Options, compare
   the volume slider at 10% and at 100%: they must differ, and renaming that .wav must
   produce the "Alert sound file is missing" banner rather than a different noise (#153).
7. **Mobile** — pair a device; untick a surface on the PC and it says "Not shared";
   tick an Epic row on the device and the desktop card repaints; curate a spawn point
   and the PC map drops the circle within a tick; "New code" drops paired devices.
8. **Mobile layout** — one surface picked fills the screen on both a phone and a tablet;
   fullscreen shrinks the chrome; no sideways scroll at 375 px.
9. **Off switch** — set `CompanionEnabled=false`, restart, confirm nothing is listening.
10. **The readout over a fullscreen game** (Linux especially) — play fullscreen, tick
    Options → Behavior → "Show EQBuddy's own CPU & memory", and keep playing for a few
    minutes. Keyboard and mouse must both keep reaching the game, and the widget must not
    visibly change width as the numbers move. (#173 — the readout used to resize a real
    always-on-top window every three seconds.)
11. **Second launch** — with EQBuddy already running, launch it again. Exactly one widget
    exists afterwards, the running one comes to the front, and settings changed just
    before the second launch are still set. (#169 — off Windows there was no guard at
    all, and the loser of the race lost every setting it had changed.)

## 6b. The docs check themselves

| Expectation | Held by |
|---|---|
| Every file `CLAUDE.md`, `docs/Architecture.md` and this plan point at actually exists | **Auto** — `DocumentationTests` |
| Every test class cited in the Held-by column above exists | **Auto** — `DocumentationTests` |
| The ratchet table in `docs/Architecture.md` matches `ArchitectureTests`' baselines | **Auto** — `DocumentationTests` |
| `CLAUDE.md` links the other docs, so none is orphaned | **Auto** — `DocumentationTests` |
| **A What's-new entry that has already SHIPPED is never edited afterwards, and a release never reuses a tagged version's heading** - the entry is the record of what players were told, and twice in three releases a session wrote new work under an already-tagged heading, where it would never have reached any popup. No unit test can see it: the defect is a disagreement with a git tag | **Auto** - `scripts/whatsnew-guard.ps1`, first stage of `check.ps1`; `-Releasing` in `release.ps1` before anything is built or signed |
| **A Linux or macOS v1 copy is never offered EQBuddy v2, and is told once why** — v2 is Windows-only, and the offer is a one-way door: once a 2.x release is `latest`, every non-Windows install is being steered toward an installer that cannot run on it, and the update they were told to take is the thing that would have carried any later fix. Windows is unchanged in every case; a further 1.99.x LEGACY patch is still offered everywhere; the automatic six-hourly check says it once (`AppSettings.LegacyFinalNoticeAcknowledged`) and Help → Check for updates always answers, because silence there is a silent no-op. Charter LEGACY-002 · #275 P0-2 | **Auto** — `LegacyPlatformUpdatePolicyTests` (full matrix: 4 platforms × 1.x/2.x × manual/automatic × acknowledged/not), plus its source scanner holding all six call sites — WPF tick/menu/click and Avalonia tick/menu/click — to the one shared policy |
| **The notice's click lands on the FINAL LEGACY RELEASE, never `releases/latest`** — that page IS the v2 release page the moment v2 ships, and its most prominent asset is `EQBuddySetup.exe`, so a correct-looking notice ending there is LEGACY-002 arriving through the back door and would read as a working feature in every screenshot. `UpdateChecker.GitHubLegacyReleasePage` names a tag | **Auto** — `LegacyPlatformUpdatePolicyTests` asserts the negative for every non-Windows × major-2 case |
| **Nothing off Windows is ever staged, run or overwritten** (LEGACY-003) — `UpdateOffer.CanAutoInstall` has required `IsWindows` since #93; the guarantee is now asserted rather than implied, from a v2 feed as well as a v1 one | **Auto** — `UpdateOfferTests` |
| **The notice a non-Windows player is shown is PAINTED, by the build that will show it** — the policy deciding correctly and the widget rendering the decision are different claims, and only the second one is the feature (trap 42). Shown once on the automatic path, nothing on the next launch, and Help → Check for updates still answers after acknowledgement. Charter LEGACY-002 · #275 P0-4 | **Auto** — `LegacyNoticeRenderTests` (Avalonia headless, from a hand-built `UpdateInfo(2.0.0)` and the real policy for `Desktop.Linux`; the Avalonia lane is the one the notice is FOR, which is why this runs before E-2 deletes it). Proven able to fail by mutation: dropping `_updateBanner.IsVisible = true` fails all three, dropping the one-time `return` fails exactly the second |
| The wire fetch itself — `UpdateChecker.CheckGitHubAsync` reaching GitHub and GitHub's own `releases/latest` semantics | **Manual, and split in two since 2026-09-04.** The URL is not reachable from a test (a `private const`, deliberately: a mutable global in Core to cover one `GetStringAsync` is a worse trade). GitHub's *endpoint* behaviour is now observed read-only against a third-party repo whose newest release is a prerelease — `PowerShell/PowerShell` published `v7.7.0-preview.4` on 2026-09-01 and `releases/latest` still answers `v7.6.5` (2026-08-14). That settles the exclusion and **does not** prove our tag naming. The real-channel confirmation is a **release-time row on #275, due at channel-open** — Evolved develops local-only, so there is no channel to publish a prerelease to, and inventing one to tick a row is the tail wagging the dog (Helm, 2026-09-04 ~3:15 PM CT) |
| **`release.ps1 -Prerelease` publishes with `--prerelease`, and the updater still reads `/releases/latest`** — the GitHub endpoint's exclusion of prereleases is the only protection that reaches an ALREADY-INSTALLED v1 client, so both halves have to hold and they live in different files. The flag is conditional (an unconditional one would hide every ordinary release from the players it is for), refuses a run with no `-Tag` rather than no-op, and `ParseRelease` returning null for `v2.0.0-beta1` is the backstop. `-Prerelease` does **not** cover the OneDrive channel, which is checked separately. Charter RELEASE-002 · #275 P0-1 | **Auto** — `ReleasePrereleaseTests`; **Manual** — publishing one real prerelease and confirming `releases/latest` still answers the v1 tag. **Deferred to channel-open, not dropped**: local-only means there is no channel to publish it to. The endpoint half is observed against a third-party repo (row above); the our-tag half is a release-time row on #275 |
| **`LEGACY-V1.md` names all three non-Windows assets, and no link on it points at `releases/latest`** — that URL becomes the v2 release page the moment v2 ships and its most prominent asset is a Windows installer, so a legacy page pointing there would look correct in every screenshot and hand a Mac user something that cannot run. Prose about `releases/latest` is fine; link targets are what is checked. Charter LEGACY-006 · #275 P0-3 | **Auto** — `scripts/legacy-notice-guard.ps1`, second stage of `check.ps1`, and in `release.ps1` before anything is built |
| **A 2.x release carries a visible "Legacy Linux/macOS" section in the README and — on the FIRST 2.x release — in its own release notes, linking to a pinned final v1 release**. The in-app notice only reaches installs that took the bridge; anyone still on an older 1.x build finds us through the release page and the README, which makes those two the only surfaces that can reach them. Later 2.x patches are not asked to repeat it: a line written to satisfy a guard every release is a line players stop reading. Charter LEGACY-007 · #275 P0-3 | **Auto** — `scripts/legacy-notice-guard.ps1` (armed by `<Version>` reaching 2.x; proven to fail via `-AssumeVersion 2.0.0` / `-Repo`, since a guard that has never failed has not been shown to guard anything) |
| **Every `v1.x.y` that `LEGACY-V1.md` and the README's Legacy section name — in a link or in prose — is the NEWEST v1 tag in the repo.** The two checks above are satisfied by *any* pinned v1 tag, forever, so neither could see `v1.99.17` stop being the final one: a Mac user following the README would have downloaded the pre-bridge build, which carries no LEGACY-002 policy and chases v2 for the life of the install. Prose counts here because the stale claim was prose too (*"planned as `v1.99.18` … not published yet"*) beside links pinned a release behind. `v1.99.x` / `v1.x` are not tags and are ignored; with no tag list visible the check skips **loudly** rather than passing quietly. Charter LEGACY-001 · #275 P0-4 | **Auto** — `scripts/legacy-notice-guard.ps1` check 4, at every version; proven to fail by running it with `-Repo` against a worktree still pinned to `v1.99.17` (it names both surfaces there) |
| **A 2.x tree cannot be published AT ALL, and there is no switch that re-enables it.** `-Prerelease` closed the GitHub hole and release.ps1's own comment says the OneDrive copy is a SEPARATE channel a prerelease still reaches — so on a 2.x tree one ordinary `release.ps1` run would auto-update every family v1 install to a Windows-only Evolved build within six hours, with no tag, no release and no flag anywhere in the story. At major ≥ 2 the script throws unless `-EvolvedLocal`, before the 172 MB publish; `-EvolvedLocal` is subtractive only — it skips the OneDrive copy, refuses `-Tag` and `-Prerelease` on their own lines, does not `/SILENT`-install over this machine's v1 copy (one `AppId`, `{autopf}\EQBuddy`, so that install replaces v1 in place and inherits its profile), and **keeps every signing step unchanged**. Opening the channel is a deliberate future edit, the same posture as having no `-SkipSign`. Charter RELEASE-002 · #275 · FABLE E-1 | **Auto** — `scripts/evolved-channel-guard.ps1`, third stage of `check.ps1`, in `release.ps1` before anything is built, and in CI. Checks 1–2 read release.ps1's TEXT: everything that reaches somebody lives in one `if (-not $EvolvedLocal)` region, with the refusal positioned before it and before the publish. Proven to fail on the pre-change tree (11 problems naming lines 14, 96, 97, 98, 142, 153, 154). **Manual** — each refusal run once and confirmed to throw before signing is even resolved: no switch at line 31, `-EvolvedLocal -Tag` at 34, `-EvolvedLocal -Prerelease` at 35 |
| **The live update folder holds no `EQBuddySetup.exe` stamped 2.x.** Checks 1 and 2 prove a script; proving the producer is not proving the effect (trap 43). If a 2.x setup is sitting in that folder, every family widget is six hours from installing it and correct script text does not help. | **Auto** — `evolved-channel-guard.ps1` check 3, resolving the folder by `UpdateChecker.FindUpdateFolder`'s own rule and reading the literal path out of release.ps1 rather than hardcoding it. Reads `FileMajorPart` alongside UpdateChecker's own `Version.TryParse` of the string, because the first cut's string handling passed on a fixture written to make it fail. Fails **open with a loud SKIPPED line** where there is no OneDrive (CI, fresh clone). Proven to fail via `-AssumeUpdateFolder` at a folder holding a 2.x-stamped exe |
| **`install-local.ps1 -Evolved` builds and signs the 2.x tree and RUNS it portable — it never installs.** v1's install, shortcut and profile stay exactly as they were while Evolved is under construction; the Evolved profile is `%AppData%\EQBuddy Evolved` via `EQBUDDY_APPDATA`. It closes only the portable copy, filtered by path, since the publish overwrites that exe and closing the installed v1 widget would cost a session for a build that never touches it. Refuses in both directions (2.x without `-Evolved`; `-Evolved` on 1.x) | **Manual, and it is the acceptance step that counts** — run it, then LOOK: verified 2026-09-04 that `C:\Users\david\OneDrive\EQBuddyDownload\EQBuddySetup.exe` still stamps **1.99.18** with all three files unchanged in size and mtime, the installed v1 exe is still `1.99.18.0` with its mtime untouched, `%AppData%\EQBuddy\settings.json` was not rewritten, no installer was built, and **both widgets ran side by side** — portable `v2.0.0.0` and installed `v1.99.18.0`, each on its own profile and therefore its own `SingleInstance` lock. A green suite is not this claim: the defect class is "the script reported success and the side effect happened anyway" |
| The published GitHub release BODY actually rendering that section, and the legacy paragraph reaching a player's disk — `docs/FeatureGuide.md` is copied into the Linux tarball and the macOS bundle by `release-assets.yml`, which is the one document a legacy user has locally | **Manual** — open a published tarball and a published `.app` zip and read the "Updates" section in the copy inside them |
| **The final legacy tag `v1.99.18` and the `legacy-v1` branch cannot be deleted or force-moved, by anyone** — `LEGACY-V1.md` promises the assets, notes, tag and source state are retained permanently, and no test in this tree can enforce that: a `gh release delete` does not ask a test for permission. The backing is a repository rule on the tag (deletion + non-fast-forward) and branch protection on `legacy-v1` with `enforce_admins`. Neither locks the branch — a later 1.99.x LEGACY patch still comes off it. Charter LEGACY-004 / LEGACY-005 · #275 P0-4 | **Manual** — configuration is not proof; verified 2026-09-04 by *attempting* both deletions. The branch came back `GH006: Cannot delete this branch`; the tag rule was probed on a scratch tag temporarily added to the same rule (never on `v1.99.18` itself, since a failed probe there is the exact harm the rule exists to prevent) and came back `Cannot delete this tag`, after which the scratch tag and the extra pattern were removed |

This is the mechanism behind §7. Documentation rots when keeping it true is a habit;
it stops rotting when the build fails. It caught a wrong path in `CLAUDE.md` on its
first run. What it cannot check is prose being *misleading* while every path resolves —
that still needs a reader.

## 7. Keeping this true

- Behaviour change → update the row in the same commit.
- New regression test → move its row from Manual/Partial to **Auto** and name it.
- A bug that reached a player → add its expectation here, and its cause to `CLAUDE.md`'s
  trap list. Every entry in §5 that becomes Auto is permanent progress.
- Re-audit the counts and the ratchet table in [Architecture.md](Architecture.md) at
  each minor version.
