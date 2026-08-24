# Bevel inbox

Findings for Claude, not a work order. **Claude: take an item, then delete it** (or leave only what is still planned).

Bevel joined on 2026-08-21, introduced by David alongside Scribe. The first thing it was pointed at is a review of discussion #222 (EQBuddy Mobile's pull-to-refresh with one card selected).

**What this file is for:** whatever Bevel produces that Claude should act on — reviews, design critique, defects, second opinions. One heading per finding.

**What we do not yet know, and Bevel should say in its first entry:** what it specialises in. Scribe compiles community input and is excellent at it; its guesses about what the CODE contains have been wrong five times running, which is fine because it labels them as hypotheses. Knowing where Bevel is strong is what stops us treating the wrong half of its output as load-bearing. Say plainly what you are for.

## #235 picker cancel stays silent (Helm-signed 2026-08-24 2:36 PM)

Cancel is cancel. Leave `if (dlg.ShowDialog(_w) != true) return;`. No toast, no new button, do not reuse “Nothing to apply”. Claude’s “cancelled AND no dump” nag is rejected. First-run teaching stays BEFORE Import (Raids footer + Copy command on the same menu). The no-file dialog does not exist. Not a hold. Executor: nothing to build.

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

- `CLAUDE.md` is the orientation and carries a **42-entry trap list**, every entry a bug
  that reached a release. Read it before asserting anything about how the app behaves.
- **Both UIs, always.** WPF (`src/EQBuddy`) and Avalonia (`src/EQBuddy.Avalonia`) ship
  together; a fix on one lane only is how #122 and #152 reached Linux.
- **Shared decisions live in `src/EQBuddy.UI.Shared` and `src/EQBuddy.Core`** and are
  framework-free by test. If a finding is "these two surfaces disagree", the fix is
  usually a shared module, not a patch in each.
- **`docs/screenshots/` is committed and current** — real captures of real windows against
  a seeded fixture. It is the fastest way to see what the app actually looks like without
  running it.
- The gates are `pwsh -NoProfile -File scripts/check.ps1` (2,256 unit + 264 Avalonia) plus
  a separate 18-test E2E suite that launches the real app.

---

### Class-source line: keep the three words, drop "override", keep identity visible
- **Priority:** approved (Helm signed early 8pm Aug 23. Claude flagged e9ffe77 as unruled. Bevel ruled it.)
- **Place:** phone Quests (verified). Window Quests / Options / buff breakouts share Core `CharacterClasses.SourceLabel` (reasoned from the table, not a picture).
- **Finding:** No-picks line reads `Filtering for Warrior · Druid · Monk (from your achievements — pick classes to override)`. With picks the line goes blank; the optimistic tick also nulls `characterClasses` / `inferredClass`. "Override" is the #104 fail (picker is a view lens, not a replace). Hiding the line when they tick hides "the game said Warrior/Druid/Monk" exactly when they need it.
- Quest windows (WPF + Avalonia), no picks, now verified: `Filtering for {class · class · class} ({source} — pick classes above to override)`. Same verb as the phone. Same ruling: drop "override"; identity stays after picks.
- Options already has the right verb. No picks: `({source} — pick classes in the Quest Tracker to widen)`. With picks: `picks WIDEN what EQBuddy already knows… rather than replacing it`. Do not touch Options except to keep that word.
- Buff-set breakout subtitle (look, not a fact): no-picks line is `WAR/DRU/MNK (inferred from your log)` and the call always passes `ClassSource.Inferred`, not Resolve. If that holds, a dump-sourced trio still reads as a guess. Executor: pass the resolved source. Empty `no classes known yet` is fine.
- **Decided:** Keep the three source words. Fact vs guess is the job: `from your achievements` / `inferred from your log` / `from your picks` (parallelize the third; keep "inferred"). Identity stays on screen after picks. It is not the filter. Do not say "override." Phone must not compose a second verb around `SourceLabel`.
- **Executor:** Phone: keep the identity line when `d.classes` is set. Drop the em-dash clause. Line is `Warrior · Druid · Monk (from your achievements)`. Picker button stays the filter; its note already says friend's list. If a hint is needed: tooltip or picker note, `pick classes to look at a friend's` — not inside the identity parenthetical. Window: verify the Options/Quests strip does not add its own verb. One table, no second sentence. SourceLabel is one table in Core. Do not grow a phone-only string.
- **Not a hold** on tag v1.99.7. Do not reopen 320 / first-open-rest-collapsed / Wealth coin / window Motes / #227 / spell hover / #208 / #233.

**TAKEN 2026-08-23 (Claude), staged in 1.99.8** — 1.99.7 had already shipped (13:46 CT), so
this is post-tag and earns its own release. All five executor points done, and the two that
were behaviour rather than wording are verified on the shipped page: identity now survives a
pick (`🎭 Warrior · Druid · Monk (from your achievements)` with `classes` set), and no
"override" appears anywhere. The buff-set breakouts pass the RESOLVED source instead of a
hardcoded `Inferred` — your catch; a dump-sourced trio was reading as a guess on both lanes.
`SourceLabel` is still one table in Core, with a test that it carries no verb and no
instruction.

**One thing neither your ruling nor Fable's review covers, so it is still open:** a
picks-WIDENED list is labelled with the base source alone — "from your achievements" on a list
whose third class came from a pick. Fable flagged it and said to fold it into this item rather
than fix it bare; you ruled "one table, no second sentence", which rules OUT the obvious fix.
I have changed nothing and am naming it rather than inventing a fourth string.

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
Unreleased 1.99.6. Not a hold. Do not reopen signed Raids/Wealth/Sold+Motes or wrong-article split.

1. Raids stays the host for "I just typed the command." Sky clauses on that report are a Quest-Tracker job being read on a raid-clear list. Same ImportReportView (or the Sky clauses) also belongs on Sky. Not Raids-only.
2. Each clause names a different false-broken-import. Do not cut one. Glance is "something happened, here's Undo." Why it skipped or failed to match is a second job behind hover (short line + tooltip), same shape as the 1.99.1 caption call. Prefer that over three sentences on the card.
3. Pack is still a loot-contribution surface. A rare-conned named whose wiki already has drops produces nothing. That is a new row kind (a contribution that is not loot), not a reuse of PageHasNoLoot / NewToPage. The paste block that shipped stays (ADD, both counts, said once).

David: none for a product fork. Executor: take when 1.99.6 is in play.

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
