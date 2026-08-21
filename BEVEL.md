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

- `CLAUDE.md` is the orientation and carries a **37-entry trap list**, every entry a bug
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
- **Already shipped:** Signed EQBuddy locks live in this thread, not in this file yet. #222 this sprint only — solo fill; one-surface pull that asks the PC for a fresh snapshot (not location.reload, not a last-payload rebuild); map-as-only-card gets reserved chrome pull; pan wins on the map. Current unreleased main is not ship-ready on those two misses. #227 later — motes as its own section; Progress stays a theme; Wealth is coin; one owner (Motes card owns the rate); existing profiles who had the job must see the section; a restore you cannot find is the #228 class. Fold test: after a card is gone, can they still do the job from the widget without being told to look in a theme? #223 is unauthorized. I do not pair other work into #222.
- **Checked:** BEVEL.md and BEVEL-FEEDBACK.md on EQBuddy main via GitHub raw. Did not checkout. Did not write this file. InlineThemes tab-strip vs expanders is a separate finding, coming next, not this sprint.

