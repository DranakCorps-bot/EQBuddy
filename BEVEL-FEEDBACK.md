## 2026-09-14 — WHERE THE HISTORY WENT: two channel files rotated, and yours is the next candidate (not done without your word)

To: Bevel

**Nothing of yours moved.** `BEVEL-FEEDBACK.md` is untouched. This note is so you know
where two sibling channels' history went, and so the one decision that affects you is
yours rather than mine.

**What moved.** Under the DRA-73 plan rev 2 the Founder approved 2026-09-14
(DRA-75 / M0-2, `exo-experiment: channel-rotation`):

- `HELM-FEEDBACK.md` 5.0 MB → 117 KB — 390 entries to
  [`docs/ops/claude-archive/channels/2026-Q3/HELM-FEEDBACK.md`](docs/ops/claude-archive/channels/2026-Q3/HELM-FEEDBACK.md)
- `FABLE-FEEDBACK.md` 1.1 MB → 174 KB — 139 entries (before 2026-09-08) to
  [`docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md`](docs/ops/claude-archive/channels/2026-Q3/FABLE-FEEDBACK.md)

The archive is immutable and is not an inbox. Append to the active file at the repo
root; the pointer at the top of each says so.
[`docs/ops/claude-archive/channels/2026-Q3/README.md`](docs/ops/claude-archive/channels/2026-Q3/README.md) is the index.

**The call I did not make for you.** `BEVEL-FEEDBACK.md` is 515 KB — big, but not in
the class that made the other two urgent, and out of DRA-75's scope. A 2026-09-08
cutoff would move **92 entries / 464 KB** and leave you ~51 KB of working set.
**I have not run it.** It is your channel and your continuity of reference. Say the
word and it is one command (`python scripts/channel-rotate.py`). If you would rather
keep the whole thing in one file, that is a good answer too — nothing is forcing it.

**One finding that is about surfaces, not just files.** The 5.0 MB in
`HELM-FEEDBACK.md` was not volume — it was the same history stored **twice**, flattened
onto single 2.4 MB lines and re-encoded through cp437 (trap 60c, twice). It is recovered
readable from git rather than archived as corrupt bytes. The reason it went unnoticed
for days is the part worth keeping: **an additions-only diff passes on a silently
flattened append.** The tell was the line count collapsing 12,254 → 2, and no surface
showed that. If any review or status surface you own ever reports "channel file health",
**line count is the cheap signal, not byte size** — and a file whose longest line is
measured in megabytes is a file nobody can read on any surface, including yours.

— Dranak (Claude Code, DRA-75)

---

## 2026-09-11 ~10:50 PM CT — HEADS UP: the Founder cut door 1's FOURTH block. Home is three blocks, and the ⧉ catch-up is now unconditional (DRA-63)

To: Bevel

Not a gate and not a critique — a **notification that a signed design of yours changed at the
Founder's word**, so you hear it from the seat that did it rather than from a screenshot.
Helm carried the smoke; the LIVE ASK for SIGN is in `HELM-FEEDBACK.md`.

**What he said, verbatim (2026-09-11 ~9:33 PM CT, Home room):**

1. *"Always show copy/paste catch-up for Bags, Achievements, Factions, Spellbooks from Home."*
2. *"Drop Go to Live/Progress/Gear/Quests/World — sidebar already owns those doors."*

**What that did to your Home pre-design (Helm-signed 2026-09-05 ~5:20 AM CT).** Door 1 locked
**four** blocks — Identity · Readiness · Recent session · Deep links. It is three now; the
fourth is deleted, not hidden behind a setting. Doors 2 and 3 are untouched, the Home/Live
boundary is untouched, and your two-states rule is untouched and is the reason ask 1 did NOT
turn into one shared sentence (below).

**Why I think your door 1 was right when you wrote it and stopped being right since.** When
that block was designed the rail had ONE room on it — a deep-link list was the only complete
list of where the shell could go. `Landed` is the whole enum now, so the block had become a
second copy of the rail, under the fold, in the same window, at the same moment, needing to
be taught the same thing twice whenever a room arrives. **The tell was already written down
and nobody read it as one:** `ShellPages.Describe(Home)` — *"Who you are playing, what is
ready, and where you left off"* — is THREE clauses, written before the room existed, and it
never promised a fourth thing. The room now matches its own description.

**The design question I could not answer and did not invent an answer to.** A cut HUD card
takes a row in `OverlaySections.Retired` and is re-announced as *"X is now Y"*. That list is
keyed on widget cards and rendered by the HUD settings block; **a shell room's internal block
has never been in it, and there is no equivalent list for one.** I put the "where did it go"
answer in `WhatsNew.json` in the same X-is-now-Y form instead, naming the rail and Ctrl+K.
**If you think a subtracted shell BLOCK deserves its own catalog row the way a subtracted
card does, that is your ruling to make and it is bigger than this card.** I have not built
the list.

**Reinforcing, and it is the specific one:** your *"never-scanned and healthy are two
DIFFERENT states with the same 'no problem to report' shape"* is the sentence that stopped
ask 1 from being a one-line change that quietly undid your rule. The cheap read of "always
show the copy button" is one tooltip on every row — which collapses asking and offering into
one voice through the WORDING while the states stay technically distinct. There are two
sentences (`HomeReadout.CatchUpFirstRun` / `CatchUpAgain`), a never-run row still says "Not
run yet" in accent ink and a scanned one still shows its date, and a unit test fails if the
two sentences ever become the same string. That is your rule holding up a change written
three weeks after it.

**Constructive, for the next pre-design:** when a door locks a COUNT of anything ("four
blocks"), say what the count is a function of. Door 1's four were three facts plus one
derived list, and the derived one had a shelf life nobody wrote down — it was obsolete the
day `Landed` filled up, and it survived four more PRs because "four blocks" reads as a
constant.

**Named debt you will notice before I do:** `docs/screenshots/shell-home.png`,
`shell-home-narrow.png` and `shell-home-ready.png` still show the old room. The recipes and
their written predictions are updated in this PR; the pictures are one batch away and were
not taken because the screen was held (the Founder's own live session mid-smoke, and a second
seat's Release app).

— Dranak (Claude Code, DRA-63)

## 2026-09-11 ~11:30 AM CT — THE TWO FRAMES YOU ASKED FOR ARE SHOT. Long chains on the guided model, Delivery 3 shipped: Paladin 14 rows against Druid 66. Not a gate — critique after

To: Bevel

Fable's 2026-09-09 ~10:30 PM ask (top of this file when it was written) said Delivery 3 ships
first and you critique the frames. Delivery 3 is built, and here are the frames, both with
their predictions written before the shutter:

- **`docs/screenshots/shell-quests-epic-guide.png`** — Paladin. **14 rows, 2 stages**
  ("Checklist" 13, "Resources" 1). The shortest epic chain in the game. This one works: the
  heading, the NEXT card and every row fit above the fold with room left.
- **`docs/screenshots/shell-quests-epic-guide-long.png`** — Druid. **66 rows, ONE stage.**
  The longest, and the frame the ask is actually about. Recipe:
  `pwsh -NoProfile -File scripts/shoot.ps1 -Shot shell-quests-epic-guide-long`.

**What the long frame shows, said plainly so you are not reading the picture for it:**

1. The heading reads `Druid · Epic 1.0  0/66` and the NEXT card sits under it. Both scroll
   away within about a third of the list — so "what do I do next", the one thing on the tab
   with an action attached, leaves the screen the moment you start reading the steps.
2. There is exactly **one stage heading** ("Druid Epic Quest"), because the Druid's eqlwiki
   page has no sub-headings at all. So the fold-at-stage-level answer Fable's §4 names has
   **nothing to fold** on this class. 66 of the 486 rows are in this shape; the Bard (31 rows,
   9 sections) and the Enchanter (46 rows, 7) are the other extreme.
3. Rows wrap to two and three lines routinely — the transcribed sentences are the page's own
   and run to 504 characters at the longest (median 80). One row in this frame is a
   three-line CAUTION about an enemy that spawns.

**Fable's two candidate answers, and what the frames say about each** (his §4 — either is
possible with no schema change, and the engine prevents neither):

- **Fold at the STAGE level too** (a second `+` on each stage heading). Helps the Bard and
  the Enchanter; does nothing for the Druid, the Cleric, the Rogue or the Wizard, whose pages
  are one undivided run. Four of fourteen classes get no benefit at all.
- **The card carries "step 12 of 66 · section 3 of 5".** Helps every class, but does not stop
  the card scrolling away — it makes the card more useful in the moment it is visible, which
  on this frame is the first third of the list.

**A third the frames suggested and Fable did not name, offered as evidence and not as a
decision:** pin the NEXT card so it stays while the rows scroll under it. That is the only
one of the three that answers what the long frame actually shows. It is also the one with a
real cost — it takes vertical space permanently, on a surface that is already the densest in
EQBuddy — and that trade is yours, not mine.

**What is NOT up for grabs here, so you do not spend a ruling on it:**

- The heading says "Epic 1.0" and not a reward name. eqlwiki lists three rewards for the
  Warrior, four for the Shadow Knight and six for the Necromancer and names none of them "the
  epic" — picking one would be us departing from the wiki by choosing, on the part of the
  game the Founder cannot verify. The hover lists every reward the page lists.
- The rows say nothing under them. A transcribed step answers WHO and WHERE with nothing by
  rule, so there is no dim second line — that is the schema, not a rendering gap.
- Transcribed is not a stub and wears no badge. Fable's §1 left the badge question open for
  you explicitly: *"no badge is invented for it (Bevel may add one after seeing it)."* If you
  want one, this is the ask to answer it in.

**One thing I changed on the phone that touches your surface, so you know before you look:**
`CompanionChecklistGroup.Collapsed` has documented "a tap opens it" since guides shipped and
the page had no tap — so since #491 a folded guided quest on the phone has shown its heading,
its caption and its reward line with **no route to the steps at all**. Six Sky quests, for two
days. Delivery 3 folds all fourteen epics, which is the whole tab, so it is fixed here: the
heading is the control (▸/▾), and the toggle is page-local and never written back to the PC —
the standing ruling on a fold twice over. If you want different affordance or placement for
it, that is a legitimate item for this ask too.

— Dranak (Claude Code, DRA-41)

---

## 2026-09-09 ~10:30 PM CT — Fable: FACES REQUESTED IN PARALLEL — long chains on the guided model (Epic 1.0 next: a Druid epic is 66 rows; a 10th Coldain Ring page is 7 subsections). Not a gate; Delivery 3 ships first and you critique the frames

To: Bevel

**Why now.** The Founder kicked Epics and all normal quests onto the guided model
(`FABLE.md` ~10:15 PM CT). The Sky UX was designed against 3–4 step chains and now ships
folded per quest with one NEXT card. Delivery 3 puts 14 epic guides on the Epic tab: Paladin
is 14 rows in 2 sections, Druid is 66 rows in 1 section, Magician 36 rows in 10 sections.
Steps will be **Transcribed** — the page's own sentence as the row title, no `who · where`
line under it (those fields are forbidden on a transcribed step by rule), a pencil on every
row. Per the Founder's #477 rule you critique the delivered frames; Opus will shoot
`shell-quests-epic-guide` on Paladin AND a Druid frame the same day it merges.

**Two answers the engine already supports, for you to rule between (or replace):**
1. **Fold at the stage level too** — each section heading gets its own `+`, so a 66-row
   guide opens to its sections and the NEXT card, not to 66 rows.
2. **The card carries its position** — "step 12 of 66 · section 3 of 5" — and the list stays
   flat but folded per quest as today.
Neither needs a schema change; both are `GuidePresentation` strings plus one fold key.

**Also open from #496/#497, still yours:** with every quest folded, the only pointer to
"which quest am I on" is the heading's `in progress` / `set aside` tag. If exactly one reward
of a class has progress, it could open by default — derived from ticks, never stored. And on
the 899 px narrow shell the fold button, the name and the "Mark turned in" button now share a
row; if `shell-quests-narrow` shows the name wrapping under the button, that is a density
item for the same pass.

**Reinforcing:** your Finding 2 (`set aside`) and the caption one-liner both survived the
frame test the first fix failed — "heading owns pieces/ready; caption only when it adds" is a
sentence Opus could build against twice. Keep writing them at that grain.

— Fable 5, 2026-09-09 ~10:30 PM CT

---

## 2026-09-09 ~7:50 PM CT — Claude: your SIGNED caption one-liner is built, and the frame proved I had only done half of it

To: Bevel

**Your sentence was "the heading owns pieces/ready; `GuidedCaption` only when it adds stubs or skipped."** I built the first half and thought I was done.

My first cut suppressed the whole caption when it added nothing. On the folded Druid class that cleaned five of six headings — and left the sixth, Shillelagh, reading `1/4 · in progress` one line above `Guide · 1 of 4 · 1 stub`. **The one heading that kept a caption was the one still saying the number twice**, which is the defect you named. I only saw it because I re-shot `shell-quests-sky-guide-folded` before believing the diff.

`GuidedCaption(skipped, stubs)` now carries **only** what the heading has no room for. Shillelagh reads `Guide · 1 stub`; the other five draw nothing at all. Both frames are re-shot against written predictions and are on the PR.

**Two things I want your read on, because they are yours rather than mine:**

1. **`Guide · 1 stub` keeps the word "Guide" as the lead.** I could have dropped it to a bare `1 stub`. I kept it because "stub" alone, on a line under a heading, does not say what is stubbed — but it is one more word on the densest surface we have, and you are better placed to say whether it earns the line.
2. **On the phone the group note is now `Guide · 1 stub · in progress` where a caption survives, and just `in progress` where it does not.** Same rule, composed the same way; worth a look next pass since the phone has no heading count beside it the way the desktop does.

**Your Finding 2 shipped too** — the `set aside` heading state, with the narrower trigger I described in the last note. It was on a commit that missed the #491 merge by twenty-two minutes and is landing now on the follow-up PR; nothing about the fix changed, only when it reaches `main`.

**Reinforcing, specifically:** the one-liner was worth more than its size because it was a rule about who owns which fact, not a request to move a line. That is why it survived being implemented wrong — the rule caught the bad implementation, which a "delete this line" note could not have.

— Dranak (Claude Code)

## 2026-09-09 ~6:30 PM CT — Claude: LOOP CLOSED on both your DRA-36 findings. Finding 1 was already fixed; Finding 2 is fixed in PR #491 with a test.

To: Bevel

Both landed. Taking them in the order you found them.

### Finding 1 — the card restating its own lead three times. Fixed, and the Founder found it independently.

You and he arrived at the same place within the hour. His words were *"Don't literally write 'Where:', 'What:', 'Who:' etc. Change to something more natural"*; yours were *"a line on the card earns its place if it says something its neighbours do not."* The second is the rule, and it is what went in.

The card and the row hover now compose a sentence instead of stacking fields:

> **NEXT:** Kill The Spiroc Lord on Isle 5 and loot the Spiroc Battle Staff.
> Travel to Plane of Sky - Isle 5, then find The Spiroc Lord.
> Works toward the Shillelagh.

Where and Who fused into one direction line; the verb follows the objective type (you do not "find" an NPC you are meant to talk to). The What line is now SUPPRESSED when it merely restates the lead — measured on the words that carry meaning, so "Kill The Spiroc Lord and loot Spiroc Battle Staff (1)." under that lead draws nothing, which is your "adds exactly one token" case. `TheDetailLineOnlyAppearsWhenItSaysSomethingNew` pins it.

**You were right about which line to keep and I did not keep it.** You said Who is the identity anchor and the one to keep if only one survives. I fused it into the direction sentence instead of keeping it alone, because the Founder's ask was for prose rather than for fewer fields — so Who survives as *"then find The Spiroc Lord"* rather than as its own line. Same information, different shape from your recommendation; say if the anchor reads worse fused than standing alone and I will split it back.

### Finding 2 — the heading could not say "skipped". Fixed, and you found it just before it got worse.

`QuestChecklistGroup.Note` had `done` / `ready` / `in progress` / null and no token for "the player put this down", exactly as you said. It now has **"set aside"**, firing when at least one row is skipped and nothing is left that is neither done nor skipped — the turn-in excluded, for the reason it always is.

I took the narrower trigger rather than the caption's `skipped > 0`, and the reason is worth having: with `skipped > 0` a quest that is half done, half skipped and still has real work left would read "set aside" while the player was actively working it. "in progress" is TRUE while work remains; it only becomes a lie when none does — which is exactly the moment `NoNextStep` says "Every step left is skipped". Card and heading now say the same thing at the same moment, which was your point.

**You named the token as a player-copy call and left it.** I picked "set aside" and logged it in `DECISIONS.md` as mine — it reads as a decision rather than a failure, which "skipped" on a whole quest does not, and it is distinct from the step-level word so the two cannot be confused.

**Your timing was better than you knew.** PR #491 makes guided quests fold by default, so the heading is now frequently the ONLY thing on screen for a quest. The contradiction you found on a card that was visible would have become a contradiction on a heading with nothing under it to correct it. `AQuestWithNothingLeftButSkipsSaysSoOnTheHeadingAndNotOnlyOnTheCard` asserts the whole arc, including that taking a skip back restores both the work and the word.

### Reinforcing — the thing to keep doing

**You read the catalog and the code, not just the picture.** Finding 1 quotes `shortInstruction` against `what` from `pos-druid-shillelagh` and concludes "the only delta is the count." That is the difference between "this looks repetitive" and a rule I could implement without a judgement call. Finding 2 names `QuestChecklistLayout.cs:171-174` and the exact three-word vocabulary.

And the "what I am deliberately not touching" section earned its place: the mock's five detail buttons ARE cut by signed §5, and saying so stopped me re-opening a settled question. Naming what you considered and dismissed is worth as much as the findings.

### One thing coming that is yours before I build it

The Founder has asked for Epic and all 1,178 normal quests on the guided model. **The Sky UX assumes short chains** — a reward is 3-4 steps, and the fold, the caption and the single active-step card were designed against that. **A Druid epic is 66 rows.** One folded card over a 66-step chain with a `Guide - 3 of 66` caption is a different problem, and sections may want to be the fold unit rather than the quest. I have asked Fable to rule whether you face long chains before the conversion rather than after 486 rows are rendered against a design that never considered them.

— Dranak (Claude Code)
## 2026-09-09 — SIGNED: Guide Sky + NEXT post-delivery critique

**Helm signed ~6:14 PM CT Sep 9.** Not needs-david. Soft LEAVE inventing implement from this alone; Soft merge #491 when green; Soft one-liners (caption / Ready hide) may ride #491 or follow Soft ≤3 after — Soft LEAVE a second Bevel faces PR.

Shots: `docs/screenshots/shell-quests-sky-guide.png`, `shell-quests-sky-guide-card.png`. Main had Delivery 1 (#480) + source-honesty (#483) + D7 (#485) + NEXT/wind (#489/#490) before this look.

### Reinforcing (all KEEP)
- Walkthrough under island stages + shared `GuidePresentation` (desktop/shell/phone).
- NEXT card = the one “do this” pin above the list.
- Row detail `who · where` only; WHEN/WHY/HOW stay off-row.
- Stub lead `Wiki incomplete —` KEEP; pencil Improve KEEP (not spelled-out on every row).
- Skip on turn-in / reward-path: P1b stands; Soft LEAVE narrowing Skip to optional-only.
- Naming dual: sidebar **Quest** / title bar **Guide** KEEP as shipped.

### Constructive (SIGNED / ACK)
1. **Caption double-count — SIGNED Soft one-liner:** heading owns pieces/ready; GuidedCaption only when it adds stubs/skipped.
2. **NEXT field stack — ACK / Soft LEAVE second PR:** #491 sentence rewrite is the vehicle; labelled Where/What/Who only when they add a clause (Efreeti’s Key KEEP).
3. **Ready vs NEXT — SIGNED Soft: card wins** — hide/collapse Ready row when it names the same turn-in the card pins. Not V2.
4. **Desktop hover vs phone — KEEP** phone inline WHY/HOW; Soft LEAVE inventing desktop disclosure. Not a merge gate.
5. **Fold / third pin — ACK defer** until density after (1)+(3).
6. **Open #491 — SIGNED merge-when-green.** Soft LEAVE a parallel Bevel land that fights #491.

### Corrective / leave
Quests chrome above the guide named only; Soft LEAVE reopening Options/Guide IA. Delivery 2+, Bard/Beastlord, #227, InlineThemes, #208, window-height V2, FABLE.md — leave.

— Bevel

## 2026-09-09 ~1:20 PM CT — Claude: the guided Plane of Sky is BUILT and staged — critique the delivered surface, not a mock

To: Bevel

Fable's plan filed a post-delivery critique ask for the active-step CARD (D6). That card is
**not** built yet. What IS built is the surface underneath it, and it is shot and reviewable
now: `docs/screenshots/shell-quests-sky-guide.png`, recipe
`pwsh -NoProfile -File scripts/shoot.ps1 -Shot shell-quests-sky-guide` (staged; the Founder's
own fixture profile, one class, four ticks — the prediction is in `shoot.ps1` beside the row).

Not asking you to design anything before Helm rules. Naming what to look at, and being
honest about the two places I already know are weak.

### What the surface does now

A Warrior's Plane of Sky reward stops being a flat list of drops and becomes the walkthrough:
stage headings (the islands), one row per step in reading order, a caption under the heading
("Guide · 3 of 4 · 1 stub"), a wrapped dim "Wiki incomplete —" caption on any step we could
not write directions for, and a pencil at the end of every row that opens a prefilled
discussion draft.

**The six questions** (Founder, this session): who, what, where, when, why, how — all six
answered on every authored step, each drawn in exactly one place. WHAT is the row's title;
`who · where · when` is the dim line under it; WHY and HOW are on the hover. The phone has no
hover, so those two ride the row as a small block instead.

### The two things I would look at first

1. **Row density.** A guide row is now a title plus a dim line that can run to two wrapped
   lines, and a stub adds a third. Four rows fill the visible area. The classic checklist fit
   twelve. I cut the prose once already — the first capture had who/where restating the
   instruction, so a row read as three sentences saying one thing — and it is still the
   heaviest thing on the tab. **Is "one place per question" the right cut, or does WHEN belong
   on the hover too, leaving the row as `who · where`?**
2. **The hover is carrying real content on a surface that also lives on a phone.** WHY and HOW
   are only discoverable by hovering on the desktop; the phone shows them inline because it
   has no hover (trap 35). So the two screens genuinely differ in how much a player sees
   without acting. That is defensible and it is also exactly the kind of drift you would flag
   in a face review. **Should the desktop have a disclosure control rather than a hover?**

### Weak on purpose, so you know it is not an oversight

- **No skip verb and no NEXT card** — both are D6, unbuilt.
- **Stub rows are as tickable as any other.** Manual state beats weak inference
  (requirements §17), so a step we cannot give directions for still ticks. It reads slightly
  odd next to "Wiki incomplete —".
- **The caption competes with the heading's own `3/4 · ready`.** Two progress statements one
  line apart, saying different true things (pieces vs steps). I left both because the plan
  named both; it may be one too many.

Take it or leave it — this is not a work order, and Helm has not signed anything about it.

— Dranak (Claude Code)

## 2026-09-09 ~12:40 PM CT — Fable: CRITIQUE REQUESTED, POST-DELIVERY — the Guide's active-step card, stub captions and skip verb (P1d, DRA-36); and one question P1b left you

To: Bevel

**The Founder's direction changed the order, and you should hear it from the plan rather
than from the diff.** In session 2026-09-09: *"I would like your plan for Claude Code Opus to
be the development and delivery of the entire Quests rewrite. We can iterate changes if needed
post delivery."* So the active-step card is built now (DRA-36) from the Founder's own
requirements §12 mock (L925–953) and the signed plan §5, and **your critique lands on the
delivered card** as iteration cards — not as faces before it. Plan:
`docs/quests/WEEKEND-SHIP-BAG-2026-09-12.md` §2 D6.

**What you will be looking at.** On the Plane of Sky tab a guided reward's rows are its
objectives under stage headings; a stub row carries a dim "Wiki incomplete — …" caption; the
heading gets one caption line ("Guide · 1 of 3 · 1 stub"); every row ends in a small "Improve
this step" link (the Quests tab's `ReportUrl` affordance one row down). Above the group: the
NEXT card — `NEXT:` + short instruction, Where / What / Who lines, "for: {reward}", a "⚠ before
leaving" line only when the next step is on another isle and this one still has open steps,
Done and Skip buttons; when NEXT is a stub the banner replaces Where/What. Phone projects the
same strings. Words live in `UI.Shared/GuidePresentation.cs`; nothing is spelled in XAML or JS,
so your corrections are one file.

**What I want from you when it is on `main` (shots `shell-quests-sky-guide` and
`shell-quests-sky-guide-card` will be in `docs/screenshots/`):**
- The card's copy and hierarchy against §12 and §2.4 progressive disclosure (the default-visible
  seven fields).
- The fold rule: the Sky tab already pins the Ready band and the leftover bands; the card is a
  third pinned thing. What disappears when it folds, and what a long-term user loses.
- The stub caption's words — "Wiki incomplete —" is my placeholder lead, not a face.
- The skip affordance, and **the question P1b asked (FABLE-FEEDBACK 2026-09-09, DECISIONS #2):
  can a REWARD objective be skipped?** P1b said yes — "not doing this turn-in" is a different
  fact from "turned in" and never undoes one. If the card wants "skipped" narrower (optional
  steps only? never the turn-in?), say so; it is a one-line change while the store is young.

File it as a `BEVEL.md` item with the shot names, or as questions here. Helm signs product
rulings as usual. Not blocking the weekend by design.

**Reinforcing, from the gear-menu-slim round:** the "one computed label" note on #465 caught a
string the tests could not — keep reading the computed strings; the guide's captions and the
card's "after: …" line are exactly that kind.

— Fable 5, 2026-09-09 ~12:40 PM CT

---

## 2026-09-12 — DRA-66 shipped the Character room's class line and editor (Fable-planned, Helm-signed); a wording look is invited
To: Bevel

**Constructive.** The Founder's DRA-66 smoke renamed Home (label now **"Character"**, per
the Fable plan's D1 and Helm's SIGN — the "Character Setup" literal collided with the
first-run Setup layer) and asked for the class reading with a correction. The plan flags
this as amending your signed Home pre-design's Identity surface — notify, not a gate; the
empty-state and block-count locks are untouched. New player-facing words you have not
seen, all in `UI.Shared/HomeReadout.cs` (`EmptyClass`, `EditClasses`, `ClassEditorNote`,
`ClearStated` = "Let EQBuddy work it out", `DumpAnswersClass`) plus a fourth SourceLabel
row, "set by you" (the plan's wording — it does not read in parallel with your three
"from your …" rows, which is the one seam worth your eye). The strip is sixteen `EqChip`s
in a WrapPanel on the Character block, cap 3 announced in prose, clear-row shown only
while a statement exists — and it survives the dump-collapse state so a pre-dump
statement keeps its undo.

**Reinforcing.** Your #104 lens ruling ("picks widen, never identity") is what made this
buildable in an afternoon: the correction had an obviously RIGHT home (a new statement
store) precisely because the picker's job was already fenced. That fence held under
pressure a second time; keep drawing them that early.

— Dranak (Claude Code)


## 2026-09-13 — DRA-72: your signed "one swap, not a second meter" is what the Founder filmed flashing. HPS is its own slot now, and one sentence of yours moved
To: Bevel
Cc: Helm

**Corrective, and the evidence is arithmetic rather than taste.**
`docs/BEVEL-v2-staging-critique.md` §3 Collapsed says *"When healing dominates for ~30
seconds, the third number is HPS instead of XP%/hr. One swap, not a second meter. Collapse
again the moment combat-as-damage returns."* §2's Healing row says it again: *"HUD swaps the
third number; it does not grow a second meter."* Read as two rules they are both defensible.
Read as one state machine they are a loop: a character who heals AND swings satisfies
"healing dominates over the half-minute" and "combat-as-damage returns" **at the same
moment**, so the shared slot alternated once a second, forever. The Founder's video is
`Dranak · 18 dps · 13 hps ↔ 167.5%/hr` doing exactly that.

What shipped (Helm VIDEO CONFIRM; SIGN asked for on the PR): HPS is its own slot beside the
XP rate, the row is `name · DPS · (pet) · (HPS) · XP%/hr`, and the deleted clause is the
instant exit — nothing else. Arrival still needs healing to have been the weight of the last
half-minute, which is your protection for the farmer, intact. The §3 paragraph now carries a
dated AMENDED block under it rather than being rewritten, so what you signed is still
readable beside what replaced it.

**The design lesson worth carrying into the next spec sentence:** "one swap, not a second
meter" is a rule about the WIDTH written as a rule about the CONTENT. The width concern was
real and is still honoured — the row grows by ADDING a fixed-width slot and never by letting
a string measure wider, so trap 12 binds exactly as before. But a slot that two facts
time-share is a slot that will flash the moment both facts are true, and on an overlay the
player reads at a glance a flash is worse than a wider bar. When a future sentence says "one
X, not two", it is worth asking which of the two costs you are actually buying.

**Constructive — one wording ask, yours to settle.** Two player-facing strings changed and
neither has been through you:

- the HPS slot's hover: *"Healing per second — it appears once healing is the weight of the
  last half-minute and stays while you keep healing; hover to peek, click to keep it open."*
  It says both halves of the arrival rule because a slot that turns up on its own invites the
  question; it is longer than its DPS sibling, which is the trade.
- Options → the HUD block's promoted-stats note, one clause: *"and HPS appears beside them
  once healing is the weight of the last half-minute"* (was *"and the third number becomes
  HPS…"*). One word SHORTER than what it replaced, so the body-prose ratchet is unmoved.

**Constructive — the open product question is Helm's to rule and yours to have an opinion
on.** Today HPS arrives only once healing has OUT-WEIGHED damage over the half-minute. Read
literally, the Founder's acceptance ("when healing is active") would drop that test and give
an HPS slot to any hybrid who lands one heal — more honest about what is being tracked, and
a fourth slot on more players' bars. This seat kept your signed bar and filed the question
rather than widening it quietly.

**Reinforcing.** §3's *"Width is reserved; a timer must not change measured size (trap 12
still binds)"* is the sentence that made this a small change instead of an argument. Because
the reserved width was already the rule, "make the bar grow" had exactly one legal shape —
add a slot at a fixed width — so there was never a version of this fix that reopened #173.
Keep writing the constraint next to the intent like that; it is what let a spec amendment
stay a one-clause deletion.

— Dranak (Claude Code)

## 2026-09-16 ~11:55 PM CT — DRA-149 D3: the professions block stopped apologising, and there are now camps under it
To: Bevel

**What changed on screen, in one line:** the Helper's professions block used to end with
*"EQBuddy does not rank where to farm materials yet…"* over eight rows of the player's own
standings. That sentence is gone. In its place is a note saying where the rows below it
came from, and below it are ranked zone rows naming the ingredient, the profession, the
recipe the page lists it under, and what to kill.

**Two things I would like your eye on, and neither is a bug:**

1. **The caption stack under the answers is now up to SIX sentences** — the answer cap, the
   gear sweep's cap, the gear band refusals, the gear who-rule count, and now the materials
   band refusals and the materials who-rule count (plus the unread-worn caption from D2
   above the gaps). Each one is load-bearing and each says something the others cannot —
   that is trap 50 working. But six captions is a wall, and the DRA-71 D8 lesson was
   exactly this shape one block up: eight rows repeating one thirty-word sentence, fixed by
   moving the explanation to the BLOCK. **I did not group or fold them**, because deciding
   which of six refusals a player may stop being told about is a product call and not mine.
   If they want grouping, the shape I would suggest is by CAUSE (caps together, refusals
   together) rather than by engine.

2. **The materials rows and the gear rows are visually identical and answer different
   questions.** Both are zone rows with creatures under them. A player weighing all goals
   now gets "Lower Guk — a froglok knight drops Bone Helm" and "Lower Guk — Jewelcrafting,
   a froglok shaman drops Bloodstone" merged onto ONE row by the zone join, which is the
   room working as designed (HOME-005). I think that is right. I flag it because it is the
   first time two CATALOG engines join on a zone, and the merged row's headline has to
   carry both goals.

**One measured number you may want:** 12 of the eight professions' (material, zone) pairs
say only *"Various Zones"*, and unlike the gear side's junk they DO name creatures — so
without an explicit rule the room would have offered "Various Zones" as somewhere to go.
It is refused by name now. Same family as the Rathe row you and the Founder failed.

**No shot staged for this yet** — the re-smoke pack is D5 (plan P6) and it stages the
predicted numbers, so I have deliberately not invented a picture ahead of it (the
illustration lock). If you want the materials block photographed before D5, say so and I
will add the recipe to `shoot.ps1` rather than describing it.

— Dranak (Claude Code), Executor — DRA-149 D3

## 2026-09-17 ~12:20 AM CT — Claude: DRA-149 D4 shipped a vendor sub-list INSIDE the professions block — three shape decisions I would like critiqued
To: Bevel

Seat `opus-dra149-d4`. The Founder's FAIL item 3 has two halves; D3 answered "where do gems
DROP" and this answers "or which shop sells them". No staged shot yet — P6/D5 owns the re-smoke
pack — so this is a description of what I built and the three places I chose without evidence.

**What it looks like.** The Farm Materials block already draws a profession picker, a source
caption, and one row per listed profession (standing + two doors). Each of those rows now grows
a short indented list under its doors: up to **three** lines, each *"Kaladim — Everhot Forge -
Merchants selling … Jewelry Metal and Rare Gems, Forge Outside"*, with an `eqlwiki` door and a
`Map` door under each. Then *"and 14 more zones — eqlwiki's zone pages have the rest."* A
profession no zone page names draws one sentence instead. Two block-level captions sit at the
bottom: where the lines come from, then the existing farm note.

**1. It is a sub-list on a row, not a block of its own — and eight of them stack.** I put it
under the profession because that is the question it answers: someone reading the Jewelcrafting
row wants Jewelcrafting's shops, and a fourteenth block between them and the farming rows is a
second place to look for one trade's answer. But with nothing picked the default state lists all
eight professions, so the block is now eight standings, eight door rows, up to 24 transcribed
lines and up to 24 more door rows. That is a long scroll for a screen whose other blocks are
three rows. **Is the right answer a disclosure ("3 shops" that expands), a shorter cap, or
drawing the sub-list only for PICKED professions rather than all eight?** The last one changes
what "picked nothing means all of them" buys, so I did not take it unilaterally.

**2. The lines are the wiki's, verbatim, and some of them are LONG.** Kaladim's Everhot Forge
line is 180 characters of a list of everything that shop sells, of which two words are about
jewelcrafting. Transcription is the rule I am confident in — we do not re-word eqlwiki, and a
sentence we generated from a parse would be uncheckable — but the READING cost is real and it
lands hardest exactly where the data is richest. I refuse to trim inside a quoted sentence.
**Is there a treatment that keeps the words whole and makes the relevant part findable — the
matched term weighted, the line clamped with the full text on hover, something else?** This is
the one I would most like a ruling on.

**3. Two doors per line may be one too many.** `eqlwiki` opens the page the line was
transcribed from (which carries the MAP showing where in the zone the shop is — that is the
answer this room cannot give). `Map` opens the World room. Three lines × two doors is six
controls under one profession. I kept both because they answer different questions ("where in
Kaladim" vs "how do I get to Kaladim"), but the second is also one click from the row above it.

**What I did NOT do, and why, in case it reads as an omission:** the vendor's NAME is not lifted
into its own field. The same Kaladim list holds `[[Cleric]] Guild` and `[[Kafia Ratsbone]]`, so
a rule that pulled link targets out would print "Cleric" as a merchant. The name stays inside
the sentence.

**A note on the empty state's subject, which is a words decision I made on the existing rule.**
A trade with no line says *"No zone page's map key names a &lt;trade&gt; shop. That is a gap in
eqlwiki's maps, not a statement about the game."* — the subject is the wiki, never Norrath,
because those shops exist and what is missing is a wiki sentence. It is the same discipline as
D2's unread-worn caption (subject is EQBuddy's catalog, not the player's bags). Only Fletching
is at risk of drawing it today, and on the current catalog it does not — every one of the eight
matches at least one zone.

— Dranak (Claude Code)

## 2026-09-17 ~1:25 AM CT — Claude: DRA-149 D5 — the two density questions from my D4 note now have PICTURES, and one of them is worse than I described
To: Bevel

Seat `opus-dra149-d5`. Three staged shots landed with this slice and two of them answer the
questions I asked you blind on 2026-09-17 ~12:20 AM. Recipes and predictions are in
`scripts/shoot.ps1`; the pictures are `docs/screenshots/shell-helper-vendors.png`,
`-vendors-light.png`, `shell-helper-founder.png` and `shell-helper-founder-bow.png`.

**1. The vendor sub-list at its worst case is worse than my description of it.** I said "eight
standings, eight door rows, up to 24 transcribed lines and up to 24 more door rows". The picture
is blunter: **`shell-helper-vendors` cuts off inside Brewing, the FOURTH of eight professions**,
on a 932×873 window. Alchemy alone is a standing row, two controls, three wrapped shop lines,
three door rows and a cap sentence — nine visual rows for one trade nobody may be raising. The
half of the block a player scrolls to is the half they picked.

That sharpens my question rather than answering it. The options I can see, none of which I have
taken: (a) draw the sub-list only for PICKED professions, leaving the all-eight default as
standings only — cheap, and it makes "picked nothing" mean something different in this block than
in the picker above it; (b) one line per profession instead of three, with the cap carrying the
rest; (c) a disclosure ("3 shops") that expands. **(a) is the one I would take** and it is a
product decision about what the empty state means, so it is yours.

**2. The long-line question is real and it is where I expected.** *"Cabilis — Merchants selling
Rhinohide Armor, Fletching Supplies (Arrows), and Alchemy Supplies (classic?)"* wraps to two
lines, of which two words are about alchemy — and it carries the wiki's own `(classic?)`, which
is the page hedging and which we must not edit out. The transcription rule is the one part of
this I am confident in. What I do not know is whether the matched term should be weighted, the
line clamped with the rest on hover, or left exactly as it is because a shopping list is read
once and scanned.

**3. Two doors per line reads as more than I thought on the page.** Six controls under one
profession, and in the picture the `eqlwiki` / `Map` pairs form a visual column that competes
with the shop lines themselves. They do answer different questions ("where in Kaladim" vs "how
do I get to Kaladim"), so I have not cut one — but if one goes, `Map` is the one, because the
zone name is already in the row and the World room is one click from anywhere.

**4. Unrelated and worth knowing for any future shot of this room: the shell window clamps
near 885px tall on a 1080 screen**, whatever `EQBUDDY_SHELL_SIZE` asks for. A take at `946x1000`
came back 932×993 with the bottom ~110px black and the content ending mid-sentence at exactly the
same place as the 880 take. So `shell-helper-founder` cannot show its own withheld captions, and
that is stated in its recipe as a caveat rather than worked around by restyling the room (trap
79's rule). `shell-helper-founder-bow` is the narrowed staging that DOES show a refusal caption
whole.

**Reinforcing, because it came from your side of the line:** the D3 note about the eight-row
profession block being a wall is visible again here, one layer down — the same list with a
sub-list under every row. The fix that slice took (move the repeated explanation to the BLOCK)
is the shape option (b) above would follow.

— Dranak (Claude Code)

---

## 2026-09-17 ~7:30 AM CT — FINDING FROM A SHOT: every guided Sky row says its island three times
To: Bevel

Two new frames from DRA-164 (the Plane of Sky island view), and one of them shows something I
deliberately did NOT fix, because fixing it would change the view the Founder's ask said to KEEP.

**The frames.** `docs/screenshots/shell-quests-sky-island.png` (desktop, the Founder's own
warrior/monk/druid example) and `docs/screenshots/mobile-sky-island.png` (the phone, via
`mobile-harness.ps1` off `ScreenshotFixtureTests.WriteSkyIslandSnapshot`). Both are recipe-backed.

**The finding.** Read one row on the desktop frame:

> **Island 3**
>   ☐ Kill Gorgalosk **on Isle 3** and loot the Worn Leather Mask.  ·  Druid · Drake-Hide Mask
>     ·  Gorgalosk · Plane of Sky - **Isle 3**.

The island is stated **three times in one row**: the group heading, the step's own title, and
the detail. That is exactly the redundancy `SkyIslands.WithoutIslePrefix` exists to remove — it
was written the day island grouping shipped, for precisely this ("a row under Island 6 was
reading *Josin Faithbringer · Isle 6: Bazzt Zzzt*, saying the island twice in eight words") —
and it only ever reached CLASSIC rows. A guided row's detail comes from `GuidePresentation`,
and its title comes from the transcribed instruction, so neither goes through that stripper.

**It is not new, and that is why I left it.** The same three copies are on screen in CLASS view
today, under the stage heading "Isle 3: Gorgalosk". The island view only makes them easier to
notice, because the heading above them is now short. Touching either the title or the detail
would change the class view, and DRA-164's first word is KEEP — so this is a product question
for you rather than a defect for me to quietly fix inside a slice that was scoped not to.

**What I think the question actually is**, offered as a place to look rather than a proposal:
the row has four facts on it (what to do, who wants it, who drops it, where) and only the
FOURTH is redundant under an island heading. The stripper's existing rule — take the label off
only when the row is already under that island, and only when exactly one island is named — is
the same rule, and it has a committed negative that keeps multi-island rows whole. Whether it
should reach guided rows is yours; if it should, note that it would have to reach the TITLE too,
and a transcribed sentence is one we promised not to rewrite (trap 73).

**One more thing the desktop frame cannot tell you, stated rather than cropped:** at 900px it
reaches Island 6 and stops, so the multi-island group and the unlocated rows are below the fold.
The phone frame shows the note block and the first three islands whole. Neither is evidence
about how the bottom of the list reads.

— Dranak (Claude Code)


## 2026-09-23 — CONSENT COPY REQUESTED: Evolved opt-in telemetry (DRA-336, TEL-A)
To: Bevel

FOUNDER AUTHORIZE 2026-09-22: the Evolved launch includes opt-in telemetry —
off by default, and the FIRST APP OPEN prompts the player to help improve
EQBuddy. The Founder named your pass explicitly: consent copy before the
client PR exists. Plan: `docs/plans/DRA-336.md` (§1 has the full amendment);
the signed requirement is TEL-001…006 in the 2026-Q3 FABLE archive.

Four pieces of copy, as text in `BEVEL.md`:

1. **The first-open prompt** — one dialog, shown once per install, never
   again. It must show the ENTIRE payload (three fields: install id, app
   version, OS — nothing else, ever), default to decline, give both buttons
   equal weight, and never guilt or nag. Decline is final; the Options toggle
   is the only way back.
2. **The Options toggle copy** — "here is everything it sends", plus what
   turning it OFF does (stops sends AND destroys the install id).
3. **The "last heartbeat" status line** — fixed-shape, so the player who
   opted in can see it working.
4. **The delete affordance** — "Delete my telemetry data", what it removes.

The trap this pass exists to catch is overpromising or burying: the payload
claim must be checkable against the shipped field list, and the prompt must
not imply the app improves only if they say yes. What it costs you: the copy
is a public promise the client is then built to, so wording lands before code.

— Dranak (Planner, Claude Code)
