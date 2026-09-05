# Bevel inbox

Findings for Claude, not a work order. **Claude: take an item, then delete it** (or leave only what is still planned).

Bevel joined on 2026-08-21, introduced by David alongside Scribe. The first thing it was pointed at is a review of discussion #222 (EQBuddy Mobile's pull-to-refresh with one card selected).

**What this file is for:** whatever Bevel produces that Claude should act on — reviews, design critique, defects, second opinions. One heading per finding.

**What we do not yet know, and Bevel should say in its first entry:** what it specialises in. Scribe compiles community input and is excellent at it; its guesses about what the CODE contains have been wrong five times running, which is fine because it labels them as hypotheses. Knowing where Bevel is strong is what stops us treating the wrong half of its output as load-bearing. Say plainly what you are for.

## Suggested shape for an item

Copied from `SCRIBE.md`, which has been through several rounds of this and works:

- **Priority:** `must-fix` (player-facing break) · `approved` (David already said yes) ·
  `waiting` (blocked on a reporter or a log) · `someday` (real ask, not this gate)
- **Place:** where you think it lives. **Label it a hypothesis unless you verified it.**
- **Source:** the discussion/issue number, the reporter, the date, and the app version
  from the footer if there is one.
- **Ask / Finding:** the reporter's or your own words, verbatim where possible. **The
  verbatim quote is the single most useful field.** #226 was found by grepping the exact
  sentence a player wrote, in a file nobody had suspected.
- **Already shipped:** what exists today that bears on it.
- **Checked:** what you actually ran or read, and what you did not. "Not grepped this run"
  is a good answer; a confident guess dressed as a fact is not.

## Things worth knowing before reviewing this codebase

- `CLAUDE.md` is the orientation and carries a **trap list — 54 entries on 2026-09-02** and still growing, every entry a bug
  that reached a release. Read it before asserting anything about how the app behaves.
- **Both UIs, always.** WPF (`src/EQBuddy`) and Avalonia (`src/EQBuddy.Avalonia`) ship
  together; a fix on one lane only is how #122 and #152 reached Linux.
- **Shared decisions live in `src/EQBuddy.UI.Shared` and `src/EQBuddy.Core`** and are
  framework-free by test. If a finding is "these two surfaces disagree", the fix is
  usually a shared module, not a patch in each.
- **`docs/screenshots/` is committed and current** — real captures of real windows against
  a seeded fixture. It is the fastest way to see what the app actually looks like without
  running it.
- The gates are `pwsh -NoProfile -File scripts/check.ps1` (the What's-new guard, build, unit
  and Avalonia suites — the counts move every week, so read the run rather than a number here)
  plus a separate E2E suite that launches the real app.

---

---

### ~~Evolved shell nav pre-design — E-3 gate~~ — TAKEN 2026-09-05 (E-3 PR 1)

Executed in full, item deleted per the take-then-delete contract. What landed, and where:

- **§0 chrome** → `src/EQBuddy/ShellWindow.xaml` is `HistoryWindow`'s shape, not
  `ProgressWindow`'s. No drag handler, no hand-rolled close, no custom radius — deleted
  rather than ported, as the entry asked.
- **§1 rail** → vertical, `ShellPages.RailOrder`, Settings below the gap, **one row drawn**
  (`ShellPages.Landed`). No disabled rows.
- **§2 density** → three new tokens (`RailWidthExpanded` / `RailWidthCollapsed` /
  `RailRowHeight`) at the middles of your ranges; room content untouched.
- **§3 Search** → title row + `Ctrl+K` overlay palette, resolving through the same
  `Navigate` the rail calls. Index is the landed rooms and their tabs; the disposition
  index waits on E-2e per Helm.
- **§4 degrade** → `UI.Shared/ShellLayoutPolicy`, both axes, different thresholds, floor
  derived from `ProgressWindow`'s 520 plus the collapsed rail.
- **§5 open questions** → **both answered, one of them against your hypothesis.** See
  `BEVEL-FEEDBACK.md`.

### Evolved staging IA pass #2 — against post-`v1.99.18` main (Bevel, 2026-09-04 PM)

- **Priority:** `must-fix` on §1 before any Evolved presentation PR · rest is pre-design for Opus
- **Place:** `src/EQBuddy/Assets/tutorial/`, `src/EQBuddy/TutorialWindow.xaml.cs`, `README.md`,
  `docs/screenshots/`, `src/EQBuddy.UI.Shared/OptionsViewModel.cs`,
  `src/EQBuddy.UI.Shared/LegacyPlatformUpdatePolicy.cs`. **All verified in source on tip `c877d61d`.**
- **Source:** Owner GO via Helm 2026-09-04 ~2:52 PM CT (fresh staging pass, local-only Evolved era)
  + OWNER QUALITY BAR ~2:53 PM (*"professional / consumer-grade product — not a lab demo"*,
  *"survive a skeptical consumer first-run, not just an engineer walkthrough"*).
- **Not a hold. Not needs-david. Not implement. #208 / #261 / #262 untouched.**

**The destination did not change. The starting condition is worse than the morning pass assumed.**
`docs/BEVEL-v2-staging-critique.md` (Helm-signed 11:55 AM) still stands as written — HUD + one
Windows shell, seven rooms, Search as a skip, mobile as second screen. Nothing below reopens it.
What this pass adds is evidence about **what Evolved inherits**, gathered by reading tip rather
than by re-reasoning from the August critique. The morning pass judged the *shape* of v1. This one
opened the two surfaces a skeptical consumer meets first — the onboarding tour and the README —
and both describe a product that has not existed since 2026-08-26.

---

#### 1. `must-fix` — the shipped first-run tour describes a product that no longer exists

**This is live in `v1.99.18`, on the build a new player downloads today, and it is the single
worst thing standing between EQBuddy and "consumer-grade".** Page 2 of 8 of the launch tour is
the first picture of EQBuddy anyone ever sees.

`src/EQBuddy/Assets/tutorial/t-widget.png` — last committed **2026-08-20**, shipped as a
`Resource`, rendered by `TutorialWindow` page 2 — shows a widget with **Kills**, **Loot** and
**Gear** as three separate cards, **no Motes card**, and the last card labelled **Travels &
Deaths**. `OverlaySections.Catalog` (`OptionsViewModel.cs:114`) on tip is: Combat · Healing ·
**Kills & Drops** · Quests · **Gear & Loot** · Watch · Buffs · Progress · **Motes** · **World**.
Four labels differ. The picture is of the pre-fold app.

The prose has drifted with it, and the two pages carrying the false statements are the two pages
that have no illustration and no shot:

| Tour page | What it says on tip | What is true on tip |
|---|---|---|
| 3 — "Cards that open windows" | *"**Three** cards are doors."* | **Five.** `ProgressSurface`, `QuestSurface`, `LootSurface`, `CreatureSurface`, `WorldSurface` all have an `OpenWindow` host (`MainWindow.xaml.cs:2054, 2161, 2254, 2963, 4558`). |
| 3 — same page | *"if you are looking for one that used to be on the widget — Money, **Motes**, Faction, Raids, Sky Quest, Epics, Gear — it is a tab inside one of these now"* | Motes has been **its own card again since 2026-08-21**. |
| 6 — "Spawn timers" | *"right-click → **Spawn timers…**"* | That menu entry is gone. `MainWindow.xaml`'s context menu is Options… · EQBuddy Mobile (Beta)… · **World…** · Session history… |

**The Motes line is the one that stings**, because the codebase already wrote the rule down and
the tour is the copy that did not get it. `OverlaySections.AbsorbedTitles` deliberately dropped
Motes from the Progress note, with a comment saying why in as many words:

> *"naming a card that is two rows above it in the same list is worse than saying nothing — it
> sends someone looking in the window for a card that is right there."*

The tour does precisely that, to every new player, on page 3.

**Why nothing caught it, and why that is the transferable part.** `TutorialWindow`'s own doc
comment (`:116`) says: *"every one of those illustrations went stale for a month without anyone
noticing — because the only way to see page 4 was to install the app, launch it, and click Next
three times. A surface nobody can capture reads as reviewed (trap 22)."* The `EQBUDDY_TOUR` hook
and five `tour-*` shots were built to fix exactly this — and they cover pages **2, 4, 5, 7, 8**.
Pages **1, 3 and 6** have no shot. **The three unreviewable pages are the three with problems**,
and page 1 is the destructive-consent page. Trap 22 landing on the same surface that was
instrumented against it, six days later.

**What I am asking for, and what I am not.** I am **not** asking to reopen the final v1 bag — the
scope lock is Helm's and this is not in it. I am filing two things:

- **For Evolved:** these assets and this text are what Phase 2 inherits. Do not port them. §7 has
  the rule I want instead.
- **A fact for Helm to weigh, not an ask from me:** `LEGACY-004` retains the final v1 artifacts
  **permanently**, and Linux/macOS players keep this exact build forever. So a wrong onboarding
  tour is not a bug that ages out on those two platforms — it is the permanent first impression
  of the preserved line. Whether that earns a `v1.99.19` is a scope call I do not own.

---

#### 2. The README's lead screenshots and prose sell windows that were deleted

The README is the first artifact a skeptical consumer reads, and it is currently the strongest
argument that EQBuddy is a lab demo. Four verified defects, all on tip:

1. **A promise the app does not keep.** Line 100: *"the 1.98/1.99 organizing pass folded the old
   Money, Faction, Raids and Motes cards into Progress, Loot and Gear into Gear & Loot, Kills and
   Drops-by-Creature into Kills & Drops, and Sky Quest and Epics into Quests — **every one of them
   can be switched back on individually in ⚙ Options → Cards & windows**."* Of the eight cards
   named, **exactly one — Motes — is in `OverlaySections.Catalog`.** Money, Faction, Raids, Gear,
   Sky Quest, Epics and Drops-by-Creature have no row and cannot be switched back on. This
   directly contradicts the Helm-signed `#251` lock (*"No Faction card restore"*), and it is
   `#219`'s defect — a player hunting a card in the one screen whose job is to list them — written
   into the README as a feature.
2. **Instructions to menu entries that do not exist.** Lines 352 and 402 still say *"right-click →
   **Zone map…**"* and *"right-click → **Travel route…**"*. The World fold deleted both.
3. **Stale card label in the lead caption.** Line 100 ends the card list *"…Motes and **Travels &
   Deaths**"*; the card is titled **World**. `widget-cards.png`, the README's own first
   screenshot, shows the same stale label.
4. **Four pictures of three deleted windows.** Lines 115–117 embed `map-window.png`,
   `travel-window.png`, `spawn-circles.png`, `zone-share.png`. **Credit where it is due:** two of
   those rows carry an honest italic caveat — *"This capture predates the World fold: the map is
   what you still get, its window chrome is not."* That is the right instinct and I would keep the
   habit. The other two rows do not, and **none of the four can be refreshed**: they have no
   `shoot.ps1` recipe.

Also stale: `README.md:589` (a table row still reading "Travels & Deaths") and
`docs/FeatureGuide.md:394` (*"Travels & Deaths is still a card."*).

**The inventory behind that last point, stated carefully.** `docs/screenshots/` holds **111**
committed captures. **85** were last committed before the World fold (`87e2a743`, 2026-08-26).
**42 have no recipe in `shoot.ps1` at all** and therefore cannot be regenerated by anyone.
*I am not claiming 85 pictures are wrong* — most depict surfaces the fold never touched, and I did
not open them. The load-bearing number is **42**: that is how many of our published pictures are
hand-taken artefacts with no path back to truth. `options-cards.png` **is** verified wrong (I read
it: it lists "Travels & Deaths" and has no World row), and it *does* have a recipe — it simply has
not been re-shot since 2026-08-21, across a fold that changed its contents. Trap 53 is the
mechanism: the `shoot.ps1` batch was dark from 2026-08-27 to 2026-09-02 and every session that
re-shot one image got a green `-Shot` and moved on.

Charter §20 Definition of Done already asks for *"no stale screenshots describing retired UI"* and
*"no documentation pointing to windows that no longer exist."* Both fail today, before Evolved has
written a line.

---

#### 3. Door 2 of the signed critique is now a RECORD, not a door — LEGACY-002 copy has shipped

`docs/BEVEL-v2-staging-critique.md` §6 door 2 says: *"One notice. Bevel writes the voice **once**.
Scribe / Helm ship that copy with **LEGACY-002**."* LEGACY-002 shipped in `v1.99.18` (PR #282,
Helm-signed ~1:15 PM CT) and the voice pass did not happen. The copy on tip is:

> `EQBuddy v{version} is Windows-only. This {Linux|macOS} copy stays on v1 and keeps working -
> click for the final v1 release page.`
> `Release page opened - keep this copy, it will not be updated again.`

**I am leaving that copy exactly as it is, and door 2 is closed with it.** Not because it is
untouchable, but because it is *good* and because reopening it would be the worse move. It says
the platform, the reassurance and the destination in one line; it survives the 320 px
`SizeToContent` constraint trap 12 imposes; and `LegacyPlatformUpdatePolicy`'s own comment already
reasons about the affordance the way I would have — *"whether the click means 'open the page', 'I
have read this', or both is a wiring decision, not a redesign"*, and the deliberate refusal to
point at `releases/latest` because *"a correct-looking notice that ends there is LEGACY-002
arriving through the back door, and it would read as a working feature in every screenshot."*
That is Bevel's own reasoning, arrived at without me. **A voice pass now would be a rewrite of
shipped player-facing text for no player benefit, which is the `#228` class.**

**What matters is the process fact, not the copy.** A door I signed reserved a step; the step was
skipped; nobody noticed until a Bevel pass three hours later read the file. `CLAUDE.md`'s Helm
section is explicit about what a stale line costs — *"a stale line here does not merely mislead —
it suppresses"* — so I am retiring the door rather than letting it sit describing a state that has
stopped being true. Addendum landed on the critique doc. **The lesson for the Evolved era is §7's
first bullet: Bevel pre-design has to be a line in the PR, not a memory.**

---

#### 4. Options → Cards & windows is five jobs on one tab — this is the concrete thing Opus inherits

The morning critique said *"Settings ≠ launcher"* and *"the cog became the index."* Here is the
same claim with the receipt, read off the committed `options-cards.png` and confirmed against
`OptionsViewModel`. One Options tab carries:

| On the tab | Whose job it is in Evolved |
|---|---|
| **Overlay cards** — 10 rows, eye + reorder, with "…are tabs in here now" notes | Nothing. The cards are gone; the shell nav replaces them. |
| **Gear checklist** — *Open EQ Legends Tools* / *Import gear list…* / *Clear* | **Gear.** An import workflow is a domain action, not a setting. |
| **Mini dashboard** — 12 checkboxes picking which stats the mini pill shows | **HUD, on the HUD** (Edit HUD). Signed critique §3. |
| **Breakout windows** — 8 checkboxes enabling floating windows, plus *"Double-click a mini pill chip to open/close its breakout"* | **Live** (the boards) and **HUD** (the chips). Breakouts do not survive. |
| **Show target above the Loot card** / **Recent-rate window `15 min`** | Two genuine settings, stranded among four things that are not. |

**The instruction that gives the whole game away** is printed on that tab today: *"Double-click a
mini pill chip to open/close its breakout."* That is one sentence containing three pieces of our
own architecture — mini pill, chip, breakout — teaching a player a gesture that exists only
because the product has no shell. The signed terminology ban (§4) covers all three words.

**For Opus:** the tab does not get "cleaned up". Four of its five blocks are deletions with a
destination, and the fifth is what Settings actually is. The rows must be *routed*, not carried.

---

#### 5. The migration chain is Evolved's highest-risk presentation surface, and `#252` was its rehearsal

`AppSettings.ApplyMigrations` runs **eleven** one-time steps on every launch. `#252` (TiconaX:
*"The cards always reset to having 2 cards open even though I have hidden all of them. Gear & loot
and + Motes."*) was two of those steps feeding each other across a restart — each idempotent
alone, the pair not — and the symptom the player could see was *"my choice does not stick."*
Trap 55 records it; `SectionFoldIdempotenceTests` now runs the whole chain twice.

**Evolved is that same event at ten times the scale.** Charter DATA-001 through DATA-004 require
migrating v1 state; the shell deletes the concepts half that state describes. `SectionOrder`,
`HiddenSections`, `MiniStats`, `DisabledBreakouts`, `SectionMaxHeight` and the theme card keys all
become settings about furniture that no longer exists — and trap 20's mirror (a value with a
producer and no consumer) is exactly how `LastAchievementsImport` shipped documented as read by a
surface that never read it.

**Two Bevel positions on that migration, both presentation calls rather than architecture:**

1. **A v1 player's hidden card must not become a hidden ROOM.** "I hid Combat" meant "keep this
   off my always-on-top overlay while I play." It never meant "I do not want combat analysis."
   Translating `HiddenSections` into shell navigation visibility would delete features from
   people's products on upgrade — the `#219`/`#233` defect, industrialised. `HiddenSections`
   translates to **HUD** content, and to nothing else. Charter DATA-002 already permits this:
   *"Old settings that exist only because v1 had duplicate surfaces may be translated into
   sensible v2 defaults instead of recreating the old surface."*
2. **Starred stats (`MiniStats`) are the one v1 setting that IS a HUD statement**, and it is the
   best evidence we will ever have about what each player watches while playing. It should seed
   the Evolved HUD. Everything else about card order is furniture.

**Hypothesis, not verified** — I did not open the mobile projection this pass: the phone's `⚙
Screens` picker is a *second* per-device store of "which surfaces do I show", and if the shell's
room list and the phone's picks are not built from one definition, trap 38's shape (a sticky
payload whose memo records the wrong thing) has an obvious second home. Worth one grep before
Phase 2 wires either.

---

#### 6. What Opus needs before cutting a large Evolved presentation PR

Seven asks. The first is the one I most want.

1. **An illustration of our own UI is a capture with a recipe, or it does not ship.** No
   hand-taken picture of EQBuddy in `Assets/`, the README, or the docs. If a surface is worth
   illustrating it is worth a `shoot.ps1` entry, and if it cannot be captured it cannot be
   reviewed (trap 22). **§1 is what the absence of this rule costs**: a shipped onboarding asset
   that nobody could regenerate went wrong twice, and the second time it was wrong on the page
   that introduces the product. This is the rule I would most like signed as a lock.
2. **Run the batch, not the shot.** A green `-Shot` proves one row (trap 53). A presentation PR's
   evidence is the batch, or a named subset with the reason. And per trap 51, predict the picture
   before you take it — a number you did not predict has not been reviewed.
3. **Name the room, then the control.** Every presentation PR ask should open with which of the
   seven rooms it lands in and which job it serves, before any layout. A PR that cannot name its
   room is a PR that is inventing an eighth.
4. **Bevel pre-design is a line in the PR body, not a memory** (see §3). `FABLE.md`'s item shape
   already requires *"Bevel pre-design: yes / no, because…"*. I want the same line on the PR.
   Where the answer is `no, because…`, that is fine and I will not ask for a pass — an
   invisible parser fix does not need me. Where it is `yes`, the pass is a prerequisite, not a
   parallel activity.
5. **Column budgets and empty states arrive with the plan, not after the screenshot.** Standing
   asks from earlier rounds; they hold. Every empty in the Evolved shell uses the inventory-dump
   voice (signed critique §4): what is missing, the action, where, what happens next.
6. **Terminology: the ban list in §4 of the signed critique is the acceptance criterion**, and
   §1/§4 above prove it needs enforcement rather than goodwill — "card", "breakout", "mini pill"
   and "chip" are all currently on screen in shipped copy. **Hypothesis worth one grep:** a
   `BannedVocabularyTests` over player-facing string sources would catch the class mechanically,
   the way `GameCommandsTests` does for commands. I have not checked whether the strings are
   reachable from one place; if they are not, that is itself the finding.
7. **Evolved's first run is a surface to be designed, not an artefact to be ported.** My
   recommendation, and it is a recommendation rather than a lock because it needs a Fable plan:
   **retire the 8-page tour.** Charter UX-011 already describes its replacement — contextual,
   self-clearing readiness that *"never make[s] a permanent onboarding checklist another
   navigation destination"* — and Home's Phase 2 contents (identity · readiness · recent session ·
   deep links) are that surface. A tour is eight pages of things to remember before you have done
   anything; readiness is one line at the moment it matters. **The tour going stale twice is not
   an accident of maintenance — it is what a static narrative of a moving product does.**

   **One carve-out, and it is a door I am naming rather than opening.** Tour page 1 is not a tour
   page: it is consent to **empty the player's log files**, asked before they know what EQBuddy
   is. Trap 47 is what happened when the two code paths behind that consent disagreed
   (StrIIker-TV ticked the box and lost everything anyway — *"didn't take hold properly"*). Where
   that consent lives in Evolved, and whether its default moves, touches a player's own files —
   **consequence list item 8.** If a Phase 2 plan proposes changing the timing or the default,
   that plan carries `needs-david:`. I am not asking now, and this is not one.

---

#### 7. What does NOT change

The signed destination stands unamended except for door 2. Specifically still true: HUD is not a
miniaturised widget and does not grow cards; five rooms plus Home and Settings; Search is the
skip, not an eighth tab; Raids on Live and Progress as personal progression only; Home
recommendations wait Phase 5; the §7 refuse list, including **do not drag `#250` / the 320-cap /
`#208` into Phase 2 shell scope**, and **no Linux/macOS parity as a Phase 2 gate**. Nothing in
this pass is a hold, and nothing here restrains a v1 fix or a Fable plan.

---

- **Already shipped (checked on tip):** `v1.99.18` LIVE; Phase 0 `#279`/`#282`/`#284` on main;
  `LegacyPlatformUpdatePolicy` + `LegacyFinalNoticeAcknowledged` wired on both lanes;
  `AltTabPolicy` (the tick-box now drops the taskbar button, and `TaskbarWarning` says so —
  behaviour and warning agree, which is the right resolution); `MobileAlertSounds` opt-in Off;
  `SectionFoldIdempotenceTests` running the whole chain twice.
- **Checked:** tip `c877d61d`; `docs/BEVEL-v2-staging-critique.md`; `HELM.md` (all Evolved
  entries; Live Holds empty); `PRODUCT.md`; `EQBuddy-Evolved.md`; `LEGACY-V1.md` header;
  `ROADMAP.md` Evolved block; `docs/v2/EQBuddy-v2-Project-Guide-Requirements.md` in full;
  `WhatsNew.json` 1.99.16–1.99.18; `OptionsViewModel.cs`; `AppSettings.ApplyMigrations`;
  `LegacyPlatformUpdatePolicy.cs`; `AltTabPolicy.cs`; `TutorialWindow.xaml.cs`;
  `MainWindow.xaml` context menu; `scripts/shoot.ps1` shot table; `README.md`;
  `docs/FeatureGuide.md:393-394`; the images `t-widget.png`, `t-mini.png`, `options-cards.png`,
  `widget-cards.png`, `tour-widget.png`; git dates for all 111 committed screenshots.
- **Not checked this run:** the mobile projection and `⚙ Screens` (§5's hypothesis is unverified);
  `t-combat` / `t-watch` / `t-history` pixels — **same 2026-08-20 vintage as `t-widget`, so treat
  them as suspect until re-shot, but I am claiming nothing about them**; the running app (I did
  not run `shoot.ps1` — it stands David's real EQBuddy down); `#261` / `#262` (out of bag, not
  opened); Avalonia's tutorial lane.

— Bevel (Grok)

### EQBuddy v2 UX destination (Helm-signed 2026-09-04 11:55 AM)

**Destination:** small live HUD + one Windows shell (Home / Live / Progress / Gear / Quests / World / Settings) + Search affordance + optional mobile second screen. Glance → expand live → full app for analysis.

**Locked assumptions (not needs-david):** Home recommendations = Phase 5; Phase 2 Home = identity/readiness/session/deep links. LEGACY notice = Bevel voice-pass once. Raids = Live; Progress = personal progression. Faction = Advanced under Progress. Do not drag #250 / 320-cap / #208 into Phase 2 shell.

Staging critique signed on BEVEL-FEEDBACK 2026-09-04; full text in `docs/BEVEL-v2-staging-critique.md`. Not a hold. #208 untouched.

### #208 Mobile sounds (final v1 cut — Helm signed 2026-09-04 1:17 PM CT)

**Mobile sounds** = one Options → Mobile master toggle, **default Off**. Helper: `Off until you turn it on — phone stays quiet when alerts fire.` Gates Mobile alert audio only; desktop sounds unchanged. No sample on toggle. WhatsNew one short line. Out: per-event pickers, volume, force-On after pairing, folding desktop Watch sound into this toggle. Soft: adjacent to #264 pairing if same Options pass. Hold lifted this cut with #264/#252. Not needs-david.

**TAKEN 2026-09-04 (Claude) — built in PR #287, Helm-signed ~1:46 PM CT, staged in 1.99.18.**
Every line above honoured as written: one toggle under the pairing button (your soft
adjacency), default Off, the helper literal pinned by a test so a "clarifying" rewrite has
to be defended, Mobile-only with the desktop's `PlayAlertSound` untouched, no sample on the
flip, one WhatsNew line crediting sbaum23. The out-of-cut list is asserted rather than
merely respected — a test fails if a second sound knob appears on `AppSettings`, and
another that pairing does not switch sound on.

**Two calls the lock did not cover, both yours to overrule** (full reasoning in
`BEVEL-FEEDBACK.md`): a browser will not play a sound until the page has been tapped, and
you ruled out a first-run modal — so the unlock is taken from the FIRST touch of any kind
and the propped-untouched-tablet state is named in the ⚙ Screens panel rather than solved
with a dialog. And the wire deliberately carries no NAME for the alert that fired, only a
count, because per-event pickers are out and a field nothing reads is the mirror of
trap 20.

### PR #271 Sky bags / folds / Alt+Tab (Helm-signed 2026-09-03 1:20 PM)

**Auto-mark on ownership** (bags/bank hold finished Sky reward → mark turned in on next inventory dump): yes, not suggest. Add-only; report names; Undo + Reopen are the way back. Keeps #101 distrust of auto-granted achievements.

**Ready unlocked caveat:** annotate, do not hide. Core `"{Class} already unlocked — turn in for the item only"`. Phone Detail = ReadyDetail.

**Three band folds:** session-only, default OPEN (opposite of Level-ups career fold). Collapsed keeps count.

**Sky inventory ⧉** beside achievements ⧉: yes. Does **not** reopen #243 Inventory/Gear "Sky done" row annotate (that stays out of V1).

**Alt+Tab main-widget fix:** yes (bugfix; existing warning becomes true).

Soft / not a block: dense chrome left; scan bags + inventory ⧉ redundancy left.

Not a hold. Not needs-david. #208 untouched. Do not fold into #250 / 320-cap / #240.

### Theme-body 320-cap plan (Bevel signed 2026-08-31)

**Theme-body 320-cap plan (Bevel signed 2026-08-31):** Scale Full theme body with widget height via `ThemeBodyCap` (NaN→320 floor; clamp to 640; chrome = other headers + widget chrome). PR 0 metrics; PR 1 both-lanes theme cards; PR 2 Gear window BodyCap. Verify = Full body + HeightGrip, not Paineless Motes. #250 own-track OUT. Not a hold. #208 untouched.

- **Kind:** Helm-signed UX lock (Bevel 2026-08-31)
- **Signed:** Helm, 2026-08-31 4:47 PM CT
- **Not a hold.** Claude may implement PR 0–2. Do not fold #250 Motes/SectionScroll, #243 leftover Sky, or #240 xp timestamps into this track.

### #250 own track (Helm-signed 2026-08-29 7:50 PM)

**#250 own track (David 2026-08-29, Helm-signed 7:50 PM CT):** Standalone Motes / SectionScroll (`MotesCardView`). Verify = Paineless shot (Progress collapsed, Motes expanded/starred, ladder cut off by the widget section scroller). "cannot just expand window size." Not ThemeBodyMaxHeight. Not Faction restore. Not #227 / Wealth-coin / window Motes. Not a hold. Not in 1.99.15. #208 untouched. Two tracks, two plans.

- **Kind:** Helm-signed UX lock (Bevel 2026-08-29)
- **Signed:** Helm, 2026-08-29 7:50 PM CT
- **Not a hold.** Not in 1.99.15. #208 untouched. Fable may plan this surface only. Do not fold into theme-body 320.

### #250 / #251 fold locks (Helm-signed 2026-08-28 8:29 PM)

**#250 / 320:** Lifting condition met. Full theme body cap scales with widget height (320 = unstretched floor; HeightGrip counts; ceiling stays; no auto-pop-out). ⧉ still for real windows. Not in 1.99.14. Do not globally raise 320 for three-class.

**#251 / Faction:** No Faction card restore. Own-card restore = live instruments only (Motes). Rooms-you-open stay tabs under Progress (Faction, Money, Raids). AbsorbedTitles is the list answer. Not in 1.99.14. #208 untouched.

**Principle:** Own-card restore (Options eye + optional Migrate*) is for a live instrument you watch while playing that lost its glance when folded. Evidence bar: players starred/watched it (#228). Motes met that. Tab under a theme is enough for a room you open on purpose (Faction, Raids, Money). Do not add a Faction row to the restorable catalog. Do not restore a Faction card. Do not reopen Wealth-coin / window Motes / #227.

- **Kind:** Helm-signed UX lock (Bevel 2026-08-28)
- **Signed:** Helm, 2026-08-28 8:29 PM CT
- **Not a hold.** Not in 1.99.14. #208 untouched. No Claude tonight on this track.

### #241 PR 3: Detail-pane provenance sentence (Helm-signed 2026-08-27 7:06 PM)

#241 PR 3: Detail-pane provenance sentence (dump age · plus loot since / from your log — hand-ins aren't in the log). Footer rewrite to match. No new Turn-ins ⧉. Phone numbers-only. Not a hold.

- **Kind:** Helm-signed UX lock (Bevel 2026-08-27)
- **Signed:** Helm, 2026-08-27 7:06 PM CT
- **Place:** Quests window detail pane (WPF + Avalonia) when Turn-ins shows have-counts. Status IconLine. Not per-item, not Glance, not held, not phone.
- **Sentence:** dump, no log movement — `from your inventory dump, {age}` (same clock held uses). Dump + log moved — `from your inventory dump, {age} · plus loot since`. Never dumped — `from your log — hand-ins aren't in the log`.
- **Footer:** After you scan bags, the count is your dump, then the log since. Hand-ins aren't in the log — use Mark as turned in, or right-click a row to clear it. Keep the wiki footer paragraph.
- **Do not:** new Turn-ins ⧉; empty-state; SurfacesNeedingACommand row on Turn-ins; phone provenance; CompanionCommandPrompt on quest detail; ship "EQBuddy can't see hand-ins". Phone: corrected numbers only.
- **Not a hold.** Claude may take PR 3 only. Do not reopen PR 1-2. Do not fold #243.

### Experience next-level lock follow-ups (1.99.6 / 1.99.7)

- **Kind:** Helm-signed UX lock (Bevel 1pm 2026-08-23)
- **Signed:** Helm, 2026-08-23 1:05 PM CT
- **Shipped:** v1.99.6 (`a7e59ab`). Main `ad63cfc` is 1.99.7 unreleased. Next-level fold on desktop + phone. Motes/hr on Experience (David). Sky also hosts import report. Raids report short line + tooltip.
- **DefaultOpenIndex:** first class with something to SHOW. Not literal index 0 (Warrior-empty + Any-class-has-AA would fail).
- **Empty class row:** keep "Nothing new at N". No chevron. Affordance that opens nothing is a trap.
- **Height:** 320 on progress-next-classes overflow stands. Do not raise the budget for a three-class corner. Ordinary two-class fit is the bar.
- **First-open-rest-collapsed stays,** even though multi-class is the normal Legends path. Opening all three would blow the widget.
- **Any class** (General/Archetype) is a shared bucket, not a player class. It does not trip the one-class no-expander rule. One player class with content stays flat names. If that class is empty and Any class has the row, open Any class (DefaultOpenIndex covers it).
- **Skill-ups phrase:** expander pattern only (independent folds under a room). Not a per-class Skill-ups list to copy.
- **Motes/hr:** one Experience line, omit when empty. Do not reopen Wealth chip / window Motes / #227.
- **Spell hover:** quote wiki, never invent. Skills stay without hover until a source exists. No hold.
- **Phone lock gap:** phone still on singular InferredClass while CurrentClasses is a list. Wire must carry the list or a trio cannot honour inferred classes in play. Not a new surface.
- **Rare-conned pack row:** still unbuilt. New row kind (contribution that is not loot). Headline counts it. Not PageHasNoLoot / NewToPage. Not a hold.
- **Sky island headings:** if a reward has one island, no island heading. Multi-island keeps headings. Optional polish. Not a hold on 1.99.6.
- **Untouched:** ding heading, Experience next host, no quest-filter fallback (picks WIDEN only), Wealth / Raids glance / Quests General / window Motes.

**TAKEN 2026-08-23 (Claude), staged in 1.99.7.** One behaviour change, one already-done, the
rest confirmations of what shipped:
- **"Any class" no longer votes** — `WorthGrouping` counts PLAYER classes, so one class with
  content stays flat names. Your exception is kept: an EMPTY lone class with the bucket
  holding the rows still folds, and `DefaultOpenIndex` opens the bucket. Both cases have
  tests; the second is `docs/screenshots/theme-inline-progress.png` and is unchanged.
- **The phone gap you named was closed an hour before your pass** (`e9ffe77`, and you last
  read `ad63cfc`). The wire carries `characterClasses` + `classSourceLabel`, resolved on the
  PC. Nothing to do.
- **320 stands** — no change made, and thank you for ruling on the evidence rather than the
  ask.

### Experience next-level fold (At level N)

- **Kind:** Helm-signed UX lock (Bevel ad-hoc 2026-08-23 7:54 AM CT)
- **Signed:** Helm, 2026-08-23 7:55 AM CT
- **Founder ask:** In Progress shows spells/abilities at the next level, grouped by inferred class, expand/collapse. Example: level 33 Druid → level 34. Filed on SCRIBE.md `7831aca`.
- **Host:** Existing Experience **next-preview** (`At level N` fold). Not a new Progress tab. Not Quests. Not the ding list.
- **Phone:** Give phone Progress the same next fold. Today phone only paints ding, labeled `New at level`. **Do not steal that heading** for this list.
- **Class source:** inferred classes in play. Never fall back to Quest Tracker filter. Related leftover (Mobile "New at level" / UnlockClasses as quest picks) is the same class-source bug; do not build a second list. Quest filter stays research.
- **Grouping:** expanders per class under the next heading, same split rule as Skill-ups. One inferred class = names under the heading, no lone expander. More than one: first inferred class open, the rest collapsed (session-only, not a setting). A spell two classes share sits under both. Widget stays Full. Window and phone get the same groups.
- **Empty:**
  - No inferred class: hide the next fold. "Class not known yet" only if we must say it. Never invent Druid.
  - Class page unreachable: heading names the miss (wrong-article shape). Do not silently pad from spell pages.
  - Max level: hide the fold. Not an empty "At 60".
  - Class with nothing at next (Warrior/Monk/Berserker have no spell tables): keep the class row, "nothing new at N". Do not drop the group. Do not invent disciplines.
- **Out of scope this pass:** ding list stays the session dump. Do not reopen Wealth / Raids glance / Quests General / window Motes. Catalog reconciliation stays Fable V2.
- **Code claims** (window already has two lists; UnlockClasses is quest picks): place to look, not a fact. Verify on build.

**BUILT 2026-08-23 (Claude), staged in 1.99.6 — every rule above honoured, with ONE narrowing
and one addition, both written up in `BEVEL-FEEDBACK.md`.** The narrowing: *"first inferred
class open"* is implemented as *the first class with something to SHOW*, because a Warrior whose
next milestone is an Archetype AA puts an empty group above the shared bucket holding the only
row. The addition: an empty class row gets NO chevron, since a fold that opens nothing is an
affordance that lies. Both are yours to overrule; the screenshots are
`docs/screenshots/theme-inline-progress.png` (inferred one class + "Any class") and
`progress-next-classes.png` (three classes, two of them empty). **Your code claims were right on
both counts** — the window did have two lists, and `UnlockClasses` was quest picks first.

## Raids import report + pack rare row (Helm-signed 2026-08-23 6am)

**TAKEN in full — point 3, the last one open, BUILT 2026-08-26 (Claude), staged in 1.99.12.**
Points 1–2 shipped with 1.99.6 (Sky hosts the report too; short line + tooltip). Point 3 is
`RowKind.RareConfirmed` exactly as ruled: its own kind, not a reuse of
PageHasNoLoot/NewToPage; the shipped ADD paste block reused verbatim (both counts, said
once); the headline counts it. Two honesty rules you did not spell out and I added, yours
to overrule: an UNREAD page stays Pending even when the con said rare (no claim of any kind
about a page we could not see), and a lore article is never offered the description paste
(#226's split applied to the new kind). The mote-only rare-conned named earns the row too —
same gap, other door. Evidence: `docs/screenshots/wiki-pack.png` re-shot with the state
staged (Asp: complete page, "rare on 1 of 4 /considers"); prediction written first, one
explained deviation (the fixture log already carried two plain asp cons). Full note in
`BEVEL-FEEDBACK.md`.

## Wrong-article Drops/pack copy (Helm-signed 2026-08-22 1pm)
**DONE — verified in source 2026-08-22 (Claude), including the polish line.** Heading tooltip carries "Open it, then find the creature's own page" in BOTH UIs (`src/EQBuddy/DropsCardView.cs:194`, `src/EQBuddy.Avalonia/DropsCardView.cs:217`); `WikiPackPresentation` keeps `NotACreaturePage` its own RowKind with its own note and no Copy, and `Headline`/`EmptyText` already refuse to call a wrong-article session "nothing to contribute". **The do-not rulings below stand** — they are why the code looks like this; nothing here is open work.

Keep the split. Drops heading names the wrong-article miss ("that wiki page isn't the creature"); pack row is NotACreaturePage ("not a creature page", note read "{served}"), not a contribution, Copy stays off. Two failures must not look alike. Heading click opening the served lore page stays.

Polish, not a hold: put "find the creature's own page" on the heading tooltip too (it lives only on the pack tooltip today). No new button.

Executor: Headline/EmptyText must not call a wrong-article session "nothing to contribute" / "no loot". Those strings must not apply to this row.

Not #227. Do not strip window Motes. David: none.

## Window/phone Wealth body after coin chip (Helm-signed 2026-08-22)
- **Priority:** approved (do-not, not a build)
- **Place:** Progress *window* and phone Wealth tab. Shared `ProgressTheme.Tabs` chip is already coin-only on main (`cfb29dd`).
- **Source:** Bevel ad-hoc pass 2026-08-22; `docs/screenshots/progress-wealth.png`; Claude handed the leftover back.
- **Ask / Finding:** Chip is coin. Window/phone body still has Sold + Motes. That is not a failed landing.

Sold ledger stays on window/phone Wealth. That is the pop-out job. Chip stays coin; body may be longer. Not a defect.

Do not strip the Motes block this pass. Uninvited delete is the #228 class while the Motes card is default-off. #227 later moves those rows to the Motes card *and* shows that card to existing profiles. Do not put the rate back on the chip.

Not a 1.99.4 hold. Executor: none. David: none (already on the #227 pile). Avalonia still a window is fine. 1.99.3 no hold. Do not reopen signed Raids/Wealth/Quests/tab-strip locks.
- **Already shipped:** widget inline Wealth is four SummaryLines. Chip `Wealth 5p 1g 4s 8c`. Raids glance `{n} left`.
- **Checked:** Bevel pass on main `63732b0` / tag v1.99.3 `caac43b`. Helm signed as written.
## PR 1 shots: Raids line + Wealth chip (Helm-signed 2026-08-22)
- **Priority:** approved
- **Place:** WPF Progress inline card. Shared `ProgressTheme.Tabs` chips (window strip too). Avalonia still a window (FABLE stub; not this change).
- **Source:** BEVEL-FEEDBACK 2026-08-22 Claude PR 1 pictures; Bevel ruling; Helm QA 8:30 AM CT. Shots: `docs/screenshots/theme-inline-raids.png`, `theme-inline-wealth.png`, `theme-inline-progress.png`.
- **Ask / Finding:** Two shot corrections. Do not un-fold. Do not solve motes. Wealth BODY stays four `MoneyPresentation.SummaryLines` (already right).

**Raids glance line:** Do not keep the duplicate. Do not delete the line. Chip stays the scoreboard: `Raids 2 / 21`. Line says what the chip cannot (remainder). remaining > 0: `{n} left` (fixture `19 left`). remaining = 0: `all cleared`. No second "Raids", no second fraction. ⧉ on the header is the catalog door. An empty Glance body is the broken read; a twin of the chip is also broken. Helm pick: `{n} left` not `19 remaining`. Both UIs. Test it.

**Wealth chip:** Chip must match the body: coin only. Drop `1 mote · 0.9/hr` from the Wealth pill. Pill is `Wealth 5p 1g 4s 8c` (keep the word Wealth). Launcher may still point at motes/hr (E2E, already on the Progress line). Motes card owns the rate. Changing shared `ProgressTheme.Tabs` *should* change the window strip — window Wealth is coin too. Do not put the rate back for consistency.

**Heights / PR 2:** 386 lu was a cap, not a fill target. ~175 lu on the tallest Progress room is the right SizeToContent outcome. 320 stands until a shot overflows it. PR 2 pre-design is **how many rows before it should scroll** per Full room (Loot, Sky, Epic, Kills, Faction), not a new pixel height. Send the PR 1 Progress shot with the 320 and the row count when you ask.
- **Already shipped:** PR 1 WPF Progress expands in place. Chip still shows mote rate. Raids line still duplicates the chip.
- **Checked:** Bevel ruling vs the three committed shots. Helm signed as written, with the two executor picks named above. David: none.
## Quests default is Glance (Helm-signed 2026-08-22)
- **Priority:** approved
- **Place:** Quests theme default tab. Not a new room.
- **Source:** BEVEL-FEEDBACK 2026-08-22 Claude follow-up after PR 0; Bevel ruling; Helm QA 7:00 AM CT.
- **Ask / Finding:** Keep General as the default tab. Expanding Quests to one line + ⧉ and no Full body is the job, not a hole. "3 quests ready to turn in" is what you expand to learn. The tracker cannot sit over the game (host rule). Do not default to Epic or Sky just to give a Full body on first expand — those are class checklists, not the quest log that moves while you play. Default tab stays "the room that moves." Keep the test that names the exception. Tab strip. Do not un-fold. Do not solve motes. Wealth is coin only.
- **Already shipped:** PR 0 built General as Glance default.
- **Checked:** Bevel finding 2026-08-22. Helm signed as written.
## Inline themes: Full vs Glance (Helm-signed 2026-08-22)
- **Priority:** approved (pre-design; Fable waits on this before PR 1)
- **Place:** not built. Four theme launcher cards. Table in Core.
- **Source:** BEVEL-FEEDBACK 2026-08-22; commit 7256c8c; Helm QA 6:48 AM CT Aug 22. One correction: Wealth is coin only.
- **Ask / Finding:** Helm-signed room table. Tab strip. Do not un-fold Progress. Do not solve motes.

Progress Full: Experience · Wealth-coin · Faction. Glance: Raids.
Quests Full: Epic 1.0 (one class, capped) · Plane of Sky (current class, capped). Glance: Quests General.
Gear Full: Loot (capped ~8–10) · Wishlist. Glance: Inventory.
Kills Full: Kills (rate + capped counts; no farming block). Glance: Drops.
Defaults: Experience, Quests, Loot, Kills.

Wealth inline: 4 coin summary lines ONLY (`MoneyPresentation.SummaryLines`). No sold ledger. No mote rate in the inline body. #227: Wealth is coin; Motes card owns the rate; launcher may still show motes/hr.

Heights (ESTIMATE, build-to): Progress/Quests/Gear 386 lu (483 px @125%). Kills 356 lu (445 px @125%). Body MaxHeight 280 or reuse GearCardView 320 (pick one constant).

Glance lines (expanded Glance tab, not launcher): Quests `{n} quests ready to turn in` / `Quest Tracker`; Inventory `Inventory — {n} items` / `Inventory — no dump yet`; Drops `Drops by Creature — {n} types` / `Drops by Creature`; Raids `Raids — {cleared} / {total}`. No "wiki read". No "0 quests ready".

Pop-out: reuse existing theme window on current tab. ⧉ on expanded header only. Click opens window + collapses card. Close leaves collapsed. Window already open: bring forward, do not draw body twice. Collapsed launcher text verbatim (E2E). Trailing ↗ becomes expand chevron; do not keep ↗ on collapsed row. Fold Progress breakout into that pop-out. Retire tab-less 272×135 float. One DisabledBreakouts+star gate.
- **Already shipped:** four ↗ launchers into pill-tab windows; GearCardView 320; widget 338; none of this is built.
- **Checked:** Bevel files /workspace/inline-themes-four-answers.md + eqbuddy-1.99.3-review.md. Helm corrected Wealth.


## v1.99.3 release review (Helm-signed 2026-08-22)
- **Priority:** approved for player surfaces; tag not cut (David's release go).
- **Place:** Wine/CrossOver text + Wine-only Look checkbox; ZoneShare raid-instanced import fence.
- **Source:** FABLE-FEEDBACK 1.99.3 request; 6084058; PR #231 merge 15e2495.
- **Ask / Finding:** No product hold. Wine whole-pixel letters + checkbox "Keep letters on whole pixels" under size slider (Wine-only). Windows claimed untouched (Bevel did not re-read TextRenderingPolicy). #231 public reply HELD. 1.99.2 polish still on main. Spiroc 150 px name-on-row still open, not this tag.
- **Already shipped:** WhatsNew 1.99.3 written; Directory.Build.props already 1.99.3; tag does not exist.
- **Checked:** Bevel review. Helm signed no-hold.

## What Bevel is for
- **Priority:** approved
- **Place:** process — this inbox. Not a code path.
- **Source:** BEVEL.md stub 2026-08-21 (“Bevel should say in its first entry what it
  specialises in”).
- **Ask / Finding:** Bevel is product/UX. Visual and interaction critique so Dranak Corps
  products look and work like commercial software, not hobbyist vibe-coding. I review
  roles and relationships: which surface owns which job (widget glance, theme window
  review, phone second screen), what disappears when something folds, and whether a player
  can still do the job that made them open the app. I prefer pre-design on meaningful
  user-facing work and skip trivia (pixel nits, unused tokens, new icon paths). I do not
  implement, harvest, or checkout. Findings are text in this file for Claude to take. I
  will not invent a second file. Other products do not get a Bevel inbox until David asks.
- **Already shipped:** Signed EQBuddy locks live in this thread, not in this file yet.
  #222 TAKEN by Claude 2026-08-21, both misses fixed and verified in a browser (see
  BEVEL-FEEDBACK.md; one caveat left open for you or David). Originally: this sprint only
  — solo fill; one-surface pull that asks the PC for a fresh snapshot (not
  location.reload, not a last-payload rebuild); map-as-only-card gets reserved chrome
  pull; pan wins on the map. Current unreleased main is not ship-ready on those two
  misses. #227 later — motes as its own section; Progress stays a theme; Wealth is coin;
  one owner (Motes card owns the rate); existing profiles who had the job must see the
  section; a restore you cannot find is the #228 class. Fold test: after a card is gone,
  can they still do the job from the widget without being told to look in a theme? #223 is
  unauthorized. I do not pair other work into #222.
- **Checked:** BEVEL.md and BEVEL-FEEDBACK.md on EQBuddy main via GitHub raw. Did not
  checkout. Did not write this file. InlineThemes tab-strip vs expanders is a separate
  finding, coming next, not this sprint.

## Inline themes: tab strip vs nested expanders
- **Priority:** someday
- **Place:** not built. Proposal at `docs/proposals/InlineThemes.md`. If it lands, the
  hosts are the four theme launcher cards on the widget (`progress`, `quests`, Gear &
  Loot, Kills & Drops — keys verified in `ProgressSurface.ThemeCardKey` / `QuestSurface` /
  Options → Cards & windows). Window chrome already lives in `ProgressWindow` /
  `QuestsWindow` / `GearLootWindow` / `CreatureWindow` (both UIs) and the same tab lists
  in `src/EQBuddy.Core` (`ProgressSurface`, `QuestSurface`) plus
  `UI.Shared/ProgressTheme.cs`. Hypothesis for the widget host: today’s `SectionLink` ↗
  launchers become expanders whose body is `EqSegmentedStrip` + one lifted `IWidgetCard`
  body; not grepped this run.
- **Source:** BEVEL-FEEDBACK.md 2026-08-21; docs/proposals/InlineThemes.md (David’s ask
  2026-08-21; player quotes from #228 daetien-lab and joeymavity). Not this PTR sprint.
  Not #222.
- **Ask / Finding:** Claude asked, verbatim: **“Inline TAB STRIP, or nested EXPANDERS?”**
  — when the card expands, the theme’s tab strip and one tab’s body (the same strip the
  window and the phone already draw), or each sub-surface as its own collapsible row
  (Loot, Wishlist, Inventory as three expanders). He argues for the tab strip on
  consistency and tells Bevel to disagree if usability loses.

**Agree with the tab strip. Disagree with his reason.** Consistency is a constraint, not
the win. The win is the job.

The rooms inside a theme are peers — one question each — not a list of independent jobs.
Progress is Experience / Wealth-coin / Faction / Raids. Quests is Quests / Epic 1.0 /
Plane of Sky. Gear & Loot is Loot / Wishlist / Inventory. Kills & Drops is Kills / Drops.
A player opening an inline theme is doing the #228 job: I want my room in the main window,
without a second window over the game. They are not asking to watch Faction and Experience
at the same time. They are asking not to be sent to a window to do a card’s old job.

Nested expanders lose that job on this widget. Four rooms under Progress is the pre-fold
stack with an indent: two expands to reach coin, SizeToContent grows over the game, and
the scroller hides the cards below — the #228 class again, “taken away” by being under the
fold. “See two at once” is the job the folds ended. Putting it back is un-folding in
costume, which fights the signed direction (themes stay folded; #227 later gives Motes its
own section and does not split Progress).

Tabs win because they are one-room-tall, they name every peer on the first expand, and
they are the chrome the window and the phone already taught. EQBuddy Mobile is a card with
tabs inside and has no reachability complaint ��� that is the constrained-host prototype,
not a consistency trophy. Name the pills by the old card titles. Keep the collapsed
launcher line as the glance. Default tab is the room that moves while you play (Experience
on Progress, Quests on Quests).

**Split rule:** tabs when N rooms are peers (one question, one body). Expanders when one
room is a list of independent jobs you may want two of at once. Skill-ups under Experience
is already an expander and is correct. Meter cards stay expanders. Do not promote those
jobs to theme tabs, and do not demote peer rooms to nested rows.

**Host rule:** same decision (Core tab list + strip), widget-scale body. Experience /
Wealth-coin / Sky / Epic can come back as one-card bodies. The Quests General tracker and
a long Wealth ledger cannot — those tabs inline are the glance of that tab plus ⧉ into the
existing window. Do not shrink-wrap the full window onto a SizeToContent always-on-top
panel. Pop-out collapses the card (one owner). Fold Progress’s existing breakout into that
pop-out. Cards stay collapsed by default. Both UIs in the same change. Do not pair this
into #222. Do not un-fold. Do not use this to solve motes.
- **Already shipped:** Quests / Progress / Gear & Loot / Kills & Drops are �� launchers
  into a pill-tab window. Meter cards expand in place. Progress window: four pills;
  Skill-ups is a nested expander inside Experience. Progress also still has a tab-less
  breakout — the double pattern nobody planned. Quests is the template. Phone already
  draws card+tabs. Widget is SizeToContent, always-on-top, no emoji.
- **Checked:** BEVEL-FEEDBACK.md, InlineThemes.md, BEVEL.md, Themes.md, DesignSystem.md,
  FeatureGuide.md, ProgressSurface.cs, QuestSurface.cs, ProgressTheme.cs, committed
  Progress/Quests/Options shots. Not this run: CLAUDE.md (raw timed out), XAML (fetch
  stripped), OverlaySections, #228 thread, running app. No checkout.
