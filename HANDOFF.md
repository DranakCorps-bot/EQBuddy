# EQBuddy — handoff

**Don't re-derive the codebase.** `CLAUDE.md` loads automatically and carries the commands,
the non-negotiable rules, the where-things-live index, the trap list (20) and the
surface-allocation rule. `docs/Architecture.md` and `docs/TestPlan.md` sit behind it, and
`DocumentationTests` fails the build if any go stale. Start with
`pwsh -NoProfile -File scripts/status.ps1`.

---

## State: Gate 5c is FINISHED (2026-08-19). 1.93.2 is live; main is clean and pushed

**2,000 unit + 231 Avalonia + 10 E2E green.** All four widget files are on
`DesignRatchetTests.Migrated` — `MainWindow.xaml`, both `BreakoutWindow` files, and now
**`MainWindow.xaml.cs`**, the 4,571-line hotspot that §11.8 predicted could not join.
Full write-up in `docs/DesignSystem.md` **§11.10**; the two new traps are CLAUDE.md 21–22.

**Unreleased player-visible work exists now** — chip icons, the Watch card's sort strip,
the alert banner's lost ★, reworded tooltips. Whatever release ships next needs a
`WhatsNew.json` entry covering it. Nothing has been released; source only, as always.

### NEXT — in this order

1. **`EQBuddy.Avalonia/MainWindow.cs`** — the other 4.5k-line widget, ~39 glyphs and ~104
   literal sizes. Two of its glyph sites were already fixed in passing (the mez/slow chip
   icons and the buff-set "missing" label) because the parity rule required it, so the
   count is slightly lower than the last measurement. Same two-pass shape: sizes, look,
   glyphs, look. Its own screenshot path is `tests/EQBuddy.Avalonia.Tests` render tests
   plus `CaptureRenderedFrame`, not `shoot.ps1`.
2. **The three heavy card BODIES** — sparkline, breakdown lists, ding unlocks. These want
   their own session and they are the ones that buy hotspot headroom (§11.9's seam).
   `FillList` is the shared drawing routine they all still use; `EqCardRows` is what
   replaced it everywhere else.
3. **5d — `Theme.xaml`'s 6 glyphs**, inside shared `ControlTemplates`, so they belong to
   no single card.

**Hotspot headroom is down to 130 lines** (`MainWindow*.xaml.cs` 4,571 against a 4,274
baseline, limit 4,701). Pass 2 spent ~100 of it, nearly all on comments. The next change
in that file should be a LIFT, not an addition — see CLAUDE.md's note on why another
partial buys nothing.

**Open in `SCRIBE.md`, still untaken:** item-grouped Sky search (#108/#210, "who wants
this drop?"). **#191** (configurable mini bar) is approved and was deferred until Gate 5
finished — Gate 5c is done, so it is unblocked as soon as 5d lands; it reworks the bar
`MiniBarPresentation` now owns.

---

## Run things in the form the allowlist grants

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File C:/Users/david/source/EQBuddy/scripts/release.ps1 -Tag vX.Y.Z
```

`check.ps1`, `status.ps1`, `shoot.ps1` and `shot.ps1` take the same shape and work through
Bash. `shoot.ps1 -Shot a,b,c` needs `pwsh -Command "& '…/shoot.ps1' -Shot a,b,c"` — the
`-File` form passes the list as one string. Chaining with `&&` sometimes trips the
classifier where the same commands run fine apart; split them.

---

## State: Gate 4 + four fixes, shipped as 1.91.0

`main` carries Gate 4 (Loot) plus four community fixes — #135, #182, #189 and #197.
**1,834 unit + 207 Avalonia + 10 E2E green.**

All four discussion replies are posted, including the correction on #182 where a wrong
public diagnosis had to be withdrawn.

Three releases went out on 2026-08-17:

- **1.89.0** — Gate 2 (Quests rebuilt as list + detail pane), plus liminalwarmth's #198
  loot provenance, #199 mini-bar double-click and #200 (Disabled) alert sound.
- **1.90.0** — Gate 3 (Spawns rebuilt with progress bars and a state-aware countdown),
  plus quasarj's #194 CrossOver overlay fix.
- **1.91.0** — Gate 4 (Loot), plus #135 item-clicky charms, #182 breakdown rows,
  #189 auto-hide satellites and #197 sound formats.

---

## Gate 4, as built — Loot. Full write-up in `docs/DesignSystem.md` §11.7

Four files joined `DesignRatchetTests.Migrated`:

| File | What it is |
|---|---|
| `UI.Shared/LootPresentation.cs` | The decisions, once: strip options + tooltips, view/sort normalization, strip visibility, empty-slice wording, both headers, the target heading. **34 unit tests where there were none** |
| `EQBuddy/LootCardView.cs` | The widget's Loot card, lifted out the way `QuestChecklistView` was |
| `EQBuddy/LootBreakoutView.cs` | The Loot breakout's contents, lifted out of the six-kind `BreakoutWindow` |
| `EQBuddy.Avalonia/LootCardView.cs` | The Linux/macOS card — **which was a whole feature behind** |

Three things worth carrying forward:

1. **The strips were the symptom.** The duplicated *rules* were the disease: which strips
   are up, which chip is lit, whether "recent" is offered, and what an empty slice says
   were derived twice from the same four lists and had already drifted. When a gate's
   surface looks like a paint job, check whether the same decision is being made in two
   places — that is where the value is.
2. **The Avalonia card had never called the shared row builder**, so #198's filters and
   provenance simply were not there. Worth assuming, on every gate, that the other lane is
   further behind than the file list suggests.
3. **The screenshot review earned itself for the third gate running.** The Loot breakout's
   strips were built, selected, painted — and invisible, because the XAML host they hang in
   was declared `Visibility="Collapsed"` and only the panel inside it was ever toggled.
   That is trap 15 now.

Two new shots exist: `shoot.ps1 -Shot loot-card` (via `EQBUDDY_EXPAND=loot`, which now
takes card keys as well as `1`) and `-Shot loot-breakout` (no hook needed — the window
shows whenever the widget is minimized and its stat is starred, both plain settings).

---

## CharmTracker is out of SessionStats (2026-08-18)

`EQBuddy.Core/CharmTracker.cs`, 550 lines, and the ratchet baseline came down **2,766 →
2,375** with it. `Apply()` went from 787 lines to about 570. `MezTracker.cs` was the
precedent; this is the same move for the same reason.

**How it was verified**, because "behaviour-preserving" is a claim and not a fact: all
seven logs bjstrange attached to #135 were replayed before and after, tracing every
charm-state transition and every watch-rule label they produced. The two traces are
identical byte for byte. Do that again for the next refactor down there — the logs are
public on the discussion and the harness is twenty lines.

`CharmTrackerTests` is the half that could not be written before: 18 cases that ask the
state machine a question without building a session.

**Two process notes worth keeping:**

- The audit that led here found the SessionStats ratchet entry was a **literal path** while
  MainWindow's is a glob, so `SessionStats.Tracked.cs` had never been counted. Check the
  shape of a ratchet entry before trusting its number.
- Writing a test file with a shell heredoc failed exactly as `CLAUDE.md` says it will.
  Use the editing tools.

### Open: charm4.txt still reports no held time

Found while building the corpus, NOT fixed, and deliberately not guessed at. bjstrange's
charm4.txt replays with no `held` on its break — the charm is never claimed at all, so
there is nothing for the wear-off to measure. The public reply on that log addressed the
BREAK (two creatures sharing a name); the claim never happening is a different question.

A first look says `_petName` was already set when the landing arrived, so the
unknown-cast candidate path was skipped — but that is a hypothesis, and the last two times
this thread was reasoned about rather than replayed, the reasoning was wrong. **Replay it
and print every state change before touching anything.** A synthetic test written from a
guess passed while the real log failed, again, during this very session — it was written,
seen to pass for the wrong reason, and deleted.

---

## Gate 5a–5b history. Full write-up in `docs/DesignSystem.md` §11.8–§11.9

**Superseded in part by the section at the top of this file: 5c is finished.** The
prediction below that `MainWindow.xaml.cs` "probably cannot" join the ratchet was wrong,
and §11.10 records why — the count was mostly comments, and the concession that looked
reasonable (exempt string literals) would have exempted the rule's own target.


**Gate 5 does not fit in one change.** Measured before starting: **473 ratchet violations**
across the two widget files and their Avalonia twin — 127 literal font sizes, 174 spacing
tuples, 167 glyphs, over 10,400 lines. The ratchet is per-file and all-or-nothing, so the
gate has to be staged by VOCABULARY (finish one shared thing everywhere it appears) rather
than card by card. 5a did the two things every card has: **the fourteen headings** and
**the sort strips**. 427 left.

Both landed in both UIs, and the screenshot review earned itself twice inside one gate —
the strips first OVERLAPPED their headings (one-cell Grid, fine as four small words, a
collision as pills), then TRIMMED them ("Damage b…") until the redundant "sort:" caption
went. Neither is visible in a diff or a test.

**5b has started: the card SEAM exists and is proved on the Kills card** (§11.9). The
lesson that shaped it: lifting files was moving lines without moving dependencies —
`MainWindow` carries **61 internal members**, most of them there so a lifted view can reach
back. `IWidgetCard` + `ICardContext` (six methods, implemented explicitly) fix that, and
`KillsPresentationTests` is the first card content ever asserted without launching a
window. Convert the remaining cards onto that seam, one at a time, presentation into
UI.Shared first.

**Batch one converted: Motes, Money, Faction** (plus Kills with the seam). Two shared
things came out of it and should be spent, not re-invented, by every later batch:
`UI.Shared/CardRow.cs` (what a row IS — name, value, indent, note, item-ness, value ink)
and `EQBuddy/EqCardRows.cs` (the one place a row is drawn, replacing `FillList` and its
per-surface copies). A card's `Item: true` rows get the wiki click, the stats hover and the
quest badge through `ICardContext` and nothing else does.

**Batch two: Combat, Healing and Progress SUMMARIES** moved to
`UI.Shared/CombatPresentation.cs` and `ProgressPresentation.cs` with tests. Their bodies
did NOT move — those three own the sparkline, the fight split, the breakdown lists with
their resist/blocked lookups and the ding-unlock rows, which is heavy WPF machinery and a
separate job. The summaries were the prize: a dozen conditional fragments each, on the
cards a player reads most, and the densest untested text left in the app.

**Batch three started, and stopped at a safe point rather than half-done.**
`EQBuddy/EqIcon.cs` is the XAML-addressable icon (`<local:EqIcon Glyph="Copy"/>`) — WPF's
`Path` is sealed, so it wraps one — plus Bolt, Paw and Phone in `IconPaths`. The Combat
card's ⧉/⧗ buttons and the 🐾/⚡ star glyphs are vectors now.
`MainWindow.xaml` is at **79 literal attributes and 16 glyphs**, down from 87/20.

**Read this before continuing — the finish line is not where §11.8 assumed.**

`MainWindow.xaml` can be made ratchet-clean: the remaining 16 glyphs are chevrons in
toggle labels (▸/▾, flipped from code), three menu headers, and **six occurrences inside
XML COMMENTS**, which the ratchet counts because it scans every line. All are convertible
or rewordable.

**`MainWindow.xaml.cs` probably cannot, and that is a real finding rather than a shortfall.**
It carries ~74 glyphs and most of them are not controls: they are inside user-facing
STRINGS — alert text, tooltips, "re-enable in ⚙ Options → Breakout windows". `CLAUDE.md`
explicitly permits emoji in "user-facing TEXT where they are content rather than UI", and
`DesignRatchetTests` cannot tell the two apart. So either those strings move to a resource
the ratchet doesn't scan, or the glyph test needs a way to exempt string literals, or that
file joins the list only after a deliberate pass over its copy. **Decide which before
starting 5d** — it changes what "Gate 5 complete" means.

**Remaining cards: Gear, Watch, Buffs, Raids, Travels & Deaths** — plus the three bodies
above
— then 5c (chrome) and 5d (`Theme.xaml` templates).

**The old note, still true for the rest: 5b — the card bodies.** Lift surfaces into their own files the way `LootCardView`
was; that is the only thing that buys hotspot headroom as well as ratchet coverage. Then
**5c** the chrome (carries #191, and §8b's reserved widths are non-negotiable — #173), then
**5d** `Theme.xaml`'s templates, where the ⭐ and ▸ glyphs live inside shared
ControlTemplates and so belong to no single card.

---

## THE OLD NEXT TASK — Gate 5 of the UI/UX rework: the main widget

`docs/DesignSystem.md` §11.5 is the amended order; §10, §11.6 and §11.7 are the three
worked examples. Gate 5 is the widget itself — the card chrome, the thirteen card headers,
and the **~14 hand-built segmented strips still in `MainWindow.xaml`**.

Two things Gate 4 deliberately left for it, and the reasons matter:

- **Card headers were not touched.** Thirteen cards wear the same `Section` expander and
  the same emoji-and-count header; migrating one of them reads as a bug rather than a
  migration. They change together or not at all.
- **`MainWindow.xaml.cs` still cannot join the ratchet**, which is why Gate 4 lifted its
  surface out rather than migrating in place. Gate 5's real deliverable is getting that
  file onto the list — and `LootCardView`/`QuestChecklistView` are the pattern for how.

The hotspot ratchet has room: `MainWindow*.xaml.cs` is 4,507 lines against a 4,274
baseline (limit 4,701). Gate 4 took 86 lines out of it and did not go under the baseline,
so the baseline is unchanged — but a gate that lifts several more surfaces will, and the
rule is to lower it in the same commit.

## Capability restored: turning a Plane of Sky reward in (2026-08-18)

**A reorganisation cost a feature, and nothing caught it.** The widget's Sky card carried a
per-REWARD turn-in check. When that card became a launcher (2026-08-16) and the tracker was
rebuilt around a list and a detail pane, the per-ITEM ticks came across and the per-reward
one did not. `AppSettings.SkyQuestCompleted` kept being READ — by `QuestChecklistLayout`,
by both desktops and by EQBuddy Mobile — while the only thing left that could WRITE it was
the achievements import. A player who turned a reward in and had no achievements export to
paste could not say so: every piece ticked, the reward permanently "ready", the Sky counter
unable to move past it.

`UI.Shared/SkyCompleteToggle.cs` restores it, beside `EpicCompleteToggle` — the ASYMMETRY
between those two is what let it go missing. The old card's rules are kept verbatim because
they were right: turning in acquires every item in the reward and resolves any parked
auto-tick, and reopening leaves the item boxes alone (a mis-click costs one click to undo,
not six). `QuestChecklistGroup` now carries `CompletionKey` and `Completed`, so a view asks
the layout rather than parsing the note string, and Epic groups carry no key — its
completion is per class, and a turn-in button must not appear there by accident.

Surfaced as **"Mark turned in" / "Reopen"** on the reward heading in both Quest Trackers,
matching the General tab's own turn-in button from Gate 2 rather than inventing a control.

**Visually inspected 2026-08-18** — `shoot.ps1 -Shot sky-checklist` stages all three reward
states on one screen (turned in → "Reopen", every piece held → "Mark turned in", part
collected → neither) and `EQBUDDY_QUESTS=sky` opens straight onto the tab. Staging it found
a SECOND bug, in Core rather than in the new code: `ApplyDefaultSkyQuestChecklist` refreshed
a row's NPC, reward, item and source by Id but never its **ClassName** — and every surface
groups and filters by class, so a row whose class drifted from the catalog was invisible in
all of them while its tick sat in `settings.json`. Fixed and pinned.

**Worth generalising:** this went missing because the DATA survived the move and only the
WRITE path did not, which no test and no ratchet can see. When folding a surface, check
what still writes each setting it owned — a setting that only readers touch is the
signature.

## Debts and open threads

**Owed publicly, from replies posted 2026-08-17.** These are commitments, not ideas:

| # | Reporter | What | Where it belongs |
|---|---|---|---|
| #135 | bjstrange | **DONE in 1.91.0.** Replayed charm7.txt: an item clicky prints no cast line, so nothing recorded the landing and the wear-off had nothing to measure. The caster-only "Master" tell starts the clock now; his file gives "held 0:19". | closed |
| #182 | Ladylag | **DONE in 1.91.0, and my public diagnosis was WRONG.** The `.` rows are not a parser failure — see below. Name column, hover text and resize band all fixed. **The correction is posted.** | closed |
| #189 | wizen | **DONE in 1.91.0** — every window follows the widget's auto-hide now, by a deny-list so later windows follow too. The settings-across-updates half is still waiting on his `error.log` (trap 13); his paste showed no overwrite line. | half closed |
| #197 | wizen | **DONE in 1.91.0** — one shared list, six call sites; Windows had two formats and Avalonia already had three. | closed |
| #192 | wizen | Waiting on his exact forage line — if Legends writes "some", the regex misses it and that's a one-line fix. | Waiting on him |
| #202 | bjstrange | Mobile loot/watches card refresh loop. I checked the loot fingerprint and it has no clock in it, so my first hypothesis is dead. Four questions asked; waiting. | Waiting on him |
| #190 | wizen | **Approved:** tracked-quest chips — double-click opens the tracker with that quest selected, right-click dismisses. | Gate 6 |
| #191 | TheMegaSage | **Approved:** the mini bar's contents become configurable and removable. §8b's reserved widths are non-negotiable (#173). | Gate 6 |

**Still worth doing on Gate 3:** the fixture has no running timer in a catalogued zone, so
the progress bar is unit-tested but has never been *seen*. Seeding one named kill into
`tests/fixtures/eqlog_Testchar_fixture.txt` would close that.

---

## Findings worth not re-learning

- **A component nobody can reach gets rebuilt by hand.** Gate 2 built the chip primitive
  and left it private inside `QuestsWindow`; six hours later #198 hand-built two more. That
  is why gate 2b exists and why anything shared goes somewhere reachable immediately.
- **`Auto` columns lie in a header row.** A header has no buttons, so an `Auto` action
  column measures zero there and ~115 in a row — every label lands left of the column it
  names. Fixed lanes also stop rows reflowing when a button appears mid-edit.
- **A progress bar in one column is a sliver.** David, 2026-08-17: *"we have room between
  the columns."* Span it across the row.
- **#193's damage cannot be repaired.** Wildcard ticks went through the normal path and are
  indistinguishable from honest ones. The reply says so plainly; don't promise a cleanup.
- **Replay the reporter's actual log file.** A hand-condensed charm5.txt passed while the
  real one failed; same for charm6 and the #183 mez log. charm7 makes it seven for seven.
- **Look at the reporter's SCREENSHOT before believing your own diagnosis.** #182's rows
  reading `.` and `..` were called a parser bug in public — by me — and they were nothing
  of the kind: the name column was starved to its ellipsis by a stat line that took
  whatever width it liked, which the same screenshot proves, because "Damage shield" (short
  stat line) printed in full three rows below one that printed nothing. The correction is posted.
- **Check when a fix shipped before agreeing it is broken.** The same thread has me
  accepting "drag only works on the bottom edge" as a defect I had got wrong. Edge resize
  landed in 1.35.0 and works; the band was six pixels wide and unmarked, so the corner grip
  was the only findable way in. The honest fix was a wider band, not a new feature.
- **A host that hides itself is a second switch.** Gate 4's breakout strips were correct,
  selected and never once shown, because the `ContentControl` they hang in was declared
  collapsed in XAML. When you lift a surface into a class, its host gets no `Visibility`
  and no `Margin` — the lifted control carries both. Trap 15.
- **`EQBUDDY_EXPAND` takes card keys now**, not just `1`. A card's expanded state is not
  persisted, so before this the only way to photograph one card BODY was to open all
  thirteen and hope it fit above the fold. It didn't.

---

## Hard lines (see `CLAUDE.md` for the full set)

- Never measure other players. Values line, not technical.
- Releases wait for David's explicit go. Ask "want me to cut it?" — don't hand him a
  command block; that once had two people release two minutes apart.
- Curated catalogs are never auto-written; **learned** data is.
- eqlwiki is the tie-breaker; other sources where it's silent, marked as such.
- A `UI.Shared`/Core fix must reach **both** UIs in the same change — that is what carried
  #122 and #152 to Linux.
- Every player-noticeable change earns a `WhatsNew.json` entry in the release that ships
  it, crediting the reporter by name and discussion number.
