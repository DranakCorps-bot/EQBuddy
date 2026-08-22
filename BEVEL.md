# Bevel inbox

Findings for Claude, not a wor
k order. **Claude: take an item, then delete 
it** (or leave
only what is still planned).


Bevel joined on 2026-08-21, introduced by Dav
id alongside Scribe. The first thing it was
p
ointed at is a review of discussion #222 (EQB
uddy Mobile's pull-to-refresh with one card
s
elected).

**What this file is for:** whateve
r Bevel produces that Claude should act on �
� reviews,
design critique, defects, second o
pinions. One heading per finding.

**What we 
do not yet know, and Bevel should say in its 
first entry:** what it specialises
in. Scribe
 compiles community input and is excellent at
 it; its guesses about what the
CODE contains
 have been wrong five times running, which is
 fine because it labels them as
hypotheses. K
nowing where Bevel is strong is what stops us
 treating the wrong half of its
output as loa
d-bearing. Say plainly what you are for.

## 
Suggested shape for an item

Copied from `SCR
IBE.md`, which has been through several round
s of this and works:

- **Priority:** `must-f
ix` (player-facing break) · `approved` (Davi
d already said yes) ·
  `waiting` (blocked o
n a reporter or a log) · `someday` (real ask
, not this gate)
- **Place:** where you think
 it lives. **Label it a hypothesis unless you
 verified it.**
- **Source:** the discussion/
issue number, the reporter, the date, and the
 app version from
  the footer if there is on
e.
- **Ask / Finding:** the reporter's or you
r own words, verbatim where possible. **The
 
 verbatim quote is the single most useful fie
ld.** #226 was found by grepping the exact
  
sentence a player wrote, in a file nobody had
 suspected.
- **Already shipped:** what exist
s today that bears on it.
- **Checked:** what
 you actually ran or read, and what you did n
ot. "Not grepped this run"
  is a good answer
; a confident guess dressed as a fact is not.


## Things worth knowing before reviewing th
is codebase

- `CLAUDE.md` is the orientation
 and carries a **42-entry trap list**, every 
entry a bug
  that reached a release. Read it
 before asserting anything about how the app 
behaves.
- **Both UIs, always.** WPF (`src/EQ
Buddy`) and Avalonia (`src/EQBuddy.Avalonia`)
 ship
  together; a fix on one lane only is h
ow #122 and #152 reached Linux.
- **Shared de
cisions live in `src/EQBuddy.UI.Shared` and `
src/EQBuddy.Core`** and are
  framework-free 
by test. If a finding is "these two surfaces 
disagree", the fix is
  usually a shared modu
le, not a patch in each.
- **`docs/screenshot
s/` is committed and current** — real captu
res of real windows against
  a seeded fixtur
e. It is the fastest way to see what the app 
actually looks like without
  running it.
- T
he gates are `pwsh -NoProfile -File scripts/c
heck.ps1` (2,256 unit + 264 Avalonia) plus
  
a separate 18-test E2E suite that launches th
e real app.

---

## What Bevel is for
- **Pr
iority:** approved
- **Place:** process — t
his inbox. Not a code path.
- **Source:** BEV
EL.md stub 2026-08-21 (“Bevel should say in
 its first entry what it specialises in”).

- **Ask / Finding:** Bevel is product/UX. Vis
ual and interaction critique so Dranak Corps 
products look and work like commercial softwa
re, not hobbyist vibe-coding. I review roles 
and relationships: which surface owns which j
ob (widget glance, theme window review, phone
 second screen), what disappears when somethi
ng folds, and whether a player can still do t
he job that made them open the app. I prefer 
pre-design on meaningful user-facing work and
 skip trivia (pixel nits, unused tokens, new 
icon paths). I do not implement, harvest, or 
checkout. Findings are text in this file for 
Claude to take. I will not invent a second fi
le. Other products do not get a Bevel inbox u
ntil David asks.
- **Already shipped:** Signe
d EQBuddy locks live in this thread, not in t
his file yet. #222 TAKEN by Claude 2026-08-21
, both misses fixed and verified in a browser
 (see BEVEL-FEEDBACK.md; one caveat left open
 for you or David). Originally: this sprint o
nly — solo fill; one-surface pull that asks
 the PC for a fresh snapshot (not location.re
load, not a last-payload rebuild); map-as-onl
y-card gets reserved chrome pull; pan wins on
 the map. Current unreleased main is not ship
-ready on those two misses. #227 later — mo
tes as its own section; Progress stays a them
e; Wealth is coin; one owner (Motes card owns
 the rate); existing profiles who had the job
 must see the section; a restore you cannot f
ind is the #228 class. Fold test: after a car
d is gone, can they still do the job from the
 widget without being told to look in a theme
? #223 is unauthorized. I do not pair other w
ork into #222.
- **Checked:** BEVEL.md and BE
VEL-FEEDBACK.md on EQBuddy main via GitHub ra
w. Did not checkout. Did not write this file.
 InlineThemes tab-strip vs expanders is a sep
arate finding, coming next, not this sprint.


## Inline themes: tab strip vs nested expand
ers
- **Priority:** someday
- **Place:** not 
built. Proposal at `docs/proposals/InlineThem
es.md`. If it lands, the hosts are the four t
heme launcher cards on the widget (`progress`
, `quests`, Gear & Loot, Kills & Drops — ke
ys verified in `ProgressSurface.ThemeCardKey`
 / `QuestSurface` / Options → Cards & windo
ws). Window chrome already lives in `Progress
Window` / `QuestsWindow` / `GearLootWindow` /
 `CreatureWindow` (both UIs) and the same tab
 lists in `src/EQBuddy.Core` (`ProgressSurfac
e`, `QuestSurface`) plus `UI.Shared/ProgressT
heme.cs`. Hypothesis for the widget host: tod
ay’s `SectionLink` ↗ launchers become exp
anders whose body is `EqSegmentedStrip` + one
 lifted `IWidgetCard` body; not grepped this 
run.
- **Source:** BEVEL-FEEDBACK.md 2026-08-
21; docs/proposals/InlineThemes.md (David’s
 ask 2026-08-21; player quotes from #228 daet
ien-lab and joeymavity). Not this PTR sprint.
 Not #222.
- **Ask / Finding:** Claude asked,
 verbatim: **“Inline TAB STRIP, or nested E
XPANDERS?”** — when the card expands, the
 theme’s tab strip and one tab’s body (th
e same strip the window and the phone already
 draw), or each sub-surface as its own collap
sible row (Loot, Wishlist, Inventory as three
 expanders). He argues for the tab strip on c
onsistency and tells Bevel to disagree if usa
bility loses.

**Agree with the tab strip. Di
sagree with his reason.** Consistency is a co
nstraint, not the win. The win is the job.

T
he rooms inside a theme are peers — one que
stion each — not a list of independent jobs
. Progress is Experience / Wealth-coin / Fact
ion / Raids. Quests is Quests / Epic 1.0 / Pl
ane of Sky. Gear & Loot is Loot / Wishlist / 
Inventory. Kills & Drops is Kills / Drops. A 
player opening an inline theme is doing the #
228 job: I want my room in the main window, w
ithout a second window over the game. They ar
e not asking to watch Faction and Experience 
at the same time. They are asking not to be s
ent to a window to do a card’s old job.

Ne
sted expanders lose that job on this widget. 
Four rooms under Progress is the pre-fold sta
ck with an indent: two expands to reach coin,
 SizeToContent grows over the game, and the s
croller hides the cards below — the #228 cl
ass again, “taken away” by being under th
e fold. “See two at once” is the job the 
folds ended. Putting it back is un-folding in
 costume, which fights the signed direction (
themes stay folded; #227 later gives Motes it
s own section and does not split Progress).


Tabs win because they are one-room-tall, they
 name every peer on the first expand, and the
y are the chrome the window and the phone alr
eady taught. EQBuddy Mobile is a card with ta
bs inside and has no reachability complaint �
�� that is the constrained-host prototype, no
t a consistency trophy. Name the pills by the
 old card titles. Keep the collapsed launcher
 line as the glance. Default tab is the room 
that moves while you play (Experience on Prog
ress, Quests on Quests).

**Split rule:** tab
s when N rooms are peers (one question, one b
ody). Expanders when one room is a list of in
dependent jobs you may want two of at once. S
kill-ups under Experience is already an expan
der and is correct. Meter cards stay expander
s. Do not promote those jobs to theme tabs, a
nd do not demote peer rooms to nested rows.


**Host rule:** same decision (Core tab list +
 strip), widget-scale body. Experience / Weal
th-coin / Sky / Epic can come back as one-car
d bodies. The Quests General tracker and a lo
ng Wealth ledger cannot — those tabs inline
 are the glance of that tab plus ⧉ into the
 existing window. Do not shrink-wrap the full
 window onto a SizeToContent always-on-top pa
nel. Pop-out collapses the card (one owner). 
Fold Progress’s existing breakout into that
 pop-out. Cards stay collapsed by default. Bo
th UIs in the same change. Do not pair this i
nto #222. Do not un-fold. Do not use this to 
solve motes.
- **Already shipped:** Quests / 
Progress / Gear & Loot / Kills & Drops are �
� launchers into a pill-tab window. Meter car
ds expand in place. Progress window: four pil
ls; Skill-ups is a nested expander inside Exp
erience. Progress also still has a tab-less b
reakout — the double pattern nobody planned
. Quests is the template. Phone already draws
 card+tabs. Widget is SizeToContent, always-o
n-top, no emoji.
- **Checked:** BEVEL-FEEDBAC
K.md, InlineThemes.md, BEVEL.md, Themes.md, D
esignSystem.md, FeatureGuide.md, ProgressSurf
ace.cs, QuestSurface.cs, ProgressTheme.cs, co
mmitted Progress/Quests/Options shots. Not th
is run: CLAUDE.md (raw timed out), XAML (fetc
h stripped), OverlaySections, #228 thread, ru
nning app. No checkout.


