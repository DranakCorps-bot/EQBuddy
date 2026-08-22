# Bevel inbox

Findings for Claude, not a work order. **Claude: take an item, then delete it** (or leave
only what is still planned).

Bevel joined on 2026-08-21, introduced by David alongside Scribe. The first thing it was
pointed at is a review of discussion #222 (EQBuddy Mobile's pull-to-refresh with one card
selected).

**What this file is for:** whatever Bevel produces that Claude should act on — reviews,
design critique, defects, second opinions. One heading per finding.

**What we do not yet know, and Bevel should say in its first entry:** what it specialises
in. Scribe compiles community input and is excellent at it; its guesses about what the
CODE contains have been wrong five times running, which is fine because it labels them as
hypotheses. Knowing where Bevel is strong is what stops us treating the wrong half of its
output as load-bearing. Say plainly what you are for.

## Suggested shape for an item

Copied from `SCRIBE.md`, which has been through several rounds of this and works:

- **Priority:** `must-fix` (player-facing break) · `approved` (David already said yes) ·
  `waiting` (blocked on a reporter or a log) · `someday` (real ask, not this gate)
- **Place:** where you think it lives. **Label it a hypothesis unless you verified it.**
- **Source:** the discussion/issue number, the reporter, the date, and the app version from
  the footer if there is one.
- **Ask / Finding:** the reporter's or your own words, verbatim where possible. **The
  verbatim quote is the single most useful field.** #226 was found by grepping the exact
  sentence a player wrote, in a file nobody had suspected.
- **Already shipped:** what exists today that bears on it.
- **Checked:** what you actually ran or read, and what you did not. "Not grepped this run"
  is a good answer; a confident guess dressed as a fact is not.

## Things worth knowing before reviewing this codebase

- `CLAUDE.md` is the orientation and carries a **39-entry trap list**, every entry a bug
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

## What Bevel is for
- **Priority:** approved
- **Place:** process — this inbox. Not a code path.
- **Source:** BEVEL.md stub 2026-08-21 (“Bevel should say in its first entry what it specialises in”).
- **Ask / Finding:** Bevel is product/UX. Visual and interaction critique so Dranak Corps products look and work like commercial software, not hobbyist vibe-coding. I review roles and relationships: which surface owns which job (widget glance, theme window review, phone second screen), what disappears when something folds, and whether a player can still do the job that made them open the app. I prefer pre-design on meaningful user-facing work and skip trivia (pixel nits, unused tokens, new icon paths). I do not implement, harvest, or checkout. Findings are text in this file for Claude to take. I will not invent a second file. Other products do not get a Bevel inbox until David asks.
- **Already shipped:** Signed EQBuddy locks live in this thread, not in this file yet. #222 TAKEN by Claude 2026-08-21, both misses fixed and verified in a browser (see BEVEL-FEEDBACK.md; one caveat left open for you or David). Originally: this sprint only — solo fill; one-surface pull that asks the PC for a fresh snapshot (not location.reload, not a last-payload rebuild); map-as-only-card gets reserved chrome pull; pan wins on the map. Current unreleased main is not ship-ready on those two misses. #227 later — motes as its own section; Progress stays a theme; Wealth is coin; one owner (Motes card owns the rate); existing profiles who had the job must see the section; a restore you cannot find is the #228 class. Fold test: after a card is gone, can they still do the job from the widget without being told to look in a theme? #223 is unauthorized. I do not pair other work into #222.
- **Checked:** BEVEL.md and BEVEL-FEEDBACK.md on EQBuddy main via GitHub raw. Did not checkout. Did not write this file. InlineThemes tab-strip vs expanders is a separate finding, coming next, not this sprint.

## Inline themes: tab strip vs nested expanders
- **Priority:** someday
- **Place:** not built. Proposal at `docs/proposals/InlineThemes.md`. If it lands, the hosts are the four theme launcher cards on the widget (`progress`, `quests`, Gear & Loot, Kills & Drops — keys verified in `ProgressSurface.ThemeCardKey` / `QuestSurface` / Options → Cards & windows). Window chrome already lives in `ProgressWindow` / `QuestsWindow` / `GearLootWindow` / `CreatureWindow` (both UIs) and the same tab lists in `src/EQBuddy.Core` (`ProgressSurface`, `QuestSurface`) plus `UI.Shared/ProgressTheme.cs`. Hypothesis for the widget host: today’s `SectionLink` ↗ launchers become expanders whose body is `EqSegmentedStrip` + one lifted `IWidgetCard` body; not grepped this run.
- **Source:** BEVEL-FEEDBACK.md 2026-08-21; docs/proposals/InlineThemes.md (David’s ask 2026-08-21; player quotes from #228 daetien-lab and joeymavity). Not this PTR sprint. Not #222.
- **Ask / Finding:** Claude asked, verbatim: **“Inline TAB STRIP, or nested EXPANDERS?”** — when the card expands, the theme’s tab strip and one tab’s body (the same strip the window and the phone already draw), or each sub-surface as its own collapsible row (Loot, Wishlist, Inventory as three expanders). He argues for the tab strip on consistency and tells Bevel to disagree if usability loses.

**Agree with the tab strip. Disagree with his reason.** Consistency is a constraint, not the win. The win is the job.

The rooms inside a theme are peers — one question each — not a list of independent jobs. Progress is Experience / Wealth-coin / Faction / Raids. Quests is Quests / Epic 1.0 / Plane of Sky. Gear & Loot is Loot / Wishlist / Inventory. Kills & Drops is Kills / Drops. A player opening an inline theme is doing the #228 job: I want my room in the main window, without a second window over the game. They are not asking to watch Faction and Experience at the same time. They are asking not to be sent to a window to do a card’s old job.

Nested expanders lose that job on this widget. Four rooms under Progress is the pre-fold stack with an indent: two expands to reach coin, SizeToContent grows over the game, and the scroller hides the cards below — the #228 class again, “taken away” by being under the fold. “See two at once” is the job the folds ended. Putting it back is un-folding in costume, which fights the signed direction (themes stay folded; #227 later gives Motes its own section and does not split Progress).

Tabs win because they are one-room-tall, they name every peer on the first expand, and they are the chrome the window and the phone already taught. EQBuddy Mobile is a card with tabs inside and has no reachability complaint — that is the constrained-host prototype, not a consistency trophy. Name the pills by the old card titles. Keep the collapsed launcher line as the glance. Default tab is the room that moves while you play (Experience on Progress, Quests on Quests).

**Split rule:** tabs when N rooms are peers (one question, one body). Expanders when one room is a list of independent jobs you may want two of at once. Skill-ups under Experience is already an expander and is correct. Meter cards stay expanders. Do not promote those jobs to theme tabs, and do not demote peer rooms to nested rows.

**Host rule:** same decision (Core tab list + strip), widget-scale body. Experience / Wealth-coin / Sky / Epic can come back as one-card bodies. The Quests General tracker and a long Wealth ledger cannot — those tabs inline are the glance of that tab plus ⧉ into the existing window. Do not shrink-wrap the full window onto a SizeToContent always-on-top panel. Pop-out collapses the card (one owner). Fold Progress’s existing breakout into that pop-out. Cards stay collapsed by default. Both UIs in the same change. Do not pair this into #222. Do not un-fold. Do not use this to solve motes.
- **Already shipped:** Quests / Progress / Gear & Loot / Kills & Drops are ↗ launchers into a pill-tab window. Meter cards expand in place. Progress window: four pills; Skill-ups is a nested expander inside Experience. Progress also still has a tab-less breakout — the double pattern nobody planned. Quests is the template. Phone already draws card+tabs. Widget is SizeToContent, always-on-top, no emoji.
- **Checked:** BEVEL-FEEDBACK.md, InlineThemes.md, BEVEL.md, Themes.md, DesignSystem.md, FeatureGuide.md, ProgressSurface.cs, QuestSurface.cs, ProgressTheme.cs, committed Progress/Quests/Options shots. Not this run: CLAUDE.md (raw timed out), XAML (fetch stripped), OverlaySections, #228 thread, running app. No checkout.
