# Bevel inbox

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

## 2026-09-07 ~3:50 PM CT — HELM OWNER LOCK folded into the transition one-pager: Evolved captures use teal + grey (not parchment/brass) (Bevel)

**Priority:** docs-only amend, no code, no re-shoot, same branch/PR (#402). Folding
`HELM-FEEDBACK.md`'s ~3:45 PM CT OWNER LOCK (standing) into `docs/BEVEL-transition-ux-one-pager.md`
so the next seat that stages any of §1–§4's surfaces (first-run import screen, dual-install
page, release-page section) doesn't reach for `ParchmentBrass` out of habit.

**The lock:** Evolved screenshots / tutorial pages / What's-new entries / `shoot.ps1` captures
use **teal + grey** going forward, not parchment/brass. Added as a new §6 in the one-pager,
right before "What this file is not," so it reads alongside the surfaces it will eventually
bind rather than living only in the mailbox.

**Nothing staged or re-shot in this pass** — the one-pager is copy/shape only and carries no
committed images, so there is nothing to re-shoot yet. The note is there so the theme gets set
**before** the first capture of these surfaces, not corrected after (trap 31). Flagged in the
new section: no palette named for teal + grey exists yet in `UI.Shared/DesignTokens.cs`'s
`ThemePalettes` — whoever stages first should check and raise it to Helm rather than guess at
the nearest dark theme.

— Bevel, 2026-09-07 ~3:50 PM CT

---

## 2026-09-07 ~3:15 PM CT — Ruling: the leading-article dedupe finding (`hud-expand-loot.png`) is real, small, and V0–V1 — not filed to Fable (Bevel)

**Priority:** ruling only, no code in this pass (this session is docs/UX-only, per its own
scope — see the transition one-pager above this entry). Filed so the next seat that opens
`MainWindow.TargetDropsContent` doesn't have to re-derive what Dranak already found in
`HELM-FEEDBACK.md` (~2:35 PM CT) and flagged to this seat.

**The finding, confirmed by reading the source, not just the screenshot:**
`EqlWikiItemService.NormalizeTitle` (`src/EQBuddy.Core/EqlWikiItems.cs:62`) strips only the
in-game "+N" upgrade suffix — it does not strip a leading "A "/"An "/"The ". The wiki's own
page titles carry that article for some mobs' drop tables; the player's observed loot line
does not. `TargetDropsContent`'s fold-to-base-name dedupe runs on that same normaliser, so
"Spider Venom Sac" (your kill) and "A Spider Venom Sac" (the wiki's row) look like two
different items and both draw — three lines apart in the 300-wide HUD peek, further apart
and easier to miss on the Loot card and the Loot float's Target view, which have carried the
same bug since they were built. **Confirmed still open on `main` at this tip
(`e08c6f0a`)** — nothing since #392/#400 touched `NormalizeTitle` or the dedupe call site.

**Ruling: this is V0–V1, not a `FABLE.md` item.** It is a one-line fix in one shared method
(strip a leading article before the dedupe key, same place the "+N" strip already lives) that
corrects all three surfaces at once, per Dranak's own read. It is mechanical, localized, and
the only judgment call — whether the DISPLAYED text also drops its article, or only the
dedupe KEY does — is small enough to decide inline rather than plan: **keep the displayed
wiki row exactly as the wiki writes it (articles are part of a proper mob/item name on that
page and stripping them from the shown text would be an unasked-for content edit); strip the
article only for the comparison key that decides whether two rows are "the same item."** That
keeps `WikiContribution`'s "match the wiki" rule (`CLAUDE.md`) intact for anything actually
shown, and fixes only the false-duplicate.

**Not done here, on purpose:** this session's scope is docs/UX only (no `src/`), so the fix
itself is left for the next available V0–V1 pass — it needs no plan, no design call beyond
the one above, and a shipped fix earns its own `WhatsNew.json` line (a player-visible content
correction) plus a `docs/screenshots/hud-expand-loot.png` re-shoot once fixed, since the
current committed shot is evidence of the bug it will no longer reproduce.

— Bevel, 2026-09-07 ~3:15 PM CT

---

## 2026-09-07 ~1:40 PM CT — Loot mini-bar peek fixed to target scope; Options/cog IA re-audit (lock 6): current, nothing further to strip (Bevel)

**Priority:** `approved` — owner-locked (`HELM-FEEDBACK.md` ~12:55 PM CT SIGN, on the owner's ~12:54 PM CT feedback testing Evolved Desktop `2.0.0+3a1e8654`). Soft Bevel seat, soft max ≤3, Play Console OFF, not needs-david. Item 1 of that feedback (settings-reset on publish, HIGH) is Opus's, not this pass's; item 3 (OE-9 peek content — Motes/Kills/Procs/Money) is Fable-seated and explicitly blocked from Opus until signed — neither touched here.
**Place:** `src/EQBuddy.UI.Shared/HudExpandPeek.cs` (`Loot`), `src/EQBuddy.UI.Shared/LootPresentation.cs` (`NoTargetNote`, new), `src/EQBuddy/HudExpandWindow.cs` (`LootPeek`, new — the `_main.TargetDropsContent`/`TargetEmptyNote` read), `src/EQBuddy/LootBreakoutView.cs` (one-line: shares the new constant instead of its own literal), `tests/EQBuddy.Tests/HudExpandPeekTests.cs` (Loot tests rewritten for the new signature), `docs/TestPlan.md` (new row beside the other OE-7 peek rows), `src/EQBuddy.Core/Data/WhatsNew.json` (new 2.0.0 highlight), `DECISIONS.md`.
**Source:** `HELM-FEEDBACK.md` ~12:54–12:55 PM CT (owner: *"Hover/peek must show target loot drops (same scope as pop-out), not session. If no target: peek states that a target needs to be selected... Pop-out already defaults to target — peek must match."*), and the same message's item 4 (*"Options/cog full IA pass... lock-6 style — not half-left"*). Verified against tip `3a1e8654` before edits; branch merged forward to `36c3664a` (HELM-FEEDBACK/HELM.md only) before this entry.

---

### 1. Loot peek — was session, now target; fixed, tested, WhatsNew'd

**Verified the bug first, against the owner's own words.** `HudExpandPeek.Loot(s.Loot, s.LootTotal)` built its subtext as `"Session · N items · M kinds"` from the same session list the Loot CARD already shows in full — never touching `MainWindow.TargetDropsContent`, the method `LootBreakoutView.Render` calls for that window's Target scope (confirmed: `_w.TargetScope` defaults true via `ScopeSetting() != "session"`, matching the owner's "pop-out already defaults to target"). So the chip's hover and its own ↗ genuinely described two different things — the exact defect reported.

**Fixed by reading the SAME source the float reads**, not a second derivation (trap 33): `HudExpandWindow.LootPeek` now calls `_main.TargetDropsContent(s)`/`TargetEmptyNote(s)` — the identical two calls `LootBreakoutView.Render` makes — and `HudExpandPeek.Loot` takes `(names, detail, rows, emptyNote)` instead of the session list. Three states, matching the ask's own three: a target with known rows; a target with nothing known yet (the caller's own `TargetEmptyNote` wording — wiki-offline, no-page, looking-up, all ride through unchanged); and no target at all, which now says `LootPresentation.NoTargetNote` (**"Swing at something — or /consider it — and its↵possible drops appear here."**) instead of ever falling back to a session count. That constant is shared with `LootBreakoutView`'s own no-target line (was a private literal there) so the two surfaces cannot describe "nothing targeted" two different ways later (trap 4) — the `\n` in it is deliberate and pre-existing: the float's `EmptyText` doesn't wrap, the peek's own empty line does, and the same string is correct on both without either host reformatting it.

**The gauge bar is deliberately flat (`Share = 1.0`) for every target row**, not computed. Target rows mix an observed count ("4 this session · 40%") with a bare wiki rarity word ("common") — two units in one list — and a proportional bar across them would assert a comparison neither number supports. Same reasoning `HudExpandPeek.Buffs` already uses to sort an unknown duration last rather than guess it forward; named here as a decision rather than an oversight, since a future editor could otherwise "improve" it into a misleading bar.

**Verified:** build green; full unit suite 3,595/3,595 (3 new/rewritten `HudExpandPeekTests` cases: target-with-rows, no-target, target-with-nothing-known-yet). Not run against the live app this pass (no screenshot — the fixture's target-drops path needs a live `/consider` or a kill, which `shoot.ps1`'s standard fixture doesn't stage; flagging per trap 22/23 rather than claiming a look I didn't take).

---

### 2. Options/cog IA (lock 6) — re-audited against the now-landed OE-7 + OE-8; found CURRENT, not stale

**Read this as a verification pass, not a fresh design** — the ask's own premise ("much of Settings/cog is redundant under the shell now") predates a lot of cleanup that already happened inside OE-7 and OE-8 themselves, and the honest finding is that most of what the ask worried about has already been swept by the PRs that created the redundancy in the first place.

**Checked and found CURRENT (already reworded/removed in OE-7's own diff, not left behind):**
- `BreakoutPresentation.Heading`/`DismissTip`/`Blurb`/`Note` — all rewritten "by itself" language, `ReEnableRoute` and both toast strings already deleted (confirmed by reading the class doc, which narrates its own history accurately: *"`ReEnableRoute` used to live beside this and is gone (OE-7)"*).
- `SettingsHudView.DoubleClickChipsBlurb` — already describes hover-peek/click-pin/↗-pop, not the old "double-click brings it back" wording that predates OE-1/OE-7.
- The Edit HUD context-menu tooltip's family list (mez/slow, spawn, watch alerts, buffs) — grepped `HudChipFamily` (four members: Mez, Spawn, WatchFire, Buffs) against the tooltip text in `MainWindow.xaml:68`. Matches exactly; not stale.

**Checked and confirmed correct, not stale despite looking suspicious at first pass:** HUD-adjacent tooltips (`HudEditChip.cs`, `HudChipRowWindow.cs`) still say "Options → Alerts & chips". This looked like a shell-era staleness candidate (the shell's own Settings room calls itself "Settings", four tabs, no "Cards & windows" tab name) — but `SettingsRoom`'s own doc comment is explicit that `OptionsWindow` is "not retired, not renamed and not reshaped" by the shell's existence, and it stays reachable from the widget's own context menu (which is what a player minimized to the HUD chip row actually right-clicks) regardless of whether the Evolved shell happens to also be open. The v1 destination these tooltips name is real and current; I did not change them. Recorded so the next pass doesn't re-open the same question from scratch.

**Free-drag (OE-8) added NO Options surface to reconcile, by design.** Grepped for any settings row or checkbox referencing HUD row/panel position — none exists, and none should: the only way back from a parked chip row is Edit HUD's "Follow the HUD again" control, which is deliberately NOT in Options (trap 2's tombstone: nothing is persisted that a settings screen would ever need a row for — NaN-means-slaved is the whole state).

**Not yet applicable, and said so rather than invented:** the 7:10 AM lock-6 entry (below, same file) named items 3 (expand direction) and 4 (right-click-hide beyond Buff) as blocking a full ruling. Neither has shipped as of tip `3a1e8654` — grepped `HudChipRow.SetMuted` call sites: still only `HudChipRowWindow.cs`'s Edit-HUD wiring, no live-chip right-click hook. So there is nothing in Options today that duplicates or is made stale by either — when they ship, the sweep this ask asks for applies to them, and doesn't today.

**No code change made for this half of the ask.** The honest ruling is "checked thoroughly, found current" — inventing a change to justify the pass would be worse than reporting a clean bill, and the two prior SR-series passes (I-11, the OE-7 sweep itself) already did the actual re-laying this ask is asking for a second time.

---

**Not filed to `HELM-FEEDBACK.md`:** nothing here is a LIVE ASK — the Loot peek fix is a bounded, tested, in-scope fix on an owner-signed lock, and the IA half found nothing to change. Logged in `DECISIONS.md` and `docs/TestPlan.md` per the standing rule.

— Bevel (Claude Sonnet 5)

---

## 2026-09-07 ~11:35 AM CT — OE-8 affordance faces, ruled: cursor+tooltip drag tell, un-park chicklet ships as built (Bevel)

**Priority:** `approved` — soft face-only round, PR #381's review item; not a re-gate on the
Helm-signed OE-8 mechanism (`a0a82076`).
**Place:** `src/EQBuddy/HudChipRowWindow.cs` (constructor, the `Cursor`/`ToolTip` lines just
above the `WrapPanel`), `src/EQBuddy/HudExpandWindow.cs` (constructor `Cursor`/`ToolTip`
lines above `_chrome`, plus the new `OnHoverCursor` method beside `OnEdgeMove`),
`src/EQBuddy/HudEditChip.cs:96-158` (`Unpark` — read, not changed), `MainWindow.xaml:697-724`
(`ResizeGrip`/`HeightGrip`, the precedent this reuses), `src/EQBuddy/FramelessResize.cs` (the
other place this app already answers "how does a player discover a drag/resize edge").
**Source:** `BEVEL-FEEDBACK.md` ~11:10 AM CT ("OE-8 free placement is BUILT; two affordance
faces are yours at PR #381's review, and one scope call is flagged for you"), against tip
`f413ac87`. Verified in source and run through `dotnet build`, the unit suite (3,594) and
`scripts/check.ps1` (all green) on this branch.

---

### 1. The drag tell — `Cursor` + `ToolTip`, not a drawn handle, and it is now built (small, face-only diff)

The ask named three options: a cursor change on hover, a grip dot on the panel's header, or a
one-time Edit-HUD hint. **A cursor change is the right one, and it isn't actually a new idea in
this app** — `MainWindow.xaml`'s `HeightGrip` (`Cursor="SizeNS"` + a static `ToolTip`) and
`ResizeGrip` (`Cursor="SizeNWSE"` + a static `ToolTip`) already answer "how does a player
discover this drags/resizes" exactly this way, and `FramelessResize`'s own doc comment cites
that pair as precedent for every other frameless window in the app. A grip dot or a hint line
would be a THIRD visual vocabulary for the same fact on a row that already carries chicklets —
competing with them for the same few pixels, which the widget's own chrome never asks a grip to
do. Cursor + tooltip costs nothing drawn and matches what a player who has used the widget's
edges has already learned.

**Shipped, in the two files, in the same shape as the precedent:**
- `HudChipRowWindow`: static `Cursor = Cursors.SizeAll` (the whole box is one drag target, lock
  1) and a `ToolTip` naming the gesture and the way back, in the same words
  `SettingsAlertsView.cs:643` already uses for the spawn-chip explanation ("Edit HUD… → Follow
  the HUD again").
- `HudExpandWindow`: the same default, plus `OnHoverCursor` — a plain (bubbling) `MouseMove`
  handler that calls the SAME `ResizeZones.Hit` the press handler (`OnEdgePress`) already uses,
  and swaps to `Cursors.SizeWE` over the two vertical zones, `Cursors.SizeAll` elsewhere. Guarded
  on `_grip.Dragging`/`_edge != None` so an active gesture doesn't flicker as the pointer crosses
  back over an edge it's no longer negotiating.

**Why this needed a tiny code change rather than staying a paragraph here:** the tell has to
exist to be reviewable at all — "nothing" was the finding, and a cursor is not something Options
copy or a doc can stand in for. It touches zero persistence: no new field, no new write, no
change to `HudDragGrip`'s park/write mechanism or `ScreenGuard`. Verified: build green, all 3,594
unit tests green, `check.ps1` all green.

**Not built, and flagged rather than assumed free:** an automated assertion for the cursor swap.
This suite has no synthetic-pointer-hover harness that reaches WPF's cursor resolution (the
existing `drag-verify.ps1` drives real drags/resizes, not hover-only moves), so this is `Manual`
in `docs/TestPlan.md`, the same tier as the un-park chicklet's own P3 phase below. If a future
pass wants this automated, `WidgetDump` could carry the resolved `Cursor` as a fact the way it
already carries `hudRowGrip`/`hudPanelGrip` — named here as a follow-up, not built.

---

### 2. The un-park chicklet — ships exactly as built, no face change

Read `HudEditChip.Unpark` in full against this app's own standing rules rather than against
taste, and it already clears every bar a review would apply:

- **Two DIFFERENT vectors for state vs. click** (`Pin` emblem, `Undo` button) — not a repeated
  shape, which is #148/#166's failure and the reason the doc comment explicitly rules out
  reusing the mute toggle's `Check`/`Close` or the order nudges' chevrons.
- **Drawn always, dimmed via `IsEnabled` + `Opacity` when nothing is parked** — trap 17 (an
  `IsEnabled` with no visual is invisible in this app's styles) and trap 59 (a way back that only
  appears once you're already lost is one nobody has seen before they need it) are both honored
  by construction, not by afterthought.
- **`Pin` is also `QuestsView`'s tracked/untracked vector, and that is not a collision.** The two
  never appear on screen together, and the underlying idea — something is anchored to a place —
  is the same fact told twice, not a contradiction (unlike #148/#166's failure, which was two
  identical shapes claiming two different things on ONE surface at once).

**No change recommended.** The mechanism is face-agnostic per the ask's own framing, and the face
it already wears is the one this review would have designed from scratch.

---

### 3. Scope call on lock 3 (vertical edges only) — agreed as shipped

The panel's body is a peek capped at five rows with the ↗ carrying the full list; a height a
player could take by dragging a horizontal edge would promise rows the panel has no way to
deliver. Reading "any corner or any edge" against what the body actually is, vertical-only is the
correct reading, not a shortfall — closing the flag raised at review rather than filing it as a
gap.

### 4. Mouse-down→up click move — no design question here

Disclosed, not asked. It protects a chicklet's own due-timer from a reach-past-to-drag, which is
the row's own correctness concern, not a face one; nothing about which edge of the click a
gesture lands on changes what a player sees or learns.

---

**Not filed to `HELM-FEEDBACK.md`:** nothing here changes a signed default — the OE-8
persistence/park/resize mechanism (`a0a82076`, Helm-signed plan #377/#378) is untouched byte for
byte. This is a face-only PR on top of it.

**Logged:** `DECISIONS.md` (today, "Bevel — OE-8 affordance faces"), `docs/TestPlan.md` (new row
under the OE-8 rows), reply filed in `BEVEL-FEEDBACK.md` this date.

— Bevel (Claude Sonnet 5)

---

## 2026-09-07 ~7:10 AM CT — Owner LOCK (six, amended): toast kill / free-drag / expand direction / right-click hide / expand-for-all / Options cleanup — one-liners vs OE-1 (Bevel)

**Priority:** #4 (Buff only) and #3 (if #2 is deferred) = `approved`, ship as one-liners; #1 and
#5 = `approved` IA but ONE seat together, not two (see §1); #2 = **not a one-liner — recommend a
Fable seat**, it reopens a Helm-signed architecture; #6 = `waiting` on #1/#4/#5 landing (I can lay
out what survives, not finalize it, until those ship). None of the six is `needs-david`.
**Source:** `HELM-FEEDBACK.md` ~6:32 AM CT lock (items 1–5) + ~6:35 AM CT amend (item 6), both on
`channel/owner-next-evolved-pass-20260907` tip `e3680dc5`; `BEVEL-FEEDBACK.md` this date, same
tip. OE-1 built (`claude/oe1-minibar-expand-20260906`, this file's item 4, owner locks 1–10 in
`HudExpandTests`). OE-1b is PR #351 (e2e-red) — see the flag at the bottom of this entry; I could
not read a clean statement of "OE-1b's four locks" from its own diff.
**Checked:** `src/EQBuddy.UI.Shared/HudExpand.cs` (whole file), `HudChipRow.cs` (whole file),
`src/EQBuddy/HudChip.cs:147`, `HudChipRowWindow.cs:196-212`, `HudExpandWindow.cs:196-206`,
`HudEditChip.cs`, `src/EQBuddy/BreakoutHost.cs` (whole file), `src/EQBuddy.UI.Shared/
BreakoutPresentation.cs:105-123`, `src/EQBuddy/SettingsHudView.cs:447-529`, `AppSettings.cs`
(`DisabledBreakouts`, `DoubleClickChipsToggleBreakouts`, `MiniStats`, `HudChipOrder`,
`MutedChipFamilies`), `WhatsNew.json:1143`, and this file's own item-4 fallback-door table (the
Live pre-design, tip `54fc1dc3`) for the Motes/Loot/Kills destinations item 5 names. **Not run
against the live app** — this is a source-read pre-design, same caveat as the 2026-09-06 round.
**Out (confirmed honored below):** no OptionsWindow retirement invented beyond what items 1/6
name, no TEL, no Play Console, no player-door code, no seat naming (that's Fable's).

---

### 1. Toast kill — the real scope is bigger than deleting a string, and it is the same change as #5

**Place:** `src/EQBuddy/BreakoutHost.cs:74-94` — the six `BreakoutKind` floats' ✕ handler adds
the kind to `settings.DisabledBreakouts` (persistent) and, when `DoubleClickChipsToggleBreakouts`
is off (the default), fires `main.AlertTile.ShowAlert($"{k} breakout hidden — re-enable in
{BreakoutPresentation.ReEnableRoute}")` — a shipped, `WhatsNew.json:1143`-documented feature.
Compare `src/EQBuddy.UI.Shared/HudExpand.cs:236-241` (`WindowClosed`, OE-1's float close): no
disable, ever — the chip just goes back to hover/click, which is verbatim the ask's own words
("close → chip/bar available; hover/click restores").

**The nag is not universal today** — `BreakoutHost.cs:91` already suppresses it when
`DoubleClickChipsToggleBreakouts` is on, because a double-click already undoes the close. The
persistent-disable-on-✕ exists to fix discussion #45 (a float that silently reappeared every
minimize after being explicitly closed — "whack-a-mole"). **Delete the disable without changing
anything else and #45 comes back** for anyone who leaves that setting off (the default): the six
breakout windows auto-show while minimized, they are not summoned from a bar chip the way DPS/
HPS/Progress are, so "closed" currently has nowhere honest to persist except that flag.

Making close safe here, the way OE-1 made it safe, means these six get the SAME summon model
item 5 asks for — a bar chip you hover to peek / click to pin / ⧉ to pop — not a one-line delete.
**Items 1 and 5 are the same change described from two ends.** Do #5's plumbing for a
`BreakoutKind` and #1 falls out of it for free; do #1 as a string-delete alone and #45 reopens.

---

### 2. Free-drag chips — reopens a Helm-signed architecture, not a UI tweak

**Place:** `HudChipRow.Placement` (`src/EQBuddy.UI.Shared/HudChipRow.cs:400-409`) and
`HudExpandWindow`'s positioning (`src/EQBuddy/HudExpandWindow.cs:196-206`, its own comment: "same
`HudChipRow.Placement` arithmetic"). **Both windows are slaved companions with no geometry of
their own and nothing persisted, recomputed from the widget's position every tick** — the SA-2
hosting amendment, Helm-signed 2026-09-05, and `CLAUDE.md` trap 2's tombstone names exactly this:
"there is nothing left to save in a `Closed` handler... the three files retired together." That
rule exists **because of** #122/#152 — a saved x/y walking a window up the screen across reopens.

"Park anywhere" asks for a saved position again, on the surface built specifically to need none.
Re-solving that safely means re-answering trap 2's whole question set for these two windows: what
happens on reopen with a stale or off-screen point, a monitor that's gone, the widget moving away
while the row is parked, a profile reset. That is design work with real edge cases, not a drag
handle. **Zero drag code exists in either file today** (grepped `Drag`/`MouseMove`/
`MouseLeftButtonDown`/`CaptureMouse` — no hits), so there's no partial implementation to extend.

Vs OE-1b: its own title ("drag-to-place under-bar panel") reads as scoped to placing the panel
near/under the bar, not arbitrary screen position; this ask's "not stuck in a fixed horizontal
under-bar line" removes the anchor rule entirely, which is wider. **Recommend a Fable seat, not a
Bevel one-liner** — the product question ("one saved spot for the whole row, or one per chip? one
per family?") is mine to pre-design once it's scoped as its own item; the persistence/reopen
mechanics need a plan.

---

### 3. Expand direction (up/down/left/right) — same placement code as #2; shrinks to a one-liner if #2 waits

**Place:** the same `HudChipRow.Placement` call site, `HudExpandWindow.cs:203-206`. Today it is
**always** directly under the widget, left edges aligned, flipping ABOVE only when it would run
off the bottom of the work area (`HudChipRow.cs:404-408`) — there is no left/right concept
anywhere in this arithmetic.

If #2 ships free placement, direction is naturally "which way the panel opens from wherever it's
parked" — one more argument to the same function, cheap once a parked point exists. If #2 is
deferred (my recommendation), direction from the CURRENT fixed anchor is a small, real pre-design
on its own: four screen-edge-aware flip rules instead of today's one (up must not paint into the
widget; left/right need a horizontal work-area check `Placement` doesn't do today). **This is the
one item of the five that is genuinely one-liner-sized on its own** — I'll write the four-rule
version the same day #2's fate is decided, whichever way that goes.

---

### 4. Right-click hide — Buff is a clean reuse; Mez/Spawn/WatchFire collide with a shipped gesture

**Place:** `src/EQBuddy/HudChip.cs:147` — `MouseRightButtonUp` is already wired to `onDismiss` for
any chip that carries one. `HudChipRow.cs:453-474` (Slow) and `:497-511` (WatchFire) chips DO
carry a per-instance `OnDismiss` today — **right-click on those already means "dismiss this one
instance,"** shipped and tested (the Slow tooltip literally says "· right-click to dismiss").
`HudChipRow.cs:546-572` (Buff) and the wake half of Mez (`:422-447`) carry **no** `OnDismiss` —
right-click on those chips does nothing today.

The mute mechanism the ask wants reused already exists, keyed at exactly the right grain:
`HudChipRow.SetMuted`/`IsMuted`/`MutedChipFamilies` (`:226-274`), already wired to a live control —
`HudEditChip.Build(family, ..., onMute: () => HudChipRow.SetMuted(...))` inside "Edit HUD…" mode
(`HudChipRowWindow.cs:204-212`). So the ask is precisely "call that same `SetMuted` from a live
chip's right-click, not only from inside Edit mode" — **clean and small for Buff**, because
nothing on that chip owns right-click yet.

Doing the identical thing on Mez, Spawn or WatchFire would silently change what right-click means
on a chip a player already learned — a per-instance dismiss becoming, or competing with, a
whole-family hide. That's "silent no-ops are broken" with the switch flipped: the gesture used to
do one thing and now maybe does another, with nothing in a diff or a screenshot to show it.
**Carve-out: ship right-click-hide on Buff first** (zero collision); Mez/Spawn/WatchFire need
either a second gesture (a small ⋯ affordance, or a modifier key) or an explicit owner call that
knowingly trades away per-instance dismiss for kind-hide on those three. Also confirm the way
back: `HudEditChip`'s unmute row already exists for this, but it lives in Edit HUD mode, which a
player who has just hidden their only Buff chip needs a way to *find* with nothing left on the row
to click into it from — worth a one-line check before shipping, not a redesign.

---

### 5. Expand-for-all trackers — the enum already promises this; the destinations are the real work

**Place:** `HudExpandTarget` (`src/EQBuddy.UI.Shared/HudExpand.cs:8-21`) is written to grow — its
own doc comment: "this enum grows and nothing else about the model does" (owner locks 8/9). So
the peek/pin/pop **mechanism** is not new work per target; a **destination to pop to** is. Checked
against this file's own fallback-door table (item 4 below this one, the Live pre-design, tip
`54fc1dc3`) for the exact list the ask names:

- **Motes, money** — land in the Progress room's Wealth tab, the same shape Progress/xp already
  uses (a room TAB destination, not a card's own window). Should be clean **if built via the room-
  tab route.** The earlier "no fallback door exists, full stop" finding for Motes was about
  `MotesCardView` having no `OpenWindow` of its own — a different question (removing the v1 card),
  not this one — but the executor should re-confirm rather than assume it's free.
- **Kills, deaths — named carve-out, keep OUT of this pass.** This file's own table already
  blocks Kills & Drops for an unrelated reason: 3 of the 4 real destinations (World's Drops tab,
  a search/disposition lookup, Gear's "what dropped for you") don't exist yet, in as many words.
  Nothing about this pass changes that.
- **Loot** — same shape as Kills: one destination (`GearRoom`), plausible, not verified this pass.
- **Pet, procs** — no destination named anywhere I read this pass. Flag as **open, not blocked** —
  needs a source check before promising either way.
- **Watch, spawn, mez/slow, buffs — the one place I'd push back on doing this verbatim.** These
  are `HudChipRow` families, not `HudExpandTarget` mini-bar slots, and they are a different kind
  of object already: ephemeral, self-dismissing chicklets that exist only while the alert is live,
  not an always-on glance number with a persistent detail behind it. "Pop" for these would open a
  RULES screen (Settings → Alerts) or nothing, never a live-data float — mapping the identical
  three verbs onto them is plausible but is its own small design question, not a slot in a growing
  enum. Worth its own one-liner once named as its own item, not folded into this one silently.

---

### 6. Options/Settings cleanup — mostly waits on 1/4/5; one concrete stale pair found already

**Place:** `src/EQBuddy/SettingsHudView.cs:463-529` (`BuildBreakouts`) is "Options → Breakout
windows" — one checkbox per `BreakoutKind`, ticked = "this floating window may open while
minimized." **This does NOT go stale.** Reading `BreakoutHost.cs`'s ✕ handler against it: closing
a float today calls `settings.DisabledBreakouts.Add(k)`, which is exactly what unticking this same
checkbox does — the ✕ and the Options row are **one setting today**, not two. If item 1 removes
the ✕'s write to that flag, this checklist becomes the ONLY intentional on/off control for these
six windows, which is item 6's own framing ("what remains") applied correctly — **keep it,
unchanged**, once #1 lands.

**What IS stale, and only once #1 ships:** `BreakoutPresentation.ReEnableRoute`/`HideTooltip`
(`:110-123`) and the two format strings in `BreakoutHost.cs:92-93` — they exist only to name the
Options row a ✕ just silently wrote to, and that write is exactly what #1 removes. Delete them in
the same PR as #1, not later; a leftover tooltip pointing at a route nothing writes to any more is
`SCRIBE.md`'s own trap-20 shape (a sentence describing a reader that no longer exists).

**Checked and found no other stale artifact:** the widget's context menu (`MainWindow.xaml`) has
no per-breakout toggle to clean — Options is the only home for `DisabledBreakouts`. `SA-4`'s
`MutedChipFamilies` (the chip-row mute) has no Options row at all today, only the Edit-HUD-mode
control — so items 4/5 add no NEW Options surface to reconcile, only the in-context one already
built. **I cannot finish this item today.** #6's own ask is to re-lay what stays "vs chip/expand/
free-drag/right-click-hide controls" — those controls don't exist yet, so there is nothing to lay
out against beyond the one pair above. Recommend #6 rides as a checklist item on whichever PR(s)
implement #1/#4/#5, not its own seat: "does this PR leave an Options row or a tooltip describing a
control this PR just replaced?" — answerable in one grep per PR, the way I answered it here.

---

### Soft max ≤3 — my read

**#4 (Buff only) and #3 (if #2 waits) are true one-liners today** — small, no architecture
reopened, ship those first. **#1 cannot ship safely as a one-liner without doing #5's plumbing for
at least the six `BreakoutKind` stars** (minus Kills/Deaths, minus the four chip-row families) —
treat #1+#5 as ONE seat, not two. **#2 is the one I'd hand to Fable as its own plan** — it reopens
a Helm-signed no-persisted-geometry rule and needs its own trap-2 re-solve, not a paragraph here.
**#6 rides along on #1/#4/#5's PRs as a checklist line**, per above — it is not a fourth seat.
That reads as two real seats (the #1+#5 combo, and #2 once Fable scopes it) plus #3/#4 as small
riders on whichever lands first — inside soft max ≤3 without inventing a fourth.

**Flagging separately, not one of the six:** PR #351 (OE-1b, e2e-red) is not just stale — diffed
against current `origin/main` it is a whole-file mojibake re-encoding of `BEVEL-FEEDBACK.md`,
`HELM-FEEDBACK.md`, `FABLE.md`, `HELM.md` and `DECISIONS.md` (every em dash → `ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â`,
trap 60b's exact signature), and it also reverts real, already-merged source
(`SpellbookFile.cs`, `SpellbookBuffTests.cs`, parts of `BuffTracker.cs` — OE-5's spellbook-buff
work, merged in #361) because the branch predates that merge. I could not read a clean statement
of "OE-1b's four locks" out of it. **Recommend closing #351 rather than rebasing it** — a rebase
would try to replay a whole-file corruption onto four days of channel history — and that Fable/
Helm re-derive the four OE-1b locks from wherever they were last stated cleanly, or re-ask David,
rather than from this PR's diff.

---

## 2026-09-06 ~5:49 PM CT — Owner Evolved iterative feedback (David) — four pre-designs (Bevel)

**Priority:** (3) Home/shell recovery = `must-fix`; (1) buff density = `approved` IA, (1) timer
correctness = `waiting` (no reproduction yet); (2) `approved`; (4) `approved` (owner-locked
intent, IA below is the shape, not a re-litigation).
**Source:** David via Helm, Sun 2026-09-06 ~5:49 PM CT (`HELM-FEEDBACK.md` this date). Evolved
local shell, Play Console off. Verified against tip `75faf324` + this branch's own capture
commit `ae806b13` — no product code on this branch, so "verified" below means read-from-source,
not run-and-observed.
**Checked:** full source read of `BuffTracker.cs`, `MainWindow.xaml.cs` (buff/xp/breakout
paths), `HudChipRow.cs`, `HudGlance.cs`, `HudBarView.cs`, `SessionStats.cs`,
`ProgressPresentation.cs`, `QuestLedgerStore.cs`, `LogParser.cs`, `HomeReadout.cs`,
`HomeRoom.cs`, `IShellRoom.cs`, `ShellWindow.xaml.cs`, `ShellHost.cs`, `ShellPages.cs`,
`ThemeHost.cs`, `BreakoutWindow.xaml.cs`, `AppSettings.cs`, `DECISIONS.md`. Not run against the
live app this pass — no screenshot for item 1's timer complaint either; that half stays a
reproduction request, not a diagnosis.
**Out (confirmed honored below):** no OptionsWindow retirement, no TEL, no Play Console, no
player-door code, no seats invented for Fable.

---

### 1. Buffs — density IA + a timer-correctness checklist (not a fix, because there's nothing to reproduce yet)

**Place:** `src/EQBuddy/MainWindow.xaml.cs:1406-1519` (`RenderBuffs`) is the surface the owner
means — a `Grid` per buff, two columns (name `Star`, clock `Auto`), `Margin(0,1,0,1)`, **no
icon**. This is a different surface from the HUD's buff-*expiring* chip
(`src/EQBuddy.UI.Shared/HudChipRow.cs:513-578`, `BuffChips`), which already carries an icon +
draining gauge but only appears once a buff enters `BuffWarnWindow` — it is not the always-on
list. Do not fold these into one recommendation; they solve different jobs (glance-while-fading
vs. full roster).

**Density — verified, there is no compact mode today.** `AppSettings.cs:175-204,597-625` has
exactly two buff-adjacent settings — `BuffTimersExpiringOnly`, `BuffWarnSeconds` — neither
touches row height or layout. The row is already tight (1px margin, no icon) and still reads as
"needs to be far more compact" to the owner, so the lever isn't margin — it's **shape**. The
current list is one column, one buff per row, full width. Recommend: **wrap the roster into a
multi-column chip grid** (name + timer as one compact chip, `WrapPanel`-based per trap 25 —
never a horizontal `StackPanel`, which clips silently) so N buffs fill the card's width before
they grow its height. This is the same move the HUD chip family already uses for a *different*
buff surface; reusing `HudChipRow`'s visual language (without its warn-window gate) gives the
full list a consistent, already-compact building block instead of inventing a second chip style.
**Hypothesis, not yet designed in full:** whether the "est" suffix and duration source (below)
still fit inside a chip at that density, or need to move to a tooltip — flag for the follow-up
pass once a screenshot exists to measure against.

**Timers — do not change anything yet; get the reproduction first.** No trap entry, no
`CLAUDE.md` line, and no test names a buff-timer bug — this is a first report, from memory, with
no screenshot. What IS verified, so the next report can be triaged fast: `BuffState.ExpiresAt`
is set **once**, at landing (`BuffTracker.cs:203-230`), from (in order) a per-character *learned*
duration persisted from a past natural fade, else the wiki `BuffDurations.json` base times the
player's Spell Casting Reinforcement AA bonus (own casts only), else — if the spell can't be
resolved to one candidate — the **longest** matching candidate, marked `Estimated = true` ("est"
suffix). Every render just re-evaluates `(ExpiresAt - now)`; it never re-derives the duration.
So a "wrong" timer has exactly four possible sources, and the report needs to say which:
(a) an `Estimated` duration picked the wrong (longest) candidate for an ambiguous spell name,
(b) the Reinforcement AA rank read for that character is wrong, inflating every one of that
character's own-cast buffs by a fixed ratio, (c) a *learned* duration from a previous fade was
captured off a laggy fade event and is now systematically short/long, (d) a **refresh before
expiry** (recasting a buff that's still up) doesn't reset `ExpiresAt` correctly — worth checking
`OnLanding`'s re-entry behavior specifically, since that's the one path this reading did not
trace end-to-end.
**Recommend:** ask David for one screenshot with the buff name, the shown countdown, and — if
knowable — the actual remaining time, before any code changes. This is exactly the shape
`SCRIBE.md`/`BEVEL.md`'s own rule already states: a hypothesis is a place to look, not a fact,
and here there isn't even a hypothesis to test yet, only four candidate mechanisms.

---

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

### ~~3. Home / shell recovery — real gap, but it is bigger than Home~~ — TAKEN 2026-09-06 (OE-2)

Built on `claude/oe2-open-eqbuddy-20260906`. Every source claim held on inspection, including
the one that set the priority: `ShellHost.Show` had exactly one caller in the whole app, gated
on `EQBUDDY_SHELL`. **The re-scoping is what made the fix right** — Home cannot be closed
independently, so a Home-recovery affordance would have fixed nothing; what closed was the
whole `ShellWindow`. The door is the widget's `Open EQBuddy…` context-menu row (first in the
menu, above `EQBuddy Mobile`), through `ShellHost.OpenDoor` — a row rather than a hotkey
because nothing is bound by default (trap 59), and not a HUD-bar control because lane W owns
`HudBarView` while OE-1/OE-3/OE-4 run. It names no address, so it comes back on **Home** and
FRONTS an already-open shell rather than sending a reading player there. The three comments
that promised the door, and four more claims of the same family, are updated in the same
change. **One premise you own moved underneath a signed rule** — `RetiredCardsTests`' reason
for refusing to name an Evolved room was "`EQBUDDY_SHELL` is the only way into one today" —
and the question is in `BEVEL-FEEDBACK.md`; the rule itself is untouched.

---

### ~~4. Mini-bar tracked item → anchored expand → still-poppable window~~ — TAKEN 2026-09-06 (OE-1)

Built on `claude/oe1-minibar-expand-20260906`: the `ThemeHost` read was right and needed no
fourth state, the hosting is a slaved companion window (`HudChipRowWindow`'s SA-2 shape —
neither of the two options your "not designed here" named, because both of them measure), and
single-click is the primary path with the opt-in double-click untouched. Owner locks 1–10 are
asserted one-test-per-lock in `HudExpandTests`. One intersection the locks do not cover — a
hover on a chip that is NOT the pinned one — was decided as peek-and-revert and is flagged for
your look in `BEVEL-FEEDBACK.md`.

---
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


> **Rotated 2026-09-20 -- DRA-258 (DRA-144 F9).** The 20 dated pre-designs that sat
> under this heading (2026-08-16 .. 2026-09-05), and the nine Helm-signed review
> entries that followed them (2026-08-16 .. 2026-08-23), were moved verbatim to
> [`docs/ops/claude-archive/channels/2026-Q3/BEVEL.md`](docs/ops/claude-archive/channels/2026-Q3/BEVEL.md).
> This heading stays live because it is an undated container: a date cut cannot see
> it, so the cut was made at `###` inside it and the orientation notes above are
> still current. Nothing above this line was touched.
