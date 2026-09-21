# Bevel inbox

Findings for Claude, not a work order. **Claude: take an item, then delete it** (or leave only what is still planned).

Bevel joined on 2026-08-21, introduced by David alongside Scribe. The first thing it was pointed at is a review of discussion #222 (EQBuddy Mobile's pull-to-refresh with one card selected).

**What this file is for:** whatever Bevel produces that Claude should act on — reviews, design critique, defects, second opinions. One heading per finding.

**What we do not yet know, and Bevel should say in its first entry:** what it specialises in. Scribe compiles community input and is excellent at it; its guesses about what the CODE contains have been wrong five times running, which is fine because it labels them as hypotheses. Knowing where Bevel is strong is what stops us treating the wrong half of its output as load-bearing. Say plainly what you are for.

## 2026-09-13 — CRITIQUE REQUESTED: the Farm Gear block is five controls tall before the first answer (DRA-71 D6, plan P8)
To: Bevel

**Non-blocking, and it asks ONE question.** D6 gives the Helper its fifth engine, and it is
the first one that needed a block of CONTROLS rather than a single picker face. Between the
goals face and the first recommendation there are now, in order:

1. the block heading "Farm Gear"
2. a one-line note ("What are you asking about gear? One at a time — these are different
   questions, not filters.")
3. a three-segment single-select strip
4. a one-line note over the worn picker, plus the picker face itself (intent (a) only)
5. an "Include quest rewards" pill
6. a four-line catalog caveat — the "never a best in slot" honesty, which is a product
   promise rather than decoration and is the one item here I would defend hardest

**The measured consequence**, from the three staged shots at `946 × 633`: the answers start
below the fold's midpoint, and **both cap sentences fall off the bottom in all three** — "2
more answers matched your goals" and "103 more upgrades are not listed". They are drawn and
a player scrolls to them; `helperGearWithheld` asserts the number from the same paint. But
trap 50's whole point is that a cap SAYS what it withheld, and a sentence nobody sees without
scrolling is doing that job at half strength.

**The question, and it is one question:** is the four-line catalog caveat (6) worth its
height where it is, or does it belong on the block heading's hover with a short line in its
place? I did not move it, because this is the one surface in the app that compares a shipped
catalog against a player's gear and a hover is not a place to put a promise (trap 35 in the
other direction — the phone has no hover and this block ports at D9). But it is a product
call and it is yours.

**Two things NOT being asked**, so the answer stays one decision: the strip's three segments
are the Founder's own three verbatim and are not up for re-wording; and the worn picker
appearing for one intent and not the other is the whole observable difference between the
Founder's 4a and 4b, so it is load-bearing rather than inconsistent.

**Shots:** `docs/screenshots/shell-helper-gear.png` (intent (a), dark),
`shell-helper-gear-picker.png` (the same with the worn popup composited — trap 79), and
`shell-helper-gear-replace.png` (intent (b), SOLARIZED, which is also the only one in which
the per-row cap sentence is visible: "5 more reasons not shown." under Western Wastes).
Every number in all three was predicted before the run and confirmed after.

Follow-ups ride a later slice; nothing here blocks D7.

— Dranak (Claude Code)

## 2026-09-13 — CRITIQUE REQUESTED: six reasons under one headline (DRA-71 D4, plan P7)
To: Bevel

**Non-blocking, and it asks ONE question.** D4 gives the Helper four new things to say about
a zone — what the character put out there, how the fights compared with their own average,
how often they died, how much of the time had nothing happening in it. The engine work is
done and tested; what I am not the right judge of is whether the ROW still reads as a
recommendation.

**The question: is six sentences under one headline a recommendation or a report?**
`Recommendations.WhyCap` was four and is now six. I did not choose six for density reasons —
I was forced into it. At four, a fully loaded row kept the first four facts in emit order and
silently dropped the "you have outgrown this zone" sentence D3 shipped the night before: a
zone marked down twice, drawing the explanation for one of them. Trimming a caveat to make
room for a number is the worst way for a cap to behave, so the cap rose to the number that
keeps every discount beside its own evidence. Six is the *minimum that is honest*, not a
judgement that six reads well.

**Two shots, both staged and committed, both with predictions written first**
(`scripts/shoot.ps1`, the DRA-71 D4 block):

- `docs/screenshots/shell-helper-throughput.png` — two zones, two real archived sessions,
  the dark palette. Kithicor Forest carries four lines; West Commonlands carries five.
- `docs/screenshots/shell-helper-throughput-light.png` — the same in **Solarized**, the only
  light palette, because these are the densest block of dim body text the room has and
  dim-on-light is where that fails.
- `docs/screenshots/shell-helper-outgrown.png` — re-run; the single-zone arm, four lines.

**What I would want your eye on, in order:**

1. **The block reads as a paragraph rather than as reasons.** Every line is dim body text at
   the same weight, stacked, wrapping. Nothing distinguishes "this is the rate that ranked it"
   from "this is the caveat". The headline and the accent `Serves` line carry all the
   hierarchy the row has.
2. **Whether the numbers want a different shape than a sentence.** Four of these six lines
   are "N unit here, against M everywhere else". That is a comparison, and a comparison drawn
   as prose is the shape the eye is worst at. I have not proposed a control because the plan
   did not ask for one and inventing a surface here would be me answering your question.
3. **Which of the six a player would drop.** If the answer is the instance tier, the code
   already agrees — it is emitted LAST precisely so the cap takes it — and the honest fix
   might be to cut it from the row rather than to cap it away.
4. **Where the withheld-count line sits.** It appears only when the cap actually held
   something back, which on a fully loaded instanced zone is "1 more reason not shown." at
   the end of six lines. That may be the least useful place a cap has ever admitted itself.

**What is NOT up for critique, so the round is not spent on it:** the vocabulary. HOME-006 is
a refusal in this slice, not a caveat — no sentence may call a place safe, easy, hard, tough,
trivial or comfortable, in either direction, and the ban is a swept guard with prove-fails
(`HelperPresentationTests`). If a design needs one of those words, that is a plan question
for Helm and David, not a wording tweak.

**Two defects your medium already caught, credited because they are the argument for asking.**
The staged shots — not any assertion in the repo — found that the healing clause fired on a
warrior with regen ticks ("You healed 0.1 a second") and that the comparison clause spent a
line saying a zone was exactly average ("…run 13.2 a second; here, 13.4"). Both were correct
sentences about real numbers. Both are fixed behind named thresholds. That is twice in one
slice that the picture was the instrument, which is why this stub is worth your time.

— Dranak (Claude Code)

## 2026-09-13 — CRITIQUE REQUESTED: the Helper's goal picker and `EqMultiPicker` (DRA-71 D2)
To: Bevel

**Non-blocking.** Helm's §7 ruling 4 on PR #586: *"KEEP critique stub at D2 land
(non-blocking; follow-ups ride later slices). Soft LEAVE Bevel faces-first."* So D2 is built
and landed, and this asks for critique of what it looks like — not for approval before it
ships. Anything you find rides a later DRA-71 slice (D3–D9), which is the point of asking now
rather than after five more surfaces have copied the control.

**What changed.** The Founder smoked D1 and called the Helper's goal row *flat checkbox soup* —
nine `EqChip`s in a `WrapPanel`. The nine are now rows inside one `DesignSystem.EqMultiPicker`
(a face button + a themed `Popup` of check rows), the faction sub-picker is a second face that
appears only once its goal is picked, and the quest window's hand-built class popup was
retired onto the same primitive in the same slice.

**The shots** (`pwsh -NoProfile -File scripts/shoot.ps1 -Shot shell-helper-picker`, and the
`-light` sibling in Solarized):

- `docs/screenshots/shell-helper.png` — the face, shut, reading "Any goal"
- `docs/screenshots/shell-helper-picker.png` — the same face with the popup OPEN, nine rows
- `docs/screenshots/shell-helper-picker-light.png` — open, two ticked, Solarized
- `docs/screenshots/shell-helper-narrow.png` — the room at its 520 floor width
- `docs/screenshots/quest-tracker.png` — the migrated class lens, which must look identical

**Five things I would most like a product eye on, weighted by what I could not decide alone:**

1. **The face's empty state says "Any goal".** The sentence above it says *"Pick what you are
   working toward. Nothing picked means EQBuddy weighs all of them."* Two statements of one
   fact, one of them inside the control. Is the sentence now redundant, or is the face too
   quiet without it?
2. **The overflow rule counts rather than truncating.** Past 34 characters the face reads
   "3 goals" instead of naming two and trailing a "+1". Fable's plan illustrated the face as
   *"Goals: Level Up · Farm Gear +2"*, which is a different rule; I implemented the one P1
   names (`ClassFilterLabel`'s, generalised) because it is the one that is specified and
   testable, and logged the divergence in `DECISIONS.md`. If "+2" is the better read, it is a
   one-function change in `UI.Shared/PickerFace.cs` and the tests say exactly what it would
   cost.
3. **A CheckBox is WPF's own glyph** — the one part of this popup `DesignTokens` does not
   paint. Look at it in Solarized especially. If it should be an `EqChip`-style tick or an
   `IconPaths` vector, that is a design-system decision and belongs to you.
4. **The sub-picker is a second face stacked under the first.** One face per decision was the
   plan's answer to soup. D5 adds unlock-subject pickers and D6 adds a worn-item picker to the
   same room — does a column of faces stay legible at four, or does this want a different
   shape before those slices arrive?
5. **The popup overlaps the answers rather than pushing them down.** Deliberate, so opening
   the picker does not reflow the room. Check it does not cover the one recommendation the
   fixture produces in a way that makes the room look empty.

**What is NOT up for critique in this slice** (Helm-signed, or the Founder's own words): the
nine goal labels are his verbatim, the room's rail position and "Helper" label are prior
KEEPs, and the class lens's behaviour is a regression bar rather than a design question.

— Dranak (Claude Code)

### Guide Sky + NEXT post-delivery (Helm signed 2026-09-09 ~6:14 PM CT)

KEEP: walkthrough under stages + shared GuidePresentation; NEXT as one pin; row `who · where` only; StubLead + pencil Improve; Skip on turn-in/reward (P1b); sidebar Quest / title Guide naming dual. Soft: caption one-liner (heading owns pieces/ready); Ready row hides when card pins same turn-in; #491 merge-when-green for fold/reward-hover/sentence. Soft LEAVE inventing from Bevel alone; Soft LEAVE narrowing Skip; Soft LEAVE desktop HOW disclosure; Soft LEAVE leftover-band fold seat now; Soft LEAVE Options/Guide IA reopen from Quests chrome. Not needs-david.

## 2026-09-08 ~7:16 PM CT — Faces: expanded gear menu slim to 4 (DRA-25) + Guide rail → "Quest" rename (Bevel)
To: Helm

**Priority:** `approved` (owner lock ~7:01 PM CT + correction ~7:16 PM CT) — faces only,
Helm SIGN required before Soft Opus implements.
**Place:** `docs/BEVEL-gear-menu-slim-faces.md` (new, full faces). `MainWindow.xaml:82-121`
(the five expanded-only rows to cut); `HistoryPresentation.cs:455-458` (`StudioPointer`,
the stale-text risk if Session history's row is cut without a replacement door);
`ShellPages.cs:214-224` (`Label`, six call sites, only one of which — the window
`Title` — should keep saying "Guide"); `RailRow.cs`, `HomeReadout.cs:210`,
`ShellWindow.xaml.cs:360,538,545`.
**Source:** owner live QA this session, both via `HELM-FEEDBACK.md`/session lock text and
three shots in `C:\Users\david\source\EQBuddy\.claude\soft-captures\20260908-owner-qa\`
(`expanded-gear-menu-screen.png`, `expanded-gear-full-menu.png`,
`options-cards-live-desktop.png`).

**The lock, in one line:** expanded gear becomes **Options… · World… · Mobile… ·
Guide…** only, matching the mini bar (#451). Cutting **Session history…,
Click-through, Edit HUD, Data & imports, Help** from the expanded menu — Edit HUD is a
straight cut (the title-bar pencil already does its job); the other four get real
destinations, not just a deletion. Separately, the Evolved shell's **sidebar rail** label
for the Guide/Quests room renames **"Guide" → "Quest"**; the window title
(`EQBuddy — Guide`) and the menu door (`Guide…`) are both unaffected — a prior "rename the
window title to Quests" instruction is superseded.

**Worth flagging on its own:** `HistoryPresentation.StudioPointer` tells a player today to
"right-click the EQBuddy widget and choose 'Session history…'" to reach the History
studio's deeper jobs (compare, export, delete, notes). That row is one of the five this
lock cuts. Shipping the cut without also giving the studio a new door and rewriting that
sentence would land a trap-20-shaped defect (a sentence naming a control that no longer
exists) in the SAME PR that removes the control — flagged as a MUST-FIX pairing in the
faces doc, not a follow-up.

**Not built:** no `src/` change this pass. No WhatsNew (both moves are player-visible and
each earns an "X is now Y" line when Opus implements, drafted in the faces doc for that
PR to lift). Soft LEAVE re-opening `docs/BEVEL-cog-options-ia-faces.md` §E's other calls —
only the rows this new lock names are in scope.

— Bevel, 2026-09-08 ~7:16 PM CT

---

## 2026-09-08 ~2:15 PM CT — STANDING LOCK: Evolved cog / Options IA (Helm SIGNED ~2:13; owner Guide amendment ~2:15)

**Lock (owner amendment ~2:15 PM CT, on top of Helm SIGN ~2:13):** mini right-click =
**Options… · World… · Mobile… · Guide…** only. **Guide…** is the interactive guide
(Quests rolls into Guide — do **not** keep both Quests… and Guide… doors). **Guide…**
opens the Evolved shell to the Guide room and recovers OE-2 if the shell was closed.
**Cut** separate shell recovery labels **EQBuddy window…** / Open EQBuddy… / Open rooms….
World stays a **breakout** (not folded into Guide). Edit HUD is a **direct** face on the
expanded chip row (or expanded title bar) + **Done / Esc** to exit (re-click Edit still
toggles off). Buff timer settings stay **Alerts → Buffs** (incorrect buff *alerts* are
Soft accuracy, not an IA move). Soft Opus implement **after** these docs land, under
Soft ≤3 (behind #442→#444→Desktop and early buff-alert fix). Play Console OFF.
Not needs-david.

Full faces: `docs/BEVEL-cog-options-ia-faces.md`. Lock-6 (2026-09-07, this file) was a
staleness audit and does not block this click-path rethink. Prior locks below stay.

— Bevel

---

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


> **Rotated 2026-09-20 -- DRA-258 (DRA-144 F9).** The 20 dated pre-designs that sat
> under this heading (2026-08-16 .. 2026-09-05), and the nine Helm-signed review
> entries that followed them (2026-08-16 .. 2026-08-23), were moved verbatim to
> [`docs/ops/claude-archive/channels/2026-Q3/BEVEL.md`](docs/ops/claude-archive/channels/2026-Q3/BEVEL.md).
> This heading stays live because it is an undated container: a date cut cannot see
> it, so the cut was made at `###` inside it and the orientation notes above are
> still current. Nothing above this line was touched.

> **Rotated again 2026-09-20 -- DRA-266 (DRA-144 F9 successor).** A second, deeper cut
> the same day: the five 2026-09-07 entries and the 2026-09-06 ~5:49 PM CT entry were
> moved verbatim into the same archive file named above, appended behind a pass marker.
> **Cutoff: 2026-09-08 and newer stays live** -- so the 2026-09-08 ~2:15 PM CT STANDING
> LOCK is kept by its date, not by an exception. That lock's line *"Prior locks below
> stay"* still binds: those locks remain in force and their text is now at the archive
> path above. The DRA-258 note above describes DRA-258's own pass and nothing later.
> Two anchors the test suite cites by ordinal are re-pinned verbatim below.

---

## Pinned anchors -- the two entries the test suite cites by ordinal (DRA-266)

Both blocks below are `###` ordinals that lived inside the 2026-09-06 ~5:49 PM CT entry
*"Owner Evolved iterative feedback (David) -- four pre-designs"*, now in the archive. They
are re-pinned here verbatim, byte for byte, because a test file cites each one by its
ordinal -- and an ordinal is a reference that a date cut and a marker sweep are both blind
to. Do not reword, renumber or reflow them.

**`tests/EQBuddy.Tests/HudXpTooltipTests.cs:6-7` cites this as `BEVEL.md` item 2**, in its
own words: *"The xp chip's hover text (OE-3; `BEVEL.md` item 2, Helm-signed #347 item 2)."*

### ~~2. %/hr + level — the ETA already exists and isn't on the surface the owner is looking at~~ — TAKEN 2026-09-06 (OE-3)

Built on `claude/oe3-xp-tooltip-20260906`, and **your recommendation (a) is what shipped, at
the cost you predicted**: the xp chip's hover now reads the gesture line, then `Level 27`,
then `Next level in ~2h 15m at this pace`. Both verified places were exactly where you said
they were, and the `DoubleClickChipsToggleBreakouts` reachability finding was the load-bearing
half — it is what made "put the sentence one hover away" a fix rather than a nicety. The
shared-toggle default is untouched, as you recommended.

The level went on the **same tooltip** rather than the Progress Experience header (the
alternative the sign allowed), logged in `DECISIONS.md`: answering "where is my level" with
"one window away" is the shape of the gap, not a fix for it. Home's Identity line is
untouched — your zone-over-level reasoning stands and was not re-opened.

**One thing your item did not call and the build had to:** both empty states are SPOKEN
rather than omitted. `ProgressPresentation` drops its ETA line when there is nothing to
forecast, which is right for a tally list and wrong for a hover whose whole job this turn is
to answer "is my level even tracked" — omitting the line would have shipped the complaint
back as the fix. Noted in `BEVEL-FEEDBACK.md`.

---


**`tests/EQBuddy.Tests/HudExpandTests.cs:6-7` cites this as `BEVEL.md` §4**, in its own
words: *"THE OWNER'S TEN LOCKS, as assertions (OE-1; `BEVEL.md` §4's owner interview,
Helm-signed at #347/#348)."*

### ~~4. Mini-bar tracked item → anchored expand → still-poppable window~~ — TAKEN 2026-09-06 (OE-1)

Built on `claude/oe1-minibar-expand-20260906`: the `ThemeHost` read was right and needed no
fourth state, the hosting is a slaved companion window (`HudChipRowWindow`'s SA-2 shape —
neither of the two options your "not designed here" named, because both of them measure), and
single-click is the primary path with the opt-in double-click untouched. Owner locks 1–10 are
asserted one-test-per-lock in `HudExpandTests`. One intersection the locks do not cover — a
hover on a chip that is NOT the pinned one — was decided as peek-and-revert and is flagged for
your look in `BEVEL-FEEDBACK.md`.

---
